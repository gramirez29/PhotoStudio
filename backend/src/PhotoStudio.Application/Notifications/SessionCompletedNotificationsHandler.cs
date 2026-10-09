using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Bookings.Events;

namespace PhotoStudio.Application.Notifications;

/// <summary>
/// Reacts to a completed session: its reminder no longer makes sense, and the balance notice is scheduled so that a client
/// who still owes money is chased a day later.
/// </summary>
/// <param name="planner">Plans the notifications of a booking.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class SessionCompletedNotificationsHandler(NotificationPlanner planner, TimeProvider timeProvider)
    : IDomainEventHandler<SessionCompleted>
{
    /// <summary>
    /// Drops the session reminder and schedules the balance notice.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the notifications are planned.</returns>
    public async Task HandleAsync(SessionCompleted domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var now = timeProvider.GetUtcNow();
        await planner.ReconcileSessionReminderAsync(domainEvent.BookingId, now, cancellationToken);
        await planner.ScheduleBalanceDueAsync(domainEvent.BookingId, domainEvent.OccurredAt, now, cancellationToken);
    }
}
