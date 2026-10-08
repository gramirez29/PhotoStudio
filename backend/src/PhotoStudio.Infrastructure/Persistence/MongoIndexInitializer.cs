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
    /// How long after they expire refresh tokens are kept before MongoDB removes them.
    /// </summary>
    public static readonly TimeSpan RefreshTokenRetention = TimeSpan.FromDays(7);

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

        // Serves the outbox claim, which takes the oldest due message: the index follows the sort order (status, then
        // occurredAt, then _id) so MongoDB walks the pending messages oldest first and stops at the first one that is due.
        // Putting nextAttemptAt in the index would turn the sort into an in-memory sort of the whole backlog, which made
        // every claim examine every pending message (20 000 keys and documents to take one).
        var outboxClaimIndex = new CreateIndexModel<OutboxMessageDocument>(
            Builders<OutboxMessageDocument>.IndexKeys
                .Ascending(message => message.Status)
                .Ascending(message => message.OccurredAt)
                .Ascending(message => message.Id),
            new CreateIndexOptions { Name = "ix_outbox_claim" });

        // Removes delivered messages after the retention period; messages that are not delivered have no processedAt.
        var outboxRetentionIndex = new CreateIndexModel<OutboxMessageDocument>(
            Builders<OutboxMessageDocument>.IndexKeys.Ascending(message => message.ProcessedAt),
            new CreateIndexOptions { Name = "ix_outbox_retention", ExpireAfter = OutboxRetention });

        var accounts = database.GetCollection<PhotographerAccountDocument>(MongoPhotographerAccountRepository.CollectionName);

        // The email identifies the account at login, and uniqueness is enforced here, not by checking first and inserting after.
        var accountEmailIndex = new CreateIndexModel<PhotographerAccountDocument>(
            Builders<PhotographerAccountDocument>.IndexKeys.Ascending(account => account.Email),
            new CreateIndexOptions { Name = "ix_account_email", Unique = true });

        var refreshTokens = database.GetCollection<RefreshTokenDocument>(MongoRefreshTokenRepository.CollectionName);

        // A refresh token is found by the hash of its secret.
        var refreshHashIndex = new CreateIndexModel<RefreshTokenDocument>(
            Builders<RefreshTokenDocument>.IndexKeys.Ascending(token => token.TokenHash),
            new CreateIndexOptions { Name = "ix_refresh_hash", Unique = true });

        // Serves revoking a whole login family and every session of a photographer.
        var refreshFamilyIndex = new CreateIndexModel<RefreshTokenDocument>(
            Builders<RefreshTokenDocument>.IndexKeys.Ascending(token => token.FamilyId),
            new CreateIndexOptions { Name = "ix_refresh_family" });
        var refreshPhotographerIndex = new CreateIndexModel<RefreshTokenDocument>(
            Builders<RefreshTokenDocument>.IndexKeys.Ascending(token => token.PhotographerId),
            new CreateIndexOptions { Name = "ix_refresh_photographer" });

        // Removes tokens a week after they expire. They are kept that long so a replayed expired token is still recognized.
        var refreshRetentionIndex = new CreateIndexModel<RefreshTokenDocument>(
            Builders<RefreshTokenDocument>.IndexKeys.Ascending(token => token.ExpiresAt),
            new CreateIndexOptions { Name = "ix_refresh_retention", ExpireAfter = RefreshTokenRetention });

        try
        {
            await bookings.Indexes.CreateManyAsync([photographerSlotIndex, expirationIndex], cancellationToken: cancellationToken);
            await outbox.Indexes.CreateManyAsync([outboxClaimIndex, outboxRetentionIndex], cancellationToken: cancellationToken);
            await accounts.Indexes.CreateOneAsync(accountEmailIndex, cancellationToken: cancellationToken);
            await refreshTokens.Indexes.CreateManyAsync(
                [refreshHashIndex, refreshFamilyIndex, refreshPhotographerIndex, refreshRetentionIndex],
                cancellationToken: cancellationToken);
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
