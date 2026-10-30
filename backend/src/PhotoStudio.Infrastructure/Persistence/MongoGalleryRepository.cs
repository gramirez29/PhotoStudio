using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Pagination;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IGalleryRepository"/> over the <c>galleries</c> collection. There is one document per
/// booking, enforced by a unique index on the booking and not by looking first, so the event that creates the gallery can be
/// delivered twice, or by two workers at once, and still creates one.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoGalleryRepository(IMongoDatabase database) : IGalleryRepository
{
    /// <summary>
    /// Name of the galleries collection.
    /// </summary>
    public const string CollectionName = "galleries";

    private readonly IMongoCollection<GalleryDocument> _collection = database.GetCollection<GalleryDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<Gallery?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(gallery => gallery.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<Gallery?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(gallery => gallery.BookingId == bookingId).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<bool> TryAddAsync(Gallery gallery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gallery);

        try
        {
            await _collection.InsertOneAsync(gallery.ToDocument(gallery.Version + 1), cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Gallery gallery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gallery);

        var expectedVersion = gallery.Version;
        var result = await _collection.ReplaceOneAsync(
            stored => stored.Id == gallery.Id && stored.Version == expectedVersion,
            gallery.ToDocument(expectedVersion + 1),
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            throw new ConflictException(
                ApplicationErrorCodes.ConcurrencyConflict,
                $"Gallery {gallery.Id} was modified by another request. Reload it and try again.");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Gallery>> ListByPhotographerAsync(Guid photographerId, PageCursor? after, int limit, CancellationToken cancellationToken)
    {
        var filters = Builders<GalleryDocument>.Filter;
        var filter = filters.Eq(gallery => gallery.PhotographerId, photographerId);
        if (after is { } cursor)
        {
            // Keyset page: older than the cursor, or the same instant with a smaller identifier (the tie-break of the sort).
            var at = cursor.At.UtcDateTime;
            filter &= filters.Lt(gallery => gallery.CreatedAt, at)
                | (filters.Eq(gallery => gallery.CreatedAt, at) & filters.Lt(gallery => gallery.Id, cursor.Id));
        }

        // Served by the (photographerId, createdAt, _id) index.
        var documents = await _collection
            .Find(filter)
            .SortByDescending(gallery => gallery.CreatedAt)
            .ThenByDescending(gallery => gallery.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return [.. documents.Select(document => document.ToDomain())];
    }
}
