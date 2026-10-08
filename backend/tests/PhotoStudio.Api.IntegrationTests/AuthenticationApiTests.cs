using System.Net;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of sign-in, token refresh and sign-out, against the API running in memory.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class AuthenticationApiTests(ApiFixture fixture)
{
    /// <summary>
    /// Correct credentials return a session whose access token the API accepts.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsASessionTheApiAccepts()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var account = await fixture.CreateAccountAsync();
        var client = fixture.CreateClient();

        var session = await client.LoginAsync(account);

        session.PhotographerId.ShouldBe(account.Id);
        session.Email.ShouldBe(account.Email);
        session.AccessToken.ShouldNotBeNullOrWhiteSpace();
        session.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        var response = await client.SendAsync(HttpMethod.Get, "/api/bookings", session.AccessToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The email is matched ignoring case and surrounding spaces.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_IgnoresTheCaseAndSpacesOfTheEmail()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var account = await fixture.CreateAccountAsync();

        var response = await fixture.CreateClient().LoginRawAsync($"  {account.Email.ToUpperInvariant()} ", account.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A wrong password and an unknown email produce the same response, so the API does not reveal which emails exist.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownEmail_ReturnsTheSameGenericFailure()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var account = await fixture.CreateAccountAsync();
        var client = fixture.CreateClient();

        var wrongPassword = await client.LoginRawAsync(account.Email, "not the password");
        var unknownEmail = await client.LoginRawAsync("nobody@example.com", "not the password");

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await wrongPassword.ReadCodeAsync()).ShouldBe("auth.invalid_credentials");
        (await unknownEmail.ReadCodeAsync()).ShouldBe("auth.invalid_credentials");
        (await wrongPassword.ReadJsonAsync()).GetProperty("detail").GetString()
            .ShouldBe((await unknownEmail.ReadJsonAsync()).GetProperty("detail").GetString());
        wrongPassword.Headers.WwwAuthenticate.ToString().ShouldBe("Bearer");
    }

    /// <summary>
    /// A malformed body or an empty password is a generic failure, not a server error.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_WithEmptyCredentials_IsRejectedWithoutAServerError()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();

        var response = await client.LoginRawAsync(string.Empty, string.Empty);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// After five wrong passwords the account is locked: even the correct password is refused, with the time to wait.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_AfterFiveFailures_LocksTheAccountEvenForTheCorrectPassword()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var account = await fixture.CreateAccountAsync();
        var client = fixture.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await client.LoginRawAsync(account.Email, "wrong")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var locked = await client.LoginRawAsync(account.Email, account.Password);

        locked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await locked.ReadCodeAsync()).ShouldBe("auth.account_locked");
        var retryAfter = int.Parse(locked.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture);
        retryAfter.ShouldBeInRange(1, 15 * 60);
    }

    /// <summary>
    /// Four failures followed by the right password do not lock, and the success clears the counter.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_ASuccessBeforeTheLimit_ClearsTheFailures()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var account = await fixture.CreateAccountAsync();
        var client = fixture.CreateClient();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await client.LoginRawAsync(account.Email, "wrong");
        }

        (await client.LoginRawAsync(account.Email, account.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await client.LoginRawAsync(account.Email, "wrong");
        }

        (await client.LoginRawAsync(account.Email, account.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Sign-in is limited to ten calls per minute per client address; another address is not affected.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_IsRateLimitedPerClientAddress()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var noisy = fixture.CreateClient();
        var quiet = fixture.CreateClient();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            (await noisy.LoginRawAsync("nobody@example.com", "x")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var limited = await noisy.LoginRawAsync("nobody@example.com", "x");
        var other = await quiet.LoginRawAsync("nobody@example.com", "x");

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await limited.ReadCodeAsync()).ShouldBe("rate_limit.exceeded");
        other.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Refreshing returns a new session and the old refresh token stops working.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Refresh_RotatesTheToken()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var first = await client.LoginAsync(await fixture.CreateAccountAsync());

        var refreshed = await client.RefreshRawAsync(first.RefreshToken);

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = await refreshed.ReadSessionAsync();
        second.RefreshToken.ShouldNotBe(first.RefreshToken);
        second.PhotographerId.ShouldBe(first.PhotographerId);
        (await client.SendAsync(HttpMethod.Get, "/api/bookings", second.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Using a refresh token a second time is treated as theft: it is refused and the new token of the same login dies too.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Refresh_WithAnAlreadyUsedToken_RevokesTheWholeLogin()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var first = await client.LoginAsync(await fixture.CreateAccountAsync());
        var second = await (await client.RefreshRawAsync(first.RefreshToken)).ReadSessionAsync();

        var replay = await client.RefreshRawAsync(first.RefreshToken);
        var legitimate = await client.RefreshRawAsync(second.RefreshToken);

        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await replay.ReadCodeAsync()).ShouldBe("auth.invalid_refresh_token");
        legitimate.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Revoking one login does not sign out the other devices of the same photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Refresh_ReplayOnOneDevice_DoesNotSignOutAnotherDevice()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var account = await fixture.CreateAccountAsync();
        var client = fixture.CreateClient();
        var phone = await client.LoginAsync(account);
        var tablet = await client.LoginAsync(account);
        await client.RefreshRawAsync(phone.RefreshToken);
        await client.RefreshRawAsync(phone.RefreshToken);

        var tabletRefresh = await client.RefreshRawAsync(tablet.RefreshToken);

        tabletRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A token nobody issued, or an empty one, is refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Refresh_WithAnUnknownOrEmptyToken_Returns401()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();

        (await client.RefreshRawAsync("never-issued")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.RefreshRawAsync(string.Empty)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Signing out revokes the session, and signing out again, or with an unknown token, is still a quiet 204.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Logout_RevokesTheSessionAndIsIdempotent()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(await fixture.CreateAccountAsync());

        var first = await client.SendAsync(HttpMethod.Post, "/api/auth/logout", null, new { refreshToken = session.RefreshToken });
        var again = await client.SendAsync(HttpMethod.Post, "/api/auth/logout", null, new { refreshToken = session.RefreshToken });
        var unknown = await client.SendAsync(HttpMethod.Post, "/api/auth/logout", null, new { refreshToken = "never-issued" });

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.RefreshRawAsync(session.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
