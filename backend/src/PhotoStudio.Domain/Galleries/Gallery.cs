using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries.Events;

namespace PhotoStudio.Domain.Galleries;

/// <summary>
/// The delivery of the photos of a session: which photos the client may choose, and what an extra one costs. There is one per
/// booking and it is created by the system when the session is completed, not by anyone. The state machine of `CLAUDE.md`
/// §8.6 is built one transition per pull request; today the gallery is created and its terms can be edited.
/// </summary>
public sealed class Gallery : AggregateRoot<Guid>
{
    /// <summary>
    /// Most photos a gallery holds, and so the most a client can be allowed to pick (included plus gift).
    /// </summary>
    public const int MaxPhotos = 150;

    /// <summary>
    /// Most a ZIP with the photos can weigh: 500 MB. It is made by hand and sent in a single request.
    /// </summary>
    public const long MaxZipBytes = 524_288_000;

    /// <summary>
    /// Longest reason accepted when the selection is reopened.
    /// </summary>
    public const int MaxReasonLength = 500;

    /// <summary>
    /// Days after delivery when the photographer is told that the gallery can be released. The system does not delete anything then:
    /// the photographer decides.
    /// </summary>
    public const int ArchiveNoticeDays = 90;

    /// <summary>
    /// Days after delivery when the system archives the gallery by itself (G8), as a last resort for the ones the photographer never
    /// released: the photos and the ZIP are deleted from the storage.
    /// </summary>
    public const int RetentionDays = 180;

    /// <summary>
    /// Initializes a new instance of the <see cref="Gallery"/> class. Use <see cref="Create"/> or <see cref="Restore"/>.
    /// </summary>
    private readonly List<GalleryPhoto> _photos;

    /// <summary>The photos the client chose, in the order they were chosen.</summary>
    private readonly List<Guid> _selectedPhotoIds;

    private Gallery(
        Guid id,
        long version,
        Guid photographerId,
        Guid bookingId,
        GallerySnapshot snapshot,
        GalleryStatus status,
        int includedPhotos,
        int bonusPhotos,
        Money extraPhotoPrice,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IEnumerable<GalleryPhoto> photos,
        DateTimeOffset? selectionNotifiedAt,
        IEnumerable<Guid> selectedPhotoIds,
        DateTimeOffset? selectionSubmittedAt,
        DateTimeOffset? finalsPublishedAt,
        DateTimeOffset? deliveredAt,
        DateTimeOffset? deliveryNotifiedAt,
        DateTimeOffset? extrasReleasedAt,
        long? zipBytes,
        DateTimeOffset? zipUploadedAt,
        int zipDownloads,
        DateTimeOffset? zipLastDownloadedAt,
        DateTimeOffset? selectionReopenedAt,
        string? selectionReopenReason,
        DateTimeOffset? reopenNotifiedAt)
        : base(id, version)
    {
        PhotographerId = photographerId;
        BookingId = bookingId;
        Snapshot = snapshot;
        Status = status;
        IncludedPhotos = includedPhotos;
        BonusPhotos = bonusPhotos;
        ExtraPhotoPrice = extraPhotoPrice;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        _photos = [.. photos];
        SelectionNotifiedAt = selectionNotifiedAt;
        _selectedPhotoIds = [.. selectedPhotoIds];
        SelectionSubmittedAt = selectionSubmittedAt;
        FinalsPublishedAt = finalsPublishedAt;
        DeliveredAt = deliveredAt;
        DeliveryNotifiedAt = deliveryNotifiedAt;
        ExtrasReleasedAt = extrasReleasedAt;
        ZipBytes = zipBytes;
        ZipUploadedAt = zipUploadedAt;
        ZipDownloads = zipDownloads;
        ZipLastDownloadedAt = zipLastDownloadedAt;
        SelectionReopenedAt = selectionReopenedAt;
        SelectionReopenReason = selectionReopenReason;
        ReopenNotifiedAt = reopenNotifiedAt;
    }

    /// <summary>
    /// Gets the photographer (tenant) the gallery belongs to.
    /// </summary>
    public Guid PhotographerId { get; }

    /// <summary>
    /// Gets the booking the gallery comes from.
    /// </summary>
    public Guid BookingId { get; }

    /// <summary>
    /// Gets who the gallery is for and which session it comes from.
    /// </summary>
    public GallerySnapshot Snapshot { get; }

    /// <summary>
    /// Gets where the gallery is in its life.
    /// </summary>
    public GalleryStatus Status { get; private set; }

    /// <summary>
    /// Gets how many photos the package includes.
    /// </summary>
    public int IncludedPhotos { get; private set; }

    /// <summary>
    /// Gets how many photos the photographer gives on top of the included ones.
    /// </summary>
    public int BonusPhotos { get; private set; }

    /// <summary>
    /// Gets how many photos the client can pick without paying more: the included ones plus the gift.
    /// </summary>
    public int SelectablePhotos => IncludedPhotos + BonusPhotos;

