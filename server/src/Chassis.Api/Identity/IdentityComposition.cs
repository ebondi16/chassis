using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Chassis.Api.Identity;

/// <summary>
/// The one place the web build's auth stack is composed (auth doc §2, §5.1):
/// ASP.NET Core Identity for user management, OpenIddict as the OAuth/OIDC
/// authorization server, and — because Chassis runs the Backend-for-Frontend
/// pattern — an OpenID Connect <em>client</em> pointed back at that same server so
/// the browser only ever holds an <c>HttpOnly</c> session cookie, never a token.
/// </summary>
/// <remarks>
/// Mirrors <c>DesktopComposition</c> on the other deployment seam: <c>Program.cs</c>
/// calls only <see cref="AddChassisIdentity"/> and <see cref="UseChassisIdentity"/>,
/// and only for a web run. The desktop build (auth doc §5.3) never loads any of
/// this and keeps its <c>LocalFixedTenantProvider</c>.
/// </remarks>
internal static class IdentityComposition
{
    /// <summary>Confidential OAuth client id for the BFF (seeded by <see cref="BffClientSeeder"/>).</summary>
    public const string BffClientId = "chassis-bff";

    /// <summary>Session cookie the SPA rides — the only artifact the browser keeps (auth doc §5.1 step 3).</summary>
    public const string BffScheme = "Chassis.Bff";

    /// <summary>OIDC client scheme: the server-side half of the Authorization Code + PKCE exchange.</summary>
    public const string OidcScheme = "Chassis.Oidc";

    /// <summary>
    /// Keeps the auth context's migration history apart from the domain context's
    /// in the one shared database (auth doc §6).
    /// </summary>
    public const string AuthMigrationsHistoryTable = "__EFMigrationsHistoryAuth";

    private const string AuthorizeEndpoint = "connect/authorize";
    private const string TokenEndpoint = "connect/token";
    private const string UserInfoEndpoint = "connect/userinfo";
    private const string LoginPath = "/Identity/login";
    private const string CallbackPath = "/auth/callback";

    public static void AddChassisIdentity(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;
        var isDevelopment = builder.Environment.IsDevelopment();
        var connectionString = config.GetConnectionString("Chassis") ?? "Data Source=chassis.db";

        // Auth tables live in the same database as the domain data, in their own
        // DbContext + migration history so nothing leaks into Infrastructure (§6).
        services.AddDbContext<ChassisAuthDbContext>(options =>
        {
            options.UseSqlite(
                connectionString,
                sql => sql.MigrationsHistoryTable(AuthMigrationsHistoryTable));
            options.UseOpenIddict();
        });

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;       // email uniqueness is global, not per-tenant (§4.1)
                options.SignIn.RequireConfirmedAccount = false; // email confirmation is a later session
                options.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ChassisAuthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAntiforgery();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = BffScheme;
                options.DefaultSignInScheme = BffScheme;
                // A protected API call challenges the cookie scheme → 401 (see the
                // events below), never a redirect to the auth server. The one place
                // the OIDC redirect is wanted, GET /auth/login, asks for it by name.
                options.DefaultChallengeScheme = BffScheme;
            })
            // Auth-server-side "who is the human" cookie, set by SignInManager on
            // the login page and read by the /connect/authorize handler.
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "Chassis.Identity";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.LoginPath = LoginPath;
                options.ReturnUrlParameter = "returnUrl";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = true;
            })
            // The SPA session cookie — the one artifact the browser keeps (auth
            // doc §5.1): plain, HttpOnly, Secure. Never redirects: an
            // unauthenticated API call gets a status code, not an HTML login page.
            .AddCookie(BffScheme, options =>
            {
                options.Cookie.Name = "Chassis.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = isDevelopment
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            // The BFF's server-side OIDC client: it runs the redirect + PKCE code
            // exchange against our own OpenIddict server and drops the result into
            // the BFF cookie. SaveTokens is off — the browser holds no token (§5.1).
            .AddOpenIdConnect(OidcScheme, options =>
            {
                options.Authority = config["Auth:Authority"] ?? "http://localhost:5030/";
                options.ClientId = BffClientId;
                options.ClientSecret = config["Auth:ClientSecret"] ?? "chassis-bff-dev-secret";
                options.ResponseType = "code";
                // Plain query-string callback (?code=...), not the default
                // self-submitting form_post page — a straightforward 302 the BFF's
                // own middleware and any HTTP client can follow.
                options.ResponseMode = "query";
                options.UsePkce = true;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.CallbackPath = CallbackPath;
                options.SignInScheme = BffScheme;
                options.RequireHttpsMetadata = !isDevelopment;
                options.MapInboundClaims = false;
                options.Scope.Clear();
                options.Scope.Add(Scopes.OpenId);
                options.Scope.Add(Scopes.Profile);
                options.Scope.Add(Scopes.Email);
                options.TokenValidationParameters.NameClaimType = Claims.Name;
                options.TokenValidationParameters.RoleClaimType = Claims.Role;

                // The BFF's IdP is its own origin, so the correlation/nonce cookies
                // ride a same-site redirect — Lax, not the cross-site default of
                // None (which forces Secure and so is dropped on a dev http origin).
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SameSite = SameSiteMode.Lax;
                var handshakeCookieSecurity = isDevelopment
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.CorrelationCookie.SecurePolicy = handshakeCookieSecurity;
                options.NonceCookie.SecurePolicy = handshakeCookieSecurity;
            });

        services.AddAuthorization();

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore().UseDbContext<ChassisAuthDbContext>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris(AuthorizeEndpoint)
                       .SetTokenEndpointUris(TokenEndpoint)
                       .SetUserInfoEndpointUris(UserInfoEndpoint);

                options.AllowAuthorizationCodeFlow()
                       .RequireProofKeyForCodeExchange();

                options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles);

                // §6: ephemeral dev keys. A managed key store (key vault) is a
                // deployment-time concern, deliberately deferred.
                options.AddEphemeralEncryptionKey()
                       .AddEphemeralSigningKey();

                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough();

                if (isDevelopment)
                {
                    // Dev inner loop runs on http://localhost:5030 (see AGENTS.md).
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            });

        services.AddHostedService<BffClientSeeder>();
    }

    public static void UseChassisIdentity(WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapLoginEndpoints();
        app.MapAuthorizationEndpoints();
        app.MapBffEndpoints();
    }
}
