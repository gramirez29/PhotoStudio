using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IPasswordResetRepository"/>, backed by the <c>password_resets</c> collection. The document id
/// is the user, so a new code replaces the previous one, and a guess is counted with one atomic increment.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoPasswordResetRepository(IMongoDatabase database) : IPasswordResetRepository
{
    /// <summary>
    /// Name of the collection that holds the codes.
    /// </summary>
    public const string CollectionName = "password_resets";

    private readonly IMongoCollection<PasswordResetDocument> _collection = database.GetCollection<PasswordResetDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<PasswordResetCode?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(code => code.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    /// <inheritdoc />
    public Task SaveAsync(PasswordResetCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _collection.ReplaceOneAsync(
            stored => stored.UserId == code.UserId,
            code.ToDocument(),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PasswordResetCode?> RegisterAttemptAsync(Guid userId, CancellationToken cancellationToken)
    {
        var updated = await _collection.FindOneAndUpdateAsync(
            code => code.UserId == userId,
            Builders<PasswordResetDocument>.Update.Inc(code => code.FailedAttempts, 1),
            new FindOneAndUpdateOptions<PasswordResetDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updated?.ToDomain();
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId, CancellationToken cancellationToken) =>
        _collection.DeleteOneAsync(code => code.UserId == userId, cancellationToken);
}
