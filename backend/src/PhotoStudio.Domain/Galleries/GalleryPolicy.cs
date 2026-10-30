using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Galleries;

/// <summary>
/// How many photos each package includes and what an extra photo costs. A gallery copies these values when it is created, so
/// changing the policy never affects galleries that already exist; the photographer can still adjust them in one gallery.
/// </summary>
public sealed record GalleryPolicy
{
    /// <summary>
    /// Gets the policy used when nothing is configured: 15, 30 and 50 photos, and ₡3 000 per extra photo.
    /// </summary>
    public static GalleryPolicy Default { get; } = new(15, 30, 50, 3_000m);

    private GalleryPolicy(int includedBasic, int includedStandard, int includedPremium, decimal extraPhotoPrice)
    {
        IncludedBasic = includedBasic;
        IncludedStandard = includedStandard;
        IncludedPremium = includedPremium;
        ExtraPhotoPrice = extraPhotoPrice;
    }

    /// <summary>
    /// Gets how many photos the basic package includes.
    /// </summary>
    public int IncludedBasic { get; }

    /// <summary>
    /// Gets how many photos the standard package includes.
    /// </summary>
    public int IncludedStandard { get; }

    /// <summary>
    /// Gets how many photos the premium package includes.
    /// </summary>
    public int IncludedPremium { get; }

    /// <summary>
    /// Gets the price of each photo the client picks beyond the included ones, in the currency of the booking.
    /// </summary>
    public decimal ExtraPhotoPrice { get; }

    /// <summary>
    /// Creates a validated policy.
    /// </summary>
    /// <param name="includedBasic">Photos of the basic package.</param>
    /// <param name="includedStandard">Photos of the standard package.</param>
    /// <param name="includedPremium">Photos of the premium package.</param>
    /// <param name="extraPhotoPrice">Price of an extra photo.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="DomainException">When a count is outside 0 to <see cref="Gallery.MaxPhotos"/> or the price is negative.</exception>
    public static GalleryPolicy Create(int includedBasic, int includedStandard, int includedPremium, decimal extraPhotoPrice)
    {
        foreach (var count in new[] { includedBasic, includedStandard, includedPremium })
        {
            if (count is < 0 or > Gallery.MaxPhotos)
            {
                throw new DomainException(DomainErrorCodes.GalleryInvalidTerms, $"The included photos must be between 0 and {Gallery.MaxPhotos}.");
            }
        }

        if (extraPhotoPrice < 0m)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidTerms, "The price of an extra photo cannot be negative.");
        }

        return new GalleryPolicy(includedBasic, includedStandard, includedPremium, extraPhotoPrice);
    }

    /// <summary>
    /// Gives the photos a package includes. A custom or other package, and a booking made before packages had a size, include
    /// none: the photographer sets them in the gallery.
    /// </summary>
    /// <param name="tier">Package of the booking, if it has one.</param>
    /// <returns>The number of included photos.</returns>
    public int IncludedFor(PackageTier? tier) => tier switch
    {
        PackageTier.Basic => IncludedBasic,
        PackageTier.Standard => IncludedStandard,
        PackageTier.Premium => IncludedPremium,
        _ => 0,
    };
}
