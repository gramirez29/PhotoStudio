using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Notifications;
using PhotoStudio.Application.Notifications.DeliverDue;
using PhotoStudio.Application.Notifications.Push;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Domain.Galleries.Events;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.UnitTests.Notifications;

/// <summary>
/// Tests of the warning that the deadline to publish the final photos is near: when it is scheduled from the policy frozen in the
/// booking, when it is still relevant, and how it is worded.
/// </summary>
public sealed class DeliveryDeadlineNotificationsTests
{
    private static readonly DateTimeOffset SelectionSentAt = BookingFactory.CreatedAt.AddDays(12);

    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
    private readonly IPushDeviceRepository _devices = Substitute.For<IPushDeviceRepository>();
    private readonly IPushSender _sender = Substitute.For<IPushSender>();

    /// <summary>
    /// The event schedules one warning, three days before the deadline of the policy counted from the selection, keyed by the
    /// gallery and the instant of the selection.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Handler_SchedulesTheWarningThreeDaysBeforeTheDeadline()
    {
        var booking = BookingFactory.Confirmed();
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        var occurred = Event(booking);

        await NewHandler(SelectionSentAt).HandleAsync(occurred, TestContext.Current.CancellationToken);

        await _notifications.Received(1).TryAddAsync(
            Arg.Is<Notification>(notice =>
                notice.Type == NotificationType.DeliveryDeadlineApproaching
                && notice.PhotographerId == booking.PhotographerId
                && notice.BookingId == booking.Id
                && notice.DedupKey == Notification.DeliveryDeadlineKey(occurred.GalleryId, SelectionSentAt)
                && notice.DueAt == SelectionSentAt.AddDays(21).AddDays(-3)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A policy with a short deadline, already closer than the warning lead time, makes the warning due right away.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Handler_WithADeadlineCloserThanTheLeadTime_SchedulesItForNow()
    {
        var booking = BookingWithDeadline(2);
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        await NewHandler(SelectionSentAt).HandleAsync(Event(booking), TestContext.Current.CancellationToken);

        await _notifications.Received(1).TryAddAsync(Arg.Is<Notification>(notice => notice.DueAt == SelectionSentAt), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The deadline is the one frozen in the booking, not a fixed number.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Handler_UsesTheDeadlineOfTheBookingPolicy()
    {
        var booking = BookingWithDeadline(30);
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        await NewHandler(SelectionSentAt).HandleAsync(Event(booking), TestContext.Current.CancellationToken);

        await _notifications.Received(1).TryAddAsync(Arg.Is<Notification>(notice => notice.DueAt == SelectionSentAt.AddDays(27)), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Without its booking there is nothing to warn about.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Handler_WithoutTheBooking_SchedulesNothing()
    {
        var booking = BookingFactory.Confirmed();
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns((Booking?)null);

        await NewHandler(SelectionSentAt).HandleAsync(Event(booking), TestContext.Current.CancellationToken);

        await _notifications.DidNotReceive().TryAddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the warning comes due and the photos are still being edited for that selection, it is delivered with the deadline.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Deliver_WhileTheSelectionIsBeingEdited_CarriesTheDeadline()
    {
        var (booking, _, notice) = Scenario(publish: false, reopen: false);

        var result = await HandlerAt(SelectionSentAt.AddDays(18)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(1, 0, 0));
        notice.Status.ShouldBe(NotificationStatus.Delivered);
        notice.Details!.DeliveryDeadline.ShouldBe(SelectionSentAt.AddDays(21));
        notice.Details.ClientName.ShouldBe(booking.Client.Name);
    }

    /// <summary>
    /// Once the final photos are published the warning is stale and is dropped.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Deliver_AfterThePhotosArePublished_DropsTheWarning()
    {
        var (_, _, notice) = Scenario(publish: true, reopen: false);

        var result = await HandlerAt(SelectionSentAt.AddDays(18)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(0, 1, 0));
        notice.Status.ShouldBe(NotificationStatus.Cancelled);
    }

    /// <summary>
    /// If the photographer reopened the selection, the warning of the old one is stale: the deadline restarts with the new selection.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Deliver_AfterTheSelectionWasReopened_DropsTheWarning()
    {
        var (_, _, notice) = Scenario(publish: false, reopen: true);

        var result = await HandlerAt(SelectionSentAt.AddDays(18)).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(0, 1, 0));
        notice.Status.ShouldBe(NotificationStatus.Cancelled);
    }

    /// <summary>
    /// The push names the client and the package and gives the last day, in Costa Rica time.
    /// </summary>
    [Fact]
    public void PushText_SaysWhoseDeliveryIsDueAndWhen()
    {
        var notice = Notification.Schedule(
            Guid.CreateVersion7(), Guid.CreateVersion7(), NotificationType.DeliveryDeadlineApproaching, "k", BookingFactory.CreatedAt, BookingFactory.CreatedAt);
        notice.Deliver(
            new NotificationDetails("María Pérez", "+50688888888", "Retrato Familiar", BookingFactory.SessionStart, null, DeliveryDeadline: new DateTimeOffset(2026, 11, 20, 20, 0, 0, TimeSpan.Zero)),
            BookingFactory.CreatedAt);

        var (title, body) = NotificationPushText.Describe(notice);

        title.ShouldBe("Entrega próxima");
        body.ShouldBe("Las fotos de María Pérez (Retrato Familiar) deben entregarse antes del vie 20 nov.");
    }

    /// <summary>
    /// Builds the event of a selection sent at <see cref="SelectionSentAt"/>.
    /// </summary>
    /// <param name="booking">Booking of the gallery.</param>
    /// <returns>The event.</returns>
    private static SelectionSubmitted Event(Booking booking) =>
        new(Guid.CreateVersion7(), booking.Id, booking.PhotographerId, 3, 1, 3_000m, "CRC", SelectionSentAt);

    /// <summary>
    /// Builds a confirmed booking whose policy gives the photographer the number of days to deliver.
    /// </summary>
    /// <param name="deadlineDays">Days to deliver.</param>
    /// <returns>The booking.</returns>
    private static Booking BookingWithDeadline(int deadlineDays)
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ClientContact.Create("María Pérez", "+506 8888-8888"),
            "Retrato familiar",
            Money.Create(100_000m, "CRC"),
            TimeSlot.Create(BookingFactory.SessionStart, BookingFactory.SessionStart.AddHours(2)),
            BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, deliveryDeadlineDays: deadlineDays),
            BookingFactory.CreatedAt);
        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, BookingFactory.CreatedAt);
        booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(50_000m, "CRC"), PaymentMethod.Cash, "deposit-1", BookingFactory.CreatedAt);
        return booking;
    }

    /// <summary>
    /// Builds a booking, its gallery with a selection sent at <see cref="SelectionSentAt"/> and the warning about it, and makes the
    /// repositories return them.
    /// </summary>
    /// <param name="publish">Whether the final photos were already published.</param>
    /// <param name="reopen">Whether the photographer reopened the selection afterwards.</param>
    /// <returns>The booking, the gallery and the warning.</returns>
    private (Booking Booking, Gallery Gallery, Notification Notice) Scenario(bool publish, bool reopen)
    {
        var booking = BookingFactory.Confirmed();
        var photos = Enumerable.Range(0, 5).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"{index}.jpg", 10, BookingFactory.CreatedAt)).ToList();
        var gallery = Gallery.Restore(
            Guid.CreateVersion7(), 1, booking.PhotographerId, booking.Id, new GallerySnapshot(booking.Client.Name, booking.PackageName, booking.Slot.Start),
            GalleryStatus.InSelection, 2, 0, Money.Create(3_000m, "CRC"), BookingFactory.CreatedAt, BookingFactory.CreatedAt, photos);
        gallery.SubmitSelection([.. photos.Take(3).Select(photo => photo.Id)], SelectionSentAt);
        var notice = Notification.Schedule(
            booking.PhotographerId, booking.Id, NotificationType.DeliveryDeadlineApproaching, Notification.DeliveryDeadlineKey(gallery.Id, SelectionSentAt), SelectionSentAt.AddDays(18), SelectionSentAt);

        if (reopen)
        {
            gallery.ReopenSelection("Client asked", SelectionSentAt.AddDays(1));
        }

        if (publish)
        {
            // Publishing needs files and storage; what the warning reads is only the state, so the gallery is restored in it.
            gallery = Gallery.Restore(
                gallery.Id, gallery.Version, booking.PhotographerId, booking.Id, new GallerySnapshot(booking.Client.Name, booking.PackageName, booking.Slot.Start),
                GalleryStatus.Ready, 2, 0, Money.Create(3_000m, "CRC"), BookingFactory.CreatedAt, BookingFactory.CreatedAt, photos);
        }

        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        _galleries.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        _notifications.ListDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([notice]);
        return (booking, gallery, notice);
    }

    /// <summary>
    /// Creates the handler that plans the warning, with a fixed clock.
    /// </summary>
    /// <param name="now">Instant the clock reports.</param>
    /// <returns>The handler.</returns>
    private DeliveryDeadlineNotificationsHandler NewHandler(DateTimeOffset now) =>
        new(new NotificationPlanner(_bookings, _notifications), new FixedTimeProvider(now));

    /// <summary>
    /// Creates the delivery handler with a fixed clock.
    /// </summary>
    /// <param name="now">Instant the clock reports.</param>
    /// <returns>The handler.</returns>
    private DeliverDueNotificationsHandler HandlerAt(DateTimeOffset now) =>
        new(_bookings, _galleries, _notifications, new NotificationPusher(_devices, _sender, NullLogger<NotificationPusher>.Instance), new FixedTimeProvider(now));
}
