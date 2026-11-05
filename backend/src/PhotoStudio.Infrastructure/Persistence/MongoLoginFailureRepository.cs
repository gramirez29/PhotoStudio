using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="ILoginFailureRepository"/> over the <c>login_failures</c> collection. There is one document
/// per account and address, identified by both, and a failure is counted with one atomic upsert-and-increment, so guesses sent in
/// parallel are all counted. A record forgets itself a day after its last failure (retention index).
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoLoginFailureRepository(IMongoDatabase database) : ILoginFailureRepository
{
    /// <summary>
    /// Name of the collection that holds the failures.
    /// </summary>
    public const string CollectionName = "login_failures";

    /// <summary>
    /// How long a record is kept after its last failure or the end of its block.
    /// </summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(1);

    private readonly IMongoCollection<LoginFailureDocument> _collection = database.GetCollection<LoginFailureDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<LoginAddressFailure?> GetAsync(Guid userId, string addressKey, CancellationToken cancellationToken)
    {
        var id = LoginFailureDocument.IdOf(userId, addressKey);
        var document = await _collection.Find(failure => failure.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document is null
            ? null
            : new LoginAddressFailure(
                document.UserId,
                document.AddressKey,
                document.Attempts,
                document.BlockedUntil is { } until ? new DateTimeOffset(DateTime.SpecifyKind(until, DateTimeKind.Utc)) : null);
    }

    /// <inheritdoc />
    public async Task<int> RegisterFailureAsync(Guid userId, string addressKey, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var id = LoginFailureDocument.IdOf(userId, addressKey);
        var updated = await _collection.FindOneAndUpdateAsync(
            failure => failure.Id == id,
            Builders<LoginFailureDocument>.Update
                .Inc(failure => failure.Attempts, 1)
                .Set(failure => failure.ExpiresAt, (now + Retention).UtcDateTime)
                .SetOnInsert(failure => failure.UserId, userId)
                .SetOnInsert(failure => failure.AddressKey, addressKey),
            new FindOneAndUpdateOptions<LoginFailureDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updated?.Attempts ?? 1;
    }

    /// <inheritdoc />
    public Task BlockAsync(Guid userId, string addressKey, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var id = LoginFailureDocument.IdOf(userId, addressKey);
        return _collection.UpdateOneAsync(
            failure => failure.Id == id,
            Builders<LoginFailureDocument>.Update
                .Set(failure => failure.BlockedUntil, until.UtcDateTime)
                .Set(failure => failure.Attempts, 0)
                .Set(failure => failure.ExpiresAt, (until + Retention).UtcDateTime)
                .SetOnInsert(failure => failure.UserId, userId)
                .SetOnInsert(failure => failure.AddressKey, addressKey),
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId, string addressKey, CancellationToken cancellationToken)
    {
        var id = LoginFailureDocument.IdOf(userId, addressKey);
        return _collection.DeleteOneAsync(failure => failure.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        _collection.DeleteManyAsync(failure => failure.UserId == userId, cancellationToken);
}
