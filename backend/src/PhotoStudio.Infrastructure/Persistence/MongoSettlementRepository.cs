using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Billing;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="ISettlementRepository"/> over the <c>settlements</c> collection. There is one
/// document per booking, enforced by a unique index on the booking and not by looking first, so two events that settle the
/// same booking at the same time cannot create two settlements.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoSettlementRepository(IMongoDatabase database) : ISettlementRepository
{
    /// <summary>
    /// Name of the settlements collection.
    /// </summary>
    public const string CollectionName = "settlements";

    private readonly IMongoCollection<SettlementDocument> _collection = database.GetCollection<SettlementDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<BookingSettlement?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(settlement => settlement.BookingId == bookingId).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<bool> TryAddAsync(BookingSettlement settlement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        try
        {
            await _collection.InsertOneAsync(settlement.ToDocument(settlement.Version + 1), cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(BookingSettlement settlement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        var expectedVersion = settlement.Version;
        var result = await _collection.ReplaceOneAsync(
            stored => stored.Id == settlement.Id && stored.Version == expectedVersion,
            settlement.ToDocument(expectedVersion + 1),
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            throw new ConflictException(
                ApplicationErrorCodes.ConcurrencyConflict,
                $"Settlement {settlement.Id} was modified by another request. Reload it and try again.");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookingSettlement>> ListPendingRefundsAsync(Guid photographerId, int limit, CancellationToken cancellationToken)
    {
        // Served by the (photographerId, refundStatus, createdAt) index.
        var documents = await _collection
            .Find(settlement => settlement.PhotographerId == photographerId && settlement.RefundStatus == nameof(RefundStatus.Pending))
            .SortBy(settlement => settlement.CreatedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return [.. documents.Select(document => document.ToDomain())];
    }

    /// <inheritdoc />
    public async Task<int> CountPendingRefundsAsync(Guid photographerId, CancellationToken cancellationToken) =>
        (int)await _collection.CountDocumentsAsync(
            settlement => settlement.PhotographerId == photographerId && settlement.RefundStatus == nameof(RefundStatus.Pending),
            cancellationToken: cancellationToken);
}
