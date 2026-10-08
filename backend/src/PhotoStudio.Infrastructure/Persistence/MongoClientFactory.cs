using MongoDB.Driver;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// Builds the MongoDB client with explicit timeouts and retries, so connection drops fail fast and transient errors are retried.
/// </summary>
public static class MongoClientFactory
{
    /// <summary>
    /// Creates a configured client. The client is thread-safe and must be registered as a singleton.
    /// </summary>
    /// <param name="options">Connection settings.</param>
    /// <returns>The MongoDB client.</returns>
    public static IMongoClient Create(MongoDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
        settings.ApplicationName = "photostudio-api";
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
        settings.ConnectTimeout = TimeSpan.FromSeconds(10);
        settings.RetryReads = true;
        settings.RetryWrites = true;

        // Atlas M0 allows 500 connections in total; keep the pool well below that limit.
        settings.MaxConnectionPoolSize = 50;

        return new MongoClient(settings);
    }
}
