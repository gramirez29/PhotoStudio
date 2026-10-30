using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB data model of a gallery (collection <c>galleries</c>). The status is stored as a string, the price as
/// <c>Decimal128</c> and instants as UTC dates. Mapping lives in <see cref="GalleryDocumentMappings"/>.
/// </summary>
public sealed class GalleryDocument
{
    /// <summary>Gets or sets the gallery identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the optimistic concurrency version.</summary>
    [BsonElement("version")]
    public long Version { get; set; }

    /// <summary>Gets or sets the photographer (tenant) the gallery belongs to.</summary>
    [BsonElement("photographerId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid PhotographerId { get; set; }

    /// <summary>Gets or sets the booking the gallery comes from; unique across the collection.</summary>
    [BsonElement("bookingId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid BookingId { get; set; }

    /// <summary>Gets or sets the client name.</summary>
    [BsonElement("clientName")]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Gets or sets the package name.</summary>
    [BsonElement("packageName")]
    public string PackageName { get; set; } = string.Empty;

    /// <summary>Gets or sets the session start (UTC).</summary>
    [BsonElement("sessionStart")]
    public DateTime SessionStart { get; set; }

    /// <summary>Gets or sets the status name.</summary>
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets how many photos the package includes.</summary>
    [BsonElement("includedPhotos")]
    public int IncludedPhotos { get; set; }

    /// <summary>Gets or sets how many photos are a gift.</summary>
    [BsonElement("bonusPhotos")]
    public int BonusPhotos { get; set; }

    /// <summary>Gets or sets the currency of the price of an extra photo.</summary>
    [BsonElement("currency")]
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the price of an extra photo.</summary>
    [BsonElement("extraPhotoPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal ExtraPhotoPrice { get; set; }

    /// <summary>Gets or sets the instant the gallery was created (UTC).</summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the instant the gallery last changed (UTC).</summary>
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Mapping between galleries and their MongoDB documents.
/// </summary>
public static class GalleryDocumentMappings
{
    /// <summary>
    /// Maps a gallery to its document.
    /// </summary>
    /// <param name="gallery">Gallery to map.</param>
    /// <param name="version">Version to store.</param>
    /// <returns>The document.</returns>
    public static GalleryDocument ToDocument(this Gallery gallery, long version)
    {
        ArgumentNullException.ThrowIfNull(gallery);

        return new GalleryDocument
        {
            Id = gallery.Id,
            Version = version,
            PhotographerId = gallery.PhotographerId,
            BookingId = gallery.BookingId,
            ClientName = gallery.Snapshot.ClientName,
            PackageName = gallery.Snapshot.PackageName,
            SessionStart = gallery.Snapshot.SessionStart.UtcDateTime,
            Status = gallery.Status.ToString(),
            IncludedPhotos = gallery.IncludedPhotos,
            BonusPhotos = gallery.BonusPhotos,
            Currency = gallery.ExtraPhotoPrice.Currency,
            ExtraPhotoPrice = gallery.ExtraPhotoPrice.Amount,
            CreatedAt = gallery.CreatedAt.UtcDateTime,
            UpdatedAt = gallery.UpdatedAt.UtcDateTime,
        };
    }

    /// <summary>
    /// Maps a document back to a gallery.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The gallery, with the version that was stored.</returns>
    public static Gallery ToDomain(this GalleryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Gallery.Restore(
            document.Id,
            document.Version,
            document.PhotographerId,
            document.BookingId,
            new GallerySnapshot(document.ClientName, document.PackageName, ToOffset(document.SessionStart)),
            Enum.Parse<GalleryStatus>(document.Status),
            document.IncludedPhotos,
            document.BonusPhotos,
            Money.Restore(document.ExtraPhotoPrice, document.Currency),
            ToOffset(document.CreatedAt),
            ToOffset(document.UpdatedAt));
    }

    /// <summary>
    /// Turns a date read from MongoDB into an instant, treating it as UTC whatever its kind.
    /// </summary>
    /// <param name="value">The stored date.</param>
    /// <returns>The instant.</returns>
    private static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
