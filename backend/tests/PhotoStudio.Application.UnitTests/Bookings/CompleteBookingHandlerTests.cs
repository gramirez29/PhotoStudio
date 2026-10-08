using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.CompleteBooking;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="CompleteBookingHandler"/>.
/// </summary>
public sealed class CompleteBookingHandlerTests
{
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
            () => HandlerAt(BookingFactory.SessionStart).HandleAsync(
                new CompleteBookingCommand(Guid.CreateVersion7()),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Once the session has started, a confirmed booking is completed and saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AfterTheSessionStarted_CompletesAndSaves()
    {
        var booking = BookingFactory.Confirmed();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var response = await HandlerAt(BookingFactory.SessionStart.AddHours(1)).HandleAsync(
            new CompleteBookingCommand(booking.Id),
            TestContext.Current.CancellationToken);

        response.Status.ShouldBe(BookingStatus.Completed);
        await _repository.Received(1).UpdateAsync(booking, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Before the session starts the booking cannot be completed and nothing is saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_BeforeTheSessionStarts_ThrowsGuardFailed()
    {
        var booking = BookingFactory.Confirmed();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var exception = await Should.ThrowAsync<DomainException>(
            () => HandlerAt(BookingFactory.SessionStart.AddMinutes(-1)).HandleAsync(
                new CompleteBookingCommand(booking.Id),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A booking that is not confirmed cannot be completed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTentativeBooking_ThrowsInvalidTransition()
    {
        var booking = BookingFactory.Tentative();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var exception = await Should.ThrowAsync<DomainException>(
            () => HandlerAt(BookingFactory.SessionStart.AddHours(1)).HandleAsync(
                new CompleteBookingCommand(booking.Id),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidTransition);
    }

    /// <summary>
    /// Creates the handler with a clock fixed at the given instant.
    /// </summary>
    /// <param name="now">Instant returned by the clock.</param>
    /// <returns>The handler.</returns>
    private CompleteBookingHandler HandlerAt(DateTimeOffset now) => new(_repository, new FixedTimeProvider(now));
}
