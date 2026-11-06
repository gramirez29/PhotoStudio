using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoBookingRepository.CountByStatusAsync"/> against a real MongoDB replica set: the counts by status, the
/// period, and that another photographer's bookings are never counted.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoBookingStatsTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private const string SkipReason = "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.";

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Noon = new(2026, 11, 10, 18, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The bookings are counted by status, and the statuses without bookings are not in the answer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CountByStatusAsync_CountsEachStatus()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var photographerId = Guid.CreateVersion7();
        await AddAsync(photographerId, Noon.AddHours(1));
        await AddAsync(photographerId, Noon.AddHours(4));
        await AddAndCancelAsync(photographerId, Noon.AddHours(7));

        var counts = await NewRepository().CountByStatusAsync(photographerId, null, null, TestContext.Current.CancellationToken);

        counts[BookingStatus.Tentative].ShouldBe(2);
        counts[BookingStatus.Cancelled].ShouldBe(1);
        counts.ContainsKey(BookingStatus.Confirmed).ShouldBeFalse();
    }

    /// <summary>
    /// A booking belongs to a period by the start of its session, and the period is half open.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CountByStatusAsync_CountsOnlyTheSessionsThatStartInThePeriod()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var photographerId = Guid.CreateVersion7();
        await AddAsync(photographerId, Noon.AddDays(-2));
        await AddAsync(photographerId, Noon);
        await AddAsync(photographerId, Noon.AddDays(1).AddHours(-4));
        await AddAsync(photographerId, Noon.AddDays(1));

        var counts = await NewRepository().CountByStatusAsync(photographerId, Noon, Noon.AddDays(1), TestContext.Current.CancellationToken);
        var onlyAfter = await NewRepository().CountByStatusAsync(photographerId, Noon.AddDays(1), null, TestContext.Current.CancellationToken);
        var onlyBefore = await NewRepository().CountByStatusAsync(photographerId, null, Noon, TestContext.Current.CancellationToken);

        counts[BookingStatus.Tentative].ShouldBe(2);
        onlyAfter[BookingStatus.Tentative].ShouldBe(1);
        onlyBefore[BookingStatus.Tentative].ShouldBe(1);
    }

    /// <summary>
    /// The bookings of another photographer are never counted, and a photographer with none gets an empty answer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task CountByStatusAsync_NeverCountsTheBookingsOfAnotherPhotographer()
    {
        Assert.SkipUnless(fixture.IsAvailable, SkipReason);
        var mine = Guid.CreateVersion7();
        var theirs = Guid.CreateVersion7();
        await AddAsync(mine, Noon.AddHours(1));
        await AddAsync(theirs, Noon.AddHours(1));
        await AddAsync(theirs, Noon.AddHours(4));

        var own = await NewRepository().CountByStatusAsync(mine, null, null, TestContext.Current.CancellationToken);
        var nobody = await NewRepository().CountByStatusAsync(Guid.CreateVersion7(), null, null, TestContext.Current.CancellationToken);

        own.Values.Sum().ShouldBe(1);
        nobody.ShouldBeEmpty();
    }

    /// <summary>
    /// Stores a tentative booking of an hour that starts at the given instant.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="start">Session start.</param>
    /// <returns>The stored booking.</returns>
    private async Task<Booking> AddAsync(Guid photographerId, DateTimeOffset start)
    {
        var booking = Booking.Create(
            Guid.CreateVersion7(),
            photographerId,
            ClientContact.Create("María Pérez", "+506 8888-8888"),
            "Retrato familiar",
            Money.Create(100_000m, "CRC"),
            TimeSlot.Create(start, start.AddHours(1)),
            BookingPolicy.Default,
            Now);
        await NewRepository().AddAsync(booking, TestContext.Current.CancellationToken);
        return booking;
    }

    /// <summary>
    /// Stores a tentative booking and cancels it. The stored copy is read back first, because updating needs the version the database holds.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="start">Session start.</param>
    /// <returns>A task that completes when the booking is cancelled.</returns>
    private async Task AddAndCancelAsync(Guid photographerId, DateTimeOffset start)
    {
        var repository = NewRepository();
        var added = await AddAsync(photographerId, start);
        var stored = (await repository.GetByIdAsync(added.Id, TestContext.Current.CancellationToken))!;
        stored.Cancel(Actor.Photographer, null, Now);
        await repository.UpdateAsync(stored, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Builds a repository over the test database.
    /// </summary>
    /// <returns>The repository.</returns>
    private MongoBookingRepository NewRepository() => new(fixture.Database);
}
