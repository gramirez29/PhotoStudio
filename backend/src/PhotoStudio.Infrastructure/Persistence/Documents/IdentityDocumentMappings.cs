using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// Mapping between the identity domain objects and their MongoDB documents.
/// </summary>
public static class IdentityDocumentMappings
{
    /// <summary>
    /// Maps an account to its document.
    /// </summary>
    /// <param name="account">Account to map.</param>
    /// <param name="version">Version to store.</param>
    /// <returns>The document.</returns>
    public static PhotographerAccountDocument ToDocument(this PhotographerAccount account, long version)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new PhotographerAccountDocument
        {
            Id = account.Id,
            Version = version,
            Email = account.Email,
            PasswordHash = account.PasswordHash,
            CreatedAt = account.CreatedAt.UtcDateTime,
            FailedLoginAttempts = account.FailedLoginAttempts,
            LockedUntil = account.LockedUntil?.UtcDateTime,
        };
    }

    /// <summary>
    /// Maps an account document to the domain account.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The account.</returns>
    public static PhotographerAccount ToDomain(this PhotographerAccountDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return PhotographerAccount.Restore(
            document.Id,
            document.Version,
            document.Email,
            document.PasswordHash,
            ToOffset(document.CreatedAt),
            document.FailedLoginAttempts,
            document.LockedUntil is null ? null : ToOffset(document.LockedUntil.Value));
    }

    /// <summary>
    /// Maps a refresh token to its document.
    /// </summary>
    /// <param name="token">Token to map.</param>
    /// <returns>The document.</returns>
    public static RefreshTokenDocument ToDocument(this RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return new RefreshTokenDocument
        {
            Id = token.Id,
            PhotographerId = token.PhotographerId,
            FamilyId = token.FamilyId,
            TokenHash = token.TokenHash,
            CreatedAt = token.CreatedAt.UtcDateTime,
            ExpiresAt = token.ExpiresAt.UtcDateTime,
            RevokedAt = token.RevokedAt?.UtcDateTime,
            ReplacedById = token.ReplacedById,
        };
    }

    /// <summary>
    /// Maps a refresh token document to the domain token.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The token.</returns>
    public static RefreshToken ToDomain(this RefreshTokenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return RefreshToken.Restore(
            document.Id,
            document.PhotographerId,
            document.FamilyId,
            document.TokenHash,
            ToOffset(document.CreatedAt),
            ToOffset(document.ExpiresAt),
            document.RevokedAt is null ? null : ToOffset(document.RevokedAt.Value),
            document.ReplacedById);
    }

    /// <summary>
    /// Converts a stored date, which MongoDB returns as UTC, to a UTC offset instant.
    /// </summary>
    /// <param name="value">Stored date.</param>
    /// <returns>The same instant with a zero offset.</returns>
    private static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
