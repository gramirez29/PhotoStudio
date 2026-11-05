using System.Net;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the recovery of a forgotten password, against a copy of the API that requires the email to be verified, as in
/// production: the code goes to the verified email, choosing the new password closes every session, and nothing the endpoints answer
/// tells which accounts exist.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class PasswordRecoveryApiTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";
    private const string OldPassword = "a long enough password";
    private const string NewPassword = "a different long password";

    /// <summary>
    /// The whole path: the user forgets the password, asks by email, types the code, and from then on only the new password works;
    /// the sessions that existed are closed and the owner is told.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Flow_ForgotPassword_CodeNewPasswordAndSessionsClosed()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var (username, address, session) = await RegisterAndVerifyAsync(client);

        var requested = await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = address.ToUpperInvariant() });
        var code = fixture.Emails.LastCodeFor(address);
        var confirmed = await client.PostJsonAsync("/api/auth/password-reset/confirm", new { identifier = username, code, newPassword = NewPassword });

        requested.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.LoginRawAsync(username, OldPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.LoginRawAsync(username, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.RefreshRawAsync(session.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        fixture.Emails.Messages.Last(message => message.To == address).Subject.ShouldContain("cambió");
    }

    /// <summary>
    /// A code works once: the same code cannot choose a second password.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_TheCodeIsSingleUse()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var (username, address, _) = await RegisterAndVerifyAsync(client);
        await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = username });
        var code = fixture.Emails.LastCodeFor(address);
        (await client.PostJsonAsync("/api/auth/password-reset/confirm", new { identifier = username, code, newPassword = NewPassword })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var again = await client.PostJsonAsync("/api/auth/password-reset/confirm", new { identifier = username, code, newPassword = "yet another password" });

        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await again.ReadCodeAsync()).ShouldBe("auth.invalid_reset_code");
        (await client.LoginRawAsync(username, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Asking for the code of an account that does not exist answers exactly like asking for one that does, and sends nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Request_ForAnUnknownAccount_AnswersLikeForAKnownOne()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var (username, _, _) = await RegisterAndVerifyAsync(client);
        var before = fixture.Emails.Messages.Count;

        var known = await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = username });
        var unknown = await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = $"nobody{Guid.NewGuid():N}@example.com" });

        unknown.StatusCode.ShouldBe(known.StatusCode);
        (await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(await known.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        fixture.Emails.Messages.Count.ShouldBe(before + 1);
    }

    /// <summary>
    /// An account whose email was never verified cannot recover its password by email: an address nobody proved proves nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Request_ForAnUnverifiedAccount_SendsNothing()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var username = NewUsername();
        await client.RegisterRawAsync(username, OldPassword);
        var before = fixture.Emails.Messages.Count(message => message.To == $"{username}@example.com");

        var response = await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = username });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        fixture.Emails.Messages.Count(message => message.To == $"{username}@example.com").ShouldBe(before);
    }

    /// <summary>
    /// A wrong code is a 422, and after five wrong guesses even the right code no longer works.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_AfterFiveWrongGuesses_SpendsTheCode()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var (username, address, _) = await RegisterAndVerifyAsync(client);
        await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = username });
        var right = fixture.Emails.LastCodeFor(address);
        var wrong = right == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostJsonAsync("/api/auth/password-reset/confirm", new { identifier = username, code = wrong, newPassword = NewPassword });
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await response.ReadCodeAsync()).ShouldBe("auth.invalid_reset_code");
        }

        (await client.PostJsonAsync("/api/auth/password-reset/confirm", new { identifier = username, code = right, newPassword = NewPassword })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.LoginRawAsync(username, OldPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A password that is too short is refused and does not spend the code, which still works with a good password.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_WithAWeakPassword_IsRefusedAndTheCodeStillWorks()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var (username, address, _) = await RegisterAndVerifyAsync(client);
        await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = username });
        var code = fixture.Emails.LastCodeFor(address);

        var weak = await client.PostJsonAsync("/api/auth/password-reset/confirm", new { identifier = username, code, newPassword = "short" });

        weak.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await weak.ReadCodeAsync()).ShouldBe("user.weak_password");
        (await client.PostJsonAsync("/api/auth/password-reset/confirm", new { identifier = username, code, newPassword = NewPassword })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A second request right away sends nothing and answers the same: the limit of the code is kept without saying the account exists.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Request_Twice_InsideTheCooldown_SendsOneEmail()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var (username, address, _) = await RegisterAndVerifyAsync(client);
        var before = fixture.Emails.Messages.Count(message => message.To == address);

        var first = await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = username });
        var second = await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = username });

        second.StatusCode.ShouldBe(first.StatusCode);
        fixture.Emails.Messages.Count(message => message.To == address).ShouldBe(before + 1);
    }

    /// <summary>
    /// Recovering the password also lifts the lockout that failed logins caused, so someone who forgot the password and tried too
    /// many times is not locked out of the account they just recovered.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_LiftsTheLockoutOfFailedLogins()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var (username, address, _) = await RegisterAndVerifyAsync(client);
        for (var attempt = 0; attempt < User.MaxFailedLoginAttempts; attempt++)
        {
            await client.LoginRawAsync(username, "not the password");
        }

        (await client.LoginRawAsync(username, OldPassword)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = username });
        var code = fixture.Emails.LastCodeFor(address);

        (await client.PostJsonAsync("/api/auth/password-reset/confirm", new { identifier = username, code, newPassword = NewPassword })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.LoginRawAsync(username, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The recovery of one account cannot be done with the code of another: the code is bound to its user.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Confirm_WithTheCodeOfAnotherAccount_IsRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var (victim, _, _) = await RegisterAndVerifyAsync(client);
        var (attacker, attackerAddress, _) = await RegisterAndVerifyAsync(client);
        await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = attacker });
        await client.PostJsonAsync("/api/auth/password-reset/request", new { identifier = victim });
        var attackersCode = fixture.Emails.LastCodeFor(attackerAddress);

        var response = await client.PostJsonAsync("/api/auth/password-reset/confirm", new { identifier = victim, code = attackersCode, newPassword = NewPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.LoginRawAsync(victim, OldPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Registers an account, verifies its email with the code that was sent, and returns what a test needs about it.
    /// </summary>
    /// <param name="client">HTTP client of the factory that requires verification.</param>
    /// <returns>The username, the address and the session that existed before recovering.</returns>
    private async Task<(string Username, string Address, SessionTokens Session)> RegisterAndVerifyAsync(HttpClient client)
    {
        var username = NewUsername();
        var address = $"{username}@example.com";
        var unverified = await (await client.RegisterRawAsync(username, OldPassword)).ReadSessionAsync();
        var code = fixture.Emails.LastCodeFor(address);
        var verified = await client.SendAsync(HttpMethod.Post, "/api/account/email/verify", unverified.AccessToken, new { code });
        verified.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (username, address, await verified.ReadSessionAsync());
    }

    /// <summary>
    /// Builds a username that is not taken.
    /// </summary>
    /// <returns>The username.</returns>
    private static string NewUsername() => $"r{Guid.NewGuid():N}"[..14];
}
