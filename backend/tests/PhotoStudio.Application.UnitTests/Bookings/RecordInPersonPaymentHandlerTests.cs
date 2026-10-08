using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.RecordInPersonPayment;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="RecordInPersonPaymentHandler"/>.
/// </summary>
public sealed class RecordInPersonPaymentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();
    private readonly RecordInPersonPaymentHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordInPersonPaymentHandlerTests"/> class.
    /// </summary>
    public RecordInPersonPaymentHandlerTests()
    {
        _handler = new RecordInPersonPaymentHandler(_repository, new FixedTimeProvider(Now));
    }

    /// <summary>
    /// A missing booking produces <see cref="NotFoundException"/>.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithUnknownBooking_ThrowsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Booking?)null);

        await Should.ThrowAsync<NotFoundException>(
            () => _handler.HandleAsync(Command(Guid.CreateVersion7(), "key-1"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Scenario 4 of the workbook: with the contract signed in person, a cash deposit confirms the booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithSignedContract_ConfirmsAndPersists()
    {
        var booking = TentativeBooking();
        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);
        _repository.GetByIdAsync(Arg.Is(booking.Id), Arg.Any<CancellationToken>()).Returns(booking);

        var response = await _handler.HandleAsync(Command(booking.Id, "key-1"), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(BookingStatus.Confirmed);
        response.TotalPaid.Amount.ShouldBe(50_000m);
        await _repository.Received(1).UpdateAsync(Arg.Is(booking), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A retry with the same idempotency key does not write again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithRepeatedKey_DoesNotPersistTwice()
    {
        var booking = TentativeBooking();
        _repository.GetByIdAsync(Arg.Is(booking.Id), Arg.Any<CancellationToken>()).Returns(booking);

        await _handler.HandleAsync(Command(booking.Id, "key-1"), TestContext.Current.CancellationToken);
        await _handler.HandleAsync(Command(booking.Id, "key-1"), TestContext.Current.CancellationToken);

        booking.Payments.Count.ShouldBe(1);
        await _repository.Received(1).UpdateAsync(Arg.Is(booking), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Builds a cash payment command for half the package price.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="idempotencyKey">Idempotency key.</param>
    /// <returns>The command.</returns>
    private static RecordInPersonPaymentCommand Command(Guid bookingId, string idempotencyKey) =>
        new(bookingId, 50_000m, "CRC", PaymentMethod.Cash, idempotencyKey);

    /// <summary>
    /// Builds a tentative booking for a session ten days from now.
    /// </summary>
    /// <returns>The booking.</returns>
    private static Booking TentativeBooking() => Booking.Create(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        ClientContact.Create("María Pérez", "+506 8888-8888"),
        "Retrato familiar",
        Money.Create(100_000m),
        TimeSlot.Create(Now.AddDays(10), Now.AddDays(10).AddHours(2)),
        BookingPolicy.Default,
        Now);
}
