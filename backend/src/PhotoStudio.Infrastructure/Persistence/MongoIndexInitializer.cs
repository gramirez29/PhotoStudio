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

        var users = database.GetCollection<UserDocument>(MongoUserRepository.CollectionName);

        // The username identifies the user at login, and uniqueness is enforced here, not by checking first and inserting after.
        var usernameIndex = new CreateIndexModel<UserDocument>(
            Builders<UserDocument>.IndexKeys.Ascending(user => user.Username),
            new CreateIndexOptions { Name = MongoUserRepository.UsernameIndexName, Unique = true });

        // The email is unique too. The index is partial so users created before the email existed (empty email) never collide
        // with each other; only real addresses are constrained.
        var emailIndex = new CreateIndexModel<UserDocument>(
            Builders<UserDocument>.IndexKeys.Ascending(user => user.Email),
            new CreateIndexOptions<UserDocument>
            {
                Name = MongoUserRepository.EmailIndexName,
                Unique = true,
                PartialFilterExpression = Builders<UserDocument>.Filter.Gt(user => user.Email, string.Empty),
            });

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

        var notifications = database.GetCollection<NotificationDocument>(MongoNotificationRepository.CollectionName);

        // The key makes scheduling idempotent: the same notice cannot exist twice, whatever delivers the event that asks for it.
        var notificationKeyIndex = new CreateIndexModel<NotificationDocument>(
            Builders<NotificationDocument>.IndexKeys.Ascending(notification => notification.DedupKey),
            new CreateIndexOptions { Name = "ix_notification_dedup", Unique = true });

        // Serves the delivery job: scheduled notifications that came due.
        var notificationDueIndex = new CreateIndexModel<NotificationDocument>(
            Builders<NotificationDocument>.IndexKeys
                .Ascending(notification => notification.Status)
                .Ascending(notification => notification.DueAt),
            new CreateIndexOptions { Name = "ix_notification_due" });

        // Serves the inbox: the delivered notifications of a photographer, the most recent first.
        var notificationInboxIndex = new CreateIndexModel<NotificationDocument>(
            Builders<NotificationDocument>.IndexKeys
                .Ascending(notification => notification.PhotographerId)
                .Ascending(notification => notification.Status)
                .Descending(notification => notification.DeliveredAt),
            new CreateIndexOptions { Name = "ix_notification_inbox" });

        // Serves dropping the scheduled notifications of a booking when it changes.
        var notificationBookingIndex = new CreateIndexModel<NotificationDocument>(
            Builders<NotificationDocument>.IndexKeys
                .Ascending(notification => notification.BookingId)
                .Ascending(notification => notification.Type)
                .Ascending(notification => notification.Status),
            new CreateIndexOptions { Name = "ix_notification_booking" });

        // Removes notifications after their retention period (retainUntil is in the document, so the delay here is zero).
        var notificationRetentionIndex = new CreateIndexModel<NotificationDocument>(
            Builders<NotificationDocument>.IndexKeys.Ascending(notification => notification.RetainUntil),
            new CreateIndexOptions { Name = "ix_notification_retention", ExpireAfter = TimeSpan.Zero });

        var settlements = database.GetCollection<SettlementDocument>(MongoSettlementRepository.CollectionName);

        // One settlement per booking: two events that settle the same booking cannot create two.
        var settlementBookingIndex = new CreateIndexModel<SettlementDocument>(
            Builders<SettlementDocument>.IndexKeys.Ascending(settlement => settlement.BookingId),
            new CreateIndexOptions { Name = "ix_settlement_booking", Unique = true });

        // Serves the list of refunds a photographer still has to give back, the oldest first.
        var settlementRefundIndex = new CreateIndexModel<SettlementDocument>(
            Builders<SettlementDocument>.IndexKeys
                .Ascending(settlement => settlement.PhotographerId)
                .Ascending(settlement => settlement.RefundStatus)
                .Ascending(settlement => settlement.CreatedAt),
            new CreateIndexOptions { Name = "ix_settlement_refund" });

        try
        {
            await bookings.Indexes.CreateManyAsync([photographerSlotIndex, expirationIndex], cancellationToken: cancellationToken);
            await outbox.Indexes.CreateManyAsync([outboxClaimIndex, outboxRetentionIndex], cancellationToken: cancellationToken);
            await users.Indexes.CreateManyAsync([usernameIndex, emailIndex], cancellationToken: cancellationToken);
            await notifications.Indexes.CreateManyAsync(
                [notificationKeyIndex, notificationDueIndex, notificationInboxIndex, notificationBookingIndex, notificationRetentionIndex],
                cancellationToken: cancellationToken);
            await settlements.Indexes.CreateManyAsync([settlementBookingIndex, settlementRefundIndex], cancellationToken: cancellationToken);
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
