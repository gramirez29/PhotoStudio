using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.MarkClientAbsent;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="MarkClientAbsentHandler"/>.
/// </summary>
public sealed class MarkClientAbsentHandlerTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(BookingPolicy.Default.ClientAbsentToleranceMinutes);

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
                new MarkClientAbsentCommand(Guid.CreateVersion7()),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Once the tolerance has passed, the client is marked absent and the booking is saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AfterTheTolerance_MarksTheClientAbsentAndSaves()
    {
        var booking = BookingFactory.Confirmed();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var response = await HandlerAt(BookingFactory.SessionStart + Tolerance).HandleAsync(
            new MarkClientAbsentCommand(booking.Id),
            TestContext.Current.CancellationToken);

        response.Status.ShouldBe(BookingStatus.ClientAbsent);
        response.AllowedActions.ShouldContain(BookingAction.RevertClientAbsent);
        await _repository.Received(1).UpdateAsync(booking, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Before the tolerance has passed the client cannot be marked absent and nothing is saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_BeforeTheTolerance_ThrowsGuardFailed()
    {
        var booking = BookingFactory.Confirmed();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var exception = await Should.ThrowAsync<DomainException>(
            () => HandlerAt(BookingFactory.SessionStart + Tolerance - TimeSpan.FromMinutes(1)).HandleAsync(
                new MarkClientAbsentCommand(booking.Id),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A booking that is not confirmed cannot be marked absent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithTentativeBooking_ThrowsInvalidTransition()
    {
        var booking = BookingFactory.Tentative();
        _repository.GetByIdAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);

        var exception = await Should.ThrowAsync<DomainException>(
            () => HandlerAt(BookingFactory.SessionStart + Tolerance).HandleAsync(
                new MarkClientAbsentCommand(booking.Id),
                TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidTransition);
    }

    /// <summary>
    /// Creates the handler with a clock fixed at the given instant.
    /// </summary>
    /// <param name="now">Instant returned by the clock.</param>
    /// <returns>The handler.</returns>
    private MarkClientAbsentHandler HandlerAt(DateTimeOffset now) => new(_repository, new FixedTimeProvider(now));
}
