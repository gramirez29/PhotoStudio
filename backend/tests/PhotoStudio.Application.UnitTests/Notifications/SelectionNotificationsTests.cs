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
/// Tests of the notice that tells the photographer a client sent the photos they chose: how it is scheduled, delivered and worded.
/// </summary>
public sealed class SelectionNotificationsTests
{
    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly IGalleryRepository _galleries = Substitute.For<IGalleryRepository>();
    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
    private readonly IPushDeviceRepository _devices = Substitute.For<IPushDeviceRepository>();
    private readonly IPushSender _sender = Substitute.For<IPushSender>();

    /// <summary>
    /// The event schedules one notice, due right away, whose key holds the gallery.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Handler_SchedulesTheNoticeOfTheGallery()
    {
        var booking = BookingFactory.Confirmed();
        var occurred = new SelectionSubmitted(Guid.CreateVersion7(), booking.Id, booking.PhotographerId, 3, 1, 3_000m, "CRC", BookingFactory.CreatedAt);

        await new SelectionNotificationsHandler(_notifications, new FixedTimeProvider(BookingFactory.CreatedAt)).HandleAsync(occurred, TestContext.Current.CancellationToken);

        await _notifications.Received(1).TryAddAsync(
            Arg.Is<Notification>(notice =>
                notice.Type == NotificationType.SelectionSubmitted
                && notice.PhotographerId == booking.PhotographerId
                && notice.BookingId == booking.Id
                && notice.DedupKey == Notification.SelectionSubmittedKey(occurred.GalleryId)
                && notice.DueAt == BookingFactory.CreatedAt),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the notice comes due and the selection stands, it is delivered with how many photos were chosen and how many are extra.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Deliver_WhenTheSelectionStands_CarriesTheCounts()
    {
        var (booking, gallery, notice) = Scenario(chosen: 3, included: 2, submit: true);

        var result = await HandlerAt(BookingFactory.CreatedAt).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(1, 0, 0));
        notice.Status.ShouldBe(NotificationStatus.Delivered);
        notice.Details!.SelectedPhotos.ShouldBe(3);
        notice.Details.ExtraPhotos.ShouldBe(1);
        notice.Details.ClientName.ShouldBe(booking.Client.Name);
        gallery.SelectionSubmittedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// A notice whose selection is gone (the gallery has none) is dropped, not delivered.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Deliver_WhenThereIsNoSelection_DropsTheNotice()
    {
        var (_, _, notice) = Scenario(chosen: 0, included: 2, submit: false);

        var result = await HandlerAt(BookingFactory.CreatedAt).HandleAsync(new DeliverDueNotificationsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new DeliverDueNotificationsResult(0, 1, 0));
        notice.Status.ShouldBe(NotificationStatus.Cancelled);
    }

    /// <summary>
    /// The push says who chose how many photos, and mentions the extra ones only when there are.
    /// </summary>
    [Fact]
    public void PushText_SaysHowManyPhotosWereChosen()
    {
        var notice = Delivered(selected: 12, extra: 2);
        var single = Delivered(selected: 1, extra: 0);

        NotificationPushText.Describe(notice).ShouldBe(("Selección recibida", "María Pérez eligió 12 fotos (2 extra)."));
        NotificationPushText.Describe(single).ShouldBe(("Selección recibida", "María Pérez eligió 1 foto."));
    }

    /// <summary>
    /// Builds a booking, its gallery with a selection (or without one) and the notice about it, and makes the repositories return them.
    /// </summary>
    /// <param name="chosen">Photos in the selection.</param>
    /// <param name="included">Photos the package includes.</param>
    /// <param name="submit">Whether the client sent the selection.</param>
    /// <returns>The booking, the gallery and the notice.</returns>
    private (Booking Booking, Gallery Gallery, Notification Notice) Scenario(int chosen, int included, bool submit)
    {
        var booking = BookingFactory.Confirmed();
        var photos = Enumerable.Range(0, 5).Select(index => new GalleryPhoto(Guid.CreateVersion7(), $"{index}.jpg", 10, BookingFactory.CreatedAt)).ToList();
        var gallery = Gallery.Restore(
            Guid.CreateVersion7(), 1, booking.PhotographerId, booking.Id, new GallerySnapshot(booking.Client.Name, booking.PackageName, booking.Slot.Start),
            GalleryStatus.InSelection, included, 0, Money.Create(3_000m, "CRC"), BookingFactory.CreatedAt, BookingFactory.CreatedAt, photos);
        if (submit)
        {
            gallery.SubmitSelection([.. photos.Take(chosen).Select(photo => photo.Id)], BookingFactory.CreatedAt);
        }

        var notice = Notification.Schedule(
            booking.PhotographerId, booking.Id, NotificationType.SelectionSubmitted, Notification.SelectionSubmittedKey(gallery.Id), BookingFactory.CreatedAt, BookingFactory.CreatedAt);
        _bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        _galleries.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(gallery);
        _notifications.ListDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([notice]);
        return (booking, gallery, notice);
    }

    /// <summary>
    /// Builds a delivered selection notice.
    /// </summary>
    /// <param name="selected">Photos chosen.</param>
    /// <param name="extra">Photos that are extra.</param>
    /// <returns>The notification.</returns>
    private static Notification Delivered(int selected, int extra)
    {
        var notice = Notification.Schedule(
            Guid.CreateVersion7(), Guid.CreateVersion7(), NotificationType.SelectionSubmitted, "k", BookingFactory.CreatedAt, BookingFactory.CreatedAt);
        notice.Deliver(new NotificationDetails("María Pérez", "+50688888888", "Retrato Familiar", BookingFactory.CreatedAt, null, null, selected, extra), BookingFactory.CreatedAt);
        return notice;
    }

    /// <summary>
    /// Creates the delivery handler with a fixed clock.
    /// </summary>
    /// <param name="now">Instant the clock reports.</param>
    /// <returns>The handler.</returns>
    private DeliverDueNotificationsHandler HandlerAt(DateTimeOffset now) =>
        new(_bookings, _galleries, _notifications, new NotificationPusher(_devices, _sender, NullLogger<NotificationPusher>.Instance), new FixedTimeProvider(now));
}
