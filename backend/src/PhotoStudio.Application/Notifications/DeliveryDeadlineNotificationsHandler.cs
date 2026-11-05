using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Application.Notifications;

/// <summary>
/// Starts the clock of the delivery deadline when a client sends the photos they chose: the photographer is warned
/// <see cref="NotificationPolicy.DeliveryDeadlineLeadTime"/> before the deadline of the policy frozen in the booking, if the final
/// photos are still not published by then. A selection sent again after being reopened is a new selection with its own deadline. It is
/// idempotent: the key holds the gallery and the instant of the selection, so an event delivered twice by the outbox is one warning.
/// </summary>
/// <param name="planner">Plans the notifications of a booking.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class DeliveryDeadlineNotificationsHandler(NotificationPlanner planner, TimeProvider timeProvider)
    : IDomainEventHandler<SelectionSubmitted>
{
    /// <summary>
    /// Schedules the warning of the deadline.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the warning is scheduled.</returns>
    public Task HandleAsync(SelectionSubmitted domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return planner.ScheduleDeliveryDeadlineAsync(domainEvent.BookingId, domainEvent.GalleryId, domainEvent.OccurredAt, timeProvider.GetUtcNow(), cancellationToken);
    }
}
