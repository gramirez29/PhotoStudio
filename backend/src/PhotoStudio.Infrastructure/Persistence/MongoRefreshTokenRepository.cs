using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IRefreshTokenRepository"/>. Rotation revokes the used token with a conditional
/// update and inserts its replacement in the same transaction, so a token can be used exactly once and a failed rotation
/// never leaves the user without a valid token.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoRefreshTokenRepository(IMongoDatabase database) : IRefreshTokenRepository
{
    /// <summary>
    /// Name of the refresh tokens collection.
    /// </summary>
    public const string CollectionName = "refresh_tokens";

    private static readonly TransactionOptions MajorityTransaction = new(writeConcern: WriteConcern.WMajority);

    private readonly IMongoCollection<RefreshTokenDocument> _collection =
        database.GetCollection<RefreshTokenDocument>(CollectionName);

    /// <inheritdoc />
    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        return _collection.InsertOneAsync(token.ToDocument(), cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(token => token.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<bool> RotateAsync(RefreshToken current, RefreshToken replacement, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(replacement);

        var replacementDocument = replacement.ToDocument();
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);

        return await session.WithTransactionAsync(
            async (transactionSession, token) =>
            {
                // Matches only while the token is unused: of two concurrent rotations, exactly one finds it unrevoked.
                var revoked = await _collection.UpdateOneAsync(
                    transactionSession,
                    stored => stored.Id == current.Id && stored.RevokedAt == null,
                    Builders<RefreshTokenDocument>.Update
                        .Set(stored => stored.RevokedAt, now.UtcDateTime)
                        .Set(stored => stored.ReplacedById, replacement.Id),
                    cancellationToken: token);

                if (revoked.ModifiedCount == 0)
                {
                    return false;
                }

                await _collection.InsertOneAsync(transactionSession, replacementDocument, cancellationToken: token);
                return true;
            },
            MajorityTransaction,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var result = await _collection.UpdateManyAsync(
            token => token.FamilyId == familyId && token.RevokedAt == null,
            Builders<RefreshTokenDocument>.Update.Set(token => token.RevokedAt, now.UtcDateTime),
            cancellationToken: cancellationToken);

        return (int)result.ModifiedCount;
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllAsync(Guid photographerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var result = await _collection.UpdateManyAsync(
            token => token.PhotographerId == photographerId && token.RevokedAt == null,
            Builders<RefreshTokenDocument>.Update.Set(token => token.RevokedAt, now.UtcDateTime),
            cancellationToken: cancellationToken);

        return (int)result.ModifiedCount;
    }
}
