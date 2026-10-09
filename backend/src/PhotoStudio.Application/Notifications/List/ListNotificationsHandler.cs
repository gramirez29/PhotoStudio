using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Notifications.List;

/// <summary>
/// Returns the inbox of a photographer: their latest delivered notifications and how many are unread.
/// </summary>
/// <param name="notifications">Notification repository.</param>
public sealed class ListNotificationsHandler(INotificationRepository notifications)
    : IQueryHandler<ListNotificationsQuery, NotificationListResponse>
{
    /// <summary>
    /// Maximum number of notifications returned. The inbox is a to-do list, not an archive.
    /// </summary>
    public const int MaxItems = 50;

    /// <summary>
    /// Reads the inbox.
    /// </summary>
    /// <param name="query">Photographer whose inbox is read.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The notifications and the unread count.</returns>
    public async Task<NotificationListResponse> HandleAsync(ListNotificationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var delivered = await notifications.ListDeliveredAsync(query.PhotographerId, MaxItems, cancellationToken);
        var unread = await notifications.CountUnreadAsync(query.PhotographerId, cancellationToken);

        return new NotificationListResponse([.. delivered.Select(notification => notification.ToResponse())], unread);
    }
}
