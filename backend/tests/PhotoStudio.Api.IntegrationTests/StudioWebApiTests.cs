using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the web area of the photographer: password plus a code by email, a session that is a cookie, remembered
/// browsers, and what that cookie can and cannot open. Every HTTP client the test creates is a different browser with its own cookies.
/// </summary>
/// <param name="fixture">Shared API host.</param>
[Collection(ApiCollection.Name)]
public sealed class StudioWebApiTests(ApiFixture fixture)
{
    private const string SiteOrigin = "http://localhost:5173";

    /// <summary>
    /// The whole path: the password in a new browser asks for a code, a wrong code is refused, the right one signs in, and the
    /// session cookie then identifies the user. The cookies are not readable by scripts and are never sent from another site.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ANewBrowser_NeedsTheCode_AndTheRightCodeSignsIn()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = fixture.CreateClient();
        (await browser.GetAsync("/api/web/auth/session", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var login = await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password });
        var required = await login.ReadJsonAsync();

        login.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        required.GetProperty("status").GetString().ShouldBe("CodeRequired");
        required.GetProperty("maskedEmail").GetString()!.ShouldNotContain(user.Username);
        var challengeCookie = SetCookie(login, "ps-challenge");
        challengeCookie.ShouldContain("httponly", Case.Insensitive);
        challengeCookie.ShouldContain("samesite=strict", Case.Insensitive);
        (await browser.GetAsync("/api/web/auth/session", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var wrong = await PostAsync(browser, "/api/web/auth/code/verify", new { code = "000000", rememberDevice = false });
        wrong.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await wrong.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("auth.invalid_login_code");

        var verify = await PostAsync(browser, "/api/web/auth/code/verify", new { code = CodeOf(user), rememberDevice = false });
        verify.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await verify.ReadJsonAsync()).GetProperty("user").GetProperty("username").GetString().ShouldBe(user.Username);
        var sessionCookie = SetCookie(verify, "ps-session");
        sessionCookie.ShouldContain("httponly", Case.Insensitive);
        sessionCookie.ShouldContain("samesite=strict", Case.Insensitive);
        sessionCookie.ShouldContain("path=/", Case.Insensitive);

        var session = await browser.GetAsync("/api/web/auth/session", TestContext.Current.CancellationToken);
        session.StatusCode.ShouldBe(HttpStatusCode.OK);
        session.Headers.CacheControl?.NoStore.ShouldBeTrue();
        (await session.ReadJsonAsync()).GetProperty("user").GetProperty("username").GetString().ShouldBe(user.Username);
        var galleries = await (await browser.GetAsync("/api/web/galleries", TestContext.Current.CancellationToken)).ReadJsonAsync();
        galleries.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    /// <summary>
    /// The cookie of the web area opens the web area and nothing else, and the token of the app does not open the web area: the two
    /// worlds do not mix.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheCookieOpensOnlyTheWebArea_AndTheAppTokenDoesNotOpenIt()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = await SignInAsync(user, remember: false);

        (await browser.GetAsync("/api/web/galleries", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await browser.GetAsync("/api/bookings", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await browser.GetAsync("/api/galleries", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await browser.GetAsync("/api/account/email/resend", TestContext.Current.CancellationToken)).StatusCode.ShouldNotBe(HttpStatusCode.OK);

        var app = fixture.CreateClient();
        var tokens = await app.LoginAsync(user);
        (await app.SendAsync(HttpMethod.Get, "/api/web/galleries", tokens.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await app.SendAsync(HttpMethod.Get, "/api/web/auth/session", tokens.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A browser the user chose to remember skips the code the next time, and a different browser does not.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ARememberedBrowser_SkipsTheCode_AndAnotherBrowserDoesNot()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = await SignInAsync(user, remember: true);
        (await PostAsync(browser, "/api/web/auth/logout", new { forgetDevice = false })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await browser.GetAsync("/api/web/auth/session", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var emailsBefore = fixture.Emails.Messages.Count;

        var again = await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password });

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.ReadJsonAsync()).GetProperty("status").GetString().ShouldBe("SignedIn");
        fixture.Emails.Messages.Count.ShouldBe(emailsBefore);
        SetCookie(again, "ps-device").ShouldContain("httponly", Case.Insensitive);
        (await browser.GetAsync("/api/web/auth/session", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var other = fixture.CreateClient();
        (await PostAsync(other, "/api/web/auth/login", new { username = user.Username, password = user.Password })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>
    /// A remembered browser still needs the password: the cookie skips the code and nothing else.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ARememberedBrowser_StillNeedsThePassword()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = await SignInAsync(user, remember: true);
        await PostAsync(browser, "/api/web/auth/logout", new { forgetDevice = false });

        var wrong = await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = "not the password" });

        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await browser.GetAsync("/api/web/auth/session", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Signing out and forgetting the browser makes the next sign-in ask for the code again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ForgettingTheBrowser_MakesTheNextSignInAskForTheCode()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = await SignInAsync(user, remember: true);
        await PostAsync(browser, "/api/web/auth/logout", new { forgetDevice = true });

        var again = await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password });

        again.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>
    /// Signing out closes the session on the server: a copy of the cookie that someone kept stops working.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task SigningOut_KillsACopyOfTheCookie()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = fixture.CreateClient();
        await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password });
        var verify = await PostAsync(browser, "/api/web/auth/code/verify", new { code = CodeOf(user), rememberDevice = false });
        var copy = CookieValue(SetCookie(verify, "ps-session"), "ps-session");
        (await WithCookie(fixture.CreateClient(), "ps-session", copy).GetAsync("/api/web/auth/session", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await PostAsync(browser, "/api/web/auth/logout", new { });

        (await WithCookie(fixture.CreateClient(), "ps-session", copy).GetAsync("/api/web/auth/session", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Changing the password signs the user out of the web area and makes it forget the remembered browsers: whoever had the old
    /// password, or a copy of a cookie, is out.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ChangingThePassword_SignsOutTheWebArea_AndForgetsTheBrowsers()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = await SignInAsync(user, remember: true);
        var app = fixture.CreateClient();
        var tokens = await app.LoginAsync(user);

        var changed = await app.SendAsync(HttpMethod.Post, "/api/account/password", tokens.AccessToken, new { currentPassword = user.Password, newPassword = "a brand new password" });
        changed.EnsureSuccessStatusCode();

        (await browser.GetAsync("/api/web/auth/session", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var again = await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = "a brand new password" });
        again.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>
    /// A request that changes something must come from the site of the web area: another site, or no origin at all, is refused with
    /// 403 before anything is read, and reading is not affected.
    /// </summary>
    /// <param name="origin">The origin the request claims, or none.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("http://localhost:5174")]
    [InlineData(null)]
    public async Task ARequestFromAnotherSite_IsRefused(string? origin)
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = fixture.CreateClient();
        var emailsBefore = fixture.Emails.Messages.Count;

        var response = await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password }, origin);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("request.forbidden_origin");
        fixture.Emails.Messages.Count.ShouldBe(emailsBefore);
        (await browser.GetAsync("/api/web/auth/session", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// CORS names the one site and allows credentials only for it; another site gets no permission at all.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Cors_AllowsCredentialsOnlyForTheSiteOfTheWebArea()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();

        var allowed = await Preflight(client, SiteOrigin);
        var foreign = await Preflight(client, "https://evil.example.com");

        allowed.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([SiteOrigin]);
        allowed.Headers.GetValues("Access-Control-Allow-Credentials").ShouldBe(["true"]);
        foreign.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
        foreign.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }

    /// <summary>
    /// The code is tied to the browser that gave the password: the right code sent from another browser is refused, and the real
    /// browser can still use it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheCode_IsTiedToTheBrowserThatGaveThePassword()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = fixture.CreateClient();
        await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password });
        var code = CodeOf(user);

        var stranger = await PostAsync(fixture.CreateClient(), "/api/web/auth/code/verify", new { code, rememberDevice = false });
        var owner = await PostAsync(browser, "/api/web/auth/code/verify", new { code, rememberDevice = false });

        stranger.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        owner.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A code works once: using it again after it signed in is refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheCode_WorksOnce()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = fixture.CreateClient();
        await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password });
        var code = CodeOf(user);
        (await PostAsync(browser, "/api/web/auth/code/verify", new { code, rememberDevice = false })).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await PostAsync(browser, "/api/web/auth/code/verify", new { code, rememberDevice = false })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// A new code cannot be asked for right after the first one: 429 with the time to wait.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AskingForANewCodeRightAway_IsRateLimited()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = fixture.CreateClient();
        await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password });

        var resend = await PostAsync(browser, "/api/web/auth/code/resend", new { });
        var secondLogin = await PostAsync(fixture.CreateClient(), "/api/web/auth/login", new { username = user.Username, password = user.Password });

        resend.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await resend.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("auth.login_code_rate_limited");
        resend.Headers.RetryAfter.ShouldNotBeNull();
        secondLogin.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// A browser that never gave the password cannot ask for codes.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ABrowserWithoutAPasswordStep_CannotResendOrVerify()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var browser = fixture.CreateClient();

        (await PostAsync(browser, "/api/web/auth/code/resend", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await PostAsync(browser, "/api/web/auth/code/verify", new { code = "123456", rememberDevice = true })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// An account whose email is not verified cannot use the web area, because the second factor goes to that email; no code is sent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AnAccountWithoutAVerifiedEmail_IsDenied()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync();
        var emailsBefore = fixture.Emails.Messages.Count;

        var response = await PostAsync(fixture.CreateClient(), "/api/web/auth/login", new { username = user.Username, password = user.Password });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("auth.web_requires_verified_email");
        fixture.Emails.Messages.Count.ShouldBe(emailsBefore);
    }

    /// <summary>
    /// A wrong password and an unknown user get the same answer, and the failures count toward the same lockout as the app's.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WrongPasswords_AreTheSameAnswer_AndLockTheAccount()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var user = await fixture.CreateUserAsync(verifiedEmail: true);
        var browser = fixture.CreateClient();

        var unknown = await PostAsync(browser, "/api/web/auth/login", new { username = "nobody-here", password = "whatever password" });
        var codes = new List<string?>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = "not the password" });
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            codes.Add((await response.ReadJsonAsync()).GetProperty("code").GetString());
        }

        var locked = await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password });

        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await unknown.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe(codes[0]);
        locked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await locked.ReadJsonAsync()).GetProperty("code").GetString().ShouldBe("auth.account_locked");
    }

    /// <summary>
    /// Signs a user in the web area, with the code read from the email, and returns the browser that is now signed in.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="remember">Whether to remember the browser.</param>
    /// <returns>The browser.</returns>
    private async Task<HttpClient> SignInAsync(TestUser user, bool remember)
    {
        var browser = fixture.CreateClient();
        (await PostAsync(browser, "/api/web/auth/login", new { username = user.Username, password = user.Password })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await PostAsync(browser, "/api/web/auth/code/verify", new { code = CodeOf(user), rememberDevice = remember })).StatusCode.ShouldBe(HttpStatusCode.OK);
        return browser;
    }

    /// <summary>
    /// Reads the code of the last email sent to the user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The six digits.</returns>
    private string CodeOf(TestUser user) => fixture.Emails.LastCodeFor($"{user.Username}@example.com");

    /// <summary>
    /// Sends a POST the way the page of the web area does: JSON, with the origin of the site.
    /// </summary>
    /// <param name="client">The browser.</param>
    /// <param name="path">Request path.</param>
    /// <param name="body">JSON body.</param>
    /// <param name="origin">The origin to send; none when <see langword="null"/>.</param>
    /// <returns>The response.</returns>
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string? origin = SiteOrigin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Sends the preflight request a browser sends before a request with credentials.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="origin">The origin of the page.</param>
    /// <returns>The response.</returns>
    private static async Task<HttpResponseMessage> Preflight(HttpClient client, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/web/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reads the <c>Set-Cookie</c> header the response sets for a cookie.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="name">Cookie name.</param>
    /// <returns>The whole header value, with its attributes.</returns>
    private static string SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith(name + "=", StringComparison.Ordinal) && !value.StartsWith(name + "=;", StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"The response does not set the cookie {name}.")
            : throw new InvalidOperationException("The response sets no cookies.");

    /// <summary>
    /// Extracts the value of a cookie from a <c>Set-Cookie</c> header.
    /// </summary>
    /// <param name="header">The header value.</param>
    /// <param name="name">Cookie name.</param>
    /// <returns>The value.</returns>
    private static string CookieValue(string header, string name) => header[(name.Length + 1)..header.IndexOf(';', StringComparison.Ordinal)];

    /// <summary>
    /// Makes a client send a cookie by hand, as a thief with a copy of it would.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="name">Cookie name.</param>
    /// <param name="value">Cookie value.</param>
    /// <returns>The same client.</returns>
    private static HttpClient WithCookie(HttpClient client, string name, string value)
    {
        client.DefaultRequestHeaders.Add("Cookie", $"{name}={value}");
        return client;
    }
}
