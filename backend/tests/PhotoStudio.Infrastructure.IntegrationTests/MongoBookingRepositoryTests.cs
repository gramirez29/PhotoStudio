using MongoDB.Driver;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence;
using PhotoStudio.Infrastructure.Persistence.Documents;

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
    /// Rescheduling frees the old slot for others and takes the new one.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateReservingSlotAsync_MovesTheReservation()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var original = NewConfirmedBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(original, TestContext.Current.CancellationToken);

        var moved = await RescheduleAsync(repository, original.Id, SessionStart.AddDays(1), SessionStart.AddDays(1).AddHours(2));
        var intoOldSlot = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var intoNewSlot = NewBooking(photographerId, SessionStart.AddDays(1), SessionStart.AddDays(1).AddHours(2));
        await repository.AddAsync(intoOldSlot, TestContext.Current.CancellationToken);
        var exception = await Should.ThrowAsync<ConflictException>(
            () => repository.AddAsync(intoNewSlot, TestContext.Current.CancellationToken));

        moved.Slot.Start.ShouldBe(SessionStart.AddDays(1));
        moved.RescheduleCount.ShouldBe(1);
        exception.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
    }

    /// <summary>
    /// A slot that overlaps another booking is rejected and the booking keeps its stored slot and reservation.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateReservingSlotAsync_WhenTheNewSlotIsTaken_KeepsTheBookingAndItsOldReservation()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var original = NewConfirmedBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var blocker = NewBooking(photographerId, SessionStart.AddDays(1), SessionStart.AddDays(1).AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(original, TestContext.Current.CancellationToken);
        await repository.AddAsync(blocker, TestContext.Current.CancellationToken);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => RescheduleAsync(repository, original.Id, SessionStart.AddDays(1).AddHours(1), SessionStart.AddDays(1).AddHours(3)));

        exception.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
        var stored = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Slot.Start.ShouldBe(SessionStart);
        stored.Version.ShouldBe(1);
        var intoOldSlot = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var stillReserved = await Should.ThrowAsync<ConflictException>(
            () => repository.AddAsync(intoOldSlot, TestContext.Current.CancellationToken));
        stillReserved.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
    }

    /// <summary>
    /// A booking can move to a slot that overlaps its own previous slot: it does not conflict with itself.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateReservingSlotAsync_WhenShiftingInsideItsOwnSlot_Succeeds()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var original = NewConfirmedBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(original, TestContext.Current.CancellationToken);

        var moved = await RescheduleAsync(repository, original.Id, SessionStart.AddHours(1), SessionStart.AddHours(3));

        moved.Slot.Start.ShouldBe(SessionStart.AddHours(1));
        var before = NewBooking(photographerId, SessionStart.AddHours(-1), SessionStart.AddHours(1));
        await repository.AddAsync(before, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A stale copy cannot reschedule, and its target slot is not left reserved by the failed attempt.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateReservingSlotAsync_WithStaleVersion_ThrowsConcurrencyConflictAndReservesNothing()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var original = NewConfirmedBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(original, TestContext.Current.CancellationToken);
        var staleCopy = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        staleCopy.ShouldNotBeNull();
        await RescheduleAsync(repository, original.Id, SessionStart.AddDays(1), SessionStart.AddDays(1).AddHours(2));
        staleCopy.Reschedule(TimeSlot.Create(SessionStart.AddDays(5), SessionStart.AddDays(5).AddHours(2)), Actor.Photographer, Now);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => repository.UpdateReservingSlotAsync(staleCopy, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.ConcurrencyConflict);
        var intoStaleTarget = NewBooking(photographerId, SessionStart.AddDays(5), SessionStart.AddDays(5).AddHours(2));
        await repository.AddAsync(intoStaleTarget, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A booking whose calendar range is missing (created before the calendar existed) still reschedules, restoring the range.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateReservingSlotAsync_WhenTheCalendarRangeIsMissing_ReservesTheNewSlot()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var original = NewConfirmedBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(original, TestContext.Current.CancellationToken);
        await fixture.Database
            .GetCollection<PhotographerCalendarDocument>(MongoBookingRepository.CalendarCollectionName)
            .UpdateOneAsync(
                calendar => calendar.Id == photographerId,
                Builders<PhotographerCalendarDocument>.Update.Set(calendar => calendar.Entries, []),
                cancellationToken: TestContext.Current.CancellationToken);

        await RescheduleAsync(repository, original.Id, SessionStart.AddDays(1), SessionStart.AddDays(1).AddHours(2));

        var intoNewSlot = NewBooking(photographerId, SessionStart.AddDays(1), SessionStart.AddDays(1).AddHours(2));
        var exception = await Should.ThrowAsync<ConflictException>(
            () => repository.AddAsync(intoNewSlot, TestContext.Current.CancellationToken));
        exception.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
    }

    /// <summary>
    /// A reschedule racing many new bookings for the same slot: exactly one request wins and the stored data is consistent.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateReservingSlotAsync_RacingNewBookingsForTheSameSlot_LetsExactlyOneWin()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        const int competitors = 10;
        var photographerId = Guid.CreateVersion7();
        var original = NewConfirmedBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        await NewRepository().AddAsync(original, TestContext.Current.CancellationToken);
        var contested = TimeSlot.Create(SessionStart.AddDays(2), SessionStart.AddDays(2).AddHours(2));
        var newcomers = Enumerable.Range(0, competitors)
            .Select(_ => NewBooking(photographerId, contested.Start, contested.End))
            .ToList();

        var reschedule = TryRescheduleAsync(original.Id, contested);
        var additions = newcomers.Select(booking => TryAddAsync(booking)).ToList();
        var outcomes = await Task.WhenAll(additions.Prepend(reschedule));

        outcomes.Count(succeeded => succeeded).ShouldBe(1);
        var holders = 0;
        foreach (var booking in newcomers.Prepend(original))
        {
            var stored = await NewRepository().GetByIdAsync(booking.Id, TestContext.Current.CancellationToken);
            if (stored is not null && stored.Slot.Overlaps(contested))
            {
                holders++;
            }
        }

        holders.ShouldBe(1);
    }

    /// <summary>
    /// <c>UpdateAsync</c> refuses to save a moved active booking, because it would leave the calendar stale.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateAsync_WhenAnActiveBookingChangedItsSlot_Throws()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var original = NewConfirmedBooking(Guid.CreateVersion7(), SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(original, TestContext.Current.CancellationToken);
        var loaded = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.Reschedule(TimeSlot.Create(SessionStart.AddDays(1), SessionStart.AddDays(1).AddHours(2)), Actor.Photographer, Now);

        await Should.ThrowAsync<InvalidOperationException>(
            () => repository.UpdateAsync(loaded, TestContext.Current.CancellationToken));

        var stored = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Slot.Start.ShouldBe(SessionStart);
    }

    /// <summary>
    /// A booking that no longer holds a slot cannot be saved through the slot-reserving operation.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpdateReservingSlotAsync_WithAnInactiveBooking_Throws()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var booking = NewBooking(Guid.CreateVersion7(), SessionStart, SessionStart.AddHours(2));
        booking.Cancel(Actor.Photographer, null, Now.AddDays(1));

        await Should.ThrowAsync<ArgumentException>(
            () => NewRepository().UpdateReservingSlotAsync(booking, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Reverting a client-absent mark takes the slot back when nobody else took it meanwhile.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RevertingClientAbsent_WhenTheSlotIsStillFree_ConfirmsAgainAndReservesTheSlot()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var original = NewConfirmedBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(original, TestContext.Current.CancellationToken);
        var markedAt = await MarkClientAbsentAsync(repository, original.Id);

        var absent = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        absent.ShouldNotBeNull();
        absent.RevertClientAbsent("Marcado por error", markedAt.AddHours(1));
        await repository.UpdateReservingSlotAsync(absent, TestContext.Current.CancellationToken);

        var stored = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(BookingStatus.Confirmed);
        var intruder = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var exception = await Should.ThrowAsync<ConflictException>(
            () => repository.AddAsync(intruder, TestContext.Current.CancellationToken));
        exception.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
    }

    /// <summary>
    /// Marking the client absent frees the slot; if another booking takes it, the reversal is rejected and the mark stays.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RevertingClientAbsent_WhenAnotherBookingTookTheSlot_IsRejectedAndKeepsTheMark()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var photographerId = Guid.CreateVersion7();
        var original = NewConfirmedBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        var repository = NewRepository();
        await repository.AddAsync(original, TestContext.Current.CancellationToken);
        var markedAt = await MarkClientAbsentAsync(repository, original.Id);
        var replacement = NewBooking(photographerId, SessionStart, SessionStart.AddHours(2));
        await repository.AddAsync(replacement, TestContext.Current.CancellationToken);

        var absent = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        absent.ShouldNotBeNull();
        absent.RevertClientAbsent("Marcado por error", markedAt.AddHours(1));
        var exception = await Should.ThrowAsync<ConflictException>(
            () => repository.UpdateReservingSlotAsync(absent, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.SlotUnavailable);
        var stored = await repository.GetByIdAsync(original.Id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(BookingStatus.ClientAbsent);
        stored.Version.ShouldBe(2);
    }

    /// <summary>
    /// Loads a confirmed booking, marks its client absent once the tolerance has passed and saves it, which frees its slot.
    /// </summary>
    /// <param name="repository">Repository to use.</param>
    /// <param name="bookingId">Booking to mark.</param>
    /// <returns>The instant at which the client was marked absent.</returns>
    private static async Task<DateTimeOffset> MarkClientAbsentAsync(MongoBookingRepository repository, Guid bookingId)
    {
        var markedAt = SessionStart.AddMinutes(BookingPolicy.Default.ClientAbsentToleranceMinutes);
        var booking = await repository.GetByIdAsync(bookingId, TestContext.Current.CancellationToken);
        booking.ShouldNotBeNull();
        booking.MarkClientAbsent(markedAt);
        await repository.UpdateAsync(booking, TestContext.Current.CancellationToken);
        return markedAt;
    }

    /// <summary>
    /// Loads a booking, moves it to the given slot as the photographer and saves it reserving the slot.
    /// </summary>
    /// <param name="repository">Repository to use.</param>
    /// <param name="bookingId">Booking to move.</param>
    /// <param name="start">New session start.</param>
    /// <param name="end">New session end.</param>
    /// <returns>The booking as saved.</returns>
    private static async Task<Booking> RescheduleAsync(MongoBookingRepository repository, Guid bookingId, DateTimeOffset start, DateTimeOffset end)
    {
        var booking = await repository.GetByIdAsync(bookingId, TestContext.Current.CancellationToken);
        booking.ShouldNotBeNull();
        booking.Reschedule(TimeSlot.Create(start, end), Actor.Photographer, Now);
        await repository.UpdateReservingSlotAsync(booking, TestContext.Current.CancellationToken);
        return booking;
    }

    /// <summary>
    /// Tries to move a booking with its own repository instance, like a concurrent HTTP request would.
    /// </summary>
    /// <param name="bookingId">Booking to move.</param>
    /// <param name="slot">Target slot.</param>
    /// <returns><see langword="true"/> when moved; <see langword="false"/> when the slot was taken.</returns>
    private async Task<bool> TryRescheduleAsync(Guid bookingId, TimeSlot slot)
    {
        try
        {
            await Task.Yield();
            await RescheduleAsync(NewRepository(), bookingId, slot.Start, slot.End);
            return true;
        }
        catch (ConflictException exception) when (exception.Code == ApplicationErrorCodes.SlotUnavailable)
        {
            return false;
        }
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
    /// Creates a confirmed booking for the slot: contract signed and the deposit paid in person.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="start">Session start.</param>
    /// <param name="end">Session end.</param>
    /// <returns>The new booking.</returns>
    private static Booking NewConfirmedBooking(Guid photographerId, DateTimeOffset start, DateTimeOffset end)
    {
        var booking = NewBooking(photographerId, start, end);
        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);
        booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(50_000m, "CRC"), PaymentMethod.Cash, "deposit-1", Now);
        return booking;
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
}
