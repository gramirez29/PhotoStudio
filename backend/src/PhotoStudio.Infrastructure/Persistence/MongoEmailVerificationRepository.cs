using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IEmailVerificationRepository"/>, backed by the <c>email_verifications</c> collection. The
/// document id is the user, so a new code replaces the previous one, and a guess is counted with one atomic increment.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoEmailVerificationRepository(IMongoDatabase database) : IEmailVerificationRepository
{
    /// <summary>
    /// Name of the collection that holds the codes.
    /// </summary>
    public const string CollectionName = "email_verifications";

    private readonly IMongoCollection<EmailVerificationDocument> _collection = database.GetCollection<EmailVerificationDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<EmailVerification?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(verification => verification.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    /// <inheritdoc />
    public Task SaveAsync(EmailVerification verification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verification);
        return _collection.ReplaceOneAsync(
            stored => stored.UserId == verification.UserId,
            verification.ToDocument(),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EmailVerification?> RegisterAttemptAsync(Guid userId, CancellationToken cancellationToken)
    {
        var updated = await _collection.FindOneAndUpdateAsync(
            verification => verification.UserId == userId,
            Builders<EmailVerificationDocument>.Update.Inc(verification => verification.FailedAttempts, 1),
            new FindOneAndUpdateOptions<EmailVerificationDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updated?.ToDomain();
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId, CancellationToken cancellationToken) =>
        _collection.DeleteOneAsync(verification => verification.UserId == userId, cancellationToken);
}
