using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Portal;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IBookingLinkRepository"/> over the <c>booking_links</c> collection. Replacing a link
/// revokes the old ones and inserts the new one in a single transaction, so a booking never has two working links.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoBookingLinkRepository(IMongoDatabase database) : IBookingLinkRepository
{
    /// <summary>
    /// Name of the links collection.
    /// </summary>
    public const string CollectionName = "booking_links";

    private static readonly TransactionOptions MajorityTransaction = new(writeConcern: WriteConcern.WMajority);

    private readonly IMongoCollection<BookingLinkDocument> _collection = database.GetCollection<BookingLinkDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<BookingLink?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(link => link.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task ReplaceActiveAsync(BookingLink link, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        var document = link.ToDocument();
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);

        await session.WithTransactionAsync(
            async (transactionSession, token) =>
            {
                await _collection.UpdateManyAsync(
                    transactionSession,
                    stored => stored.BookingId == link.BookingId && stored.RevokedAt == null,
                    Builders<BookingLinkDocument>.Update.Set(stored => stored.RevokedAt, now.UtcDateTime),
                    cancellationToken: token);

                await _collection.InsertOneAsync(transactionSession, document, cancellationToken: token);
                return true;
            },
            MajorityTransaction,
            cancellationToken);
    }
}
