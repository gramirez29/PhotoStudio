using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PhotoStudio.Domain.Billing;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB data model of the settlement of a booking (collection <c>settlements</c>). Enums are stored as strings, amounts as
/// <c>Decimal128</c> and instants as UTC dates. Mapping lives in <see cref="SettlementDocumentMappings"/>.
/// </summary>
public sealed class SettlementDocument
{
    /// <summary>Gets or sets the settlement identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the optimistic concurrency version.</summary>
    [BsonElement("version")]
    public long Version { get; set; }

    /// <summary>Gets or sets the photographer (tenant) the settlement belongs to.</summary>
    [BsonElement("photographerId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid PhotographerId { get; set; }

    /// <summary>Gets or sets the booking the settlement is about; unique across the collection.</summary>
    [BsonElement("bookingId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid BookingId { get; set; }

    /// <summary>Gets or sets the client name.</summary>
    [BsonElement("clientName")]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Gets or sets the client phone.</summary>
    [BsonElement("clientPhone")]
    public string ClientPhone { get; set; } = string.Empty;

    /// <summary>Gets or sets the package name.</summary>
    [BsonElement("packageName")]
    public string PackageName { get; set; } = string.Empty;

    /// <summary>Gets or sets the session start (UTC).</summary>
    [BsonElement("sessionStart")]
    public DateTime SessionStart { get; set; }

    /// <summary>Gets or sets the reason name.</summary>
    [BsonElement("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the currency shared by every amount.</summary>
    [BsonElement("currency")]
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the sum of the verified payments.</summary>
    [BsonElement("totalPaid")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalPaid { get; set; }

    /// <summary>Gets or sets the retained amount.</summary>
    [BsonElement("retained")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Retained { get; set; }

    /// <summary>Gets or sets the retention status name.</summary>
    [BsonElement("retentionStatus")]
    public string RetentionStatus { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount owed back.</summary>
    [BsonElement("refundAmount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal RefundAmount { get; set; }

    /// <summary>Gets or sets the refund status name.</summary>
    [BsonElement("refundStatus")]
    public string RefundStatus { get; set; } = string.Empty;

    /// <summary>Gets or sets the method name used to give the refund back.</summary>
    [BsonElement("refundMethod")]
    public string? RefundMethod { get; set; }

    /// <summary>Gets or sets the note left when the refund was completed.</summary>
    [BsonElement("refundNote")]
    public string? RefundNote { get; set; }

    /// <summary>Gets or sets the instant the refund was completed (UTC).</summary>
    [BsonElement("refundCompletedAt")]
    public DateTime? RefundCompletedAt { get; set; }

    /// <summary>Gets or sets the instant the settlement was opened (UTC).</summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the instant the settlement last changed (UTC).</summary>
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Mapping between settlements and their MongoDB documents.
/// </summary>
public static class SettlementDocumentMappings
{
    /// <summary>
    /// Maps a settlement to its document.
    /// </summary>
    /// <param name="settlement">Settlement to map.</param>
    /// <param name="version">Version to store.</param>
    /// <returns>The document.</returns>
    public static SettlementDocument ToDocument(this BookingSettlement settlement, long version)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        return new SettlementDocument
        {
            Id = settlement.Id,
            Version = version,
            PhotographerId = settlement.PhotographerId,
            BookingId = settlement.BookingId,
            ClientName = settlement.Snapshot.ClientName,
            ClientPhone = settlement.Snapshot.ClientPhone,
            PackageName = settlement.Snapshot.PackageName,
            SessionStart = settlement.Snapshot.SessionStart.UtcDateTime,
            Reason = settlement.Reason.ToString(),
            Currency = settlement.TotalPaid.Currency,
            TotalPaid = settlement.TotalPaid.Amount,
            Retained = settlement.Retained.Amount,
            RetentionStatus = settlement.RetentionStatus.ToString(),
            RefundAmount = settlement.RefundAmount.Amount,
            RefundStatus = settlement.RefundStatus.ToString(),
            RefundMethod = settlement.RefundMethod?.ToString(),
            RefundNote = settlement.RefundNote,
            RefundCompletedAt = settlement.RefundCompletedAt?.UtcDateTime,
            CreatedAt = settlement.CreatedAt.UtcDateTime,
            UpdatedAt = settlement.UpdatedAt.UtcDateTime,
        };
    }

    /// <summary>
    /// Maps a settlement document to the domain settlement.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The settlement.</returns>
    public static BookingSettlement ToDomain(this SettlementDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return BookingSettlement.Restore(
            document.Id,
            document.Version,
            document.PhotographerId,
            document.BookingId,
            new SettlementSnapshot(document.ClientName, document.ClientPhone, document.PackageName, ToOffset(document.SessionStart)),
            Enum.Parse<SettlementReason>(document.Reason),
            Money.Restore(document.TotalPaid, document.Currency),
            Money.Restore(document.Retained, document.Currency),
            Enum.Parse<RetentionStatus>(document.RetentionStatus),
            Money.Restore(document.RefundAmount, document.Currency),
            Enum.Parse<RefundStatus>(document.RefundStatus),
            document.RefundMethod is null ? null : Enum.Parse<PaymentMethod>(document.RefundMethod),
            document.RefundNote,
            document.RefundCompletedAt is null ? null : ToOffset(document.RefundCompletedAt.Value),
            ToOffset(document.CreatedAt),
            ToOffset(document.UpdatedAt));
    }

    /// <summary>
    /// Converts a stored date, which MongoDB returns as UTC, to a UTC offset instant.
    /// </summary>
    /// <param name="value">Stored date.</param>
    /// <returns>The same instant with a zero offset.</returns>
    private static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
