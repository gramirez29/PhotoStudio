using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Galleries.Events;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Notifications;

/// <summary>
/// Tells the photographer that a client sent the photos they chose. The notice is scheduled for the current instant and reaches the
/// inbox, and the phone, in the delivery step of the same maintenance run that dispatched the event. The key holds the gallery, so
/// an event delivered twice by the outbox is one notice.
/// </summary>
/// <param name="notifications">Notification repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class SelectionNotificationsHandler(INotificationRepository notifications, TimeProvider timeProvider)
    : IDomainEventHandler<SelectionSubmitted>
{
    /// <summary>
    /// Schedules the notice of the selection.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the notice is scheduled.</returns>
    public async Task HandleAsync(SelectionSubmitted domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var now = timeProvider.GetUtcNow();
        await notifications.TryAddAsync(
            Notification.Schedule(
                domainEvent.PhotographerId,
                domainEvent.BookingId,
                NotificationType.SelectionSubmitted,
                Notification.SelectionSubmittedKey(domainEvent.GalleryId),
                now,
                now),
            cancellationToken);
    }
}
