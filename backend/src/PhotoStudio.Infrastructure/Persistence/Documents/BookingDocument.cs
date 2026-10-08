using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB data model of a booking. Kept separate from the domain aggregate; enums are stored as strings
/// and instants as UTC dates. Mapping lives in <see cref="BookingDocumentMappings"/>.
/// </summary>
public sealed class BookingDocument
{
    /// <summary>Gets or sets the booking identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the optimistic concurrency version.</summary>
    [BsonElement("version")]
    public long Version { get; set; }

    /// <summary>Gets or sets the photographer (tenant) identifier.</summary>
    [BsonElement("photographerId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid PhotographerId { get; set; }

    /// <summary>Gets or sets the client name.</summary>
    [BsonElement("clientName")]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Gets or sets the client phone.</summary>
    [BsonElement("clientPhone")]
    public string ClientPhone { get; set; } = string.Empty;

    /// <summary>Gets or sets the package name.</summary>
    [BsonElement("packageName")]
    public string PackageName { get; set; } = string.Empty;

    /// <summary>Gets or sets the package price.</summary>
    [BsonElement("packagePrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal PackagePrice { get; set; }

    /// <summary>Gets or sets the currency code of every amount in the booking.</summary>
    [BsonElement("currency")]
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the session start (UTC).</summary>
    [BsonElement("slotStart")]
    public DateTime SlotStart { get; set; }

    /// <summary>Gets or sets the session end (UTC).</summary>
    [BsonElement("slotEnd")]
    public DateTime SlotEnd { get; set; }

    /// <summary>Gets or sets the policy copied into the booking.</summary>
    [BsonElement("policy")]
    public BookingPolicyDocument Policy { get; set; } = new();

    /// <summary>Gets or sets the status name.</summary>
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the creation instant (UTC).</summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the end of the tentative hold (UTC).</summary>
    [BsonElement("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Gets or sets the contract signature.</summary>
    [BsonElement("contract")]
    public ContractSignatureDocument? Contract { get; set; }

    /// <summary>Gets or sets the reschedule count.</summary>
    [BsonElement("rescheduleCount")]
    public int RescheduleCount { get; set; }

    /// <summary>Gets or sets the instant the client was marked absent (UTC).</summary>
    [BsonElement("clientAbsentMarkedAt")]
    public DateTime? ClientAbsentMarkedAt { get; set; }

    /// <summary>Gets or sets the recorded payments.</summary>
    [BsonElement("payments")]
    public List<PaymentDocument> Payments { get; set; } = [];

    /// <summary>Gets or sets the transition audit trail.</summary>
    [BsonElement("history")]
    public List<StatusTransitionDocument> History { get; set; } = [];
}

/// <summary>
/// MongoDB data model of the policy copied into a booking.
/// </summary>
public sealed class BookingPolicyDocument
{
    /// <summary>Gets or sets the tentative hold, in hours.</summary>
    [BsonElement("tentativeHoldHours")]
    public int TentativeHoldHours { get; set; }

    /// <summary>Gets or sets the deposit as a fraction of the price.</summary>
    [BsonElement("depositPercentage")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DepositPercentage { get; set; }

    /// <summary>Gets or sets the free cancellation window, in hours.</summary>
    [BsonElement("freeCancellationWindowHours")]
    public int FreeCancellationWindowHours { get; set; }

    /// <summary>Gets or sets the minimum reschedule notice, in hours.</summary>
    [BsonElement("rescheduleMinNoticeHours")]
    public int RescheduleMinNoticeHours { get; set; }

    /// <summary>Gets or sets the maximum client reschedules.</summary>
    [BsonElement("maxReschedules")]
    public int MaxReschedules { get; set; }

    /// <summary>Gets or sets the client-absent tolerance, in minutes.</summary>
    [BsonElement("clientAbsentToleranceMinutes")]
    public int ClientAbsentToleranceMinutes { get; set; }

    /// <summary>Gets or sets the client-absent revert window, in days.</summary>
    [BsonElement("clientAbsentRevertWindowDays")]
    public int ClientAbsentRevertWindowDays { get; set; }
}

/// <summary>
/// MongoDB data model of a contract signature.
/// </summary>
public sealed class ContractSignatureDocument
{
    /// <summary>Gets or sets the signer name.</summary>
    [BsonElement("signerName")]
    public string SignerName { get; set; } = string.Empty;

    /// <summary>Gets or sets the template version.</summary>
    [BsonElement("templateVersion")]
    public string TemplateVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the actor name that registered the signature.</summary>
    [BsonElement("signedBy")]
    public string SignedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the channel name.</summary>
    [BsonElement("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>Gets or sets the signature instant (UTC).</summary>
    [BsonElement("signedAt")]
    public DateTime SignedAt { get; set; }
}

/// <summary>
/// MongoDB data model of a payment.
/// </summary>
public sealed class PaymentDocument
{
    /// <summary>Gets or sets the payment identifier (stored as the embedded document's _id).</summary>
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    [BsonElement("amount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the currency code.</summary>
    [BsonElement("currency")]
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the payment method name.</summary>
    [BsonElement("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>Gets or sets the channel name.</summary>
    [BsonElement("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>Gets or sets the actor name that recorded the payment.</summary>
    [BsonElement("recordedBy")]
    public string RecordedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the verification status name.</summary>
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the idempotency key.</summary>
    [BsonElement("idempotencyKey")]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the recording instant (UTC).</summary>
    [BsonElement("recordedAt")]
    public DateTime RecordedAt { get; set; }

    /// <summary>Gets or sets the verification or rejection instant (UTC).</summary>
    [BsonElement("resolvedAt")]
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Gets or sets the rejection reason.</summary>
    [BsonElement("rejectionReason")]
    public string? RejectionReason { get; set; }
}

/// <summary>
/// MongoDB data model of a transition audit entry.
/// </summary>
public sealed class StatusTransitionDocument
{
    /// <summary>Gets or sets the previous status name; <see langword="null"/> for the creation entry.</summary>
    [BsonElement("from")]
    public string? From { get; set; }

    /// <summary>Gets or sets the new status name.</summary>
    [BsonElement("to")]
    public string To { get; set; } = string.Empty;

    /// <summary>Gets or sets the actor name.</summary>
    [BsonElement("actor")]
    public string Actor { get; set; } = string.Empty;

    /// <summary>Gets or sets the channel name, if any.</summary>
    [BsonElement("channel")]
    public string? Channel { get; set; }

    /// <summary>Gets or sets the reason, if any.</summary>
    [BsonElement("reason")]
    public string? Reason { get; set; }

    /// <summary>Gets or sets the transition instant (UTC).</summary>
    [BsonElement("occurredAt")]
    public DateTime OccurredAt { get; set; }
}
