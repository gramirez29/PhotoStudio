using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Identity;

/// <summary>
/// A refresh token that lets the app obtain a new access token without asking for the password again. Only the hash of
/// the token is stored, so a database leak does not yield usable tokens. Tokens are rotated on every use: the used token
/// is revoked and replaced by a new one of the same <see cref="FamilyId"/>. Presenting an already revoked token means it
/// was stolen or replayed, so the whole family is revoked.
/// </summary>
public sealed class RefreshToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshToken"/> class. Use <see cref="Issue"/> or <see cref="Restore"/>.
    /// </summary>
    private RefreshToken(
        Guid id,
        Guid photographerId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt,
        Guid? replacedById)
    {
        Id = id;
        PhotographerId = photographerId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
        ReplacedById = replacedById;
    }

    /// <summary>
    /// Gets the token identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the photographer the token belongs to.
    /// </summary>
    public Guid PhotographerId { get; }

    /// <summary>
    /// Gets the identifier shared by every token descended from the same login.
    /// </summary>
    public Guid FamilyId { get; }

    /// <summary>
    /// Gets the hash of the secret handed to the app.
    /// </summary>
    public string TokenHash { get; }

    /// <summary>
    /// Gets the instant the token was issued (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the instant the token stops being valid (UTC).
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets the instant the token was revoked (UTC), or <see langword="null"/> while it is unused.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; }

    /// <summary>
    /// Gets the token that replaced this one when it was rotated, if any.
    /// </summary>
    public Guid? ReplacedById { get; }

    /// <summary>
    /// Gets a value indicating whether the token was already used or revoked.
    /// </summary>
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>
    /// Issues a new token.
    /// </summary>
    /// <param name="photographerId">Photographer the token belongs to.</param>
    /// <param name="familyId">Family of the login the token belongs to.</param>
    /// <param name="tokenHash">Hash of the secret.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="lifetime">How long the token stays valid.</param>
    /// <returns>The token.</returns>
    /// <exception cref="DomainException">When a value is missing or the lifetime is not positive.</exception>
    public static RefreshToken Issue(Guid photographerId, Guid familyId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (photographerId == Guid.Empty || familyId == Guid.Empty || string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The photographer, the family and the token hash are required.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The lifetime of a refresh token must be positive.");
        }

        return new RefreshToken(Guid.CreateVersion7(), photographerId, familyId, tokenHash, now, now + lifetime, null, null);
    }

    /// <summary>
    /// Rebuilds a token from persisted data, without validating it again.
    /// </summary>
    /// <param name="id">Token identifier.</param>
    /// <param name="photographerId">Photographer identifier.</param>
    /// <param name="familyId">Family identifier.</param>
    /// <param name="tokenHash">Stored hash.</param>
    /// <param name="createdAt">Issue instant.</param>
    /// <param name="expiresAt">Expiry instant.</param>
    /// <param name="revokedAt">Revocation instant, if revoked.</param>
    /// <param name="replacedById">Replacement token, if rotated.</param>
    /// <returns>The restored token.</returns>
    public static RefreshToken Restore(
        Guid id,
        Guid photographerId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt,
        Guid? replacedById) =>
        new(id, photographerId, familyId, tokenHash, createdAt, expiresAt, revokedAt, replacedById);

    /// <summary>
    /// Indicates whether the token is past its expiry at the given instant.
    /// </summary>
    /// <param name="now">Instant to evaluate.</param>
    /// <returns><see langword="true"/> when the token expired.</returns>
    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;
}
