using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.GetStats;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="GetBookingStatsHandler"/>: the bookings counted by status for the home screen.
/// </summary>
public sealed class GetBookingStatsHandlerTests
{
    private static readonly DateTimeOffset From = new(2026, 11, 1, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = From.AddDays(30);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();

    /// <summary>
    /// Every status is in the answer, in the order of the enumeration, with zero for the ones that have no bookings, and the total adds them up.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ReportsEveryStatus_WithZeroForTheMissingOnes()
    {
        var photographerId = Guid.CreateVersion7();
        IReadOnlyDictionary<BookingStatus, int> counts = new Dictionary<BookingStatus, int> { [BookingStatus.Confirmed] = 3, [BookingStatus.Cancelled] = 2 };
        _repository.CountByStatusAsync(photographerId, From, To, Arg.Any<CancellationToken>()).Returns(counts);

        var response = await new GetBookingStatsHandler(_repository).HandleAsync(new GetBookingStatsQuery(photographerId, From, To), TestContext.Current.CancellationToken);

        response.Total.ShouldBe(5);
        response.ByStatus.Select(entry => entry.Status).ShouldBe(Enum.GetValues<BookingStatus>());
        response.ByStatus.Single(entry => entry.Status == BookingStatus.Confirmed).Count.ShouldBe(3);
        response.ByStatus.Single(entry => entry.Status == BookingStatus.Cancelled).Count.ShouldBe(2);
        response.ByStatus.Single(entry => entry.Status == BookingStatus.Tentative).Count.ShouldBe(0);
    }

    /// <summary>
    /// Without a period the repository is asked to count all the bookings.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithoutAPeriod_CountsEverything()
    {
        var photographerId = Guid.CreateVersion7();
        _repository.CountByStatusAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<BookingStatus, int>());

        var response = await new GetBookingStatsHandler(_repository).HandleAsync(new GetBookingStatsQuery(photographerId), TestContext.Current.CancellationToken);

        response.Total.ShouldBe(0);
        await _repository.Received(1).CountByStatusAsync(photographerId, null, null, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A period that ends before it starts, or is empty, is refused.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithAnInvertedPeriod_Throws()
    {
        var handler = new GetBookingStatsHandler(_repository);

        await Should.ThrowAsync<ArgumentException>(() => handler.HandleAsync(new GetBookingStatsQuery(Guid.CreateVersion7(), From: To, To: From), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => handler.HandleAsync(new GetBookingStatsQuery(Guid.CreateVersion7(), From, From), TestContext.Current.CancellationToken));
    }
}
