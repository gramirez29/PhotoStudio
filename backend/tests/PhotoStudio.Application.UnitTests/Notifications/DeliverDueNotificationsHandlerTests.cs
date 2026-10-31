using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Notifications.DeliverDue;
using PhotoStudio.Application.Notifications.Push;
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
    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
    private readonly IPushDeviceRepository _devices = Substitute.For<IPushDeviceRepository>();
    private readonly IPushSender _sender = Substitute.For<IPushSender>();

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
    /// A proof notice for a payment that still waits for review is delivered with the declared amount.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ForAPendingProof_DeliversTheNoticeWithTheDeclaredAmount()
    {
        var (booking, notice) = BookingWithProofNotice();
        Due(booking, notice);

        var result = await HandlerAt(BookingFactory.CreatedAt).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(1, 0, 0));
        notice.Details!.ProofAmount.ShouldBe(Money.Create(10_000m, "CRC"));
        notice.Details.ClientName.ShouldBe(booking.Client.Name);
    }

    /// <summary>
    /// A proof notice is dropped when the photographer already resolved the proof before the notice was delivered.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheProofWasAlreadyResolved_DropsTheNotice()
    {
        var (booking, notice) = BookingWithProofNotice();
        booking.RejectPayment(booking.Payments[^1].Id, "Not received", BookingFactory.CreatedAt);
        Due(booking, notice);

        var result = await HandlerAt(BookingFactory.CreatedAt).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(0, 1, 0));
    }

    /// <summary>
    /// A proof notice whose payment is not in the booking is dropped.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenThePaymentIsNotInTheBooking_DropsTheNotice()
    {
        var (booking, _) = BookingWithProofNotice();
        var stranger = Notification.Schedule(
            booking.PhotographerId,
            booking.Id,
            NotificationType.PaymentProofSubmitted,
            Notification.PaymentProofKey(Guid.CreateVersion7()),
            BookingFactory.CreatedAt,
            BookingFactory.CreatedAt);
        Due(booking, stranger);

        var result = await HandlerAt(BookingFactory.CreatedAt).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.Cancelled.ShouldBe(1);
    }

    /// <summary>
    /// A proof notice is dropped when its booking was cancelled in the meantime.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheBookingWithAProofWasCancelled_DropsTheNotice()
    {
        var (booking, notice) = BookingWithProofNotice();
        booking.Cancel(Actor.Photographer, "Sick", BookingFactory.CreatedAt);
        Due(booking, notice);

        var result = await HandlerAt(BookingFactory.CreatedAt).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.Cancelled.ShouldBe(1);
    }

    /// <summary>
    /// A notification that reaches the inbox is also pushed to the phones of the photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_PushesTheNotificationsItDelivers()
    {
        var (booking, notice) = BookingWithProofNotice();
        Due(booking, notice);
        _devices.ListByPhotographerAsync(booking.PhotographerId, Arg.Any<CancellationToken>())
            .Returns([PushDevice.Register("ExponentPushToken[abc]", booking.PhotographerId, PushPlatform.Android, BookingFactory.CreatedAt)]);
        _sender.SendAsync(Arg.Any<IReadOnlyList<PushMessage>>(), Arg.Any<CancellationToken>()).Returns(new PushSendResult([]));

        await HandlerAt(BookingFactory.CreatedAt).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        await _sender.Received(1).SendAsync(
            Arg.Is<IReadOnlyList<PushMessage>>(messages => messages.Count == 1 && messages[0].Title == "Comprobante por verificar"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A notification that is dropped, or that another run won, is not pushed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_DoesNotPushWhatItDropsOrSkips()
    {
        var (booking, resolved) = BookingWithProofNotice();
        booking.RejectPayment(booking.Payments[^1].Id, "Not received", BookingFactory.CreatedAt);
        var reminder = Reminder(booking, booking.Slot.Start.AddDays(-3));
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        _notifications.ListDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([resolved]);
        _notifications.UpdateAsync(resolved, Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "changed")));
        _devices.ListByPhotographerAsync(booking.PhotographerId, Arg.Any<CancellationToken>())
            .Returns([PushDevice.Register("ExponentPushToken[abc]", booking.PhotographerId, PushPlatform.Android, BookingFactory.CreatedAt)]);

        await HandlerAt(BookingFactory.CreatedAt).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);
        _notifications.ListDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([reminder]);
        await HandlerAt(booking.Slot.Start.AddDays(-1)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        await _sender.DidNotReceiveWithAnyArgs().SendAsync(default!, TestContext.Current.CancellationToken);
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
    /// Builds a confirmed booking with a proof of payment waiting for review and the notice about that proof.
    /// </summary>
    /// <returns>The booking and its proof notice.</returns>
    private static (Booking Booking, Notification Notice) BookingWithProofNotice()
    {
        var booking = BookingFactory.Confirmed();
        var payment = booking.SubmitPaymentProof(
            Guid.CreateVersion7(), Money.Create(10_000m, "CRC"), PaymentMethod.SinpeMovil, "proof-1", BookingFactory.CreatedAt);
        var notice = Notification.Schedule(
            booking.PhotographerId,
            booking.Id,
            NotificationType.PaymentProofSubmitted,
            Notification.PaymentProofKey(payment.Id),
            BookingFactory.CreatedAt,
            BookingFactory.CreatedAt);
        return (booking, notice);
    }

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
    private DeliverDueNotificationsHandler HandlerAt(DateTimeOffset now) => new(_bookings, _galleries, _notifications, new NotificationPusher(_devices, _sender, NullLogger<NotificationPusher>.Instance), new FixedTimeProvider(now));
}
