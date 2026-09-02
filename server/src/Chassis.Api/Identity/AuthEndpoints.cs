using System.Security.Claims;
using Chassis.Api.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Chassis.Api.Identity;

/// <summary>
/// The BFF's own surface for the SPA (auth doc §5.1): a login entry point that
/// kicks off the server-side OIDC exchange, a logout that clears the cookies, and
/// a "who am I" probe the SPA calls on load. No tokens cross any of these — only
/// the session cookie.
/// </summary>
internal static class AuthEndpoints
{
    public static void MapBffEndpoints(this IEndpointRouteBuilder app)
    {
        // SPA sends the browser here to sign in. Challenging the OIDC scheme runs
        // the Authorization Code + PKCE redirect against our own OpenIddict server;
        // the handler's callback drops the result into the BFF session cookie.
        app.MapGet("/auth/login", (string? returnUrl) =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = LocalOrRoot(returnUrl) },
                [IdentityComposition.OidcScheme]))
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(IdentityComposition.BffScheme);
            await http.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.NoContent();
        })
            .RequireAuthorization()
            .ExcludeFromDescription();

        // The SPA calls this on load to learn whether it has a session and which
        // tenant it is scoped to. 401 (not a redirect) when signed out. Kept out
        // of the OpenAPI document for now — wiring it into the typed client is SPA
        // work for a later session.
        app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new MeResponse(
                user.FindFirstValue(Claims.Email) ?? user.Identity?.Name,
                user.FindFirstValue(ClaimsTenantProvider.TenantClaimType))))
            .RequireAuthorization()
            .ExcludeFromDescription();
    }

    private static string LocalOrRoot(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
            ? returnUrl
            : "/";

    internal sealed record MeResponse(string? Email, string? TenantId);
}
