using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.ListBookings;
using PhotoStudio.Application.Pagination;
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
    /// The handler asks the repository only for sessions that ended within the history window and for one booking more than the page, to know if there is a next one.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_QueriesTheHistoryWindowAndOneMoreThanThePage()
    {
        var photographerId = Guid.CreateVersion7();
        _repository
            .ListByPhotographerAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<PageCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _handler.HandleAsync(new ListBookingsQuery(photographerId), TestContext.Current.CancellationToken);

        await _repository.Received(1).ListByPhotographerAsync(
            photographerId,
            Now.AddDays(-ListBookingsHandler.HistoryDays),
            null,
            PageSizes.Default + 1,
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
            .ListByPhotographerAsync(photographerId, Arg.Any<DateTimeOffset>(), Arg.Any<PageCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([first, second]);

        var page = await _handler.HandleAsync(new ListBookingsQuery(photographerId), TestContext.Current.CancellationToken);

        var items = page.Items;
        items.Select(summary => summary.ClientName).ShouldBe(["Ana Vargas", "Luis Mora"]);
        items[0].Id.ShouldBe(first.Id);
        items[0].Status.ShouldBe(BookingStatus.Tentative);
        items[0].PackagePrice.Amount.ShouldBe(100_000m);
        items[0].Balance.Amount.ShouldBe(100_000m);
        items[0].SessionStart.ShouldBe(first.Slot.Start);
        page.NextCursor.ShouldBeNull();
    }

    /// <summary>
    /// A photographer without bookings gets an empty list, not an error.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithoutBookings_ReturnsAnEmptyList()
    {
        _repository
            .ListByPhotographerAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<PageCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var page = await _handler.HandleAsync(new ListBookingsQuery(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        page.Items.ShouldBeEmpty();
        page.NextCursor.ShouldBeNull();
    }

    /// <summary>
    /// When the repository finds more bookings than the page holds, the extra one is not shown and the cursor names the last
    /// booking of the page, by its creation instant.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithMoreThanAPage_CutsItAndReturnsTheCursorOfTheLastOne()
    {
        var photographerId = Guid.CreateVersion7();
        var found = new[] { NewBooking(photographerId, "Ana", 5), NewBooking(photographerId, "Luis", 6), NewBooking(photographerId, "Eva", 7) };
        _repository
            .ListByPhotographerAsync(photographerId, Arg.Any<DateTimeOffset>(), null, 3, Arg.Any<CancellationToken>())
            .Returns(found);

        var page = await _handler.HandleAsync(new ListBookingsQuery(photographerId, null, 2), TestContext.Current.CancellationToken);

        page.Items.Select(summary => summary.ClientName).ShouldBe(["Ana", "Luis"]);
        PageCursor.TryParse(page.NextCursor, out var cursor).ShouldBeTrue();
        cursor.Id.ShouldBe(found[1].Id);
        cursor.At.ShouldBe(found[1].CreatedAt);
    }

    /// <summary>
    /// The page after a cursor asks the repository to continue from it, and a size outside the bounds is brought back into them.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithACursor_ContinuesFromItAndKeepsTheSizeWithinBounds()
    {
        var photographerId = Guid.CreateVersion7();
        var after = new PageCursor(Now, Guid.CreateVersion7());
        _repository
            .ListByPhotographerAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<PageCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _handler.HandleAsync(new ListBookingsQuery(photographerId, after, 500), TestContext.Current.CancellationToken);

        await _repository.Received(1).ListByPhotographerAsync(
            photographerId, Arg.Any<DateTimeOffset>(), after, PageSizes.Max + 1, Arg.Any<CancellationToken>());
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
