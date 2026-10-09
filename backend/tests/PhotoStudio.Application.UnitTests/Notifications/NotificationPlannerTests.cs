using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Notifications;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.UnitTests.Notifications;

/// <summary>
/// Tests of <see cref="NotificationPlanner"/>.
/// </summary>
public sealed class NotificationPlannerTests
{
    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
    private readonly NotificationPlanner _planner;

    /// <summary>
    /// Creates the fixture.
    /// </summary>
    public NotificationPlannerTests() => _planner = new NotificationPlanner(_bookings, _notifications);

    /// <summary>
    /// A confirmed booking gets one reminder due 24 hours before the session, replacing any other.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ReconcileSessionReminder_ForAConfirmedBooking_SchedulesTheReminder()
    {
        var booking = BookingFactory.Confirmed();
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        var now = BookingFactory.CreatedAt;
        var key = Notification.SessionReminderKey(booking.Id, booking.Slot.Start);

        await _planner.ReconcileSessionReminderAsync(booking.Id, now, TestContext.Current.CancellationToken);

        await _notifications.Received(1).CancelScheduledAsync(booking.Id, NotificationType.SessionReminder, key, now, Arg.Any<CancellationToken>());
        await _notifications.Received(1).TryAddAsync(
            Arg.Is<Notification>(notification =>
                notification.DedupKey == key
                && notification.DueAt == booking.Slot.Start - NotificationPolicy.SessionReminderLeadTime
                && notification.PhotographerId == booking.PhotographerId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A booking that is not confirmed, or is missing, has its scheduled reminders dropped and none created.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ReconcileSessionReminder_WhenNotConfirmed_DropsTheReminders()
    {
        var booking = BookingFactory.Confirmed();
        booking.Cancel(Actor.Photographer, "Sick", BookingFactory.CreatedAt);
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        var missing = Guid.CreateVersion7();
        _bookings.GetByIdAsync(missing, Arg.Any<CancellationToken>()).Returns((Booking?)null);

        await _planner.ReconcileSessionReminderAsync(booking.Id, BookingFactory.CreatedAt, TestContext.Current.CancellationToken);
        await _planner.ReconcileSessionReminderAsync(missing, BookingFactory.CreatedAt, TestContext.Current.CancellationToken);

        await _notifications.Received(1).CancelScheduledAsync(booking.Id, NotificationType.SessionReminder, null, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _notifications.Received(1).CancelScheduledAsync(missing, NotificationType.SessionReminder, null, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _notifications.DidNotReceiveWithAnyArgs().TryAddAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A session that already started gets no reminder.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ReconcileSessionReminder_WhenTheSessionStarted_SchedulesNothing()
    {
        var booking = BookingFactory.Confirmed();
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        await _planner.ReconcileSessionReminderAsync(booking.Id, booking.Slot.Start.AddMinutes(1), TestContext.Current.CancellationToken);

        await _notifications.DidNotReceiveWithAnyArgs().TryAddAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The balance notice is due 24 hours after completion.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ScheduleBalanceDue_ForACompletedBooking_SchedulesTheNotice()
    {
        var booking = BookingFactory.Confirmed();
        var completedAt = booking.Slot.Start.AddHours(2);
        booking.Complete(completedAt);
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        await _planner.ScheduleBalanceDueAsync(booking.Id, completedAt, completedAt, TestContext.Current.CancellationToken);

        await _notifications.Received(1).TryAddAsync(
            Arg.Is<Notification>(notification =>
                notification.Type == NotificationType.BalanceDue
                && notification.DedupKey == Notification.BalanceDueKey(booking.Id)
                && notification.DueAt == completedAt + NotificationPolicy.BalanceDueDelay),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A booking that is not completed gets no balance notice.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ScheduleBalanceDue_WhenNotCompleted_SchedulesNothing()
    {
        var booking = BookingFactory.Confirmed();
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        await _planner.ScheduleBalanceDueAsync(booking.Id, BookingFactory.CreatedAt, BookingFactory.CreatedAt, TestContext.Current.CancellationToken);

        await _notifications.DidNotReceiveWithAnyArgs().TryAddAsync(default!, TestContext.Current.CancellationToken);
    }
}