    /// <summary>
    /// Gets the price of each photo the client picks beyond <see cref="SelectablePhotos"/>.
    /// </summary>
    public Money ExtraPhotoPrice { get; private set; }

    /// <summary>
    /// Gets the proofs of the gallery, in the order they were registered.
    /// </summary>
    public IReadOnlyList<GalleryPhoto> Photos => _photos;

    /// <summary>
    /// Gets the instant the client was told that the proofs are ready (UTC), or <see langword="null"/> while they were not. It makes
    /// the email idempotent: the outbox delivers an event at least once.
    /// </summary>
    public DateTimeOffset? SelectionNotifiedAt { get; private set; }

    /// <summary>
    /// Gets the photos the client chose, in the order they were chosen; empty until the client sends the selection.
    /// </summary>
    public IReadOnlyList<Guid> SelectedPhotoIds => _selectedPhotoIds;

    /// <summary>
    /// Gets the instant the client sent the selection (UTC), or <see langword="null"/> while they did not.
    /// </summary>
    public DateTimeOffset? SelectionSubmittedAt { get; private set; }

    /// <summary>
    /// Gets the instant the final photos of the package were published (G6, UTC), or <see langword="null"/> while they were not.
    /// </summary>
    public DateTimeOffset? FinalsPublishedAt { get; private set; }

    /// <summary>
    /// Gets the instant the gallery was delivered (G7, UTC), or <see langword="null"/> while it was not.
    /// </summary>
    public DateTimeOffset? DeliveredAt { get; private set; }

    /// <summary>
    /// Gets the instant the client was told that the delivery is ready (UTC), or <see langword="null"/> while they were not. It makes
    /// the email idempotent.
    /// </summary>
    public DateTimeOffset? DeliveryNotifiedAt { get; private set; }

    /// <summary>
    /// Gets the instant the photographer let the client download the extra photos (UTC), or <see langword="null"/> while they did not.
    /// The extras are agreed on and paid apart from the package, so they are released by hand.
    /// </summary>
    public DateTimeOffset? ExtrasReleasedAt { get; private set; }

    /// <summary>
    /// Gets the size of the ZIP with the photos, made and uploaded by hand by the photographer, or <see langword="null"/> while there
    /// is none.
    /// </summary>
    public long? ZipBytes { get; private set; }

    /// <summary>
    /// Gets the instant the ZIP was registered (UTC), or <see langword="null"/> while there is none.
    /// </summary>
    public DateTimeOffset? ZipUploadedAt { get; private set; }

    /// <summary>
    /// Gets how many times the client asked to download the ZIP.
    /// </summary>
    public int ZipDownloads { get; private set; }

    /// <summary>
    /// Gets the last instant the client asked to download the ZIP (UTC), or <see langword="null"/> if they never did.
    /// </summary>
    public DateTimeOffset? ZipLastDownloadedAt { get; private set; }

    /// <summary>
    /// Gets the last instant the photographer reopened the selection (UTC), or <see langword="null"/> if they never did.
    /// </summary>
    public DateTimeOffset? SelectionReopenedAt { get; private set; }

    /// <summary>
    /// Gets why the photographer last reopened the selection, or <see langword="null"/> if they never did. It is for the
    /// photographer's own record and is not sent to the client.
    /// </summary>
    public string? SelectionReopenReason { get; private set; }

    /// <summary>
    /// Gets the last instant the client was told that the selection was reopened (UTC), or <see langword="null"/> if they never were.
    /// It makes the email idempotent: a reopening is told once, and a later one is told again.
    /// </summary>
    public DateTimeOffset? ReopenNotifiedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the gallery has a ZIP.
    /// </summary>
    public bool HasZip => ZipBytes is not null;

    /// <summary>
    /// Gets the chosen photos that the package covers: the first ones the client chose, as many as <see cref="SelectablePhotos"/>.
    /// </summary>
    public IReadOnlyList<Guid> PackagePhotoIds => [.. _selectedPhotoIds.Take(SelectablePhotos)];

    /// <summary>
    /// Gets the chosen photos beyond the package, in the order the client chose them.
    /// </summary>
    public IReadOnlyList<Guid> ExtraPhotoIds => [.. _selectedPhotoIds.Skip(SelectablePhotos)];

    /// <summary>
    /// Gets how many of the chosen photos go beyond <see cref="SelectablePhotos"/>.
    /// </summary>
    public int ExtraPhotoCount => Math.Max(0, _selectedPhotoIds.Count - SelectablePhotos);

    /// <summary>
    /// Gets what the extra photos of the selection cost, with the price frozen in the gallery.
    /// </summary>
    public Money ExtraPhotoTotal => ExtraPhotoPrice.MultiplyBy(ExtraPhotoCount);

