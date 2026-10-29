using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// Mapping between the identity domain objects (users and refresh tokens) and their MongoDB documents.
/// </summary>
public static class IdentityDocumentMappings
{
    /// <summary>
    /// Maps a user to its document.
    /// </summary>
    /// <param name="user">User to map.</param>
    /// <param name="version">Version to store.</param>
    /// <returns>The document.</returns>
    public static UserDocument ToDocument(this User user, long version)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new UserDocument
        {
            Id = user.Id,
            Version = version,
            Username = user.Username,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Name = user.Name,
            Phone = user.Phone,
            CreatedAt = user.CreatedAt.UtcDateTime,
            FailedLoginAttempts = user.FailedLoginAttempts,
            LockedUntil = user.LockedUntil?.UtcDateTime,
            IsSystemOperator = user.IsSystemOperator,
            EmailVerifiedAt = user.EmailVerifiedAt?.UtcDateTime,
        };
    }

    /// <summary>
    /// Maps a user document to the domain user.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The user.</returns>
    public static User ToDomain(this UserDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return User.Restore(
            document.Id,
            document.Version,
            document.Username,
            document.Email,
            document.PasswordHash,
            document.Name,
            document.Phone,
            ToOffset(document.CreatedAt),
            document.FailedLoginAttempts,
            document.LockedUntil is null ? null : ToOffset(document.LockedUntil.Value),
            document.IsSystemOperator,
            document.EmailVerifiedAt is null ? null : ToOffset(document.EmailVerifiedAt.Value));
    }

    /// <summary>
    /// Maps the code that verifies an email to its document.
    /// </summary>
    /// <param name="verification">Record to map.</param>
    /// <returns>The document.</returns>
    public static EmailVerificationDocument ToDocument(this EmailVerification verification)
    {
        ArgumentNullException.ThrowIfNull(verification);
        return new EmailVerificationDocument
        {
            UserId = verification.UserId,
            CodeHash = verification.CodeHash,
            ExpiresAt = verification.ExpiresAt.UtcDateTime,
            SentAt = verification.SentAt.UtcDateTime,
            FailedAttempts = verification.FailedAttempts,
            SendCount = verification.SendCount,
            WindowStartedAt = verification.WindowStartedAt.UtcDateTime,
        };
    }

    /// <summary>
    /// Maps a code document to the domain record.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The record.</returns>
    public static EmailVerification ToDomain(this EmailVerificationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new EmailVerification(
            document.UserId,
            document.CodeHash,
            ToOffset(document.ExpiresAt),
            ToOffset(document.SentAt),
            document.FailedAttempts,
            document.SendCount,
            ToOffset(document.WindowStartedAt));
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
