using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Notifications;

/// <summary>
/// A delivered notification as exposed by the API. It carries what happened and the data to act on it, not text: the app
/// words it in Spanish and builds the WhatsApp message from these fields.
/// </summary>
/// <param name="Id">Notification identifier.</param>
/// <param name="Type">What the notification is about.</param>
/// <param name="BookingId">Booking it is about.</param>
/// <param name="ClientName">Name of the client.</param>
/// <param name="ClientPhone">Phone of the client, to open a WhatsApp conversation.</param>
/// <param name="PackageName">Name of the booked package.</param>
/// <param name="SessionStart">Start of the session (UTC).</param>
/// <param name="Balance">Amount still owed; present only for <see cref="NotificationType.BalanceDue"/>.</param>
/// <param name="DeliveredAt">Instant it reached the inbox (UTC).</param>
/// <param name="ReadAt">Instant the photographer read it (UTC), or <see langword="null"/> while it is unread.</param>
public sealed record NotificationResponse(
    Guid Id,
    NotificationType Type,
    Guid BookingId,
    string ClientName,
    string ClientPhone,
    string PackageName,
    DateTimeOffset SessionStart,
    MoneyResponse? Balance,
    DateTimeOffset DeliveredAt,
    DateTimeOffset? ReadAt);

/// <summary>
/// The inbox of a photographer.
/// </summary>
/// <param name="Items">Delivered notifications, the most recent first.</param>
/// <param name="UnreadCount">How many of the photographer's notifications are unread (not only the ones listed).</param>
public sealed record NotificationListResponse(IReadOnlyList<NotificationResponse> Items, int UnreadCount);

/// <summary>
/// Outcome of marking every notification as read.
/// </summary>
/// <param name="Marked">How many notifications were unread and are read now.</param>
public sealed record MarkAllReadResponse(int Marked);

/// <summary>
/// Mapping from notifications to their API responses.
/// </summary>
public static class NotificationResponseMappings
{
    /// <summary>
    /// Maps a delivered notification to its response.
    /// </summary>
    /// <param name="notification">A notification that was delivered, so it has details.</param>
    /// <returns>The response.</returns>
    /// <exception cref="InvalidOperationException">When the notification was not delivered and has nothing to show.</exception>
    public static NotificationResponse ToResponse(this Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var details = notification.Details;
        if (details is null || notification.DeliveredAt is null)
        {
            throw new InvalidOperationException($"Notification {notification.Id} was not delivered, so it has nothing to show.");
        }

        return new NotificationResponse(
            notification.Id,
            notification.Type,
            notification.BookingId,
            details.ClientName,
            details.ClientPhone,
            details.PackageName,
            details.SessionStart,
            details.Balance is null ? null : new MoneyResponse(details.Balance.Amount, details.Balance.Currency),
            notification.DeliveredAt.Value,
            notification.ReadAt);
    }
}
