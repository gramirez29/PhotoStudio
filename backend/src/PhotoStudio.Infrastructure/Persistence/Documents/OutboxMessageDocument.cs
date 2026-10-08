using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB data model of an outbox message: a domain event stored in the same transaction as the change that raised it,
/// waiting for the worker to deliver it to its handlers. Kept separate from the domain event itself.
/// </summary>
public sealed class OutboxMessageDocument
{
    /// <summary>Status of a message that still has to be delivered.</summary>
    public const string PendingStatus = "Pending";

    /// <summary>Status of a message every handler already processed.</summary>
    public const string ProcessedStatus = "Processed";

    /// <summary>Status of a message that exhausted its attempts or can no longer be read; it needs manual review.</summary>
    public const string FailedStatus = "Failed";

    /// <summary>Gets or sets the message identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name of the domain event type (the key of the event registry).</summary>
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the event serialized as JSON.</summary>
    [BsonElement("payload")]
    public string Payload { get; set; } = string.Empty;

    /// <summary>Gets or sets the instant the event occurred (UTC); deliveries follow this order.</summary>
    [BsonElement("occurredAt")]
    public DateTime OccurredAt { get; set; }

    /// <summary>Gets or sets the delivery status.</summary>
    [BsonElement("status")]
    public string Status { get; set; } = PendingStatus;

    /// <summary>Gets or sets how many deliveries were attempted.</summary>
    [BsonElement("attempts")]
    public int Attempts { get; set; }

    /// <summary>Gets or sets the earliest instant of the next delivery attempt (UTC).</summary>
    [BsonElement("nextAttemptAt")]
    public DateTime NextAttemptAt { get; set; }

    /// <summary>Gets or sets the instant until which a worker holds the message (UTC); lets another worker take over after a crash.</summary>
    [BsonElement("lockedUntil")]
    public DateTime? LockedUntil { get; set; }

    /// <summary>Gets or sets the instant the message was delivered (UTC); drives the retention index.</summary>
    [BsonElement("processedAt")]
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Gets or sets the error of the last failed attempt.</summary>
    [BsonElement("lastError")]
    public string? LastError { get; set; }
}
