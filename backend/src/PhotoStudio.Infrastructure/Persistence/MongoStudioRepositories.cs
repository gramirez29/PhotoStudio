using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IStudioSessionRepository"/> over the <c>studio_sessions</c> collection. Sessions are found
/// by the hash of their secret, which has a unique index.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoStudioSessionRepository(IMongoDatabase database) : IStudioSessionRepository
{
    /// <summary>
    /// Name of the sessions collection.
    /// </summary>
    public const string CollectionName = "studio_sessions";

    private readonly IMongoCollection<StudioSessionDocument> _collection = database.GetCollection<StudioSessionDocument>(CollectionName);

    /// <inheritdoc />
    public Task AddAsync(StudioSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return _collection.InsertOneAsync(session.ToDocument(), cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StudioSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(session => session.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    /// <inheritdoc />
    public Task TouchAsync(Guid id, DateTimeOffset lastSeenAt, CancellationToken cancellationToken) =>
        _collection.UpdateOneAsync(
            session => session.Id == id,
            Builders<StudioSessionDocument>.Update.Set(session => session.LastSeenAt, lastSeenAt.UtcDateTime),
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        _collection.DeleteOneAsync(session => session.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        _collection.DeleteManyAsync(session => session.UserId == userId, cancellationToken);
}

/// <summary>
/// MongoDB implementation of <see cref="IStudioDeviceRepository"/> over the <c>studio_devices</c> collection. Devices are found by
/// the hash of their secret, which has a unique index and changes at every use.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoStudioDeviceRepository(IMongoDatabase database) : IStudioDeviceRepository
{
    /// <summary>
    /// Name of the devices collection.
    /// </summary>
    public const string CollectionName = "studio_devices";

    private readonly IMongoCollection<StudioDeviceDocument> _collection = database.GetCollection<StudioDeviceDocument>(CollectionName);

    /// <inheritdoc />
    public Task AddAsync(StudioTrustedDevice device, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _collection.InsertOneAsync(device.ToDocument(), cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StudioTrustedDevice?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(device => device.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<bool> RotateAsync(Guid id, string oldTokenHash, string newTokenHash, DateTimeOffset lastUsedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        // The filter holds the old hash: of two requests that arrive with the same cookie, only one finds it.
        var result = await _collection.UpdateOneAsync(
            device => device.Id == id && device.TokenHash == oldTokenHash,
            Builders<StudioDeviceDocument>.Update
                .Set(device => device.TokenHash, newTokenHash)
                .Set(device => device.LastUsedAt, lastUsedAt.UtcDateTime)
                .Set(device => device.ExpiresAt, expiresAt.UtcDateTime),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        _collection.DeleteOneAsync(device => device.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        _collection.DeleteManyAsync(device => device.UserId == userId, cancellationToken);
}

/// <summary>
/// MongoDB implementation of <see cref="IStudioChallengeRepository"/> over the <c>studio_challenges</c> collection. The user is the
/// identifier, so there is one challenge per user and saving replaces it.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoStudioChallengeRepository(IMongoDatabase database) : IStudioChallengeRepository
{
    /// <summary>
    /// Name of the challenges collection.
    /// </summary>
    public const string CollectionName = "studio_challenges";

    private readonly IMongoCollection<StudioChallengeDocument> _collection = database.GetCollection<StudioChallengeDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<StudioLoginChallenge?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(challenge => challenge.UserId == userId).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<StudioLoginChallenge?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(challenge => challenge.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    /// <inheritdoc />
    public Task SaveAsync(StudioLoginChallenge challenge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return _collection.ReplaceOneAsync(
            stored => stored.UserId == challenge.UserId,
            challenge.ToDocument(),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StudioLoginChallenge?> RegisterAttemptAsync(string tokenHash, CancellationToken cancellationToken)
    {
        // One atomic increment before the code is compared: guesses made in parallel cannot get more than the allowed number.
        var updated = await _collection.FindOneAndUpdateAsync(
            challenge => challenge.TokenHash == tokenHash,
            Builders<StudioChallengeDocument>.Update.Inc(challenge => challenge.FailedAttempts, 1),
            new FindOneAndUpdateOptions<StudioChallengeDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return updated?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _collection.DeleteOneAsync(challenge => challenge.UserId == userId, cancellationToken);
        return result.DeletedCount == 1;
    }
}
