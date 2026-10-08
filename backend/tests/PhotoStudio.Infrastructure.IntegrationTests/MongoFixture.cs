using MongoDB.Bson;
using MongoDB.Driver;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Provides an isolated MongoDB database per test class. The connection string comes from the
/// <c>MONGODB_TEST_CONNECTION_STRING</c> variable and defaults to the replica set of <c>docker-compose.yml</c>.
/// When no server answers, <see cref="IsAvailable"/> is false and the tests skip themselves.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private const string ConnectionStringVariable = "MONGODB_TEST_CONNECTION_STRING";
    private const string DefaultConnectionString = "mongodb://localhost:27019/?directConnection=true";

    private IMongoClient? _client;

    /// <summary>
    /// Gets a value indicating whether a MongoDB server answered the initial ping.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Gets the name of the throwaway database used by this fixture.
    /// </summary>
    public string DatabaseName { get; } = $"photostudio_it_{Guid.NewGuid():N}";

    /// <summary>
    /// Gets the database for the tests. Only valid when <see cref="IsAvailable"/> is true.
    /// </summary>
    public IMongoDatabase Database =>
        _client?.GetDatabase(DatabaseName) ?? throw new InvalidOperationException("MongoDB is not available.");

    /// <summary>
    /// Connects to MongoDB and records whether it is reachable.
    /// </summary>
    /// <returns>A task that completes when the connection attempt finishes.</returns>
    public async ValueTask InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? DefaultConnectionString;
        var client = MongoClientFactory.Create(new MongoDbOptions(connectionString, DatabaseName));

        try
        {
            await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            _client = client;
            IsAvailable = true;
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            IsAvailable = false;
        }
    }

    /// <summary>
    /// Drops the throwaway database.
    /// </summary>
    /// <returns>A task that completes when the database is dropped.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DropDatabaseAsync(DatabaseName);
        }
    }
}
