using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB data model of a booking link (collection <c>booking_links</c>). Only the hash of the token is stored.
/// </summary>
public sealed class BookingLinkDocument
{
    /// <summary>Gets or sets the link identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the booking the link opens.</summary>
    [BsonElement("bookingId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid BookingId { get; set; }

    /// <summary>Gets or sets the photographer (tenant) that owns the booking.</summary>
    [BsonElement("photographerId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid PhotographerId { get; set; }

    /// <summary>Gets or sets the hash of the token.</summary>
    [BsonElement("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the creation instant (UTC).</summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the expiry instant (UTC); drives the retention index.</summary>
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Gets or sets the revocation instant (UTC), when the link was replaced.</summary>
    [BsonElement("revokedAt")]
    public DateTime? RevokedAt { get; set; }
}

/// <summary>
/// MongoDB data model of the current verification code of a booking (collection <c>portal_otps</c>). The identifier is the
/// booking, so there is at most one code per booking. Only a keyed hash of the code is stored.
/// </summary>
public sealed class PortalOtpDocument
{
    /// <summary>Gets or sets the booking identifier, which is also the document identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid BookingId { get; set; }

    /// <summary>Gets or sets the keyed hash of the code.</summary>
    [BsonElement("codeHash")]
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the expiry instant (UTC); drives the retention index.</summary>
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Gets or sets the instant the code was sent (UTC).</summary>
    [BsonElement("sentAt")]
    public DateTime SentAt { get; set; }

    /// <summary>Gets or sets the wrong guesses counted so far.</summary>
    [BsonElement("failedAttempts")]
    public int FailedAttempts { get; set; }

    /// <summary>Gets or sets the codes sent in the current window.</summary>
    [BsonElement("sendCount")]
    public int SendCount { get; set; }

    /// <summary>Gets or sets the start of the current sending window (UTC).</summary>
    [BsonElement("windowStartedAt")]
    public DateTime WindowStartedAt { get; set; }
}

/// <summary>
/// Mapping between the portal domain objects and their MongoDB documents.
/// </summary>
public static class PortalDocumentMappings
{
    /// <summary>
    /// Maps a link to its document.
    /// </summary>
    /// <param name="link">Link to map.</param>
    /// <returns>The document.</returns>
    public static BookingLinkDocument ToDocument(this BookingLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        return new BookingLinkDocument
        {
            Id = link.Id,
            BookingId = link.BookingId,
            PhotographerId = link.PhotographerId,
            TokenHash = link.TokenHash,
            CreatedAt = link.CreatedAt.UtcDateTime,
            ExpiresAt = link.ExpiresAt.UtcDateTime,
            RevokedAt = link.RevokedAt?.UtcDateTime,
        };
    }

    /// <summary>
    /// Maps a link document to the domain link.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The link.</returns>
    public static BookingLink ToDomain(this BookingLinkDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return BookingLink.Restore(
            document.Id,
            document.BookingId,
            document.PhotographerId,
            document.TokenHash,
            ToOffset(document.CreatedAt),
            ToOffset(document.ExpiresAt),
            document.RevokedAt is null ? null : ToOffset(document.RevokedAt.Value));
    }

    /// <summary>
    /// Maps a code record to its document.
    /// </summary>
    /// <param name="otp">Record to map.</param>
    /// <returns>The document.</returns>
    public static PortalOtpDocument ToDocument(this PortalOtp otp)
    {
        ArgumentNullException.ThrowIfNull(otp);
        return new PortalOtpDocument
        {
            BookingId = otp.BookingId,
            CodeHash = otp.CodeHash,
            ExpiresAt = otp.ExpiresAt.UtcDateTime,
            SentAt = otp.SentAt.UtcDateTime,
            FailedAttempts = otp.FailedAttempts,
            SendCount = otp.SendCount,
            WindowStartedAt = otp.WindowStartedAt.UtcDateTime,
        };
    }

    /// <summary>
    /// Maps a code document to the domain record.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The record.</returns>
    public static PortalOtp ToDomain(this PortalOtpDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new PortalOtp(
            document.BookingId,
            document.CodeHash,
            ToOffset(document.ExpiresAt),
            ToOffset(document.SentAt),
            document.FailedAttempts,
            document.SendCount,
            ToOffset(document.WindowStartedAt));
    }

    /// <summary>
    /// Converts a stored date, which MongoDB returns as UTC, to a UTC offset instant.
    /// </summary>
    /// <param name="value">Stored date.</param>
    /// <returns>The same instant with a zero offset.</returns>
    private static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
