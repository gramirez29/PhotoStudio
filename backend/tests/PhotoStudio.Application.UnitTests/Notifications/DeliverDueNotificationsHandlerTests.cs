using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Notifications.DeliverDue;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.UnitTests.Notifications;

/// <summary>
/// Tests of <see cref="DeliverDueNotificationsHandler"/>: relevance is decided when the notification comes due.
/// </summary>
public sealed class DeliverDueNotificationsHandlerTests
{
    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();

    /// <summary>
    /// A reminder for a booking that is still confirmed and unmoved is delivered with a snapshot.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ForAStillValidReminder_DeliversIt()
    {
        var booking = BookingFactory.Confirmed();
        var reminder = Reminder(booking, booking.Slot.Start);
        Due(booking, reminder);

        var result = await HandlerAt(booking.Slot.Start.AddDays(-1)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(1, 0, 0));
        reminder.Status.ShouldBe(NotificationStatus.Delivered);
        reminder.Details!.ClientName.ShouldBe(booking.Client.Name);
        await _notifications.Received(1).UpdateAsync(reminder, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A reminder planned for a start the session no longer has is dropped.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheSessionWasMoved_DropsTheReminder()
    {
        var booking = BookingFactory.Confirmed();
        var reminder = Reminder(booking, booking.Slot.Start.AddDays(-3));
        Due(booking, reminder);

        var result = await HandlerAt(booking.Slot.Start.AddDays(-1)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(0, 1, 0));
        reminder.Status.ShouldBe(NotificationStatus.Cancelled);
    }

    /// <summary>
    /// A reminder for a cancelled booking is dropped.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheBookingWasCancelled_DropsTheReminder()
    {
        var booking = BookingFactory.Confirmed();
        var reminder = Reminder(booking, booking.Slot.Start);
        booking.Cancel(Actor.Photographer, "Sick", BookingFactory.CreatedAt);
        Due(booking, reminder);

        var result = await HandlerAt(booking.Slot.Start.AddDays(-1)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.Cancelled.ShouldBe(1);
    }

    /// <summary>
    /// A reminder whose booking vanished is dropped.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheBookingIsMissing_DropsTheNotification()
    {
        var booking = BookingFactory.Confirmed();
        var reminder = Reminder(booking, booking.Slot.Start);
        _notifications.ListDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([reminder]);
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns((Booking?)null);

        var result = await HandlerAt(booking.Slot.Start.AddDays(-1)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.Cancelled.ShouldBe(1);
    }

    /// <summary>
    /// A balance notice is delivered with the amount still owed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ForAnOwedBalance_DeliversTheNoticeWithTheAmount()
    {
        var booking = BookingFactory.Confirmed();
        var completedAt = booking.Slot.Start.AddHours(2);
        booking.Complete(completedAt);
        var notice = Notification.Schedule(
            booking.PhotographerId, booking.Id, NotificationType.BalanceDue, Notification.BalanceDueKey(booking.Id), completedAt.AddDays(1), completedAt);
        Due(booking, notice);

        var result = await HandlerAt(completedAt.AddDays(1)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.Delivered.ShouldBe(1);
        notice.Details!.Balance.ShouldBe(Money.Create(50_000m, "CRC"));
    }

    /// <summary>
    /// A balance notice is dropped when the client already paid everything.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheBalanceWasPaid_DropsTheNotice()
    {
        var booking = BookingFactory.Confirmed();
        var completedAt = booking.Slot.Start.AddHours(2);
        booking.Complete(completedAt);
        booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(50_000m, "CRC"), PaymentMethod.Cash, "rest", completedAt);
        var notice = Notification.Schedule(
            booking.PhotographerId, booking.Id, NotificationType.BalanceDue, Notification.BalanceDueKey(booking.Id), completedAt.AddDays(1), completedAt);
        Due(booking, notice);

        var result = await HandlerAt(completedAt.AddDays(1)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(0, 1, 0));
    }

    /// <summary>
    /// A concurrent change is counted as skipped and does not stop the batch.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_OnAConcurrencyConflict_CountsItAsSkipped()
    {
        var booking = BookingFactory.Confirmed();
        var reminder = Reminder(booking, booking.Slot.Start);
        Due(booking, reminder);
        _notifications.UpdateAsync(reminder, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "changed")));

        var result = await HandlerAt(booking.Slot.Start.AddDays(-1)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(0, 0, 1));
    }

    /// <summary>
    /// Builds a reminder planned for a session start.
    /// </summary>
    /// <param name="booking">Booking the reminder is about.</param>
    /// <param name="plannedStart">Session start the reminder was planned for.</param>
    /// <returns>The reminder.</returns>
    private static Notification Reminder(Booking booking, DateTimeOffset plannedStart) => Notification.Schedule(
        booking.PhotographerId,
        booking.Id,
        NotificationType.SessionReminder,
        Notification.SessionReminderKey(booking.Id, plannedStart),
        booking.Slot.Start.AddDays(-1),
        BookingFactory.CreatedAt);

    /// <summary>
    /// Makes the repositories report one due notification and its booking.
    /// </summary>
    /// <param name="booking">Booking returned by the repository.</param>
    /// <param name="notification">Notification that is due.</param>
    private void Due(Booking booking, Notification notification)
    {
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        _notifications.ListDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([notification]);
    }

    /// <summary>
    /// Creates the handler with a fixed clock.
    /// </summary>
    /// <param name="now">Instant the clock reports.</param>
    /// <returns>The handler.</returns>
    private DeliverDueNotificationsHandler HandlerAt(DateTimeOffset now) => new(_bookings, _notifications, new FixedTimeProvider(now));
}
