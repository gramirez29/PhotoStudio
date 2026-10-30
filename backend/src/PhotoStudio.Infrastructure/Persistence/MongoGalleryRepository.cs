using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Pagination;
using PhotoStudio.Domain.Galleries;
using PhotoStudio.Infrastructure.Persistence.Documents;
using PhotoStudio.Infrastructure.Persistence.Outbox;

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

    private static readonly TransactionOptions MajorityTransaction = new(writeConcern: WriteConcern.WMajority);

    private readonly IMongoCollection<GalleryDocument> _collection = database.GetCollection<GalleryDocument>(CollectionName);

    private readonly IMongoCollection<OutboxMessageDocument> _outbox = database.GetCollection<OutboxMessageDocument>(OutboxProcessor.CollectionName);

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
        var document = gallery.ToDocument(expectedVersion + 1);
        var messages = gallery.DomainEvents.Select(domainEvent => domainEvent.ToOutboxMessage()).ToList();

        if (messages.Count == 0)
        {
            var result = await _collection.ReplaceOneAsync(
                stored => stored.Id == gallery.Id && stored.Version == expectedVersion,
                document,
                cancellationToken: cancellationToken);
            EnsureMatched(result.MatchedCount, gallery.Id);
            return;
        }

        // The events go to the outbox in the same transaction as the change: either both are stored or neither is.
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        await session.WithTransactionAsync(
            async (transactionSession, token) =>
            {
                var result = await _collection.ReplaceOneAsync(
                    transactionSession,
                    stored => stored.Id == gallery.Id && stored.Version == expectedVersion,
                    document,
                    cancellationToken: token);
                EnsureMatched(result.MatchedCount, gallery.Id);
                await _outbox.InsertManyAsync(transactionSession, messages, cancellationToken: token);
                return true;
            },
            MajorityTransaction,
            cancellationToken);

        gallery.ClearDomainEvents();
    }

    /// <summary>
    /// Refuses a save that matched nothing: another request changed the gallery first.
    /// </summary>
    /// <param name="matchedCount">Documents the save matched.</param>
    /// <param name="galleryId">Gallery that was being saved.</param>
    /// <exception cref="ConflictException">When nothing matched.</exception>
    private static void EnsureMatched(long matchedCount, Guid galleryId)
    {
        if (matchedCount == 0)
        {
            throw new ConflictException(
                ApplicationErrorCodes.ConcurrencyConflict,
                $"Gallery {galleryId} was modified by another request. Reload it and try again.");
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
