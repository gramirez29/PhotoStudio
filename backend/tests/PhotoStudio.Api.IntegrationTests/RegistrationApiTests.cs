using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// End-to-end tests of account creation, against the API running in memory.
/// </summary>
/// <param name="fixture">Shared API instance.</param>
[Collection(ApiCollection.Name)]
public sealed class RegistrationApiTests(ApiFixture fixture)
{
    /// <summary>
    /// Registering creates the account and returns a session the API accepts, so the person is signed in right away.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WithValidData_Returns201WithASessionTheApiAccepts()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var username = NewUsername();

        var response = await client.RegisterRawAsync(username);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var session = await response.ReadSessionAsync();
        session.Username.ShouldBe(username);
        session.PhotographerId.ShouldNotBe(Guid.Empty);
        (await response.ReadJsonAsync()).GetProperty("name").GetString().ShouldBe("Ana Pérez");
        (await response.ReadJsonAsync()).GetProperty("email").GetString().ShouldBe($"{username}@example.com");
        (await client.SendAsync(HttpMethod.Get, "/api/bookings", session.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The account created by registering can sign in later with the same credentials, whatever the case of the username.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_ThenLogin_WorksWithTheSameCredentials()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var username = NewUsername();
        var registered = await (await client.RegisterRawAsync(username, "my password 123")).ReadSessionAsync();

        var login = await client.LoginRawAsync(username.ToUpperInvariant(), "my password 123");

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await login.ReadSessionAsync()).PhotographerId.ShouldBe(registered.PhotographerId);
    }

    /// <summary>
    /// The database keeps only a hash of the password, plus the name and phone that were sent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_StoresTheUserWithOnlyAHashOfThePassword()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var username = NewUsername();
        const string password = "plain text secret 987";
        await fixture.CreateClient().RegisterRawAsync(username, password, "  Ana Pérez ", "+506 7018-9220", $"  {username.ToUpperInvariant()}@Example.com ");

        var stored = await fixture.Database.GetCollection<BsonDocument>("users")
            .Find(new BsonDocument("username", username))
            .SingleAsync(TestContext.Current.CancellationToken);

