using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace Chassis.Api.Tests;

public sealed class BffAuthenticationTests : IClassFixture<BffAuthenticationTests.Fixture>
{
    private readonly Fixture _fixture;

    public BffAuthenticationTests(Fixture fixture) => _fixture = fixture;

    public sealed class Fixture : IDisposable
    {
        internal ChassisApiFactory Factory { get; } = new();

        public void Dispose() => Factory.Dispose();
    }

    // A dotted base64url triple — what an unencrypted JWT looks like on the wire.
    private static readonly Regex JwtShape =
        new(@"^[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}$", RegexOptions.Compiled);

    [Fact]
    public async Task Signup_creates_a_user_in_a_brand_new_tenant()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/Identity/signup", new
        {
            email = NewEmail(),
            password = "Passw0rd!x",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<SignupBody>();
        body!.TenantId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Signup_rejects_an_email_that_is_already_taken_in_any_tenant()
    {
        var email = NewEmail();

        var first = await _fixture.Factory.CreateClient()
            .PostAsJsonAsync("/Identity/signup", new { email, password = "Passw0rd!x" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // A different browser / different fresh tenant, same email address.
        var second = await _fixture.Factory.CreateClient()
            .PostAsJsonAsync("/Identity/signup", new { email, password = "Passw0rd!x" });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Full_BFF_login_leaves_the_browser_with_a_session_cookie_and_no_token()
    {
        var recorder = new CookieRecordingHandler();
        var client = CreateBrowserLikeClient(recorder);

        var email = NewEmail();
        var signup = await client.PostAsJsonAsync("/Identity/signup", new { email, password = "Passw0rd!x" });
        var expectedTenant = (await signup.Content.ReadFromJsonAsync<SignupBody>())!.TenantId;

        // Signup signed us in on the auth-server cookie, so this walk runs
        // straight through /connect/authorize → callback without the login form.
        var loginWalk = await client.GetAsync("/auth/login");
        var trail = string.Join("\n", recorder.SetCookies);

        var meResponse = await client.GetAsync("/api/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"login walk ended {(int)loginWalk.StatusCode} at {loginWalk.RequestMessage?.RequestUri}. Set-Cookie trail:\n{trail}\n\nServer logs:\n{CapturedLogs.Dump()}");

        var me = await meResponse.Content.ReadFromJsonAsync<MeBody>();
        me!.Email.Should().Be(email);
        Guid.Parse(me.TenantId!).Should().Be(expectedTenant);

        var cookiePairs = recorder.SetCookies;
        cookiePairs.Should().Contain(c => c.StartsWith("Chassis.Session=", StringComparison.Ordinal));
        cookiePairs.Should().OnlyContain(c => c.ToLowerInvariant().Contains("httponly"));

        foreach (var value in cookiePairs.Select(CookieValue))
        {
            JwtShape.IsMatch(value).Should().BeFalse("no cookie should carry a raw token (auth doc §5.1)");
        }
    }

    [Fact]
    public async Task Returning_user_signs_in_through_the_password_form()
    {
        var email = NewEmail();
        const string password = "Passw0rd!x";

        // Create the account, then discard that client so the next client starts
        // with no auth-server cookie — a genuine returning visitor.
        await _fixture.Factory.CreateClient().PostAsJsonAsync(
            "/Identity/signup", new { email, password });

        var client = CreateBrowserLikeClient();

        // Lands on the login form (redirected there from /connect/authorize).
        var form = await client.GetAsync("/auth/login");
        form.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await form.Content.ReadAsStringAsync();
        form.RequestMessage!.RequestUri!.AbsolutePath.Should().Be("/Identity/login");

        var token = Extract(html, @"name=""__RequestVerificationToken"" value=""([^""]+)""");
        var returnUrl = WebUtility.HtmlDecode(Extract(html, @"name=""returnUrl"" value=""([^""]+)"""));

        var post = await client.PostAsync("/Identity/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["returnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token,
        }));
        post.IsSuccessStatusCode.Should().BeTrue("the form POST walks the redirect chain back to a session");

        var me = await client.GetFromJsonAsync<MeBody>("/api/me");
        me!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Wrong_password_does_not_establish_a_session()
    {
        var email = NewEmail();
        await _fixture.Factory.CreateClient().PostAsJsonAsync(
            "/Identity/signup", new { email, password = "Passw0rd!x" });

        var client = CreateBrowserLikeClient();
        var html = await (await client.GetAsync("/auth/login")).Content.ReadAsStringAsync();
        var token = Extract(html, @"name=""__RequestVerificationToken"" value=""([^""]+)""");
        var returnUrl = WebUtility.HtmlDecode(Extract(html, @"name=""returnUrl"" value=""([^""]+)"""));

        var post = await client.PostAsync("/Identity/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = "wrong-password",
            ["returnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token,
        }));

        post.StatusCode.Should().Be(HttpStatusCode.OK);
        (await post.Content.ReadAsStringAsync()).Should().Contain("Invalid email or password");
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Notes_api_requires_a_session_and_is_scoped_to_the_caller_tenant()
    {
        var anonymous = _fixture.Factory.CreateClient();
        (await anonymous.GetAsync("/api/notes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var alice = await SignedInClientAsync();
        var create = await alice.PostAsJsonAsync("/api/notes", new { title = "alice-note", body = "" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var aliceNotes = await alice.GetFromJsonAsync<List<NoteBody>>("/api/notes");
        aliceNotes!.Select(n => n.Title).Should().Contain("alice-note");

        var bob = await SignedInClientAsync();
        var bobNotes = await bob.GetFromJsonAsync<List<NoteBody>>("/api/notes");
        bobNotes!.Should().NotContain(n => n.Title == "alice-note");
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = CreateBrowserLikeClient();
        await client.PostAsJsonAsync("/Identity/signup", new { email = NewEmail(), password = "Passw0rd!x" });
        using (await client.GetAsync("/auth/login"))
        {
        }

        return client;
    }

    private HttpClient CreateBrowserLikeClient(params DelegatingHandler[] innerHandlers)
    {
        // RedirectHandler follows redirects internally and only surfaces the final
        // response, so anything that needs to observe each hop (the cookie
        // recorder) goes *after* it and *before* the cookie jar.
        DelegatingHandler[] handlers =
        [
            new RedirectHandler(),
            .. innerHandlers,
            new CookieContainerHandler(),
        ];

        return _fixture.Factory.CreateDefaultClient(new Uri("http://localhost"), handlers);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static string Extract(string html, string pattern)
    {
        var match = Regex.Match(html, pattern);
        match.Success.Should().BeTrue($"login page should contain a match for /{pattern}/");
        return match.Groups[1].Value;
    }

    private static string CookieValue(string setCookie)
    {
        var firstSemicolon = setCookie.IndexOf(';');
        var pair = firstSemicolon < 0 ? setCookie : setCookie[..firstSemicolon];
        var equals = pair.IndexOf('=');
        return equals < 0 ? string.Empty : pair[(equals + 1)..];
    }

    private sealed record SignupBody(string? Email, Guid TenantId);

    private sealed record MeBody(string? Email, string? TenantId);

    private sealed record NoteBody(Guid Id, string Title, string Body);
}
