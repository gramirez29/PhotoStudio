using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.RevertClientAbsent;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="RevertClientAbsentHandler"/>.
/// </summary>
public sealed class RevertClientAbsentHandlerTests
{
    private static readonly DateTimeOffset MarkedAt =
        BookingFactory.SessionStart.AddMinutes(BookingPolicy.Default.ClientAbsentToleranceMinutes);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();

    /// <summary>
    /// A missing booking produces <see cref="NotFoundException"/>.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithUnknownBooking_ThrowsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Booking?)null);

        await Should.ThrowAsync<NotFoundException>(
            () => HandlerAt(MarkedAt.AddHours(1)).HandleAsync(
                new RevertClientAbsentCommand(Guid.CreateVersion7(), "error"),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Inside the window the booking is confirmed again and saved through the slot-reserving operation, because it takes
    /// its slot back.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_InsideTheWindow_ConfirmsAgainAndReservesTheSlot()
    {
        var booking = BookingFactory.ClientAbsent();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var response = await HandlerAt(MarkedAt.AddDays(1)).HandleAsync(
            new RevertClientAbsentCommand(booking.Id, "Marcado por error"),
            TestContext.Current.CancellationToken);

        response.Status.ShouldBe(BookingStatus.Confirmed);
        await _repository.Received(1).UpdateReservingSlotAsync(booking, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The reason is mandatory, and nothing is saved without it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithoutReason_ThrowsReasonRequired()
    {
        var booking = BookingFactory.ClientAbsent();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var exception = await Should.ThrowAsync<DomainException>(
            () => HandlerAt(MarkedAt.AddDays(1)).HandleAsync(
                new RevertClientAbsentCommand(booking.Id, " "),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.ReasonRequired);
        await _repository.DidNotReceive().UpdateReservingSlotAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// After the window of the policy the mark can no longer be reverted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AfterTheWindow_ThrowsGuardFailed()
    {
        var booking = BookingFactory.ClientAbsent();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        var afterWindow = MarkedAt.AddDays(BookingPolicy.Default.ClientAbsentRevertWindowDays + 1);

        var exception = await Should.ThrowAsync<DomainException>(
            () => HandlerAt(afterWindow).HandleAsync(
                new RevertClientAbsentCommand(booking.Id, "Tarde"),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
        await _repository.DidNotReceive().UpdateReservingSlotAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A booking that is not marked absent cannot be reverted.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithConfirmedBooking_ThrowsInvalidTransition()
    {
        var booking = BookingFactory.Confirmed();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var exception = await Should.ThrowAsync<DomainException>(
            () => HandlerAt(MarkedAt.AddDays(1)).HandleAsync(
                new RevertClientAbsentCommand(booking.Id, "error"),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidTransition);
    }

    /// <summary>
    /// When another booking took the slot while the client was marked absent, the conflict reaches the caller.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheSlotWasTakenMeanwhile_PropagatesTheConflict()
    {
        var booking = BookingFactory.ClientAbsent();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        _repository
            .UpdateReservingSlotAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.SlotUnavailable, "taken")));

        var exception = await Should.ThrowAsync<ConflictException>(
            () => HandlerAt(MarkedAt.AddDays(1)).HandleAsync(
                new RevertClientAbsentCommand(booking.Id, "Marcado por error"),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
    }

    /// <summary>
    /// Creates the handler with a clock fixed at the given instant.
    /// </summary>
    /// <param name="now">Instant returned by the clock.</param>
    /// <returns>The handler.</returns>
    private RevertClientAbsentHandler HandlerAt(DateTimeOffset now) => new(_repository, new FixedTimeProvider(now));
}
