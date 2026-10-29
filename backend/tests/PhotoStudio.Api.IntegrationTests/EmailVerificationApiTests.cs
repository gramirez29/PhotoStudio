using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of the verification of the email of an account, against a copy of the API that requires it, as in production:
/// registering sends a code, an unverified account can only verify, and verifying opens the app with a new session.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class EmailVerificationApiTests(ApiFixture fixture)
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    /// <summary>
    /// The whole flow: registering sends a code to the new address, the session of the new account cannot use the app, the right
    /// code opens it with a new session, and the old token stays refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Flow_RegisterCodeVerifyAndUseTheApp()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var username = NewUsername();

        var registered = await client.RegisterRawAsync(username);
        registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        var registeredJson = await registered.ReadJsonAsync();
        registeredJson.GetProperty("emailVerified").GetBoolean().ShouldBeFalse();
        var unverified = registeredJson.GetProperty("accessToken").GetString()!;
        var code = fixture.Emails.LastCodeFor($"{username}@example.com");

        (await client.SendAsync(HttpMethod.Get, "/api/bookings", unverified)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var verified = await client.SendAsync(HttpMethod.Post, "/api/account/email/verify", unverified, new { code });
        verified.StatusCode.ShouldBe(HttpStatusCode.OK);
        var verifiedJson = await verified.ReadJsonAsync();
        verifiedJson.GetProperty("emailVerified").GetBoolean().ShouldBeTrue();
        var open = verifiedJson.GetProperty("accessToken").GetString()!;

        (await client.SendAsync(HttpMethod.Get, "/api/bookings", open)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.SendAsync(HttpMethod.Get, "/api/bookings", unverified)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await client.CreateBookingAsync(open);
    }

    /// <summary>
    /// An account that has not verified its email is kept out of everything except the two endpoints that verify it.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Path of an endpoint of the API.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("GET", "/api/bookings")]
    [InlineData("GET", "/api/notifications")]
    [InlineData("GET", "/api/billing/refunds")]
    [InlineData("POST", "/api/push-devices")]
    [InlineData("POST", "/api/account/password")]
    [InlineData("POST", "/api/maintenance/run")]
    public async Task UnverifiedAccount_IsRefusedEverywhereElse(string method, string path)
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var session = await (await client.RegisterRawAsync(NewUsername())).ReadSessionAsync();

        var response = await client.SendAsync(new HttpMethod(method), path, session.AccessToken, method == "POST" ? new { } : null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Signing in with an account that is not verified works, since the user needs a session to verify, and says so.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Login_OfAnUnverifiedAccount_SaysTheEmailIsNotVerified()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var username = NewUsername();
        await client.RegisterRawAsync(username, "a long enough password");

        var login = await client.LoginRawAsync(username, "a long enough password");

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await login.ReadJsonAsync();
        json.GetProperty("emailVerified").GetBoolean().ShouldBeFalse();
        var refreshed = await client.RefreshRawAsync(json.GetProperty("refreshToken").GetString()!);
        (await refreshed.ReadJsonAsync()).GetProperty("emailVerified").GetBoolean().ShouldBeFalse();
    }

    /// <summary>
    /// A wrong code is a 422, and after five wrong guesses even the right code no longer works.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Verify_AfterFiveWrongGuesses_SpendsTheCode()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var username = NewUsername();
        var session = await (await client.RegisterRawAsync(username)).ReadSessionAsync();
        var right = fixture.Emails.LastCodeFor($"{username}@example.com");
        var wrong = right == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.SendAsync(HttpMethod.Post, "/api/account/email/verify", session.AccessToken, new { code = wrong });
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await response.ReadCodeAsync()).ShouldBe("auth.invalid_email_code");
        }

        var afterwards = await client.SendAsync(HttpMethod.Post, "/api/account/email/verify", session.AccessToken, new { code = right });
        afterwards.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// A code works once: after it verified the account, the same code is not needed and does nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Verify_TheCodeIsSingleUse()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var username = NewUsername();
        var session = await (await client.RegisterRawAsync(username)).ReadSessionAsync();
        var code = fixture.Emails.LastCodeFor($"{username}@example.com");
        var first = await client.SendAsync(HttpMethod.Post, "/api/account/email/verify", session.AccessToken, new { code });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var open = (await first.ReadSessionAsync()).AccessToken;

        var resend = await client.SendAsync(HttpMethod.Post, "/api/account/email/resend", open);

        resend.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await resend.ReadCodeAsync()).ShouldBe("auth.email_already_verified");
    }

    /// <summary>
    /// A second code asked for right away is refused with the time to wait; the first one keeps working.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Resend_RightAfterRegistering_AnswersTooManyRequestsWithRetryAfter()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var username = NewUsername();
        var session = await (await client.RegisterRawAsync(username)).ReadSessionAsync();

        var resend = await client.SendAsync(HttpMethod.Post, "/api/account/email/resend", session.AccessToken);

        resend.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await resend.ReadCodeAsync()).ShouldBe("auth.email_code_rate_limited");
        int.Parse(resend.Headers.GetValues("Retry-After").Single(), CultureInfo.InvariantCulture).ShouldBeInRange(1, 60);
        var code = fixture.Emails.LastCodeFor($"{username}@example.com");
        (await client.SendAsync(HttpMethod.Post, "/api/account/email/verify", session.AccessToken, new { code })).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// When the cooldown has passed, a new code replaces the old one: only the newest works, and it goes to the address of the account.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Resend_AfterTheCooldown_SendsANewCodeAndTheOldOneStopsWorking()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var username = NewUsername();
        var session = await (await client.RegisterRawAsync(username)).ReadSessionAsync();
        var address = $"{username}@example.com";
        var firstCode = fixture.Emails.LastCodeFor(address);
        await AgeTheCodeAsync(session.PhotographerId);

        var resend = await client.SendAsync(HttpMethod.Post, "/api/account/email/resend", session.AccessToken);

        resend.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await resend.ReadJsonAsync()).GetProperty("maskedEmail").GetString().ShouldBe($"{address[0]}***@example.com");
        var secondCode = fixture.Emails.LastCodeFor(address);
        if (secondCode != firstCode)
        {
            (await client.SendAsync(HttpMethod.Post, "/api/account/email/verify", session.AccessToken, new { code = firstCode })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }

        (await client.SendAsync(HttpMethod.Post, "/api/account/email/verify", session.AccessToken, new { code = secondCode })).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifying, resending and signing in need a token: without one the answer is a 401.
    /// </summary>
    /// <param name="path">Path of the endpoint.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("/api/account/email/verify")]
    [InlineData("/api/account/email/resend")]
    public async Task EmailEndpoints_WithoutAToken_Answer401(string path)
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);

        var response = await client.SendAsync(HttpMethod.Post, path, null, new { code = "123456" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// An account created before the email existed has none, can never receive a code, and so is not locked out; an old one that
    /// has an email but never verified it is asked to verify.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task LegacyAccounts_WithoutEmailAreNotLockedOut_AndWithEmailAreAskedToVerify()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var withoutEmail = await AddLegacyUserAsync(factory, email: string.Empty);
        var withEmail = await AddLegacyUserAsync(factory, email: "legacy@example.com");

        var first = await (await client.LoginRawAsync(withoutEmail.Username, withoutEmail.Password)).ReadJsonAsync();
        var second = await (await client.LoginRawAsync(withEmail.Username, withEmail.Password)).ReadJsonAsync();

        first.GetProperty("emailVerified").GetBoolean().ShouldBeTrue();
        (await client.SendAsync(HttpMethod.Get, "/api/bookings", first.GetProperty("accessToken").GetString())).StatusCode.ShouldBe(HttpStatusCode.OK);
        second.GetProperty("emailVerified").GetBoolean().ShouldBeFalse();
        (await client.SendAsync(HttpMethod.Get, "/api/bookings", second.GetProperty("accessToken").GetString())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// With the requirement turned off nothing is sent and the account can use the app right away.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WhenNotRequired_RegisteringSendsNoCodeAndTheAccountCanUseTheApp()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var client = fixture.CreateClient();
        var username = NewUsername();
        var before = fixture.Emails.Messages.Count;

        var registered = await client.RegisterRawAsync(username);

        var json = await registered.ReadJsonAsync();
        json.GetProperty("emailVerified").GetBoolean().ShouldBeTrue();
        fixture.Emails.Messages.Skip(before).Any(message => message.To == $"{username}@example.com").ShouldBeFalse();
        (await client.SendAsync(HttpMethod.Get, "/api/bookings", json.GetProperty("accessToken").GetString())).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A token issued before verification existed carries no claim about it and keeps working, so deploying does not cut open sessions.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ATokenWithoutTheClaim_IsStillAccepted()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await using var factory = fixture.CreateFactoryRequiringEmailVerification();
        var client = fixture.CreateClient(factory);
        var user = await AddLegacyUserAsync(factory, email: "legacy2@example.com");
        var token = CreateTokenWithoutTheClaim(user.Id);

        (await client.SendAsync(HttpMethod.Get, "/api/bookings", token)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Builds an access token like the ones issued before verification existed: signed by the API, with no claim about the email.
    /// </summary>
    /// <param name="userId">User the token is for.</param>
    /// <returns>The signed token.</returns>
    private static string CreateTokenWithoutTheClaim(Guid userId) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = ApiFixture.Issuer,
            Audience = ApiFixture.Audience,
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFixture.SigningKey)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]),
        });

    /// <summary>
    /// Makes the code of a user old enough to ask for another one, by moving back the instants stored with it.
    /// </summary>
    /// <param name="userId">User whose code is aged.</param>
    /// <returns>A task that completes when the code was changed.</returns>
    private async Task AgeTheCodeAsync(Guid userId)
    {
        var collection = fixture.Database.GetCollection<MongoDB.Bson.BsonDocument>("email_verifications");
        var past = DateTime.UtcNow.AddMinutes(-5);
        await collection.UpdateOneAsync(
            new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(userId, MongoDB.Bson.GuidRepresentation.Standard)),
            new MongoDB.Bson.BsonDocument("$set", new MongoDB.Bson.BsonDocument { { "sentAt", past }, { "windowStartedAt", past } }),
            cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Stores a user like the ones created before verification existed: without the verification instant.
    /// </summary>
    /// <param name="factory">Factory of the API to store the user in.</param>
    /// <param name="email">Email of the user; empty for the oldest ones.</param>
    /// <returns>The user, with the password to sign in.</returns>
    private static async Task<TestUser> AddLegacyUserAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unique = Guid.NewGuid().ToString("N");
        var user = new TestUser(Guid.CreateVersion7(), $"l{unique}"[..13], "correct horse battery");
        var addressed = email.Length == 0 ? email : $"{unique[..8]}.{email}";
        await repository.AddAsync(
            User.Restore(user.Id, 0, user.Username, addressed, hasher.Hash(user.Password), "Ana Pérez", "+50670189220", DateTimeOffset.UtcNow, 0, null),
            CancellationToken.None);
        return user;
    }

    /// <summary>
    /// Builds a username that is not taken.
    /// </summary>
    /// <returns>The username.</returns>
    private static string NewUsername() => $"v{Guid.NewGuid():N}"[..14];
}
