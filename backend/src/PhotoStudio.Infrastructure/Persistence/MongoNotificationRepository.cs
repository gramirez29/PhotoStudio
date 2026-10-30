using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Pagination;
using PhotoStudio.Domain.Notifications;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="INotificationRepository"/> over the <c>notifications</c> collection. Idempotent
/// scheduling relies on a unique index on the key, not on looking first; the bulk operations (dropping scheduled
/// notifications, marking the inbox as read) are single atomic writes scoped by booking or photographer.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoNotificationRepository(IMongoDatabase database) : INotificationRepository
{
    /// <summary>
    /// Name of the notifications collection.
    /// </summary>
    public const string CollectionName = "notifications";

    private readonly IMongoCollection<NotificationDocument> _collection = database.GetCollection<NotificationDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<bool> TryAddAsync(Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            await _collection.InsertOneAsync(notification.ToDocument(notification.Version + 1), cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // The same notice was already scheduled (an event delivered twice, or two events asking for the same thing).
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> CancelScheduledAsync(
        Guid bookingId,
        NotificationType type,
        string? exceptDedupKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var filter = Builders<NotificationDocument>.Filter;
        var conditions = new List<FilterDefinition<NotificationDocument>>
        {
            filter.Eq(notification => notification.BookingId, bookingId),
            filter.Eq(notification => notification.Type, type.ToString()),
            filter.Eq(notification => notification.Status, nameof(NotificationStatus.Scheduled)),
        };

        if (exceptDedupKey is not null)
        {
            conditions.Add(filter.Ne(notification => notification.DedupKey, exceptDedupKey));
        }

        var result = await _collection.UpdateManyAsync(
            filter.And(conditions),
            Builders<NotificationDocument>.Update
                .Set(notification => notification.Status, nameof(NotificationStatus.Cancelled))
                .Set(notification => notification.CancelledAt, now.UtcDateTime)
                .Inc(notification => notification.Version, 1),
            cancellationToken: cancellationToken);

        return (int)result.ModifiedCount;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Notification>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        // Served by the (status, dueAt) index.
        var documents = await _collection
            .Find(notification => notification.Status == nameof(NotificationStatus.Scheduled) && notification.DueAt <= now.UtcDateTime)
            .SortBy(notification => notification.DueAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return [.. documents.Select(document => document.ToDomain())];
    }

    /// <inheritdoc />
    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(notification => notification.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var expectedVersion = notification.Version;
        var result = await _collection.ReplaceOneAsync(
            stored => stored.Id == notification.Id && stored.Version == expectedVersion,
            notification.ToDocument(expectedVersion + 1),
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            throw new ConflictException(
                ApplicationErrorCodes.ConcurrencyConflict,
                $"Notification {notification.Id} was modified by another request. Reload it and try again.");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Notification>> ListDeliveredAsync(
        Guid photographerId,
        PageCursor? after,
        int limit,
        CancellationToken cancellationToken)
    {
        var filters = Builders<NotificationDocument>.Filter;
        var filter = filters.Eq(notification => notification.PhotographerId, photographerId)
            & filters.Eq(notification => notification.Status, nameof(NotificationStatus.Delivered));
        if (after is { } cursor)
        {
            // Keyset page: delivered before the cursor, or the same instant with a smaller identifier (the tie-break of the sort).
            DateTime? at = cursor.At.UtcDateTime;
            filter &= filters.Lt(notification => notification.DeliveredAt, at)
                | (filters.Eq(notification => notification.DeliveredAt, at) & filters.Lt(notification => notification.Id, cursor.Id));
        }

        // Served by the (photographerId, status, deliveredAt) index.
        var documents = await _collection
            .Find(filter)
            .SortByDescending(notification => notification.DeliveredAt)
            .ThenByDescending(notification => notification.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return [.. documents.Select(document => document.ToDomain())];
    }

    /// <inheritdoc />
    public async Task<int> CountUnreadAsync(Guid photographerId, CancellationToken cancellationToken) =>
        (int)await _collection.CountDocumentsAsync(
            notification => notification.PhotographerId == photographerId
                && notification.Status == nameof(NotificationStatus.Delivered)
                && notification.ReadAt == null,
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public async Task<int> MarkAllReadAsync(Guid photographerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var result = await _collection.UpdateManyAsync(
            notification => notification.PhotographerId == photographerId
                && notification.Status == nameof(NotificationStatus.Delivered)
                && notification.ReadAt == null,
            Builders<NotificationDocument>.Update
                .Set(notification => notification.ReadAt, now.UtcDateTime)
                .Inc(notification => notification.Version, 1),
            cancellationToken: cancellationToken);

        return (int)result.ModifiedCount;
    }
}
