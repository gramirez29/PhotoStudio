using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Identity;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// A user created for a test, with the clear-text password needed to sign in.
/// </summary>
/// <param name="Id">Photographer (tenant) identifier.</param>
/// <param name="Username">Login name.</param>
/// <param name="Password">Password in clear text.</param>
public sealed record TestUser(Guid Id, string Username, string Password);

/// <summary>
/// Starts the real API in memory once for all the API tests, against a throwaway MongoDB database. The connection string
/// comes from <c>MONGODB_TEST_CONNECTION_STRING</c> and defaults to the replica set of <c>docker-compose.yml</c>. When no
/// server answers, <see cref="IsAvailable"/> is false and the tests skip themselves.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    /// <summary>
    /// Key that signs the access tokens in the tests. It contains the development marker, which is accepted because the
    /// API runs in the Development environment.
    /// </summary>
    public const string SigningKey = "dev-only-signing-key-for-api-tests-0123456789abcdef";

    /// <summary>
    /// Issuer and audience of the tokens, the defaults of the API.
    /// </summary>
    public const string Issuer = "photostudio-api";

    /// <summary>
    /// Audience of the tokens, the default of the API.
    /// </summary>
    public const string Audience = "photostudio-app";

    /// <summary>
    /// Public address of the portal in the tests; it is the origin CORS allows besides the local development server.
    /// </summary>
    public const string PortalBaseUrl = "https://portal.example.test";

    private const string ConnectionStringVariable = "MONGODB_TEST_CONNECTION_STRING";
    private const string DefaultConnectionString = "mongodb://localhost:27019/?directConnection=true";

    private static int _clientCounter;

    private WebApplicationFactory<Program>? _factory;
    private IMongoClient? _mongo;

    /// <summary>
    /// Gets the emails the API tried to send, instead of a real provider.
    /// </summary>
    public CapturingEmailSender Emails { get; } = new();

    /// <summary>
    /// Gets the push notifications the API tried to send, instead of Expo.
    /// </summary>
    public CapturingPushSender Pushes { get; } = new();

    /// <summary>
    /// Gets a value indicating whether a MongoDB server answered the initial ping.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Gets the name of the throwaway database used by the API under test.
    /// </summary>
    public string DatabaseName { get; } = $"photostudio_api_it_{Guid.NewGuid():N}";

    /// <summary>
    /// Connects to MongoDB and, when it is reachable, starts the API pointing at the throwaway database.
    /// </summary>
    /// <returns>A task that completes when the API is ready or MongoDB turned out to be unavailable.</returns>
    public async ValueTask InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? DefaultConnectionString;
        var client = MongoClientFactory.Create(new MongoDbOptions(connectionString, DatabaseName));

        try
        {
            await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            return;
        }

        _mongo = client;
        IsAvailable = true;

        // The API reads its configuration from the process environment, like in production.
        Environment.SetEnvironmentVariable("MONGODB_CONNECTION_STRING", connectionString);
        Environment.SetEnvironmentVariable("MONGODB_DATABASE_NAME", DatabaseName);
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", SigningKey);
        Environment.SetEnvironmentVariable("AUTH_REGISTRATION_ENABLED", null);
        Environment.SetEnvironmentVariable("PORTAL_BASE_URL", PortalBaseUrl);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            // Emails are captured instead of sent, so the tests can read the verification codes.
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IEmailSender>(Emails);

                // Pushes are captured instead of sent to Expo.
                services.AddSingleton<IPushSender>(Pushes);

                // Most tests are not about verifying the email, so an account can use the app as soon as it exists. The tests that are
                // about it build a factory with CreateFactoryRequiringEmailVerification.
                services.AddSingleton(new EmailVerificationSettings(false));
            });
        });
        _ = _factory.Server;
    }

    /// <summary>
    /// Stops the API and drops the throwaway database.
    /// </summary>
    /// <returns>A task that completes when everything is released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_mongo is not null)
        {
            await _mongo.DropDatabaseAsync(DatabaseName);
        }
    }

    /// <summary>
    /// Creates an HTTP client for the API. Each client gets its own fake client address through <c>X-Forwarded-For</c>, so
    /// the per-address rate limit of one test never affects another.
    /// </summary>
    /// <returns>The client.</returns>
    public HttpClient CreateClient() => CreateClient(_factory ?? throw new InvalidOperationException("MongoDB is not available."));

    /// <summary>
    /// Builds a copy of the API in which an account must verify its email before using the app, as in production. The caller disposes it.
    /// </summary>
    /// <returns>The factory.</returns>
    public WebApplicationFactory<Program> CreateFactoryRequiringEmailVerification() =>
        (_factory ?? throw new InvalidOperationException("MongoDB is not available.")).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(new EmailVerificationSettings(true))));

    /// <summary>
    /// Builds a copy of the API started with extra configuration settings, as if they were environment variables. The caller disposes it.
    /// </summary>
    /// <param name="settings">Configuration keys and values.</param>
    /// <returns>The factory.</returns>
    public WebApplicationFactory<Program> CreateFactory(IReadOnlyDictionary<string, string?> settings) =>
        (_factory ?? throw new InvalidOperationException("MongoDB is not available.")).WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    /// <summary>
    /// Creates a client of a factory, with its own client address so the rate limits of one test do not affect another.
    /// </summary>
    /// <param name="factory">Factory to create the client from.</param>
    /// <returns>The client.</returns>
    public HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var number = Interlocked.Increment(ref _clientCounter);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.{(number >> 16) & 255}.{(number >> 8) & 255}.{number & 255}");
        return client;
    }

    /// <summary>
    /// Gets the services of the API under test, to run a use case in-process (for example one maintenance pass) without going
    /// through an endpoint and its rate limit.
    /// </summary>
    public IServiceProvider Services =>
        _factory?.Services ?? throw new InvalidOperationException("MongoDB is not available.");

    /// <summary>
    /// Gets the throwaway database the API under test writes to, to check what was really stored.
    /// </summary>
    public IMongoDatabase Database =>
        _mongo?.GetDatabase(DatabaseName) ?? throw new InvalidOperationException("MongoDB is not available.");

    /// <summary>
    /// Creates a user directly in the database, without going through the registration endpoint.
    /// </summary>
    /// <param name="isSystemOperator">Whether the user gets the system operator role.</param>
    /// <param name="verifiedEmail">Whether the email of the user is already verified, which the web area requires.</param>
    /// <returns>The user and its clear-text password.</returns>
    public async Task<TestUser> CreateUserAsync(bool isSystemOperator = false, bool verifiedEmail = false)
    {
        var factory = _factory ?? throw new InvalidOperationException("MongoDB is not available.");
        await using var scope = factory.Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = new TestUser(Guid.CreateVersion7(), $"u{Guid.NewGuid():N}"[..13], "correct horse battery");
        await repository.AddAsync(
            User.Restore(
                user.Id,
                0,
                user.Username,
                $"{user.Username}@example.com",
                hasher.Hash(user.Password),
                "Ana Pérez",
                "+50670189220",
                DateTimeOffset.UtcNow,
                0,
                null,
                isSystemOperator,
                verifiedEmail ? DateTimeOffset.UtcNow : null),
            CancellationToken.None);
        return user;
    }
}

/// <summary>
/// Groups every API test in one collection so they share the single API instance of <see cref="ApiFixture"/>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    /// <summary>
    /// Name of the collection.
    /// </summary>
    public const string Name = "Api";
}
