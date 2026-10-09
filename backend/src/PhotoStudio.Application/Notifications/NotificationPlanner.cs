using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Notifications;

/// <summary>
/// Decides which notifications a booking should have and brings storage in line with that. It works from the current state
/// of the booking, not from the event that triggered it, so it gives the same result whatever the order in which events are
/// delivered and however many times each one is: the outbox delivers at least once and does not guarantee order between
/// events.
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="notifications">Notification repository.</param>
public sealed class NotificationPlanner(IBookingRepository bookings, INotificationRepository notifications)
{
    /// <summary>
    /// Makes sure the booking has exactly one scheduled session reminder if it should have one, and none otherwise. A confirmed
    /// booking whose session is still ahead should have one, due <see cref="NotificationPolicy.SessionReminderLeadTime"/>
    /// before the session; moving the session replaces it. A booking in any other state, or a missing one, should have none.
    /// </summary>
    /// <param name="bookingId">Booking to reconcile.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when storage matches the booking.</returns>
    public async Task ReconcileSessionReminderAsync(Guid bookingId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var booking = await bookings.GetByIdAsync(bookingId, cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Confirmed || booking.Slot.Start <= now)
        {
            await notifications.CancelScheduledAsync(bookingId, NotificationType.SessionReminder, null, now, cancellationToken);
            return;
        }

        var key = Notification.SessionReminderKey(booking.Id, booking.Slot.Start);

        // A reminder for a previous start is stale now that the session moved.
        await notifications.CancelScheduledAsync(bookingId, NotificationType.SessionReminder, key, now, cancellationToken);
        await notifications.TryAddAsync(
            Notification.Schedule(
                booking.PhotographerId,
                booking.Id,
                NotificationType.SessionReminder,
                key,
                booking.Slot.Start - NotificationPolicy.SessionReminderLeadTime,
                now),
            cancellationToken);
    }

    /// <summary>
    /// Schedules the balance notice of a booking whose session was completed, due
    /// <see cref="NotificationPolicy.BalanceDueDelay"/> after completion. Whether money is still owed is decided when the
    /// notice comes due, because the client may pay in the meantime. Does nothing for a booking that is not completed.
    /// </summary>
    /// <param name="bookingId">Booking whose session was completed.</param>
    /// <param name="completedAt">Instant the session was completed.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the notice is scheduled.</returns>
    public async Task ScheduleBalanceDueAsync(Guid bookingId, DateTimeOffset completedAt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var booking = await bookings.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.Completed)
        {
            return;
        }

        await notifications.TryAddAsync(
            Notification.Schedule(
                booking.PhotographerId,
                booking.Id,
                NotificationType.BalanceDue,
                Notification.BalanceDueKey(booking.Id),
                completedAt + NotificationPolicy.BalanceDueDelay,
                now),
            cancellationToken);
    }
}
