using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Notifications.MarkRead;

/// <summary>
/// Marks one notification as read. A notification of another photographer, or one that was never delivered, is reported
/// exactly like a missing one, so the response never reveals that the identifier exists.
/// </summary>
/// <param name="notifications">Notification repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class MarkNotificationReadHandler(INotificationRepository notifications, TimeProvider timeProvider)
    : ICommandHandler<MarkNotificationReadCommand, NotificationResponse>
{
    /// <summary>
    /// Marks the notification as read.
    /// </summary>
    /// <param name="command">Notification to mark.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The notification after being marked.</returns>
    /// <exception cref="NotFoundException">When it does not exist, belongs to another photographer or was not delivered.</exception>
    public async Task<NotificationResponse> HandleAsync(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var notification = await notifications.GetByIdAsync(command.NotificationId, cancellationToken);
        if (notification is null
            || notification.PhotographerId != command.PhotographerId
            || notification.Status != NotificationStatus.Delivered)
        {
            throw new NotFoundException(nameof(Notification), command.NotificationId);
        }

        // Already read: nothing to write, and the answer is the same.
        if (notification.ReadAt is null)
        {
            notification.MarkRead(timeProvider.GetUtcNow());
            await notifications.UpdateAsync(notification, cancellationToken);
        }

        return notification.ToResponse();
    }
}
