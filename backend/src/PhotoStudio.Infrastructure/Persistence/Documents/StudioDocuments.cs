using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB data model of a session of the web area (collection <c>studio_sessions</c>). A time-to-live index on the expiry removes
/// the ones that ended. Mapping lives in <see cref="StudioDocumentMappings"/>.
/// </summary>
public sealed class StudioSessionDocument
{
    /// <summary>Gets or sets the session identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the user the session belongs to.</summary>
    [BsonElement("userId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the SHA-256 of the secret in the cookie; unique.</summary>
    [BsonElement("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the instant the session began (UTC).</summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the instant the session was last used (UTC).</summary>
    [BsonElement("lastSeenAt")]
    public DateTime LastSeenAt { get; set; }

    /// <summary>Gets or sets the instant the session ends (UTC); the time-to-live index removes the document then.</summary>
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// MongoDB data model of a browser the user chose to remember (collection <c>studio_devices</c>). A time-to-live index on the
/// expiry removes the ones that were forgotten.
/// </summary>
public sealed class StudioDeviceDocument
{
    /// <summary>Gets or sets the device identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the user the device belongs to.</summary>
    [BsonElement("userId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the SHA-256 of the secret in the cookie; unique, and replaced at every use.</summary>
    [BsonElement("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the instant the browser was remembered (UTC).</summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the instant the browser last signed in (UTC).</summary>
    [BsonElement("lastUsedAt")]
    public DateTime LastUsedAt { get; set; }

    /// <summary>Gets or sets the instant the browser is forgotten (UTC); the time-to-live index removes the document then.</summary>
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// MongoDB data model of a second-factor challenge of the web sign-in (collection <c>studio_challenges</c>). The identifier is the
/// user, so there is one per user. It is removed once its sending window is over, which is also how long the password step stays valid.
/// </summary>
public sealed class StudioChallengeDocument
{
    /// <summary>Gets or sets the user the challenge is for; it is the document identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the SHA-256 of the secret in the cookie of the challenge; unique.</summary>
    [BsonElement("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the hash of the code.</summary>
    [BsonElement("codeHash")]
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the instant the code stops being accepted (UTC).</summary>
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Gets or sets the instant the code was sent (UTC).</summary>
    [BsonElement("sentAt")]
    public DateTime SentAt { get; set; }

    /// <summary>Gets or sets the wrong guesses counted against the code.</summary>
    [BsonElement("failedAttempts")]
    public int FailedAttempts { get; set; }

    /// <summary>Gets or sets the codes sent in the current window.</summary>
    [BsonElement("sendCount")]
    public int SendCount { get; set; }

    /// <summary>Gets or sets the instant the current sending window began (UTC).</summary>
    [BsonElement("windowStartedAt")]
    public DateTime WindowStartedAt { get; set; }

    /// <summary>Gets or sets the instant the document is removed (UTC): the end of the sending window.</summary>
    [BsonElement("purgeAt")]
    public DateTime PurgeAt { get; set; }
}

/// <summary>
/// Mapping between the records of the web area and their MongoDB documents.
/// </summary>
public static class StudioDocumentMappings
{
    /// <summary>
    /// Maps a session to its document.
    /// </summary>
    /// <param name="session">Session to map.</param>
    /// <returns>The document.</returns>
    public static StudioSessionDocument ToDocument(this StudioSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new StudioSessionDocument
        {
            Id = session.Id,
            UserId = session.UserId,
            TokenHash = session.TokenHash,
            CreatedAt = session.CreatedAt.UtcDateTime,
            LastSeenAt = session.LastSeenAt.UtcDateTime,
            ExpiresAt = session.ExpiresAt.UtcDateTime,
        };
    }

    /// <summary>
    /// Maps a document back to a session.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The session.</returns>
    public static StudioSession ToDomain(this StudioSessionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new StudioSession(document.Id, document.UserId, document.TokenHash, ToOffset(document.CreatedAt), ToOffset(document.LastSeenAt), ToOffset(document.ExpiresAt));
    }

    /// <summary>
    /// Maps a remembered browser to its document.
    /// </summary>
    /// <param name="device">Device to map.</param>
    /// <returns>The document.</returns>
    public static StudioDeviceDocument ToDocument(this StudioTrustedDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return new StudioDeviceDocument
        {
            Id = device.Id,
            UserId = device.UserId,
            TokenHash = device.TokenHash,
            CreatedAt = device.CreatedAt.UtcDateTime,
            LastUsedAt = device.LastUsedAt.UtcDateTime,
            ExpiresAt = device.ExpiresAt.UtcDateTime,
        };
    }

    /// <summary>
    /// Maps a document back to a remembered browser.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The device.</returns>
    public static StudioTrustedDevice ToDomain(this StudioDeviceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new StudioTrustedDevice(document.Id, document.UserId, document.TokenHash, ToOffset(document.CreatedAt), ToOffset(document.LastUsedAt), ToOffset(document.ExpiresAt));
    }

    /// <summary>
    /// Maps a challenge to its document.
    /// </summary>
    /// <param name="challenge">Challenge to map.</param>
    /// <returns>The document.</returns>
    public static StudioChallengeDocument ToDocument(this StudioLoginChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return new StudioChallengeDocument
        {
            UserId = challenge.UserId,
            TokenHash = challenge.TokenHash,
            CodeHash = challenge.CodeHash,
            ExpiresAt = challenge.ExpiresAt.UtcDateTime,
            SentAt = challenge.SentAt.UtcDateTime,
            FailedAttempts = challenge.FailedAttempts,
            SendCount = challenge.SendCount,
            WindowStartedAt = challenge.WindowStartedAt.UtcDateTime,
            PurgeAt = (challenge.WindowStartedAt + StudioLoginChallenge.SendWindow).UtcDateTime,
        };
    }

    /// <summary>
    /// Maps a document back to a challenge.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The challenge.</returns>
    public static StudioLoginChallenge ToDomain(this StudioChallengeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new StudioLoginChallenge(
            document.UserId,
            document.TokenHash,
            document.CodeHash,
            ToOffset(document.ExpiresAt),
            ToOffset(document.SentAt),
            document.FailedAttempts,
            document.SendCount,
            ToOffset(document.WindowStartedAt));
    }

    /// <summary>
    /// Turns a date read from MongoDB into an instant, treating it as UTC whatever its kind.
    /// </summary>
    /// <param name="value">The stored date.</param>
    /// <returns>The instant.</returns>
    private static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
