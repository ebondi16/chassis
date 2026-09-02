using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Chassis.Api.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Chassis.Api.Tests;

/// <summary>
/// Boots the real <c>Chassis.Api</c> composition (Identity + OpenIddict + the BFF)
/// against a throwaway SQLite file, and routes the BFF's server-side OIDC
/// back-channel calls (discovery, token, JWKS) back through the in-memory test
/// server instead of a real socket.
/// </summary>
internal sealed class ChassisApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"chassis-apitest-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development so the auth server drops its HTTPS-only transport guard
        // (the test server speaks http://localhost) — mirrors the dev inner loop.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Chassis"] = $"Data Source={_databasePath}",
                ["Auth:Authority"] = "http://localhost/",
                ["Auth:PublicOrigin"] = "http://localhost",
                ["Auth:ClientSecret"] = "test-secret",
            });
        });

        builder.ConfigureLogging(logging => logging.AddProvider(new CapturedLogProvider()));

        builder.ConfigureTestServices(services =>
        {
            // Runs in the Configure phase, before the framework's PostConfigure
            // builds the discovery ConfigurationManager — so it picks up this
            // handler and the whole OIDC back-channel stays in-process.
            services.Configure<OpenIdConnectOptions>(IdentityComposition.OidcScheme, options =>
            {
                options.Backchannel = new HttpClient(new DeferredHandler(() => Server.CreateHandler()));
                options.RequireHttpsMetadata = false;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databasePath))
        {
            try
            {
                File.Delete(_databasePath);
            }
            catch (IOException)
            {
                // A pooled SQLite connection's finalizer may still hold the file
                // briefly; a leftover temp file is harmless.
            }
        }
    }

    private sealed class DeferredHandler(Func<HttpMessageHandler> innerFactory) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            InnerHandler ??= innerFactory();
            return base.SendAsync(request, cancellationToken);
        }
    }
}
