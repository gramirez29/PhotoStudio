using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.ExpireTentativeBookings;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="ExpireTentativeBookingsHandler"/>.
/// </summary>
public sealed class ExpireTentativeBookingsHandlerTests
{
    private static readonly DateTimeOffset AfterHold = BookingFactory.CreatedAt.AddHours(BookingPolicy.Default.TentativeHoldHours + 1);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();

    /// <summary>
    /// Every due booking expires and is saved, which releases its slot.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithDueBookings_ExpiresAndSavesEach()
    {
        var first = BookingFactory.Tentative();
        var second = BookingFactory.Tentative();
        ReturnDue(first, second);

        var result = await HandlerAt(AfterHold).HandleAsync(new ExpireTentativeBookingsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new ExpireTentativeBookingsResult(Expired: 2, Skipped: 0));
        first.Status.ShouldBe(BookingStatus.Expired);
        second.Status.ShouldBe(BookingStatus.Expired);
        await _repository.Received(1).UpdateAsync(first, Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(second, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The repository is asked for the bookings due at the current instant, limited to the batch size.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_QueriesDueBookingsAtTheCurrentInstant()
    {
        ReturnDue();

        await HandlerAt(AfterHold).HandleAsync(new ExpireTentativeBookingsCommand(25), TestContext.Current.CancellationToken);

        await _repository.Received(1).ListExpiredTentativeAsync(AfterHold, 25, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Bookings whose hold has not ended yet (a stale listing) are skipped and not saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenAGuardFails_SkipsTheBookingsWithoutSaving()
    {
        ReturnDue(BookingFactory.Tentative(), BookingFactory.Tentative());

        // The clock is still inside the hold, so the domain guard rejects both.
        var beforeHold = BookingFactory.CreatedAt.AddHours(BookingPolicy.Default.TentativeHoldHours - 1);
        var result = await HandlerAt(beforeHold).HandleAsync(new ExpireTentativeBookingsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new ExpireTentativeBookingsResult(Expired: 0, Skipped: 2));
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When another write wins the race for a booking, the conflict is counted as skipped and the rest of the batch still runs.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenAnotherWriteWinsTheRace_SkipsThatBookingAndContinues()
    {
        var raced = BookingFactory.Tentative();
        var other = BookingFactory.Tentative();
        ReturnDue(raced, other);
        _repository.UpdateAsync(raced, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConflictException(ApplicationErrorCodes.ConcurrencyConflict, "modified")));

        var result = await HandlerAt(AfterHold).HandleAsync(new ExpireTentativeBookingsCommand(10), TestContext.Current.CancellationToken);

        result.ShouldBe(new ExpireTentativeBookingsResult(Expired: 1, Skipped: 1));
        await _repository.Received(1).UpdateAsync(other, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Makes the repository return the given bookings as due.
    /// </summary>
    /// <param name="bookings">Bookings to return.</param>
    private void ReturnDue(params Booking[] bookings) =>
        _repository.ListExpiredTentativeAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(bookings);

    /// <summary>
    /// Creates the handler with a clock fixed at the given instant.
    /// </summary>
    /// <param name="now">Instant returned by the clock.</param>
    /// <returns>The handler.</returns>
    private ExpireTentativeBookingsHandler HandlerAt(DateTimeOffset now) => new(_repository, new FixedTimeProvider(now));
}
