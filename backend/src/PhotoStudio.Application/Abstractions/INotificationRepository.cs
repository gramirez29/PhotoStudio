using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="Notification"/>. Implemented in the Infrastructure layer.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Stores a scheduled notification unless one with the same key already exists. This makes scheduling idempotent: an
    /// event delivered twice, or two events that ask for the same notice, create it once.
    /// </summary>
    /// <param name="notification">Notification to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when it was stored; <see langword="false"/> when its key already existed.</returns>
    Task<bool> TryAddAsync(Notification notification, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the scheduled notifications of a booking and type in one atomic operation. Delivered notifications are never
    /// touched: what already reached the inbox stays there.
    /// </summary>
    /// <param name="bookingId">Booking whose notifications are dropped.</param>
    /// <param name="type">Type to drop.</param>
    /// <param name="exceptDedupKey">Key to keep, or <see langword="null"/> to drop them all.</param>
    /// <param name="now">Current instant, stored as the cancellation instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of notifications dropped.</returns>
    Task<int> CancelScheduledAsync(Guid bookingId, NotificationType type, string? exceptDedupKey, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the scheduled notifications whose instant has come, the oldest first.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <param name="limit">Maximum number of notifications to return.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The due notifications, possibly empty.</returns>
    Task<IReadOnlyList<Notification>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a notification by identifier.
    /// </summary>
    /// <param name="id">Notification identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The notification, or <see langword="null"/> when it does not exist.</returns>
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the changes of a notification using optimistic concurrency on the version.
    /// </summary>
    /// <param name="notification">Notification to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when it is saved.</returns>
    /// <exception cref="Exceptions.ConflictException">When another write changed the notification first.</exception>
    Task UpdateAsync(Notification notification, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the delivered notifications of a photographer, the most recent first.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="limit">Maximum number of notifications to return.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The inbox, possibly empty.</returns>
    Task<IReadOnlyList<Notification>> ListDeliveredAsync(Guid photographerId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Counts the delivered notifications of a photographer that were not read yet.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of unread notifications.</returns>
    Task<int> CountUnreadAsync(Guid photographerId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks every unread delivered notification of a photographer as read, in one atomic operation.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="now">Current instant, stored as the read instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of notifications marked.</returns>
    Task<int> MarkAllReadAsync(Guid photographerId, DateTimeOffset now, CancellationToken cancellationToken);
}
