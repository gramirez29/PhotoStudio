using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IBookingRepository"/>. Each booking is a single document, and concurrent writes to
/// the same booking are detected through the version field. Double booking is prevented by the photographer's calendar
/// document: reserving a slot and storing the booking happen in one transaction (the replica set of Atlas and of the
/// local compose file supports it), so a booking never exists without its reservation or the other way around.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoBookingRepository(IMongoDatabase database) : IBookingRepository
{
    /// <summary>
    /// Name of the bookings collection.
    /// </summary>
    public const string CollectionName = "bookings";

    /// <summary>
    /// Name of the photographer calendars collection.
    /// </summary>
    public const string CalendarCollectionName = "photographer_calendars";

    private static readonly TransactionOptions MajorityTransaction = new(writeConcern: WriteConcern.WMajority);

    private readonly IMongoCollection<BookingDocument> _collection = database.GetCollection<BookingDocument>(CollectionName);

    private readonly IMongoCollection<PhotographerCalendarDocument> _calendars =
        database.GetCollection<PhotographerCalendarDocument>(CalendarCollectionName);

    /// <inheritdoc />
    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(booking => booking.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Booking>> ListByPhotographerAsync(
        Guid photographerId,
        DateTimeOffset endingAfter,
        int limit,
        CancellationToken cancellationToken)
    {
        // Served by the (photographerId, slotStart) index created at startup.
        var documents = await _collection
            .Find(booking => booking.PhotographerId == photographerId && booking.SlotEnd >= endingAfter.UtcDateTime)
            .SortBy(booking => booking.SlotStart)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return [.. documents.Select(document => document.ToDomain())];
    }

    /// <inheritdoc />
    public Task<bool> HasOverlappingActiveBookingAsync(Guid photographerId, TimeSlot slot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slot);

        string[] activeStatuses = [nameof(BookingStatus.Tentative), nameof(BookingStatus.Confirmed)];
        var filterBuilder = Builders<BookingDocument>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.Eq(booking => booking.PhotographerId, photographerId),
            filterBuilder.In(booking => booking.Status, activeStatuses),
            filterBuilder.Lt(booking => booking.SlotStart, slot.End.UtcDateTime),
            filterBuilder.Gt(booking => booking.SlotEnd, slot.Start.UtcDateTime));

        return _collection.Find(filter).AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Booking booking, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var document = booking.ToDocument(booking.Version + 1);
        await EnsureCalendarExistsAsync(booking.PhotographerId, cancellationToken);

        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        await session.WithTransactionAsync(
            async (transactionSession, token) =>
            {
                // Ranges that ended before this booking was created can no longer collide with anything.
                await _calendars.UpdateOneAsync(
                    transactionSession,
                    Builders<PhotographerCalendarDocument>.Filter.Eq(calendar => calendar.Id, booking.PhotographerId),
                    Builders<PhotographerCalendarDocument>.Update.PullFilter(
                        calendar => calendar.Entries,
                        Builders<CalendarEntryDocument>.Filter.Lte(entry => entry.End, booking.CreatedAt.UtcDateTime)),
                    cancellationToken: token);

                await ReserveSlotAsync(transactionSession, document, token);
                await _collection.InsertOneAsync(transactionSession, document, cancellationToken: token);
                return true;
            },
            MajorityTransaction,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateReservingSlotAsync(Booking booking, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);

        if (!IsActive(booking.Status))
        {
            throw new ArgumentException("Only tentative or confirmed bookings hold a slot.", nameof(booking));
        }

        var expectedVersion = booking.Version;
        var document = booking.ToDocument(expectedVersion + 1);
        await EnsureCalendarExistsAsync(booking.PhotographerId, cancellationToken);

        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        await session.WithTransactionAsync(
            async (transactionSession, token) =>
            {
                var result = await _collection.ReplaceOneAsync(
                    transactionSession,
                    stored => stored.Id == booking.Id && stored.Version == expectedVersion,
                    document,
                    cancellationToken: token);

                EnsureMatched(result, booking.Id);

                // Drop the range the booking held (if any), then reserve the new one against every other booking. If the
                // new range is taken the exception aborts the transaction and the old range stays reserved.
                await ReleaseSlotAsync(transactionSession, booking.PhotographerId, booking.Id, token);
                await ReserveSlotAsync(transactionSession, document, token);
                return true;
            },
            MajorityTransaction,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Booking booking, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var expectedVersion = booking.Version;
        var document = booking.ToDocument(expectedVersion + 1);

        // Booking still holds its slot: only the booking document changes. The stored slot must equal the new one, so a
        // caller that moved the booking and used this method by mistake fails loudly instead of leaving the calendar stale.
        if (IsActive(booking.Status))
        {
            var result = await _collection.ReplaceOneAsync(
                stored => stored.Id == booking.Id
                    && stored.Version == expectedVersion
                    && stored.SlotStart == document.SlotStart
                    && stored.SlotEnd == document.SlotEnd,
                document,
                cancellationToken: cancellationToken);

            if (result.MatchedCount == 0)
            {
                await ThrowForUnmatchedActiveUpdateAsync(booking.Id, expectedVersion, cancellationToken);
            }

            return;
        }

        // Booking left the active states (cancelled, expired, completed, client absent): free its slot in the same transaction.
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        await session.WithTransactionAsync(
            async (transactionSession, token) =>
            {
                var result = await _collection.ReplaceOneAsync(
                    transactionSession,
                    stored => stored.Id == booking.Id && stored.Version == expectedVersion,
                    document,
                    cancellationToken: token);

                EnsureMatched(result, booking.Id);

                await ReleaseSlotAsync(transactionSession, booking.PhotographerId, booking.Id, token);
                return true;
            },
            MajorityTransaction,
            cancellationToken);
    }

    /// <summary>
    /// Reserves the booking's slot in the photographer's calendar, inside the given transaction. The reservation is one
    /// conditional update: it matches only while no stored range overlaps the new one. Concurrent transactions write the
    /// same calendar document, so the server aborts the loser with a transient error that the driver retries; the retry
    /// then sees the winner's range and fails the filter.
    /// </summary>
    /// <param name="session">Session of the running transaction.</param>
    /// <param name="document">Booking document that carries the photographer, the identifier and the slot.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the slot is reserved.</returns>
    /// <exception cref="ConflictException">When another range overlaps the slot.</exception>
    private async Task ReserveSlotAsync(IClientSessionHandle session, BookingDocument document, CancellationToken cancellationToken)
    {
        var reservation = await _calendars.UpdateOneAsync(
            session,
            OverlapFreeFilter(document.PhotographerId, document.SlotStart, document.SlotEnd),
            Builders<PhotographerCalendarDocument>.Update.Push(
                calendar => calendar.Entries,
                new CalendarEntryDocument { BookingId = document.Id, Start = document.SlotStart, End = document.SlotEnd }),
            cancellationToken: cancellationToken);

        if (reservation.MatchedCount == 0)
        {
            throw new ConflictException(
                ApplicationErrorCodes.SlotUnavailable,
                "The photographer already has a booking in that slot.");
        }
    }

    /// <summary>
    /// Removes the range a booking holds from the photographer's calendar, inside the given transaction. Does nothing
    /// when the booking holds no range.
    /// </summary>
    /// <param name="session">Session of the running transaction.</param>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="bookingId">Booking whose range is removed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the range is removed.</returns>
    private Task ReleaseSlotAsync(IClientSessionHandle session, Guid photographerId, Guid bookingId, CancellationToken cancellationToken) =>
        _calendars.UpdateOneAsync(
            session,
            Builders<PhotographerCalendarDocument>.Filter.Eq(calendar => calendar.Id, photographerId),
            Builders<PhotographerCalendarDocument>.Update.PullFilter(
                calendar => calendar.Entries,
                Builders<CalendarEntryDocument>.Filter.Eq(entry => entry.BookingId, bookingId)),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Explains why the update of an active booking matched nothing: a newer version means a concurrency conflict, while
    /// the expected version with another slot means the caller used the wrong method to move the booking.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="expectedVersion">Version the caller loaded.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that always ends with an exception.</returns>
    /// <exception cref="ConflictException">When the stored booking is not at the expected version.</exception>
    /// <exception cref="InvalidOperationException">When only the slot differs.</exception>
    private async Task ThrowForUnmatchedActiveUpdateAsync(Guid bookingId, long expectedVersion, CancellationToken cancellationToken)
    {
        var versionMatches = await _collection
            .Find(stored => stored.Id == bookingId && stored.Version == expectedVersion)
            .AnyAsync(cancellationToken);

        if (versionMatches)
        {
            throw new InvalidOperationException(
                $"Booking {bookingId} changed its slot; use {nameof(UpdateReservingSlotAsync)} to move an active booking.");
        }

        throw new ConflictException(
            ApplicationErrorCodes.ConcurrencyConflict,
            $"Booking {bookingId} was modified by another request. Reload it and try again.");
    }

    /// <summary>
    /// Creates the photographer's calendar document when it does not exist yet. Done outside the transaction so the
    /// transaction never has to insert (an insert race inside a transaction aborts it instead of being retried).
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the calendar document exists.</returns>
    private async Task EnsureCalendarExistsAsync(Guid photographerId, CancellationToken cancellationToken)
    {
        try
        {
            await _calendars.UpdateOneAsync(
                Builders<PhotographerCalendarDocument>.Filter.Eq(calendar => calendar.Id, photographerId),
                Builders<PhotographerCalendarDocument>.Update.SetOnInsert(calendar => calendar.Entries, []),
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // A concurrent request created the document between our lookup and our insert: that is the desired state.
        }
    }

    /// <summary>
    /// Builds the filter that matches the calendar only while no reserved range overlaps [start, end).
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="start">Inclusive start of the new range (UTC).</param>
    /// <param name="end">Exclusive end of the new range (UTC).</param>
    /// <returns>The conditional filter.</returns>
    private static FilterDefinition<PhotographerCalendarDocument> OverlapFreeFilter(Guid photographerId, DateTime start, DateTime end)
    {
        var calendarFilter = Builders<PhotographerCalendarDocument>.Filter;
        var entryFilter = Builders<CalendarEntryDocument>.Filter;

        return calendarFilter.And(
            calendarFilter.Eq(calendar => calendar.Id, photographerId),
            calendarFilter.Not(calendarFilter.ElemMatch(
                calendar => calendar.Entries,
                entryFilter.And(
                    entryFilter.Lt(entry => entry.Start, end),
                    entryFilter.Gt(entry => entry.End, start)))));
    }

    /// <summary>
    /// Indicates whether the status keeps the slot reserved in the photographer's calendar.
    /// </summary>
    /// <param name="status">Booking status.</param>
    /// <returns><see langword="true"/> for tentative and confirmed bookings.</returns>
    private static bool IsActive(BookingStatus status) => status is BookingStatus.Tentative or BookingStatus.Confirmed;

    /// <summary>
    /// Fails with a concurrency conflict when the optimistic-concurrency filter matched no booking.
    /// </summary>
    /// <param name="result">Result of the replace operation.</param>
    /// <param name="bookingId">Booking identifier, for the message.</param>
    /// <exception cref="ConflictException">When another write changed the booking first.</exception>
    private static void EnsureMatched(ReplaceOneResult result, Guid bookingId)
    {
        if (result.MatchedCount == 0)
        {
            throw new ConflictException(
                ApplicationErrorCodes.ConcurrencyConflict,
                $"Booking {bookingId} was modified by another request. Reload it and try again.");
        }
    }
}
