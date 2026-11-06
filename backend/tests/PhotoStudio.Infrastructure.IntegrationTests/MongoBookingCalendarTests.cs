using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoBookingRepository.ListInRangeAsync"/> against a real MongoDB replica set: which bookings touch a range,
/// in which order, for whom, and that the query is served by an index.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoBookingCalendarTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Noon = new(2026, 11, 10, 18, 0, 0, TimeSpan.Zero);

    private static readonly BookingStatus[] Active = [BookingStatus.Tentative, BookingStatus.Confirmed];

    /// <summary>
    /// A session that ends exactly when the range starts, or starts exactly when it ends, is not part of it: the ranges are half open.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListInRangeAsync_LeavesOutTheSessionsThatOnlyTouchTheEdges()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var photographerId = Guid.CreateVersion7();
        await AddAsync(photographerId, Noon.AddHours(-4), Noon.AddHours(-3));
        await AddAsync(photographerId, Noon.AddHours(-3), Noon);
        var inside = await AddAsync(photographerId, Noon.AddHours(1), Noon.AddHours(2));
        await AddAsync(photographerId, Noon.AddHours(6), Noon.AddHours(8));

        var found = await NewRepository().ListInRangeAsync(photographerId, Noon, Noon.AddHours(6), Active, 100, TestContext.Current.CancellationToken);

        found.Select(booking => booking.Id).ShouldBe([inside.Id]);
    }

    /// <summary>
    /// A session that starts before the range and ends inside it, and one that starts inside it and ends after it, are part of it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListInRangeAsync_ReturnsTheSessionsThatCrossAnEdge()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var photographerId = Guid.CreateVersion7();
        var crossesTheStart = await AddAsync(photographerId, Noon.AddHours(-1), Noon.AddHours(1));
        var inside = await AddAsync(photographerId, Noon.AddHours(2), Noon.AddHours(3));
        var crossesTheEnd = await AddAsync(photographerId, Noon.AddHours(5), Noon.AddHours(7));

        var found = await NewRepository().ListInRangeAsync(photographerId, Noon, Noon.AddHours(6), Active, 100, TestContext.Current.CancellationToken);

        found.Select(booking => booking.Id).ShouldBe([crossesTheStart.Id, inside.Id, crossesTheEnd.Id]);
    }

    /// <summary>
    /// A session that covers the whole range, starting before it and ending after it, is part of it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListInRangeAsync_ReturnsASessionThatCoversTheWholeRange()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var photographerId = Guid.CreateVersion7();
        var covers = await AddAsync(photographerId, Noon.AddHours(-5), Noon.AddHours(20));

        var found = await NewRepository().ListInRangeAsync(photographerId, Noon, Noon.AddHours(1), Active, 100, TestContext.Current.CancellationToken);

        found.Select(booking => booking.Id).ShouldBe([covers.Id]);
    }

    /// <summary>
    /// The bookings come ordered by the start of the session, whatever the order they were created in.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListInRangeAsync_OrdersByTheStartOfTheSession()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var photographerId = Guid.CreateVersion7();
        var late = await AddAsync(photographerId, Noon.AddHours(8), Noon.AddHours(9));
        var early = await AddAsync(photographerId, Noon.AddHours(1), Noon.AddHours(2));
        var middle = await AddAsync(photographerId, Noon.AddHours(4), Noon.AddHours(5));

        var found = await NewRepository().ListInRangeAsync(photographerId, Noon, Noon.AddDays(1), Active, 100, TestContext.Current.CancellationToken);

        found.Select(booking => booking.Id).ShouldBe([early.Id, middle.Id, late.Id]);
    }

    /// <summary>
    /// Only the bookings of the photographer are returned.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListInRangeAsync_NeverReturnsTheBookingsOfAnotherPhotographer()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var mine = Guid.CreateVersion7();
        var theirs = Guid.CreateVersion7();
        var own = await AddAsync(mine, Noon.AddHours(1), Noon.AddHours(2));
        await AddAsync(theirs, Noon.AddHours(1), Noon.AddHours(2));

        var found = await NewRepository().ListInRangeAsync(mine, Noon, Noon.AddDays(1), null, 100, TestContext.Current.CancellationToken);

        found.Select(booking => booking.Id).ShouldBe([own.Id]);
    }

    /// <summary>
    /// The status filter keeps only the statuses asked for, and no filter returns the bookings that are over too.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListInRangeAsync_FiltersByStatus()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var photographerId = Guid.CreateVersion7();
        var repository = NewRepository();
        var tentative = await AddAsync(photographerId, Noon.AddHours(1), Noon.AddHours(2));
        var confirmed = NewBooking(photographerId, Noon.AddHours(3), Noon.AddHours(4));
        confirmed.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);
        confirmed.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(50_000m, "CRC"), PaymentMethod.Cash, "deposit", Now);
        await repository.AddAsync(confirmed, TestContext.Current.CancellationToken);
        var cancelled = await AddAndCancelAsync(photographerId, Noon.AddHours(5), Noon.AddHours(6));

        var active = await repository.ListInRangeAsync(photographerId, Noon, Noon.AddDays(1), Active, 100, TestContext.Current.CancellationToken);
        var everything = await repository.ListInRangeAsync(photographerId, Noon, Noon.AddDays(1), null, 100, TestContext.Current.CancellationToken);
        var onlyCancelled = await repository.ListInRangeAsync(photographerId, Noon, Noon.AddDays(1), [BookingStatus.Cancelled], 100, TestContext.Current.CancellationToken);

        active.Select(booking => booking.Id).ShouldBe([tentative.Id, confirmed.Id]);
        everything.Select(booking => booking.Id).ShouldBe([tentative.Id, confirmed.Id, cancelled.Id]);
        onlyCancelled.Select(booking => booking.Id).ShouldBe([cancelled.Id]);
    }

    /// <summary>
    /// A cancelled booking does not hold the slot, so another one can take it and both appear when the ended ones are asked for, side
    /// by side in time.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListInRangeAsync_ReturnsABookingThatOverlapsACancelledOne_WhenEndedOnesAreIncluded()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var photographerId = Guid.CreateVersion7();
        var repository = NewRepository();
        var cancelled = await AddAndCancelAsync(photographerId, Noon.AddHours(1), Noon.AddHours(3));
        var replacement = await AddAsync(photographerId, Noon.AddHours(2), Noon.AddHours(4));

        var everything = await repository.ListInRangeAsync(photographerId, Noon, Noon.AddDays(1), null, 100, TestContext.Current.CancellationToken);
        var active = await repository.ListInRangeAsync(photographerId, Noon, Noon.AddDays(1), Active, 100, TestContext.Current.CancellationToken);

        everything.Select(booking => booking.Id).ShouldBe([cancelled.Id, replacement.Id]);
        active.Select(booking => booking.Id).ShouldBe([replacement.Id]);
    }

    /// <summary>
    /// The limit cuts the answer, keeping the earliest sessions.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListInRangeAsync_RespectsTheLimit_KeepingTheEarliest()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var photographerId = Guid.CreateVersion7();
        var first = await AddAsync(photographerId, Noon.AddHours(1), Noon.AddHours(2));
        var second = await AddAsync(photographerId, Noon.AddHours(3), Noon.AddHours(4));
        await AddAsync(photographerId, Noon.AddHours(5), Noon.AddHours(6));

        var found = await NewRepository().ListInRangeAsync(photographerId, Noon, Noon.AddDays(1), Active, 2, TestContext.Current.CancellationToken);

        found.Select(booking => booking.Id).ShouldBe([first.Id, second.Id]);
    }

    /// <summary>
    /// With the indexes created at startup, the query is served by an index scan, not by reading every booking of the collection.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListInRangeAsync_IsServedByAnIndex()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        await new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);
        var photographerId = Guid.CreateVersion7();
        await AddAsync(photographerId, Noon.AddHours(1), Noon.AddHours(2));

        var explain = await fixture.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument
            {
                {
                    "explain",
                    new BsonDocument
                    {
                        { "find", MongoBookingRepository.CollectionName },
                        {
                            "filter",
                            new BsonDocument
                            {
                                { "photographerId", new BsonBinaryData(photographerId, GuidRepresentation.Standard) },
                                { "slotStart", new BsonDocument("$lt", Noon.AddDays(1).UtcDateTime) },
                                { "slotEnd", new BsonDocument("$gt", Noon.UtcDateTime) },
                            }
                        },
                        { "sort", new BsonDocument { { "slotStart", 1 }, { "_id", 1 } } },
                    }
                },
            },
            cancellationToken: TestContext.Current.CancellationToken);

        explain.ToJson().ShouldContain("IXSCAN");
        explain.ToJson().ShouldNotContain("COLLSCAN");
    }

    /// <summary>
    /// Stores a tentative booking for the slot and returns it.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="start">Session start.</param>
    /// <param name="end">Session end.</param>
    /// <returns>The stored booking.</returns>
    private async Task<Booking> AddAsync(Guid photographerId, DateTimeOffset start, DateTimeOffset end)
    {
        var booking = NewBooking(photographerId, start, end);
        await NewRepository().AddAsync(booking, TestContext.Current.CancellationToken);
        return booking;
    }

    /// <summary>
    /// Stores a tentative booking for the slot and cancels it, which releases the slot. The stored copy is read back first, because
    /// updating needs the version the database holds.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="start">Session start.</param>
    /// <param name="end">Session end.</param>
    /// <returns>The cancelled booking as stored.</returns>
    private async Task<Booking> AddAndCancelAsync(Guid photographerId, DateTimeOffset start, DateTimeOffset end)
    {
        var repository = NewRepository();
        var added = await AddAsync(photographerId, start, end);
        var stored = (await repository.GetByIdAsync(added.Id, TestContext.Current.CancellationToken))!;
        stored.Cancel(Actor.Photographer, null, Now);
        await repository.UpdateAsync(stored, TestContext.Current.CancellationToken);
        return stored;
    }

    /// <summary>
    /// Creates a tentative booking for the slot.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="start">Session start.</param>
    /// <param name="end">Session end.</param>
    /// <returns>The new booking.</returns>
    private static Booking NewBooking(Guid photographerId, DateTimeOffset start, DateTimeOffset end) => Booking.Create(
        Guid.CreateVersion7(),
        photographerId,
        ClientContact.Create("María Pérez", "+506 8888-8888"),
        "Retrato familiar",
        Money.Create(100_000m, "CRC"),
        TimeSlot.Create(start, end),
        BookingPolicy.Default,
        Now);

    /// <summary>
    /// Builds a repository over the test database.
    /// </summary>
    /// <returns>The repository.</returns>
    private MongoBookingRepository NewRepository() => new(fixture.Database);
}
