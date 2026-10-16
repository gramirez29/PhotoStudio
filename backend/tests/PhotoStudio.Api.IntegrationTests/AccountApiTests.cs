using System.Net;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the account endpoints (change password), against the API running in memory.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class AccountApiTests(ApiFixture fixture)
{
    private const string NewPassword = "an even better passphrase";

    /// <summary>
    /// Changing the password closes the other sessions, keeps the current device signed in and replaces the password.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ChangePassword_ReplacesThePasswordAndClosesTheOtherSessions()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var account = await fixture.CreateUserAsync();
        var client = fixture.CreateClient();
        var thisDevice = await client.LoginAsync(account);
        var otherDevice = await client.LoginAsync(account);

        var response = await client.SendAsync(
            HttpMethod.Post,
            "/api/account/password",
            thisDevice.AccessToken,
            new { currentPassword = account.Password, newPassword = NewPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renewed = await response.ReadSessionAsync();
        (await client.RefreshRawAsync(otherDevice.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.RefreshRawAsync(thisDevice.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.RefreshRawAsync(renewed.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.LoginRawAsync(account.Username, account.Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.LoginRawAsync(account.Username, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A wrong current password is a 422 (not a 401, which would make the app try to renew a session that is fine) and
    /// changes nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ChangePassword_WithAWrongCurrentPassword_Answers422AndKeepsThePassword()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var account = await fixture.CreateUserAsync();
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(account);

        var response = await client.SendAsync(
            HttpMethod.Post,
            "/api/account/password",
            session.AccessToken,
            new { currentPassword = "not the password", newPassword = NewPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadCodeAsync()).ShouldBe("user.invalid_current_password");
        (await client.LoginRawAsync(account.Username, account.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A weak new password is a 422 with the password code.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ChangePassword_WithAWeakNewPassword_Answers422()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var account = await fixture.CreateUserAsync();
        var client = fixture.CreateClient();
        var session = await client.LoginAsync(account);

        var response = await client.SendAsync(
            HttpMethod.Post,
            "/api/account/password",
            session.AccessToken,
            new { currentPassword = account.Password, newPassword = "short" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadCodeAsync()).ShouldBe("user.weak_password");
    }

    /// <summary>
    /// The endpoint requires a signed-in user.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ChangePassword_WithoutAToken_Answers401()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var response = await fixture.CreateClient().SendAsync(
            HttpMethod.Post,
            "/api/account/password",
            accessToken: null,
            new { currentPassword = "x", newPassword = NewPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
