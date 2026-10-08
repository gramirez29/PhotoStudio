using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IPhotographerAccountRepository"/>. The unique index on the email is the authority
/// that prevents two accounts with the same email; the failed-login counter uses atomic operators so parallel guesses
/// cannot slip past the lockout.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoPhotographerAccountRepository(IMongoDatabase database) : IPhotographerAccountRepository
{
    /// <summary>
    /// Name of the accounts collection.
    /// </summary>
    public const string CollectionName = "photographer_accounts";

    private readonly IMongoCollection<PhotographerAccountDocument> _collection =
        database.GetCollection<PhotographerAccountDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<PhotographerAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(account => account.Email == normalizedEmail)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<PhotographerAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(account => account.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task AddAsync(PhotographerAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        try
        {
            await _collection.InsertOneAsync(account.ToDocument(account.Version + 1), cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new ConflictException(ApplicationErrorCodes.AccountAlreadyExists, "An account with that email or identifier already exists.");
        }
    }

    /// <inheritdoc />
    public async Task<int> RegisterFailedLoginAsync(Guid id, CancellationToken cancellationToken)
    {
        var updated = await _collection.FindOneAndUpdateAsync(
            account => account.Id == id,
            Builders<PhotographerAccountDocument>.Update.Inc(account => account.FailedLoginAttempts, 1),
            new FindOneAndUpdateOptions<PhotographerAccountDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updated?.FailedLoginAttempts ?? 0;
    }

    /// <inheritdoc />
    public Task LockAsync(Guid id, DateTimeOffset until, CancellationToken cancellationToken) =>
        _collection.UpdateOneAsync(
            account => account.Id == id,
            Builders<PhotographerAccountDocument>.Update
                .Set(account => account.LockedUntil, until.UtcDateTime)
                .Set(account => account.FailedLoginAttempts, 0),
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task ResetFailedLoginsAsync(Guid id, CancellationToken cancellationToken) =>
        _collection.UpdateOneAsync(
            account => account.Id == id,
            Builders<PhotographerAccountDocument>.Update
                .Set(account => account.FailedLoginAttempts, 0)
                .Unset(account => account.LockedUntil),
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task SetPasswordHashAsync(Guid id, string passwordHash, CancellationToken cancellationToken) =>
        _collection.UpdateOneAsync(
            account => account.Id == id,
            Builders<PhotographerAccountDocument>.Update
                .Set(account => account.PasswordHash, passwordHash)
                .Set(account => account.FailedLoginAttempts, 0)
                .Unset(account => account.LockedUntil)
                .Inc(account => account.Version, 1),
            cancellationToken: cancellationToken);
}
