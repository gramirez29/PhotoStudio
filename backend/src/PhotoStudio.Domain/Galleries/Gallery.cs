using PhotoStudio.Domain.Common;

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
        DateTimeOffset updatedAt)
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
            now);
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
        DateTimeOffset updatedAt) =>
        new(id, version, photographerId, bookingId, snapshot, status, includedPhotos, bonusPhotos, extraPhotoPrice, createdAt, updatedAt);

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
    /// Lists what the photographer can do with the gallery in its current state.
    /// </summary>
    /// <returns>The allowed actions.</returns>
    public IReadOnlyList<GalleryAction> GetAllowedActions() => Status switch
    {
        GalleryStatus.Pending or GalleryStatus.InSelection => [GalleryAction.EditTerms],
        _ => [],
    };

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
