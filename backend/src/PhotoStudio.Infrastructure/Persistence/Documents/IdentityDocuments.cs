using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB data model of a user (collection <c>users</c>). The identifier is the photographer (tenant) identifier. The
/// username is stored already normalized and is unique. The password is stored only as a hash.
/// </summary>
public sealed class UserDocument
{
    /// <summary>Gets or sets the photographer identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the optimistic concurrency version.</summary>
    [BsonElement("version")]
    public long Version { get; set; }

    /// <summary>Gets or sets the normalized username.</summary>
    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the normalized email. Empty for users created before the email existed.</summary>
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the password hash.</summary>
    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the phone number.</summary>
    [BsonElement("phone")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Gets or sets the creation instant (UTC).</summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the consecutive failed logins.</summary>
    [BsonElement("failedLoginAttempts")]
    public int FailedLoginAttempts { get; set; }

    /// <summary>Gets or sets the instant the lock ends (UTC), when the account is locked.</summary>
    [BsonElement("lockedUntil")]
    public DateTime? LockedUntil { get; set; }
}

/// <summary>
/// MongoDB data model of a refresh token. Only the hash of the secret is stored.
/// </summary>
public sealed class RefreshTokenDocument
{
    /// <summary>Gets or sets the token identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the photographer the token belongs to.</summary>
    [BsonElement("photographerId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid PhotographerId { get; set; }

    /// <summary>Gets or sets the login family.</summary>
    [BsonElement("familyId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid FamilyId { get; set; }

    /// <summary>Gets or sets the hash of the secret.</summary>
    [BsonElement("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the issue instant (UTC).</summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the expiry instant (UTC); drives the retention index.</summary>
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Gets or sets the revocation instant (UTC), when the token was used or revoked.</summary>
    [BsonElement("revokedAt")]
    public DateTime? RevokedAt { get; set; }

    /// <summary>Gets or sets the token that replaced this one when it was rotated.</summary>
    [BsonElement("replacedById")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? ReplacedById { get; set; }
}
