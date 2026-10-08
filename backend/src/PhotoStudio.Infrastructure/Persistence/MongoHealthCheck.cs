using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// Readiness check that pings MongoDB. Railway uses it to know whether the service can receive traffic.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoHealthCheck(IMongoDatabase database) : IHealthCheck
{
    /// <summary>
    /// Sends a ping command to MongoDB.
    /// </summary>
    /// <param name="context">Health check context.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Healthy when MongoDB answers; unhealthy otherwise.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB is reachable.");
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("MongoDB is unreachable.", exception);
        }
    }
}
