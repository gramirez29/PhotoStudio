using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
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

    private const string ConnectionStringVariable = "MONGODB_TEST_CONNECTION_STRING";
    private const string DefaultConnectionString = "mongodb://localhost:27019/?directConnection=true";

    private static int _clientCounter;

    private WebApplicationFactory<Program>? _factory;
    private IMongoClient? _mongo;

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

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
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
    public HttpClient CreateClient()
    {
        var client = (_factory ?? throw new InvalidOperationException("MongoDB is not available.")).CreateClient();
        var number = Interlocked.Increment(ref _clientCounter);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.{(number >> 16) & 255}.{(number >> 8) & 255}.{number & 255}");
        return client;
    }

    /// <summary>
    /// Gets the throwaway database the API under test writes to, to check what was really stored.
    /// </summary>
    public IMongoDatabase Database =>
        _mongo?.GetDatabase(DatabaseName) ?? throw new InvalidOperationException("MongoDB is not available.");

    /// <summary>
    /// Creates a user directly in the database, without going through the registration endpoint.
    /// </summary>
    /// <returns>The user and its clear-text password.</returns>
    public async Task<TestUser> CreateUserAsync()
    {
        var factory = _factory ?? throw new InvalidOperationException("MongoDB is not available.");
        await using var scope = factory.Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = new TestUser(Guid.CreateVersion7(), $"u{Guid.NewGuid():N}"[..13], "correct horse battery");
        await repository.AddAsync(
            User.Create(user.Id, user.Username, $"{user.Username}@example.com", hasher.Hash(user.Password), "Ana Pérez", "+50670189220", DateTimeOffset.UtcNow),
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
