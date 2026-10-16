using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Portal;

/// <summary>
/// The unique link a client uses to reach one booking in the portal. The link carries a secret token; only its hash is
/// stored, so a database leak does not yield usable links. A booking has at most one active link: creating a new one revokes
/// the previous one, which is how a link that was shared with the wrong person is cancelled.
/// </summary>
public sealed class BookingLink
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingLink"/> class. Use <see cref="Issue"/> or <see cref="Restore"/>.
    /// </summary>
    private BookingLink(
        Guid id,
        Guid bookingId,
        Guid photographerId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt)
    {
        Id = id;
        BookingId = bookingId;
        PhotographerId = photographerId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
    }

    /// <summary>
    /// Gets the link identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the booking the link opens.
    /// </summary>
    public Guid BookingId { get; }

    /// <summary>
    /// Gets the photographer (tenant) that owns the booking.
    /// </summary>
    public Guid PhotographerId { get; }

    /// <summary>
    /// Gets the hash of the secret token that is part of the URL.
    /// </summary>
    public string TokenHash { get; }

    /// <summary>
    /// Gets the instant the link was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the instant the link stops working (UTC).
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets the instant the link was revoked (UTC), or <see langword="null"/> while it is not.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; }

    /// <summary>
    /// Issues a new link.
    /// </summary>
    /// <param name="bookingId">Booking the link opens.</param>
    /// <param name="photographerId">Photographer that owns the booking.</param>
    /// <param name="tokenHash">Hash of the secret token.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="expiresAt">Instant the link stops working; must be in the future.</param>
    /// <returns>The link.</returns>
    /// <exception cref="DomainException">When a value is missing or the expiry is not in the future.</exception>
    public static BookingLink Issue(Guid bookingId, Guid photographerId, string tokenHash, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (bookingId == Guid.Empty || photographerId == Guid.Empty || string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The booking, the photographer and the token hash are required.");
        }

        if (expiresAt <= now)
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The link must expire in the future.");
        }

        return new BookingLink(Guid.CreateVersion7(now), bookingId, photographerId, tokenHash, now, expiresAt, null);
    }

    /// <summary>
    /// Rebuilds a link from persisted data, without validating it again.
    /// </summary>
    /// <param name="id">Link identifier.</param>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="photographerId">Photographer identifier.</param>
    /// <param name="tokenHash">Stored hash.</param>
    /// <param name="createdAt">Creation instant.</param>
    /// <param name="expiresAt">Expiry instant.</param>
    /// <param name="revokedAt">Revocation instant, if revoked.</param>
    /// <returns>The restored link.</returns>
    public static BookingLink Restore(
        Guid id,
        Guid bookingId,
        Guid photographerId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt) =>
        new(id, bookingId, photographerId, tokenHash, createdAt, expiresAt, revokedAt);

    /// <summary>
    /// Indicates whether the link works at the given instant: not revoked and not expired.
    /// </summary>
    /// <param name="now">Instant to evaluate.</param>
    /// <returns><see langword="true"/> when the link opens its booking.</returns>
    public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
}