    /// <summary>
    /// Gets the instant the gallery was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the instant the gallery last changed (UTC).
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates the gallery of a completed session.
    /// </summary>
    /// <param name="photographerId">Photographer the booking belongs to.</param>
    /// <param name="bookingId">Booking the gallery comes from.</param>
    /// <param name="snapshot">Who the gallery is for and which session it comes from.</param>
    /// <param name="includedPhotos">Photos the package includes.</param>
    /// <param name="extraPhotoPrice">Price of each extra photo.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The gallery, waiting for the proofs.</returns>
    /// <exception cref="DomainException">When an identifier is missing or the photo count is not valid.</exception>
    public static Gallery Create(
        Guid photographerId,
        Guid bookingId,
        GallerySnapshot snapshot,
        int includedPhotos,
        Money extraPhotoPrice,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(extraPhotoPrice);

        if (photographerId == Guid.Empty || bookingId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The photographer and the booking of a gallery are required.");
        }

        EnsureValidCounts(includedPhotos, 0);

        return new Gallery(
            Guid.CreateVersion7(),
            0,
            photographerId,
            bookingId,
            snapshot,
            GalleryStatus.Pending,
            includedPhotos,
            0,
            extraPhotoPrice,
            now,
            now,
            [],
            null,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            null,
            null,
            null,
            null);
    }

    /// <summary>
    /// Rebuilds a gallery from persisted data, without validating it again.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="version">Stored version.</param>
    /// <param name="photographerId">Photographer.</param>
    /// <param name="bookingId">Booking.</param>
    /// <param name="snapshot">Snapshot of the client and the session.</param>
    /// <param name="status">State.</param>
    /// <param name="includedPhotos">Included photos.</param>
    /// <param name="bonusPhotos">Gift photos.</param>
    /// <param name="extraPhotoPrice">Price of an extra photo.</param>
    /// <param name="createdAt">Instant the gallery was created.</param>
    /// <param name="updatedAt">Instant the gallery last changed.</param>
    /// <param name="photos">The proofs registered so far.</param>
    /// <param name="selectionNotifiedAt">Instant the client was told the proofs are ready, if they were.</param>
    /// <param name="selectedPhotoIds">The photos the client chose, if they sent the selection.</param>
    /// <param name="selectionSubmittedAt">Instant the client sent the selection, if they did.</param>
    /// <param name="finalsPublishedAt">Instant the final photos were published, if they were.</param>
    /// <param name="deliveredAt">Instant the gallery was delivered, if it was.</param>
    /// <param name="deliveryNotifiedAt">Instant the client was told about the delivery, if they were.</param>
    /// <param name="extrasReleasedAt">Instant the extra photos were released, if they were.</param>
    /// <param name="zipBytes">Size of the ZIP, if there is one.</param>
    /// <param name="zipUploadedAt">Instant the ZIP was registered, if there is one.</param>
    /// <param name="zipDownloads">How many times the client asked for the ZIP.</param>
    /// <param name="zipLastDownloadedAt">Last instant the client asked for the ZIP, if they did.</param>
    /// <param name="selectionReopenedAt">Last instant the selection was reopened, if it was.</param>
    /// <param name="selectionReopenReason">Why the selection was last reopened, if it was.</param>
    /// <param name="reopenNotifiedAt">Last instant the client was told about a reopening, if they were.</param>
    /// <returns>The restored gallery.</returns>
    public static Gallery Restore(
        Guid id,
        long version,
        Guid photographerId,
        Guid bookingId,
        GallerySnapshot snapshot,
        GalleryStatus status,
        int includedPhotos,
        int bonusPhotos,
        Money extraPhotoPrice,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IEnumerable<GalleryPhoto>? photos = null,
        DateTimeOffset? selectionNotifiedAt = null,
        IEnumerable<Guid>? selectedPhotoIds = null,
        DateTimeOffset? selectionSubmittedAt = null,
        DateTimeOffset? finalsPublishedAt = null,
        DateTimeOffset? deliveredAt = null,
        DateTimeOffset? deliveryNotifiedAt = null,
        DateTimeOffset? extrasReleasedAt = null,
        long? zipBytes = null,
        DateTimeOffset? zipUploadedAt = null,
        int zipDownloads = 0,
        DateTimeOffset? zipLastDownloadedAt = null,
        DateTimeOffset? selectionReopenedAt = null,
        string? selectionReopenReason = null,
        DateTimeOffset? reopenNotifiedAt = null) =>
        new(id, version, photographerId, bookingId, snapshot, status, includedPhotos, bonusPhotos, extraPhotoPrice, createdAt, updatedAt, photos ?? [], selectionNotifiedAt, selectedPhotoIds ?? [], selectionSubmittedAt, finalsPublishedAt, deliveredAt, deliveryNotifiedAt, extrasReleasedAt, zipBytes, zipUploadedAt, zipDownloads, zipLastDownloadedAt, selectionReopenedAt, selectionReopenReason, reopenNotifiedAt);

