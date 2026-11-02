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

    /// <summary>Gets or sets the proofs of the gallery, in the order they were registered.</summary>
    [BsonElement("photos")]
    public List<GalleryPhotoDocument> Photos { get; set; } = [];

    /// <summary>Gets or sets the instant the client was told the proofs are ready (UTC), if they were.</summary>
    [BsonElement("selectionNotifiedAt")]
    public DateTime? SelectionNotifiedAt { get; set; }

    /// <summary>Gets or sets the photos the client chose, in the order they were chosen.</summary>
    [BsonElement("selectedPhotoIds")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public List<Guid> SelectedPhotoIds { get; set; } = [];

    /// <summary>Gets or sets the instant the client sent the selection (UTC), if they did.</summary>
    [BsonElement("selectionSubmittedAt")]
    public DateTime? SelectionSubmittedAt { get; set; }

    /// <summary>Gets or sets the instant the final photos were published (UTC), if they were.</summary>
    [BsonElement("finalsPublishedAt")]
    public DateTime? FinalsPublishedAt { get; set; }

    /// <summary>Gets or sets the instant the gallery was delivered (UTC), if it was.</summary>
    [BsonElement("deliveredAt")]
    public DateTime? DeliveredAt { get; set; }

    /// <summary>Gets or sets the instant the client was told about the delivery (UTC), if they were.</summary>
    [BsonElement("deliveryNotifiedAt")]
    public DateTime? DeliveryNotifiedAt { get; set; }

    /// <summary>Gets or sets the instant the extra photos were released (UTC), if they were.</summary>
    [BsonElement("extrasReleasedAt")]
    public DateTime? ExtrasReleasedAt { get; set; }

    /// <summary>Gets or sets the size of the ZIP with the photos, if there is one.</summary>
    [BsonElement("zipBytes")]
    public long? ZipBytes { get; set; }

    /// <summary>Gets or sets the instant the ZIP was registered (UTC), if there is one.</summary>
    [BsonElement("zipUploadedAt")]
    public DateTime? ZipUploadedAt { get; set; }

    /// <summary>Gets or sets how many times the client asked to download the ZIP.</summary>
    [BsonElement("zipDownloads")]
    public int ZipDownloads { get; set; }

    /// <summary>Gets or sets the last instant the client asked to download the ZIP (UTC), if they did.</summary>
    [BsonElement("zipLastDownloadedAt")]
    public DateTime? ZipLastDownloadedAt { get; set; }

    /// <summary>Gets or sets the last instant the selection was reopened (UTC), if it was.</summary>
    [BsonElement("selectionReopenedAt")]
    public DateTime? SelectionReopenedAt { get; set; }

    /// <summary>Gets or sets why the selection was last reopened, if it was.</summary>
    [BsonElement("selectionReopenReason")]
    public string? SelectionReopenReason { get; set; }

    /// <summary>Gets or sets the last instant the client was told about a reopening (UTC), if they were.</summary>
    [BsonElement("reopenNotifiedAt")]
    public DateTime? ReopenNotifiedAt { get; set; }

    /// <summary>Gets or sets the instant the gallery was created (UTC).</summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the instant the gallery last changed (UTC).</summary>
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// MongoDB data model of a proof, embedded in the document of its gallery: a gallery holds at most 150, so they always fit.
/// </summary>
public sealed class GalleryPhotoDocument
{
    /// <summary>Gets or sets the photo identifier.</summary>
    [BsonElement("id")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name of the file the photo came from.</summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the size of the stored proof in bytes.</summary>
    [BsonElement("proofBytes")]
    public long ProofBytes { get; set; }

    /// <summary>Gets or sets the instant the proof was registered (UTC).</summary>
    [BsonElement("uploadedAt")]
    public DateTime UploadedAt { get; set; }

    /// <summary>Gets or sets the size of the final photo in bytes, if it was uploaded.</summary>
    [BsonElement("finalBytes")]
    public long? FinalBytes { get; set; }

    /// <summary>Gets or sets the instant the final photo was registered (UTC), if it was.</summary>
    [BsonElement("finalUploadedAt")]
    public DateTime? FinalUploadedAt { get; set; }

    /// <summary>Gets or sets the width of the final in pixels, if the browser could read it.</summary>
    [BsonElement("finalWidth")]
    public int? FinalWidth { get; set; }

    /// <summary>Gets or sets the height of the final in pixels, if the browser could read it.</summary>
    [BsonElement("finalHeight")]
    public int? FinalHeight { get; set; }

    /// <summary>Gets or sets the instant the final was taken (UTC), if its metadata said so.</summary>
    [BsonElement("finalCapturedAt")]
    public DateTime? FinalCapturedAt { get; set; }

    /// <summary>Gets or sets how many times the client asked to download the final.</summary>
    [BsonElement("finalDownloads")]
    public int FinalDownloads { get; set; }

    /// <summary>Gets or sets the size of the thumbnail of the final, if it has one.</summary>
    [BsonElement("finalThumbBytes")]
    public long? FinalThumbBytes { get; set; }
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
            Photos = [.. gallery.Photos.Select(photo => new GalleryPhotoDocument
            {
                Id = photo.Id,
                Name = photo.OriginalName,
                ProofBytes = photo.ProofBytes,
                UploadedAt = photo.UploadedAt.UtcDateTime,
                FinalBytes = photo.FinalBytes,
                FinalUploadedAt = photo.FinalUploadedAt?.UtcDateTime,
                FinalWidth = photo.FinalWidth,
                FinalHeight = photo.FinalHeight,
                FinalCapturedAt = photo.FinalCapturedAt?.UtcDateTime,
                FinalDownloads = photo.FinalDownloads,
                FinalThumbBytes = photo.FinalThumbBytes,
            })],
            SelectionNotifiedAt = gallery.SelectionNotifiedAt?.UtcDateTime,
            SelectedPhotoIds = [.. gallery.SelectedPhotoIds],
            SelectionSubmittedAt = gallery.SelectionSubmittedAt?.UtcDateTime,
            FinalsPublishedAt = gallery.FinalsPublishedAt?.UtcDateTime,
            DeliveredAt = gallery.DeliveredAt?.UtcDateTime,
            DeliveryNotifiedAt = gallery.DeliveryNotifiedAt?.UtcDateTime,
            ExtrasReleasedAt = gallery.ExtrasReleasedAt?.UtcDateTime,
            ZipBytes = gallery.ZipBytes,
            ZipUploadedAt = gallery.ZipUploadedAt?.UtcDateTime,
            ZipDownloads = gallery.ZipDownloads,
            ZipLastDownloadedAt = gallery.ZipLastDownloadedAt?.UtcDateTime,
            SelectionReopenedAt = gallery.SelectionReopenedAt?.UtcDateTime,
            SelectionReopenReason = gallery.SelectionReopenReason,
            ReopenNotifiedAt = gallery.ReopenNotifiedAt?.UtcDateTime,
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
            ToOffset(document.UpdatedAt),
            document.Photos.Select(photo => new GalleryPhoto(photo.Id, photo.Name, photo.ProofBytes, ToOffset(photo.UploadedAt), photo.FinalBytes, photo.FinalUploadedAt is null ? null : ToOffset(photo.FinalUploadedAt.Value), photo.FinalWidth, photo.FinalHeight, photo.FinalCapturedAt is null ? null : ToOffset(photo.FinalCapturedAt.Value), photo.FinalDownloads, photo.FinalThumbBytes)),
            document.SelectionNotifiedAt is null ? null : ToOffset(document.SelectionNotifiedAt.Value),
            document.SelectedPhotoIds,
            document.SelectionSubmittedAt is null ? null : ToOffset(document.SelectionSubmittedAt.Value),
            document.FinalsPublishedAt is null ? null : ToOffset(document.FinalsPublishedAt.Value),
            document.DeliveredAt is null ? null : ToOffset(document.DeliveredAt.Value),
            document.DeliveryNotifiedAt is null ? null : ToOffset(document.DeliveryNotifiedAt.Value),
            document.ExtrasReleasedAt is null ? null : ToOffset(document.ExtrasReleasedAt.Value),
            document.ZipBytes,
            document.ZipUploadedAt is null ? null : ToOffset(document.ZipUploadedAt.Value),
            document.ZipDownloads,
            document.ZipLastDownloadedAt is null ? null : ToOffset(document.ZipLastDownloadedAt.Value),
            document.SelectionReopenedAt is null ? null : ToOffset(document.SelectionReopenedAt.Value),
            document.SelectionReopenReason,
            document.ReopenNotifiedAt is null ? null : ToOffset(document.ReopenNotifiedAt.Value));
    }

    /// <summary>
    /// Turns a date read from MongoDB into an instant, treating it as UTC whatever its kind.
    /// </summary>
    /// <param name="value">The stored date.</param>
    /// <returns>The instant.</returns>
    private static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
