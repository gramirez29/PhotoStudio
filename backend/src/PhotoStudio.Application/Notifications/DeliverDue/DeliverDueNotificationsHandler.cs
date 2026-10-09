using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Notifications.DeliverDue;

/// <summary>
/// Delivers the notifications whose time came. Relevance is decided now, against the current booking, not when the
/// notification was planned: a reminder is delivered only if the booking is still confirmed and still has the session the
/// reminder was planned for, and a balance notice only if the booking is completed and still owes money. Otherwise the
/// notification is dropped. The job is idempotent: a delivered or dropped notification is never due again.
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="notifications">Notification repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class DeliverDueNotificationsHandler(
    IBookingRepository bookings,
    INotificationRepository notifications,
    TimeProvider timeProvider) : ICommandHandler<DeliverDueNotificationsCommand, DeliverDueNotificationsResult>
{
    /// <summary>
    /// Delivers or drops every due notification of the batch.
    /// </summary>
    /// <param name="command">Batch size.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>How many notifications were delivered, dropped and skipped.</returns>
    public async Task<DeliverDueNotificationsResult> HandleAsync(DeliverDueNotificationsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var due = await notifications.ListDueAsync(now, command.BatchSize, cancellationToken);

        var delivered = 0;
        var cancelled = 0;
        var skipped = 0;
        foreach (var notification in due)
        {
            var booking = await bookings.GetByIdAsync(notification.BookingId, cancellationToken);
            var details = DetailsIfRelevant(notification, booking, now);

            if (details is null)
            {
                notification.Cancel(now);
            }
            else
            {
                notification.Deliver(details, now);
            }

            try
            {
                await notifications.UpdateAsync(notification, cancellationToken);
            }
            catch (ConflictException)
            {
                // Another run changed it first; it is examined again on the next one if it is still due.
                skipped++;
                continue;
            }

            if (details is null)
            {
                cancelled++;
            }
            else
            {
                delivered++;
            }
        }

        return new DeliverDueNotificationsResult(delivered, cancelled, skipped);
    }

    /// <summary>
    /// Builds what the notification should say if it is still relevant.
    /// </summary>
    /// <param name="notification">Notification that came due.</param>
    /// <param name="booking">Current state of its booking, or <see langword="null"/> when it no longer exists.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The snapshot to deliver, or <see langword="null"/> when the notification should be dropped.</returns>
    private static NotificationDetails? DetailsIfRelevant(Notification notification, Booking? booking, DateTimeOffset now)
    {
        if (booking is null)
        {
            return null;
        }

        switch (notification.Type)
        {
            case NotificationType.SessionReminder:
                // The key holds the session start the reminder was planned for: a different start means it was moved.
                var stillPlannedFor = Notification.SessionReminderKey(booking.Id, booking.Slot.Start) == notification.DedupKey;
                return booking.Status == BookingStatus.Confirmed && booking.Slot.Start > now && stillPlannedFor
                    ? Snapshot(booking, balance: null)
                    : null;

            case NotificationType.BalanceDue:
                return booking.Status == BookingStatus.Completed && booking.Balance.Amount > 0m
                    ? Snapshot(booking, booking.Balance)
                    : null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Captures the booking data a notification shows.
    /// </summary>
    /// <param name="booking">Booking the notification is about.</param>
    /// <param name="balance">Amount owed, for balance notices.</param>
    /// <returns>The snapshot.</returns>
    private static NotificationDetails Snapshot(Booking booking, Domain.Common.Money? balance) =>
        new(booking.Client.Name, booking.Client.Phone, booking.PackageName, booking.Slot.Start, balance);
}
