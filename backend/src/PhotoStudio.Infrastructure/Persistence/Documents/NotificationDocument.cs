using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB data model of a notification (collection <c>notifications</c>). Enums are stored as strings and instants as UTC
/// dates. Mapping lives in <see cref="NotificationDocumentMappings"/>.
/// </summary>
public sealed class NotificationDocument
{
    /// <summary>Gets or sets the notification identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the optimistic concurrency version.</summary>
    [BsonElement("version")]
    public long Version { get; set; }

    /// <summary>Gets or sets the photographer (tenant) the notification is for.</summary>
    [BsonElement("photographerId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid PhotographerId { get; set; }

    /// <summary>Gets or sets the booking the notification is about.</summary>
    [BsonElement("bookingId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid BookingId { get; set; }

    /// <summary>Gets or sets the type name.</summary>
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the status name.</summary>
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the idempotency key; unique across the collection.</summary>
    [BsonElement("dedupKey")]
    public string DedupKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the instant the notification becomes due (UTC).</summary>
    [BsonElement("dueAt")]
    public DateTime DueAt { get; set; }

    /// <summary>Gets or sets the scheduling instant (UTC).</summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the delivery instant (UTC).</summary>
    [BsonElement("deliveredAt")]
    public DateTime? DeliveredAt { get; set; }

    /// <summary>Gets or sets the read instant (UTC).</summary>
    [BsonElement("readAt")]
    public DateTime? ReadAt { get; set; }

    /// <summary>Gets or sets the cancellation instant (UTC).</summary>
    [BsonElement("cancelledAt")]
    public DateTime? CancelledAt { get; set; }

    /// <summary>Gets or sets the instant after which MongoDB removes the document (UTC); drives the retention index.</summary>
    [BsonElement("retainUntil")]
    public DateTime RetainUntil { get; set; }

    /// <summary>Gets or sets what the notification says; present once it is delivered.</summary>
    [BsonElement("details")]
    public NotificationDetailsDocument? Details { get; set; }
}

/// <summary>
/// MongoDB data model of what a delivered notification says.
/// </summary>
public sealed class NotificationDetailsDocument
{
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

    /// <summary>Gets or sets the amount owed, for balance notices.</summary>
    [BsonElement("balanceAmount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? BalanceAmount { get; set; }

    /// <summary>Gets or sets the currency of the amount owed.</summary>
    [BsonElement("balanceCurrency")]
    public string? BalanceCurrency { get; set; }
}

/// <summary>
/// Mapping between notifications and their MongoDB documents.
/// </summary>
public static class NotificationDocumentMappings
{
    /// <summary>
    /// Maps a notification to its document.
    /// </summary>
    /// <param name="notification">Notification to map.</param>
    /// <param name="version">Version to store.</param>
    /// <returns>The document.</returns>
    public static NotificationDocument ToDocument(this Notification notification, long version)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return new NotificationDocument
        {
            Id = notification.Id,
            Version = version,
            PhotographerId = notification.PhotographerId,
            BookingId = notification.BookingId,
            Type = notification.Type.ToString(),
            Status = notification.Status.ToString(),
            DedupKey = notification.DedupKey,
            DueAt = notification.DueAt.UtcDateTime,
            CreatedAt = notification.CreatedAt.UtcDateTime,
            DeliveredAt = notification.DeliveredAt?.UtcDateTime,
            ReadAt = notification.ReadAt?.UtcDateTime,
            CancelledAt = notification.CancelledAt?.UtcDateTime,
            RetainUntil = notification.RetainUntil.UtcDateTime,
            Details = notification.Details is null ? null : ToDocument(notification.Details),
        };
    }

    /// <summary>
    /// Maps a notification document to the domain notification.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The notification.</returns>
    public static Notification ToDomain(this NotificationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Notification.Restore(
            document.Id,
            document.Version,
            document.PhotographerId,
            document.BookingId,
            Enum.Parse<NotificationType>(document.Type),
            document.DedupKey,
            ToOffset(document.DueAt),
            Enum.Parse<NotificationStatus>(document.Status),
            ToOffset(document.CreatedAt),
            document.DeliveredAt is null ? null : ToOffset(document.DeliveredAt.Value),
            document.ReadAt is null ? null : ToOffset(document.ReadAt.Value),
            document.CancelledAt is null ? null : ToOffset(document.CancelledAt.Value),
            document.Details is null ? null : ToDomain(document.Details));
    }

    /// <summary>
    /// Maps the details of a notification to their document.
    /// </summary>
    /// <param name="details">Details to map.</param>
    /// <returns>The document.</returns>
    private static NotificationDetailsDocument ToDocument(NotificationDetails details) => new()
    {
        ClientName = details.ClientName,
        ClientPhone = details.ClientPhone,
        PackageName = details.PackageName,
        SessionStart = details.SessionStart.UtcDateTime,
        BalanceAmount = details.Balance?.Amount,
        BalanceCurrency = details.Balance?.Currency,
    };

    /// <summary>
    /// Maps a details document to the domain value.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The details.</returns>
    private static NotificationDetails ToDomain(NotificationDetailsDocument document) => new(
        document.ClientName,
        document.ClientPhone,
        document.PackageName,
        ToOffset(document.SessionStart),
        document.BalanceAmount is { } amount && document.BalanceCurrency is { } currency ? Money.Restore(amount, currency) : null);

    /// <summary>
    /// Converts a stored date, which MongoDB returns as UTC, to a UTC offset instant.
    /// </summary>
    /// <param name="value">Stored date.</param>
    /// <returns>The same instant with a zero offset.</returns>
    private static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
