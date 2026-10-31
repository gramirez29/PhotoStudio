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
        DateTimeOffset? selectionSubmittedAt)
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
        DateTimeOffset? selectionSubmittedAt = null) =>
        new(id, version, photographerId, bookingId, snapshot, status, includedPhotos, bonusPhotos, extraPhotoPrice, createdAt, updatedAt, photos ?? [], selectionNotifiedAt, selectedPhotoIds ?? [], selectionSubmittedAt);

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
        _ => [],
    };

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
