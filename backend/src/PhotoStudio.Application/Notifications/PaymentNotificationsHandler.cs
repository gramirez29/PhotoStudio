using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Bookings.Events;

namespace PhotoStudio.Application.Notifications;

/// <summary>
/// Tells the photographer that a client uploaded a proof of payment. The notice is scheduled for the current instant and
/// reaches the inbox in the delivery step of the same maintenance run that dispatched the event.
/// </summary>
/// <param name="planner">Plans the notifications of a booking.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class PaymentNotificationsHandler(NotificationPlanner planner, TimeProvider timeProvider)
    : IDomainEventHandler<PaymentSubmitted>
{
    /// <summary>
    /// Schedules the notice of the submitted proof.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the notice is scheduled.</returns>
    public Task HandleAsync(PaymentSubmitted domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return planner.SchedulePaymentProofAsync(domainEvent.BookingId, domainEvent.PaymentId, timeProvider.GetUtcNow(), cancellationToken);
    }
}