        stored["name"].AsString.ShouldBe("Ana Pérez");
        stored["email"].AsString.ShouldBe($"{username}@example.com");
        stored["phone"].AsString.ShouldBe("+50670189220");
        stored["passwordHash"].AsString.ShouldNotBeNullOrWhiteSpace();
        stored["passwordHash"].AsString.ShouldNotContain(password);
        stored.ToJson().ShouldNotContain(password);
    }

    /// <summary>
    /// A username that is already in use is refused, even when it differs only in case.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WithATakenUsername_Returns409()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var username = NewUsername();
        (await client.RegisterRawAsync(username)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var again = await client.RegisterRawAsync(username.ToUpperInvariant());

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.ReadCodeAsync()).ShouldBe("user.username_taken");
    }

    /// <summary>
    /// An email that is already registered is refused with its own code, whatever the username and the case of the email.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WithATakenEmail_Returns409WithItsOwnCode()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var email = $"{NewUsername()}@example.com";
        (await client.RegisterRawAsync(NewUsername(), email: email)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var again = await client.RegisterRawAsync(NewUsername(), email: email.ToUpperInvariant());

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.ReadCodeAsync()).ShouldBe("user.email_taken");
    }

    /// <summary>
    /// Simultaneous registrations of the same username create exactly one account.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhenManyRequestsRaceForTheSameUsername_ExactlyOneWins()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var username = NewUsername();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await Task.Yield();
            return await fixture.CreateClient().RegisterRawAsync(username);
        }));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(3);
        (await fixture.Database.GetCollection<BsonDocument>("users").CountDocumentsAsync(new BsonDocument("username", username), cancellationToken: TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    /// <summary>
    /// Each invalid field is refused with 422 and its own stable code, and nothing is created.
    /// </summary>
    /// <param name="username">Username to send.</param>
    /// <param name="email">Email to send.</param>
    /// <param name="password">Password to send.</param>
    /// <param name="name">Name to send.</param>
    /// <param name="phone">Phone to send.</param>
    /// <param name="expectedCode">Code the refusal must carry.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("a", "ana@example.com", "a long enough password", "Ana", "70189220", "user.invalid_username")]
    [InlineData("ana smith", "ana@example.com", "a long enough password", "Ana", "70189220", "user.invalid_username")]
    [InlineData("valid.name", "not-an-email", "a long enough password", "Ana", "70189220", "user.invalid_email")]
    [InlineData("valid.name", "", "a long enough password", "Ana", "70189220", "user.invalid_email")]
    [InlineData("valid.name", "ana@example.com", "short", "Ana", "70189220", "user.weak_password")]
    [InlineData("valid.name", "ana@example.com", "a long enough password", "   ", "70189220", "user.invalid_name")]
    [InlineData("valid.name", "ana@example.com", "a long enough password", "Ana", "12ab", "user.invalid_phone")]
    public async Task Register_WithAnInvalidField_Returns422(string username, string email, string password, string name, string phone, string expectedCode)
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");

        var response = await fixture.CreateClient().RegisterRawAsync(username, password, name, phone, email);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadCodeAsync()).ShouldBe(expectedCode);
    }

    /// <summary>
    /// Registration is limited to five accounts per hour per client address; another address is not affected.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_IsRateLimitedPerClientAddress()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var noisy = fixture.CreateClient();
        var quiet = fixture.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await noisy.RegisterRawAsync(NewUsername())).StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var limited = await noisy.RegisterRawAsync(NewUsername());
        var other = await quiet.RegisterRawAsync(NewUsername());

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await limited.ReadCodeAsync()).ShouldBe("rate_limit.exceeded");
        other.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>
    /// Two people who register are separate tenants: neither sees the other's bookings.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task EachRegisteredUser_IsItsOwnTenant()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var client = fixture.CreateClient();
        var ana = await (await client.RegisterRawAsync(NewUsername())).ReadSessionAsync();
        var luis = await (await fixture.CreateClient().RegisterRawAsync(NewUsername())).ReadSessionAsync();
        var booking = await client.CreateBookingAsync(ana.AccessToken);

        var luisList = await client.SendAsync(HttpMethod.Get, "/api/bookings", luis.AccessToken);
        var luisRead = await client.SendAsync(HttpMethod.Get, $"/api/bookings/{booking.GetProperty("id").GetGuid()}", luis.AccessToken);

        ana.PhotographerId.ShouldNotBe(luis.PhotographerId);
        (await luisList.ReadJsonAsync()).GetArrayLength().ShouldBe(0);
        luisRead.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// With sign-up turned off, registering answers 403 and creates nothing, while existing users can still sign in.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Register_WhenRegistrationIsDisabled_Returns403AndLoginStillWorks()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var existing = await fixture.CreateUserAsync();
        var username = NewUsername();
        Environment.SetEnvironmentVariable("AUTH_REGISTRATION_ENABLED", "false");
        try
        {
            await using var closed = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
            var client = closed.CreateClient();

            var register = await client.RegisterRawAsync(username);
            var login = await client.LoginRawAsync(existing.Username, existing.Password);

            register.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await register.ReadCodeAsync()).ShouldBe("auth.registration_disabled");
            login.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTH_REGISTRATION_ENABLED", null);
        }

        (await fixture.Database.GetCollection<BsonDocument>("users").CountDocumentsAsync(new BsonDocument("username", username), cancellationToken: TestContext.Current.CancellationToken))
            .ShouldBe(0);
    }

    /// <summary>
    /// Builds a username that is valid and unique.
    /// </summary>
    /// <returns>The username.</returns>
    private static string NewUsername() => $"u{Guid.NewGuid():N}"[..14];
}