    /// <summary>
    /// Changes the terms of the gallery: how many photos the package includes (for a custom package the photographer sets it
    /// here), how many are a gift, and what an extra photo costs. It is allowed until the client sends the selection, because
    /// from then on the selection and the charge for the extras were calculated with the old terms.
    /// </summary>
    /// <param name="includedPhotos">Photos the package includes.</param>
    /// <param name="bonusPhotos">Photos given on top.</param>
    /// <param name="extraPhotoPrice">Price of each extra photo, in the same currency as before.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the gallery no longer allows it, a count is not valid, or the currency changes.</exception>
    public void EditTerms(int includedPhotos, int bonusPhotos, Money extraPhotoPrice, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(extraPhotoPrice);

        if (!GetAllowedActions().Contains(GalleryAction.EditTerms))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"The terms of a gallery in state {Status} cannot be changed.");
        }

        EnsureValidCounts(includedPhotos, bonusPhotos);
        if (extraPhotoPrice.Currency != ExtraPhotoPrice.Currency)
        {
            throw new DomainException(DomainErrorCodes.CurrencyMismatch, "The price of an extra photo must keep the currency of the booking.");
        }

        IncludedPhotos = includedPhotos;
        BonusPhotos = bonusPhotos;
        ExtraPhotoPrice = extraPhotoPrice;
        UpdatedAt = now;
    }

    /// <summary>
    /// Registers a proof that was uploaded to the storage. Registering the same photo twice does nothing: a request that is repeated
    /// must not duplicate it.
    /// </summary>
    /// <param name="photoId">Identifier of the photo, which is part of its key in the storage.</param>
    /// <param name="originalName">Name of the file the photographer chose, as a label.</param>
    /// <param name="proofBytes">Size of the stored proof.</param>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the photo was added; <see langword="false"/> when it was already registered.</returns>
    /// <exception cref="DomainException">When the gallery no longer takes proofs, it is full, or the name or the size is not valid.</exception>
    public bool RegisterProof(Guid photoId, string originalName, long proofBytes, DateTimeOffset now)
    {
        if (photoId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The identifier of a photo is required.");
        }

        if (Status != GalleryStatus.Pending)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {Status} no longer takes proofs.");
        }

        if (_photos.Any(photo => photo.Id == photoId))
        {
            return false;
        }

        GalleryPhoto.EnsureValid(originalName, proofBytes);
        if (_photos.Count >= MaxPhotos)
        {
            throw new DomainException(DomainErrorCodes.GalleryFull, $"A gallery holds at most {MaxPhotos} photos.");
        }

        _photos.Add(new GalleryPhoto(photoId, originalName.Trim(), proofBytes, now));
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Removes a proof that was uploaded by mistake, while the selection is not open.
    /// </summary>
    /// <param name="photoId">Identifier of the photo.</param>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the photo was removed; <see langword="false"/> when the gallery had no such photo.</returns>
    /// <exception cref="DomainException">When the gallery no longer allows it.</exception>
    public bool RemoveProof(Guid photoId, DateTimeOffset now)
    {
        if (Status != GalleryStatus.Pending)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"The proofs of a gallery in state {Status} cannot be changed.");
        }

        var removed = _photos.RemoveAll(photo => photo.Id == photoId) > 0;
        if (removed)
        {
            UpdatedAt = now;
        }

        return removed;
    }

    /// <summary>
    /// Opens the selection (G2): the proofs are published and the client can choose from them. Raises <see cref="SelectionOpened"/>,
    /// which makes the system tell the client.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the gallery is not waiting for its proofs, or has none.</exception>
    public void OpenSelection(DateTimeOffset now)
    {
        if (Status != GalleryStatus.Pending)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"The selection of a gallery in state {Status} cannot be opened.");
        }

        if (_photos.Count == 0)
        {
            throw new DomainException(DomainErrorCodes.GalleryNoPhotos, "A gallery needs at least one proof before the client can choose.");
        }

        Status = GalleryStatus.InSelection;
        UpdatedAt = now;
        Raise(new SelectionOpened(Id, BookingId, PhotographerId, now));
    }

    /// <summary>
    /// Sends the selection (G3): the client chooses the photos the photographer will edit. Raises <see cref="SelectionSubmitted"/>,
    /// which makes the system tell the photographer and, later, charge the photos beyond what the gallery includes. From then on
    /// the selection is locked: only the photographer can reopen it. Sending the very same selection again does nothing, so a
    /// double click or a retry is safe.
    /// </summary>
    /// <param name="photoIds">Identifiers of the photos the client chose.</param>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the selection was recorded now; <see langword="false"/> when the same one was already recorded.</returns>
    /// <exception cref="DomainException">When the selection is not open, or the photos are none, repeated, or not of this gallery.</exception>
    public bool SubmitSelection(IReadOnlyCollection<Guid> photoIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(photoIds);

        if (SelectionSubmittedAt is not null && _selectedPhotoIds.ToHashSet().SetEquals(photoIds) && _selectedPhotoIds.Count == photoIds.Count)
        {
            return false;
        }

        if (Status != GalleryStatus.InSelection)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"The selection of a gallery in state {Status} cannot be sent.");
        }

        if (photoIds.Count == 0)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidSelection, "Choose at least one photo.");
        }

        var known = _photos.Select(photo => photo.Id).ToHashSet();
        if (photoIds.Distinct().Count() != photoIds.Count || photoIds.Any(photoId => !known.Contains(photoId)))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidSelection, "The selection has a photo that is repeated or is not in the gallery.");
        }

        _selectedPhotoIds.Clear();
        _selectedPhotoIds.AddRange(photoIds);
        SelectionSubmittedAt = now;
        Status = GalleryStatus.InEditing;
        UpdatedAt = now;
        Raise(new SelectionSubmitted(Id, BookingId, PhotographerId, _selectedPhotoIds.Count, ExtraPhotoCount, ExtraPhotoTotal.Amount, ExtraPhotoTotal.Currency, now));
        return true;
    }

    /// <summary>
    /// Tells whether a chosen photo is one of the extras, beyond what the package covers.
    /// </summary>
    /// <param name="photoId">Identifier of the photo.</param>
    /// <returns><see langword="true"/> when the photo was chosen and is beyond the package.</returns>
    public bool IsExtra(Guid photoId) => ExtraPhotoIds.Contains(photoId);

    /// <summary>
    /// Registers the final (edited) photo of a chosen photo, which was uploaded to the storage. The finals of the package can be
    /// registered or replaced until they are published; after that only those of the extras can, because the client may already be
    /// downloading the rest. Registering the same size again does nothing.
    /// </summary>
    /// <param name="photoId">Identifier of the chosen photo the final belongs to.</param>
    /// <param name="finalBytes">Size of the stored final photo.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="details">What the photographer's browser read from the file (size in pixels, when it was taken), if anything.</param>
    /// <returns><see langword="true"/> when the final was recorded now; <see langword="false"/> when it was already recorded.</returns>
    /// <exception cref="DomainException">When the gallery does not take finals in its state, the photo was not chosen, or the size is not valid.</exception>
    public bool RegisterFinal(Guid photoId, long finalBytes, DateTimeOffset now, FinalDetails? details = null)
    {
        if (Status is not (GalleryStatus.InEditing or GalleryStatus.Ready or GalleryStatus.Delivered))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {Status} does not take final photos.");
        }

        var index = _photos.FindIndex(photo => photo.Id == photoId);
        if (index < 0 || !_selectedPhotoIds.Contains(photoId))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, "Only a photo the client chose can have a final photo.");
        }

        if (Status != GalleryStatus.InEditing && !IsExtra(photoId))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, "The final photos of the package cannot change once they are published.");
        }

        GalleryPhoto.EnsureValidFinal(finalBytes);
        var kept = (details ?? new FinalDetails(null, null, null)).Normalize(now);
        var current = _photos[index];
        if (current.FinalBytes == finalBytes && (details is null || (current.FinalWidth == kept.Width && current.FinalHeight == kept.Height && current.FinalCapturedAt == kept.CapturedAt)))
        {
            return false;
        }

        // A file of another size is another file: what was said about the previous one, and its downloads, no longer apply.
        var replaced = current.FinalBytes != finalBytes;
        _photos[index] = current with
        {
            FinalBytes = finalBytes,
            FinalUploadedAt = replaced ? now : current.FinalUploadedAt,
            FinalWidth = kept.Width,
            FinalHeight = kept.Height,
            FinalCapturedAt = kept.CapturedAt,
            FinalDownloads = replaced ? 0 : current.FinalDownloads,
            FinalThumbBytes = replaced ? null : current.FinalThumbBytes,
        };
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Registers the thumbnail of a final: the small image the galleries show instead of the photo, which weighs megabytes. It is
    /// optional, because the final does not depend on it, and it follows the same rules as the final it belongs to.
    /// </summary>
    /// <param name="photoId">Identifier of the chosen photo.</param>
    /// <param name="thumbBytes">Size of the stored thumbnail.</param>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when it was recorded now; <see langword="false"/> when the same one was already recorded.</returns>
    /// <exception cref="DomainException">When the photo has no final, the final cannot change in this state, or the size is not valid.</exception>
    public bool RegisterFinalThumb(Guid photoId, long thumbBytes, DateTimeOffset now)
    {
        var index = _photos.FindIndex(photo => photo.Id == photoId && photo.HasFinal);
        if (index < 0)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, "Only a photo that has a final photo can have a thumbnail.");
        }

        if (Status != GalleryStatus.InEditing && !(IsExtra(photoId) && Status is GalleryStatus.Ready or GalleryStatus.Delivered))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, "The final photos of the package cannot change once they are published.");
        }

        if (thumbBytes is < 1 or > GalleryPhoto.MaxThumbBytes)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, $"A thumbnail must weigh between 1 byte and {GalleryPhoto.MaxThumbBytes} bytes.");
        }

        if (_photos[index].FinalThumbBytes == thumbBytes)
        {
            return false;
        }

        _photos[index] = _photos[index] with { FinalThumbBytes = thumbBytes };
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Forgets the final photo of a chosen photo, for example because the file that replaced it was refused. It follows the same rules
    /// as <see cref="RegisterFinal"/>.
    /// </summary>
    /// <param name="photoId">Identifier of the chosen photo.</param>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when a final was forgotten; <see langword="false"/> when there was none.</returns>
    /// <exception cref="DomainException">When the gallery does not take changes to the final of this photo.</exception>
    public bool RemoveFinal(Guid photoId, DateTimeOffset now)
    {
        var index = _photos.FindIndex(photo => photo.Id == photoId && photo.HasFinal);
        if (index < 0)
        {
            return false;
        }

        if (Status != GalleryStatus.InEditing && !(IsExtra(photoId) && Status is GalleryStatus.Ready or GalleryStatus.Delivered))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, "The final photos of the package cannot change once they are published.");
        }

        _photos[index] = _photos[index] with { FinalBytes = null, FinalUploadedAt = null, FinalWidth = null, FinalHeight = null, FinalCapturedAt = null, FinalDownloads = 0, FinalThumbBytes = null };
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Publishes the final photos of the package (G6). Every photo the package covers needs its final; the extras are not needed,
    /// because they are agreed on apart. Raises <see cref="FinalPhotosReady"/>, which makes the system deliver the gallery when the
    /// balance is zero.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the gallery is not being edited, or a photo of the package has no final.</exception>
    public void PublishFinals(DateTimeOffset now)
    {
        if (Status != GalleryStatus.InEditing)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"The final photos of a gallery in state {Status} cannot be published.");
        }

        if (!HasAllPackageFinals())
        {
            throw new DomainException(DomainErrorCodes.GalleryFinalsIncomplete, "Every photo of the package needs its final photo before publishing.");
        }

        Status = GalleryStatus.Ready;
        FinalsPublishedAt = now;
        UpdatedAt = now;
        Raise(new FinalPhotosReady(Id, BookingId, PhotographerId, PackagePhotoIds.Count, now));
    }

    /// <summary>
    /// Delivers the gallery (G7): the client can download the final photos. The caller checks that the balance is zero; the gallery
    /// does not know about money. Delivering a gallery that was already delivered does nothing.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when it was delivered now; <see langword="false"/> when it already was.</returns>
    /// <exception cref="DomainException">When the final photos are not published.</exception>
    public bool Deliver(DateTimeOffset now)
    {
        if (Status is GalleryStatus.Delivered or GalleryStatus.Archived)
        {
            return false;
        }

        if (Status != GalleryStatus.Ready)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {Status} cannot be delivered.");
        }

        Status = GalleryStatus.Delivered;
        DeliveredAt = now;
        UpdatedAt = now;
        Raise(new GalleryDelivered(Id, BookingId, PhotographerId, now));
        return true;
    }

    /// <summary>
    /// Archives the gallery (G8): from now on it has no files. The photographer does it to free the space before the term (Release) and
    /// the system does it when the retention ends. It cannot be undone, so the caller deletes the files of the storage first. The ZIP
    /// is forgotten too. Archiving an archived gallery does nothing.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when it was archived now; <see langword="false"/> when it already was.</returns>
    /// <exception cref="DomainException">When the gallery was not delivered: until then the client has not downloaded anything.</exception>
    public bool Archive(DateTimeOffset now)
    {
        if (Status == GalleryStatus.Archived)
        {
            return false;
        }

        if (Status != GalleryStatus.Delivered)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {Status} cannot be archived.");
        }

        RemoveZip(now);
        Status = GalleryStatus.Archived;
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Tells whether a delivered gallery has been so long that the photographer should be told it can be released.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the gallery is delivered and the notice term is over.</returns>
    public bool IsArchiveNoticeDue(DateTimeOffset now) => Status == GalleryStatus.Delivered && DeliveredAt is { } at && at.AddDays(ArchiveNoticeDays) <= now;

    /// <summary>
    /// Tells whether the retention of a delivered gallery has ended.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the gallery is delivered and its term is over.</returns>
    public bool IsRetentionOver(DateTimeOffset now) => Status == GalleryStatus.Delivered && DeliveredAt is { } at && at.AddDays(RetentionDays) <= now;

    /// <summary>
    /// Lets the client download the extra photos that already have their final. The photographer does it by hand once the client paid
    /// them; the system never charges them. Releasing twice does nothing.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when they were released now; <see langword="false"/> when they already were.</returns>
    /// <exception cref="DomainException">When the final photos are not published, or no extra photo has its final yet.</exception>
    public bool ReleaseExtras(DateTimeOffset now)
    {
        if (Status is not (GalleryStatus.Ready or GalleryStatus.Delivered))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"The extra photos of a gallery in state {Status} cannot be released.");
        }

        if (ExtrasReleasedAt is not null)
        {
            return false;
        }

        if (!ExtraPhotoIds.Any(HasFinal))
        {
            throw new DomainException(DomainErrorCodes.GalleryFinalsIncomplete, "No extra photo has its final photo yet.");
        }

        ExtrasReleasedAt = now;
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Tells whether the client may download the final photo of a chosen photo now: the gallery is delivered, the final exists, and
    /// if the photo is an extra the photographer released the extras.
    /// </summary>
    /// <param name="photoId">Identifier of the chosen photo.</param>
    /// <returns><see langword="true"/> when the client may download it.</returns>
    public bool IsDownloadable(Guid photoId) =>
        Status == GalleryStatus.Delivered && _selectedPhotoIds.Contains(photoId) && HasFinal(photoId) && (!IsExtra(photoId) || ExtrasReleasedAt is not null);

    /// <summary>
    /// Counts that the client asked to download some final photos. The caller gives them an address only if this does not throw.
    /// </summary>
    /// <param name="photoIds">Identifiers of the photos.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the gallery is not delivered, or a photo is not one the client may download.</exception>
    public void RecordFinalDownloads(IReadOnlyCollection<Guid> photoIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(photoIds);

        if (Status != GalleryStatus.Delivered)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {Status} cannot be downloaded.");
        }

        var distinct = photoIds.Distinct().ToList();
        if (distinct.Count == 0 || distinct.Any(photoId => !IsDownloadable(photoId)))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, "Only the final photos that were delivered can be downloaded.");
        }

        foreach (var photoId in distinct)
        {
            var index = _photos.FindIndex(photo => photo.Id == photoId);
            _photos[index] = _photos[index] with { FinalDownloads = _photos[index].FinalDownloads + 1 };
        }

        UpdatedAt = now;
    }

    /// <summary>
    /// Registers the ZIP with the photos that the photographer made and uploaded by hand. A ZIP of another size replaces the previous
    /// one and starts its count of downloads again.
    /// </summary>
    /// <param name="zipBytes">Size of the stored ZIP.</param>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when it was recorded now; <see langword="false"/> when the same one was already recorded.</returns>
    /// <exception cref="DomainException">When the final photos are not published, or the size is not between 1 byte and <see cref="MaxZipBytes"/>.</exception>
    public bool RegisterZip(long zipBytes, DateTimeOffset now)
    {
        if (Status is not (GalleryStatus.Ready or GalleryStatus.Delivered))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {Status} does not take a ZIP.");
        }

        if (zipBytes is < 1 or > MaxZipBytes)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidZip, $"A ZIP must weigh between 1 byte and {MaxZipBytes} bytes.");
        }

        if (ZipBytes == zipBytes)
        {
            return false;
        }

        ZipBytes = zipBytes;
        ZipUploadedAt = now;
        ZipDownloads = 0;
        ZipLastDownloadedAt = null;
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Forgets the ZIP, for example so the photographer can upload a new one. The photos stay.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when a ZIP was forgotten; <see langword="false"/> when there was none.</returns>
    public bool RemoveZip(DateTimeOffset now)
    {
        if (!HasZip)
        {
            return false;
        }

        ZipBytes = null;
        ZipUploadedAt = null;
        ZipDownloads = 0;
        ZipLastDownloadedAt = null;
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Counts that the client asked to download the ZIP. The caller gives them an address only if this does not throw.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the gallery is not delivered or has no ZIP.</exception>
    public void RecordZipDownload(DateTimeOffset now)
    {
        if (Status != GalleryStatus.Delivered || !HasZip)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, "There is no ZIP to download.");
        }

        ZipDownloads++;
        ZipLastDownloadedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Records that the client was told that the gallery was delivered.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when it was recorded now; <see langword="false"/> when it already was.</returns>
    public bool MarkDeliveryNotified(DateTimeOffset now)
    {
        if (DeliveryNotifiedAt is not null)
        {
            return false;
        }

        DeliveryNotifiedAt = now;
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Tells whether the final photo of a chosen photo was uploaded.
    /// </summary>
    /// <param name="photoId">Identifier of the chosen photo.</param>
    /// <returns><see langword="true"/> when its final was registered.</returns>
    public bool HasFinal(Guid photoId) => _photos.Any(photo => photo.Id == photoId && photo.HasFinal);

    /// <summary>
    /// Tells whether every photo the package covers has its final.
    /// </summary>
    /// <returns><see langword="true"/> when none is missing.</returns>
    private bool HasAllPackageFinals() => PackagePhotoIds.All(HasFinal);

    /// <summary>
    /// Reopens the selection (G5): the photographer lets the client choose again, for example because they asked to change their
    /// photos. What the client chose is discarded and the package and the extras are worked out again with the new choice. The final
    /// photos already uploaded are kept, tied to their photo, so choosing the same photo again finds its final waiting. Only possible
    /// before the final photos are published, because after that the client may already be downloading.
    /// </summary>
    /// <param name="reason">Why it is reopened; required and kept for the photographer's record.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the gallery is not in editing, or the reason is missing or too long.</exception>
    public void ReopenSelection(string? reason, DateTimeOffset now)
    {
        if (Status != GalleryStatus.InEditing)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"The selection of a gallery in state {Status} cannot be reopened.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(DomainErrorCodes.ReasonRequired, "A reason is required to reopen the selection.");
        }

        if (reason.Trim().Length > MaxReasonLength)
        {
            throw new DomainException(DomainErrorCodes.ReasonRequired, $"The reason cannot be longer than {MaxReasonLength} characters.");
        }

        _selectedPhotoIds.Clear();
        SelectionSubmittedAt = null;
        SelectionReopenedAt = now;
        SelectionReopenReason = reason.Trim();
        Status = GalleryStatus.InSelection;
        UpdatedAt = now;
        Raise(new SelectionReopened(Id, BookingId, PhotographerId, now));
    }

    /// <summary>
    /// Records that the client was told that the selection was reopened. A reopening is told once; the next one is told again.
    /// </summary>
    /// <param name="reopenedAt">The instant of the reopening the client was told about.</param>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when it was recorded now; <see langword="false"/> when that reopening was already told.</returns>
    public bool MarkReopenNotified(DateTimeOffset reopenedAt, DateTimeOffset now)
    {
        if (ReopenNotifiedAt is { } told && told >= reopenedAt)
        {
            return false;
        }

        ReopenNotifiedAt = now > reopenedAt ? now : reopenedAt;
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Records that the client was told that the proofs are ready.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when it was recorded now; <see langword="false"/> when it already was.</returns>
    public bool MarkClientNotified(DateTimeOffset now)
    {
        if (SelectionNotifiedAt is not null)
        {
            return false;
        }

        SelectionNotifiedAt = now;
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Lists what the photographer can do with the gallery in its current state.
    /// </summary>
    /// <returns>The allowed actions.</returns>
    public IReadOnlyList<GalleryAction> GetAllowedActions() => Status switch
    {
        GalleryStatus.Pending => PendingActions(),
        GalleryStatus.InSelection => [GalleryAction.EditTerms],
        GalleryStatus.InEditing => EditingActions(),
        GalleryStatus.Ready or GalleryStatus.Delivered => DeliveryActions(),
        _ => [],
    };

    /// <summary>
    /// Lists the actions of a gallery whose selection the photographer is editing.
    /// </summary>
    /// <returns>The allowed actions.</returns>
    private List<GalleryAction> EditingActions()
    {
        var actions = new List<GalleryAction> { GalleryAction.UploadFinals, GalleryAction.ReopenSelection };
        if (HasAllPackageFinals())
        {
            actions.Add(GalleryAction.PublishFinals);
        }

        return actions;
    }

    /// <summary>
    /// Lists the actions of a gallery whose final photos are published: only the extras can still change.
    /// </summary>
    /// <returns>The allowed actions.</returns>
    private List<GalleryAction> DeliveryActions()
    {
        var actions = new List<GalleryAction>();
        if (ExtraPhotoIds.Count > 0)
        {
            actions.Add(GalleryAction.UploadFinals);
        }

        if (ExtrasReleasedAt is null && ExtraPhotoIds.Any(HasFinal))
        {
            actions.Add(GalleryAction.ReleaseExtras);
        }

        actions.Add(GalleryAction.UploadZip);
        if (HasZip)
        {
            actions.Add(GalleryAction.RemoveZip);
        }

        if (Status == GalleryStatus.Delivered)
        {
            actions.Add(GalleryAction.Release);
        }

        return actions;
    }

    /// <summary>
    /// Lists the actions of a gallery that is waiting for its proofs.
    /// </summary>
    /// <returns>The allowed actions.</returns>
    private List<GalleryAction> PendingActions()
    {
        var actions = new List<GalleryAction> { GalleryAction.EditTerms };
        if (_photos.Count < MaxPhotos)
        {
            actions.Add(GalleryAction.UploadProofs);
        }

        if (_photos.Count > 0)
        {
            actions.Add(GalleryAction.RemoveProof);
            actions.Add(GalleryAction.OpenSelection);
        }

        return actions;
    }

    /// <summary>
    /// Checks that the photo counts make sense: none negative, and no more selectable photos than a gallery can hold.
    /// </summary>
    /// <param name="includedPhotos">Photos the package includes.</param>
    /// <param name="bonusPhotos">Photos given on top.</param>
    /// <exception cref="DomainException">When a count is negative or the two add up to more than <see cref="MaxPhotos"/>.</exception>
    private static void EnsureValidCounts(int includedPhotos, int bonusPhotos)
    {
        if (includedPhotos < 0 || bonusPhotos < 0)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidTerms, "The photo counts cannot be negative.");
        }

        if (includedPhotos + bonusPhotos > MaxPhotos)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidTerms, $"A gallery holds at most {MaxPhotos} photos, counting the gift ones.");
        }
    }
}
