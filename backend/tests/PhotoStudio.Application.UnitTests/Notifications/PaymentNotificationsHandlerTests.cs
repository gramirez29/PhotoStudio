using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Notifications;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.UnitTests.Notifications;

/// <summary>
/// Tests of <see cref="PaymentNotificationsHandler"/>.
/// </summary>
public sealed class PaymentNotificationsHandlerTests
{
    /// <summary>
    /// A submitted proof schedules its notice, due at the instant the event is handled.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ForASubmittedProof_SchedulesTheNoticeForTheCurrentInstant()
    {
        var bookings = Substitute.For<IBookingRepository>();
        var notifications = Substitute.For<INotificationRepository>();
        var booking = BookingFactory.Confirmed();
        var payment = booking.SubmitPaymentProof(
            Guid.CreateVersion7(), Money.Create(10_000m, "CRC"), PaymentMethod.SinpeMovil, "proof-1", BookingFactory.CreatedAt);
        bookings.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        var now = BookingFactory.CreatedAt.AddMinutes(3);
        var handler = new PaymentNotificationsHandler(new NotificationPlanner(bookings, notifications), new FixedTimeProvider(now));

        await handler.HandleAsync(
            new PaymentSubmitted(booking.Id, payment.Id, 10_000m, "CRC", BookingFactory.CreatedAt), TestContext.Current.CancellationToken);

        await notifications.Received(1).TryAddAsync(
            Arg.Is<Notification>(notification => notification.DedupKey == Notification.PaymentProofKey(payment.Id) && notification.DueAt == now),
            Arg.Any<CancellationToken>());
    }
}
