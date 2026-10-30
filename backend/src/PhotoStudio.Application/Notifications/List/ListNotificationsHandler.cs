using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Pagination;

namespace PhotoStudio.Application.Notifications.List;

/// <summary>
/// Returns the inbox of a photographer a page at a time: their delivered notifications, the most recent first, and how many are unread.
/// </summary>
/// <param name="notifications">Notification repository.</param>
public sealed class ListNotificationsHandler(INotificationRepository notifications)
    : IQueryHandler<ListNotificationsQuery, NotificationListResponse>
{
    /// <summary>
    /// Reads one page of the inbox.
    /// </summary>
    /// <param name="query">Photographer whose inbox is read and the position of the page.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The notifications of the page, the unread count and where the next page starts when there is one.</returns>
    public async Task<NotificationListResponse> HandleAsync(ListNotificationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, PageSizes.Max);
        // One more than the page: if it comes, there is a next page, and it is not part of this one.
        var delivered = await notifications.ListDeliveredAsync(query.PhotographerId, query.After, limit + 1, cancellationToken);
        var unread = await notifications.CountUnreadAsync(query.PhotographerId, cancellationToken);

        var page = delivered.Take(limit).ToList();
        var next = delivered.Count > limit && page[^1].DeliveredAt is { } deliveredAt ? new PageCursor(deliveredAt, page[^1].Id).Encode() : null;

        return new NotificationListResponse([.. page.Select(notification => notification.ToResponse())], unread, next);
    }
}
