using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.ListBookings;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="ListBookingsHandler"/>.
/// </summary>
public sealed class ListBookingsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();
    private readonly ListBookingsHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListBookingsHandlerTests"/> class.
    /// </summary>
    public ListBookingsHandlerTests()
    {
        _handler = new ListBookingsHandler(_repository, new FixedTimeProvider(Now));
    }

    /// <summary>
    /// The handler asks the repository only for sessions that ended within the history window and bounds the result.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_QueriesTheHistoryWindowAndTheMaximumSize()
    {
        var photographerId = Guid.CreateVersion7();
        _repository
            .ListByPhotographerAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _handler.HandleAsync(new ListBookingsQuery(photographerId), TestContext.Current.CancellationToken);

        await _repository.Received(1).ListByPhotographerAsync(
            photographerId,
            Now.AddDays(-ListBookingsHandler.HistoryDays),
            ListBookingsHandler.MaxResults,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Every booking is mapped to a summary that keeps the repository order and exposes the balance.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_MapsBookingsToSummariesInRepositoryOrder()
    {
        var photographerId = Guid.CreateVersion7();
        var first = NewBooking(photographerId, "Ana Vargas", 5);
        var second = NewBooking(photographerId, "Luis Mora", 8);
        _repository
            .ListByPhotographerAsync(photographerId, Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([first, second]);

        var summaries = await _handler.HandleAsync(new ListBookingsQuery(photographerId), TestContext.Current.CancellationToken);

        summaries.Select(summary => summary.ClientName).ShouldBe(["Ana Vargas", "Luis Mora"]);
        summaries[0].Id.ShouldBe(first.Id);
        summaries[0].Status.ShouldBe(BookingStatus.Tentative);
        summaries[0].PackagePrice.Amount.ShouldBe(100_000m);
        summaries[0].Balance.Amount.ShouldBe(100_000m);
        summaries[0].SessionStart.ShouldBe(first.Slot.Start);
    }

    /// <summary>
    /// A photographer without bookings gets an empty list, not an error.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithoutBookings_ReturnsAnEmptyList()
    {
        _repository
            .ListByPhotographerAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var summaries = await _handler.HandleAsync(new ListBookingsQuery(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        summaries.ShouldBeEmpty();
    }

    /// <summary>
    /// Creates a tentative booking that starts the given number of days after <see cref="Now"/>.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="clientName">Client name.</param>
    /// <param name="daysAhead">Days from now until the session.</param>
    /// <returns>The booking.</returns>
    private static Booking NewBooking(Guid photographerId, string clientName, int daysAhead) => Booking.Create(
        Guid.CreateVersion7(),
        photographerId,
        ClientContact.Create(clientName, "+506 8888-8888"),
        "Retrato familiar",
        Money.Create(100_000m, "CRC"),
        TimeSlot.Create(Now.AddDays(daysAhead), Now.AddDays(daysAhead).AddHours(2)),
        BookingPolicy.Default,
        Now);
}
