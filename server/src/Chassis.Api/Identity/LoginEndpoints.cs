using System;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Chassis.Api.Identity;

/// <summary>
/// Server-rendered email/password login page plus the self-service signup
/// endpoint. Kept server-rendered on purpose: the login step sits inside
/// OpenIddict's front-channel redirect, so the React SPA stays out of the OAuth
/// machinery entirely (auth doc §5.1).
/// </summary>
internal static class LoginEndpoints
{
    public static void MapLoginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/Identity").ExcludeFromDescription();

        group.MapGet("/login", (HttpContext http, IAntiforgery antiforgery, string? returnUrl) =>
        {
            var token = antiforgery.GetAndStoreTokens(http).RequestToken!;
            return Results.Content(RenderLoginPage(returnUrl, token, error: null), "text/html; charset=utf-8");
        }).AllowAnonymous();

        group.MapPost("/login", async (
            HttpContext http,
            IAntiforgery antiforgery,
            SignInManager<AppUser> signInManager,
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] string? returnUrl) =>
        {
            await antiforgery.ValidateRequestAsync(http);

            var result = await signInManager.PasswordSignInAsync(
                email, password, isPersistent: true, lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                var token = antiforgery.GetAndStoreTokens(http).RequestToken!;
                var message = result.IsLockedOut ? "This account is locked. Try again later." : "Invalid email or password.";
                http.Response.StatusCode = (int)HttpStatusCode.OK;
                return Results.Content(RenderLoginPage(returnUrl, token, message), "text/html; charset=utf-8");
            }

            return Results.LocalRedirect(SafeReturnUrl(returnUrl));
        }).AllowAnonymous().DisableAntiforgery();

        // Self-service signup (auth doc §4.2): a brand-new email mints a brand-new
        // tenant with this user as its only member. Called by the SPA as JSON; on
        // success the user is signed in on the auth-server cookie so the very next
        // /connect/authorize round-trip skips the login form.
        group.MapPost("/signup", async (
            SignupRequest body,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager) =>
        {
            if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = ["Email and password are required."],
                });
            }

            var user = new AppUser
            {
                UserName = body.Email,
                Email = body.Email,
                TenantId = Guid.NewGuid(),
            };

            var result = await userManager.CreateAsync(user, body.Password);
            if (!result.Succeeded)
            {
                return Results.ValidationProblem(result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
            }

            await signInManager.SignInAsync(user, isPersistent: true);
            return Results.Created("/api/me", new SignupResponse(user.Email, user.TenantId));
        }).AllowAnonymous();
    }

    /// <summary>Only ever redirect to a local path — never an attacker-supplied absolute URL.</summary>
    private static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
            ? returnUrl
            : "/";

    private static string RenderLoginPage(string? returnUrl, string antiforgeryToken, string? error)
    {
        var encodedReturnUrl = WebUtility.HtmlEncode(returnUrl ?? "/");
        var encodedToken = WebUtility.HtmlEncode(antiforgeryToken);
        var errorBlock = error is null
            ? string.Empty
            : $"<p class=\"error\" role=\"alert\">{WebUtility.HtmlEncode(error)}</p>";

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("<title>Sign in — Chassis</title><style>");
        html.Append("body{font:15px/1.5 system-ui,sans-serif;background:#f6f7f9;margin:0;display:flex;");
        html.Append("min-height:100vh;align-items:center;justify-content:center}");
        html.Append("form{background:#fff;padding:2rem;border-radius:12px;box-shadow:0 1px 4px rgba(0,0,0,.1);");
        html.Append("width:320px;display:flex;flex-direction:column;gap:.75rem}");
        html.Append("h1{font-size:1.1rem;margin:0 0 .5rem}");
        html.Append("label{font-weight:600;font-size:.85rem}");
        html.Append("input{padding:.5rem;border:1px solid #cbd2d9;border-radius:6px;font:inherit}");
        html.Append("button{padding:.6rem;border:0;border-radius:6px;background:#2563eb;color:#fff;");
        html.Append("font:inherit;font-weight:600;cursor:pointer}");
        html.Append(".error{color:#b91c1c;font-size:.85rem;margin:0}</style></head><body>");
        html.Append("<form method=\"post\" action=\"/Identity/login\">");
        html.Append("<h1>Sign in to Chassis</h1>");
        html.Append(errorBlock);
        html.Append("<label for=\"email\">Email</label>");
        html.Append("<input id=\"email\" name=\"email\" type=\"email\" autocomplete=\"username\" required autofocus>");
        html.Append("<label for=\"password\">Password</label>");
        html.Append("<input id=\"password\" name=\"password\" type=\"password\" autocomplete=\"current-password\" required>");
        html.Append($"<input type=\"hidden\" name=\"returnUrl\" value=\"{encodedReturnUrl}\">");
        html.Append($"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{encodedToken}\">");
        html.Append("<button type=\"submit\">Sign in</button>");
        html.Append("</form></body></html>");
        return html.ToString();
    }

    internal sealed record SignupRequest(string Email, string Password);

    internal sealed record SignupResponse(string? Email, Guid TenantId);
}
