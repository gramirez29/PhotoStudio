using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoBookingRepository"/> against a real MongoDB replica set, focused on double booking.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoBookingRepositoryTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SessionStart = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A booking whose slot overlaps an active one is rejected and not stored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AddAsync_WithOverlappingSlot_ThrowsSlotUnavailableAndStoresNothing()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var first = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var overlapping = NewBooking(photographerId, SessionStart.AddHours(1), SessionStart.AddHours(3));
        var repository = NewRepository();

        await repository.AddAsync(first, TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<ConflictException>(
            () => repository.AddAsync(overlapping, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
        (await repository.GetByIdAsync(overlapping.Id, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await repository.GetByIdAsync(first.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>
    /// Slots that only touch at an endpoint do not overlap, because ranges are half-open.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AddAsync_WithBackToBackSlots_StoresBoth()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var morning = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var afternoon = NewBooking(photographerId, SessionStart.AddHours(2), SessionStart.AddHours(4));
        var repository = NewRepository();

        await repository.AddAsync(morning, TestContext.Current.CancellationToken);
        await repository.AddAsync(afternoon, TestContext.Current.CancellationToken);

        (await repository.GetByIdAsync(afternoon.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>
    /// The same slot is free for a different photographer.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AddAsync_WithSameSlotForAnotherPhotographer_StoresBoth()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var first = NewBooking(Guid.CreateVersion7(), SessionStart, SessionStart.AddHours(2));
        var second = NewBooking(Guid.CreateVersion7(), SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();

        await repository.AddAsync(first, TestContext.Current.CancellationToken);
        await repository.AddAsync(second, TestContext.Current.CancellationToken);

        (await repository.GetByIdAsync(second.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>
    /// The race that motivated the calendar document: many simultaneous requests for the same slot, only one wins.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AddAsync_WithConcurrentRequestsForSameSlot_StoresExactlyOne()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        const int requests = 25;
        var photographerId = Guid.CreateVersion7();
        var bookings = Enumerable.Range(0, requests)
            .Select(_ => NewBooking(photographerId, SessionStart, SessionStart.AddHours(2)))
            .ToList();

        var outcomes = await Task.WhenAll(bookings.Select(booking => TryAddAsync(booking)));

        outcomes.Count(succeeded => succeeded).ShouldBe(1);
        var stored = 0;
        foreach (var booking in bookings)
        {
            if (await NewRepository().GetByIdAsync(booking.Id, TestContext.Current.CancellationToken) is not null)
            {
                stored++;
            }
        }

        stored.ShouldBe(1);
    }

    /// <summary>
    /// Partially overlapping requests racing each other never end up with two overlapping bookings stored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AddAsync_WithConcurrentOverlappingRanges_NeverStoresOverlappingBookings()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();

        // Ten 2-hour sessions that start 30 minutes apart, so each one overlaps its neighbours.
        var bookings = Enumerable.Range(0, 10)
            .Select(index => NewBooking(photographerId, SessionStart.AddMinutes(30 * index), SessionStart.AddMinutes((30 * index) + 120)))
            .ToList();

        var outcomes = await Task.WhenAll(bookings.Select(booking => TryAddAsync(booking)));

        var winners = bookings.Where((_, index) => outcomes[index]).ToList();
        winners.Count.ShouldBeGreaterThan(0);
        foreach (var left in winners)
        {
            foreach (var right in winners.Where(other => other.Id != left.Id))
            {
                left.Slot.Overlaps(right.Slot).ShouldBeFalse();
            }
        }
    }

    /// <summary>
    /// Cancelling a booking frees its slot, so another booking can take it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateAsync_WhenBookingIsCancelled_ReleasesTheSlot()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var original = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var replacement = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(original, TestContext.Current.CancellationToken);

        var stored = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Cancel(Actor.Photographer, "Client asked to cancel", Now.AddDays(1));
        await repository.UpdateAsync(stored, TestContext.Current.CancellationToken);
        await repository.AddAsync(replacement, TestContext.Current.CancellationToken);

        (await repository.GetByIdAsync(replacement.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>
    /// A stale copy of a booking cannot overwrite a newer version, and the slot stays reserved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateAsync_WithStaleVersion_ThrowsConcurrencyConflictAndKeepsTheSlot()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var original = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var rival = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(original, TestContext.Current.CancellationToken);

        var firstCopy = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        var staleCopy = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        firstCopy.ShouldNotBeNull();
        staleCopy.ShouldNotBeNull();
        firstCopy.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now.AddDays(1));
        await repository.UpdateAsync(firstCopy, TestContext.Current.CancellationToken);
        staleCopy.Cancel(Actor.Photographer, "Stale write", Now.AddDays(1));

        var exception = await Should.ThrowAsync<ConflictException>(
            () => repository.UpdateAsync(staleCopy, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.ConcurrencyConflict);
        var slotException = await Should.ThrowAsync<ConflictException>(
            () => repository.AddAsync(rival, TestContext.Current.CancellationToken));
        slotException.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
    }

    /// <summary>
    /// The list returns only the photographer's own bookings, earliest session first.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListByPhotographerAsync_ReturnsOnlyOwnBookingsOrderedBySessionStart()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var later = NewBooking(photographerId, SessionStart.AddDays(3), SessionStart.AddDays(3).AddHours(2));
        var earlier = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var foreign = NewBooking(Guid.CreateVersion7(), SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(later, TestContext.Current.CancellationToken);
        await repository.AddAsync(earlier, TestContext.Current.CancellationToken);
        await repository.AddAsync(foreign, TestContext.Current.CancellationToken);

        var bookings = await repository.ListByPhotographerAsync(photographerId, Now, 50, TestContext.Current.CancellationToken);

        bookings.Select(booking => booking.Id).ShouldBe([earlier.Id, later.Id]);
    }

    /// <summary>
    /// Sessions that ended before the cut-off instant are left out of the list.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListByPhotographerAsync_LeavesOutSessionsThatEndedBeforeTheCutOff()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var old = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var recent = NewBooking(photographerId, SessionStart.AddDays(10), SessionStart.AddDays(10).AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(old, TestContext.Current.CancellationToken);
        await repository.AddAsync(recent, TestContext.Current.CancellationToken);

        var bookings = await repository.ListByPhotographerAsync(
            photographerId,
            SessionStart.AddDays(5),
            50,
            TestContext.Current.CancellationToken);

        bookings.Select(booking => booking.Id).ShouldBe([recent.Id]);
    }

    /// <summary>
    /// The list never returns more bookings than the limit, keeping the earliest sessions.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task ListByPhotographerAsync_RespectsTheLimit()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var bookings = Enumerable.Range(0, 4)
            .Select(day => NewBooking(photographerId, SessionStart.AddDays(day), SessionStart.AddDays(day).AddHours(2)))
            .ToList();
        var repository = NewRepository();
        foreach (var booking in bookings)
        {
            await repository.AddAsync(booking, TestContext.Current.CancellationToken);
        }

        var listed = await repository.ListByPhotographerAsync(photographerId, Now, 2, TestContext.Current.CancellationToken);

        listed.Select(booking => booking.Id).ShouldBe([bookings[0].Id, bookings[1].Id]);
    }

    /// <summary>
    /// Adds a booking with its own repository instance, like concurrent HTTP requests would.
    /// </summary>
    /// <param name="booking">Booking to add.</param>
    /// <returns><see langword="true"/> when stored; <see langword="false"/> when rejected for an unavailable slot.</returns>
    private async Task<bool> TryAddAsync(Booking booking)
    {
        try
        {
            await Task.Yield();
            await NewRepository().AddAsync(booking, TestContext.Current.CancellationToken);
            return true;
        }
        catch (ConflictException exception) when (exception.Code == ApplicationErrorCodes.SlotUnavailable)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a repository over the fixture database.
    /// </summary>
    /// <returns>A new repository.</returns>
    private MongoBookingRepository NewRepository() => new(fixture.Database);

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
}
