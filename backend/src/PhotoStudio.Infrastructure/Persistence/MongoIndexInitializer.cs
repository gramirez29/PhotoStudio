using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// Creates the MongoDB indexes on startup. If the database is unreachable the API still starts;
/// the readiness health check reports the problem instead of crashing the container.
/// </summary>
/// <param name="database">MongoDB database.</param>
/// <param name="logger">Logger.</param>
public sealed partial class MongoIndexInitializer(IMongoDatabase database, ILogger<MongoIndexInitializer> logger) : IHostedService
{
    /// <summary>
    /// How long delivered outbox messages are kept before MongoDB removes them (Atlas M0 has only 0.5 GB).
    /// </summary>
    public static readonly TimeSpan OutboxRetention = TimeSpan.FromDays(30);

    /// <summary>
    /// Creates the indexes. The operation is idempotent: existing indexes are left as they are.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the indexes are ensured or the failure is logged.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bookings = database.GetCollection<BookingDocument>(MongoBookingRepository.CollectionName);
        var photographerSlotIndex = new CreateIndexModel<BookingDocument>(
            Builders<BookingDocument>.IndexKeys
                .Ascending(booking => booking.PhotographerId)
                .Ascending(booking => booking.SlotStart),
            new CreateIndexOptions { Name = "ix_photographer_slot" });

        // Serves the expiration job: tentative bookings whose hold ended.
        var expirationIndex = new CreateIndexModel<BookingDocument>(
            Builders<BookingDocument>.IndexKeys
                .Ascending(booking => booking.Status)
                .Ascending(booking => booking.ExpiresAt),
            new CreateIndexOptions { Name = "ix_status_expires" });

        var outbox = database.GetCollection<OutboxMessageDocument>(Outbox.OutboxProcessor.CollectionName);

        // Serves the outbox claim: pending messages that are due, oldest first.
        var outboxPendingIndex = new CreateIndexModel<OutboxMessageDocument>(
            Builders<OutboxMessageDocument>.IndexKeys
                .Ascending(message => message.Status)
                .Ascending(message => message.NextAttemptAt)
                .Ascending(message => message.OccurredAt),
            new CreateIndexOptions { Name = "ix_outbox_pending" });

        // Removes delivered messages after the retention period; messages that are not delivered have no processedAt.
        var outboxRetentionIndex = new CreateIndexModel<OutboxMessageDocument>(
            Builders<OutboxMessageDocument>.IndexKeys.Ascending(message => message.ProcessedAt),
            new CreateIndexOptions { Name = "ix_outbox_retention", ExpireAfter = OutboxRetention });

        try
        {
            await bookings.Indexes.CreateManyAsync([photographerSlotIndex, expirationIndex], cancellationToken: cancellationToken);
            await outbox.Indexes.CreateManyAsync([outboxPendingIndex, outboxRetentionIndex], cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            LogIndexCreationFailed(logger, exception);
        }
    }

    /// <summary>
    /// Nothing to release on shutdown.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Logs that the indexes could not be created.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Failure cause.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "MongoDB indexes could not be created on startup; they will be retried on the next start.")]
    private static partial void LogIndexCreationFailed(ILogger logger, Exception exception);
}
