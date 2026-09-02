using System.Security.Claims;
using Chassis.Api.Tenancy;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Chassis.Api.Identity;

/// <summary>
/// The interactive half of the authorization server: OpenIddict passes the
/// <c>/connect/authorize</c> request through to this handler (auth doc §2 —
/// "when it needs to know who a user is, it delegates to Identity's login page").
/// Token issuance at <c>/connect/token</c> and <c>/connect/userinfo</c> is handled
/// by OpenIddict itself and needs no code here.
/// </summary>
internal static class AuthorizationEndpoints
{
    public static void MapAuthorizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/connect/authorize", ["GET", "POST"], HandleAuthorizeAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    private static async Task<IResult> HandleAuthorizeAsync(HttpContext http, UserManager<AppUser> userManager)
    {
        var request = http.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict request could not be retrieved.");

        // Not signed in on the auth-server cookie yet → bounce to the login page,
        // preserving the full authorize request as the return URL.
        var result = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (result is not { Succeeded: true })
        {
            var returnUrl = http.Request.PathBase + http.Request.Path + http.Request.QueryString;
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                [IdentityConstants.ApplicationScheme]);
        }

        var user = await userManager.GetUserAsync(result.Principal)
            ?? throw new InvalidOperationException("The signed-in user could not be resolved.");

        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user))
                .SetClaim(Claims.Email, await userManager.GetEmailAsync(user))
                .SetClaim(Claims.Name, await userManager.GetUserNameAsync(user))
                .SetClaim(ClaimsTenantProvider.TenantClaimType, user.TenantId.ToString());

        identity.SetScopes(request.GetScopes());

        identity.SetDestinations(static claim => claim.Type switch
        {
            Claims.Name or Claims.Email or ClaimsTenantProvider.TenantClaimType =>
                [Destinations.AccessToken, Destinations.IdentityToken],
            _ => [Destinations.AccessToken],
        });

        return Results.SignIn(
            new ClaimsPrincipal(identity),
            properties: null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
