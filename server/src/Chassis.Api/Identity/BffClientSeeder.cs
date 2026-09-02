using System;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Chassis.Api.Identity;

/// <summary>
/// Ensures the single confidential OAuth client the BFF uses exists in
/// OpenIddict's store. Idempotent: safe to run on every startup. Its redirect and
/// post-logout URIs are derived from <c>Auth:PublicOrigin</c> (falling back to the
/// dev origin), so the one moving part per environment is that setting.
/// </summary>
internal sealed class BffClientSeeder(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var origin = (config["Auth:PublicOrigin"] ?? "http://localhost:5030").TrimEnd('/');

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = IdentityComposition.BffClientId,
            ClientSecret = config["Auth:ClientSecret"] ?? "chassis-bff-dev-secret",
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit, // first-party client — no consent screen
            DisplayName = "Chassis web (BFF)",
            RedirectUris = { new Uri($"{origin}/auth/callback") },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Email,
                Permissions.Scopes.Roles,
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
        };

        var existing = await applications.FindByClientIdAsync(IdentityComposition.BffClientId, cancellationToken);
        if (existing is null)
        {
            await applications.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await applications.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
