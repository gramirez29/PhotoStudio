using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Notifications.MarkRead;

/// <summary>
/// Marks every unread notification of a photographer as read, in one atomic write scoped to that photographer.
/// </summary>
/// <param name="notifications">Notification repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class MarkAllNotificationsReadHandler(INotificationRepository notifications, TimeProvider timeProvider)
    : ICommandHandler<MarkAllNotificationsReadCommand, MarkAllReadResponse>
{
    /// <summary>
    /// Marks the inbox as read.
    /// </summary>
    /// <param name="command">Photographer whose inbox is marked.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>How many notifications were unread.</returns>
    public async Task<MarkAllReadResponse> HandleAsync(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var marked = await notifications.MarkAllReadAsync(command.PhotographerId, timeProvider.GetUtcNow(), cancellationToken);
        return new MarkAllReadResponse(marked);
    }
}
