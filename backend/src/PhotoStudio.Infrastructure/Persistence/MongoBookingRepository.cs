using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IBookingRepository"/>. Each booking is a single document, so every
/// change is atomic without multi-document transactions; concurrent writes are detected through the version field.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoBookingRepository(IMongoDatabase database) : IBookingRepository
{
    /// <summary>
    /// Name of the bookings collection.
    /// </summary>
    public const string CollectionName = "bookings";

    private readonly IMongoCollection<BookingDocument> _collection = database.GetCollection<BookingDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(booking => booking.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
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
    public Task AddAsync(Booking booking, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);
        return _collection.InsertOneAsync(booking.ToDocument(booking.Version + 1), cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Booking booking, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var expectedVersion = booking.Version;
        var result = await _collection.ReplaceOneAsync(
            document => document.Id == booking.Id && document.Version == expectedVersion,
            booking.ToDocument(expectedVersion + 1),
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            throw new ConflictException(
                ApplicationErrorCodes.ConcurrencyConflict,
                $"Booking {booking.Id} was modified by another request. Reload it and try again.");
        }
    }
}
