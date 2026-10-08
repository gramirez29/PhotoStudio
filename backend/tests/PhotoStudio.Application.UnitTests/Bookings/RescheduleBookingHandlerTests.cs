using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.RescheduleBooking;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="RescheduleBookingHandler"/>.
/// </summary>
public sealed class RescheduleBookingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OriginalStart = Now.AddDays(10);
    private static readonly DateTimeOffset NewStart = Now.AddDays(12);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();
    private readonly RescheduleBookingHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="RescheduleBookingHandlerTests"/> class.
    /// </summary>
    public RescheduleBookingHandlerTests()
    {
        _handler = new RescheduleBookingHandler(_repository, new FixedTimeProvider(Now));
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
            () => _handler.HandleAsync(Command(Guid.CreateVersion7()), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A confirmed booking moves to the new slot, which is persisted through the slot-reserving operation only.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithConfirmedBooking_MovesItAndReservesTheNewSlot()
    {
        var booking = ConfirmedBooking();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var response = await _handler.HandleAsync(Command(booking.Id), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(BookingStatus.Confirmed);
        response.SessionStart.ShouldBe(NewStart);
        response.SessionEnd.ShouldBe(NewStart.AddHours(2));
        await _repository.Received(1).UpdateReservingSlotAsync(booking, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A tentative booking cannot be rescheduled and nothing is persisted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTentativeBooking_ThrowsInvalidTransition()
    {
        var booking = NewBooking();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var exception = await Should.ThrowAsync<DomainException>(
            () => _handler.HandleAsync(Command(booking.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidTransition);
        await _repository.DidNotReceive().UpdateReservingSlotAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A new slot that starts in the past is rejected and nothing is persisted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithNewSlotInThePast_ThrowsSessionInPast()
    {
        var booking = ConfirmedBooking();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        var command = new RescheduleBookingCommand(booking.Id, Now.AddDays(-1), Now.AddDays(-1).AddHours(2));

        var exception = await Should.ThrowAsync<DomainException>(
            () => _handler.HandleAsync(command, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.SessionInPast);
        await _repository.DidNotReceive().UpdateReservingSlotAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the repository finds the new slot taken, the conflict reaches the caller.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheNewSlotIsTaken_PropagatesTheConflict()
    {
        var booking = ConfirmedBooking();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        _repository
            .UpdateReservingSlotAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.SlotUnavailable, "taken")));

        var exception = await Should.ThrowAsync<ConflictException>(
            () => _handler.HandleAsync(Command(booking.Id), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
    }

    /// <summary>
    /// Builds the command that moves a booking to <see cref="NewStart"/> for two hours.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <returns>The command.</returns>
    private static RescheduleBookingCommand Command(Guid bookingId) =>
        new(bookingId, NewStart, NewStart.AddHours(2));

    /// <summary>
    /// Creates a tentative booking for <see cref="OriginalStart"/>.
    /// </summary>
    /// <returns>The booking.</returns>
    private static Booking NewBooking() => Booking.Create(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        ClientContact.Create("María Pérez", "+506 8888-8888"),
        "Retrato familiar",
        Money.Create(100_000m, "CRC"),
        TimeSlot.Create(OriginalStart, OriginalStart.AddHours(2)),
        BookingPolicy.Default,
        Now);

    /// <summary>
    /// Creates a confirmed booking: contract signed and the deposit paid in person.
    /// </summary>
    /// <returns>The booking.</returns>
    private static Booking ConfirmedBooking()
    {
        var booking = NewBooking();
        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);
        booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(50_000m, "CRC"), PaymentMethod.Cash, "deposit-1", Now);
        return booking;
    }
}
