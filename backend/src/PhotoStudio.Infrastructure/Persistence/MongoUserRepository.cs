using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IUserRepository"/> over the <c>users</c> collection. The unique index on the
/// username and on the email are the authority that prevents two users with the same name or address; the failed-login counter uses atomic operators so
/// parallel guesses cannot slip past the lockout.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoUserRepository(IMongoDatabase database) : IUserRepository
{
    /// <summary>
    /// Name of the users collection.
    /// </summary>
    public const string CollectionName = "users";

    /// <summary>
    /// Name of the unique index on the username.
    /// </summary>
    public const string UsernameIndexName = "ix_user_username";

    /// <summary>
    /// Name of the unique index on the email.
    /// </summary>
    public const string EmailIndexName = "ix_user_email";

    private readonly IMongoCollection<UserDocument> _collection = database.GetCollection<UserDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(user => user.Username == normalizedUsername)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(user => user.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    /// <inheritdoc />
    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            await _collection.InsertOneAsync(user.ToDocument(user.Version + 1), cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // The server names the index that was violated, which tells the two conflicts apart.
            throw exception.WriteError.Message.Contains(EmailIndexName, StringComparison.Ordinal)
                ? new ConflictException(ApplicationErrorCodes.EmailTaken, "That email is already registered.")
                : new ConflictException(ApplicationErrorCodes.UsernameTaken, "That username is already taken.");
        }
    }

    /// <inheritdoc />
    public async Task<int> RegisterFailedLoginAsync(Guid id, CancellationToken cancellationToken)
    {
        var updated = await _collection.FindOneAndUpdateAsync(
            user => user.Id == id,
            Builders<UserDocument>.Update.Inc(user => user.FailedLoginAttempts, 1),
            new FindOneAndUpdateOptions<UserDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updated?.FailedLoginAttempts ?? 0;
    }

    /// <inheritdoc />
    public Task LockAsync(Guid id, DateTimeOffset until, CancellationToken cancellationToken) =>
        _collection.UpdateOneAsync(
            user => user.Id == id,
            Builders<UserDocument>.Update
                .Set(user => user.LockedUntil, until.UtcDateTime)
                .Set(user => user.FailedLoginAttempts, 0),
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task ResetFailedLoginsAsync(Guid id, CancellationToken cancellationToken) =>
        _collection.UpdateOneAsync(
            user => user.Id == id,
            Builders<UserDocument>.Update
                .Set(user => user.FailedLoginAttempts, 0)
                .Unset(user => user.LockedUntil),
            cancellationToken: cancellationToken);
}
