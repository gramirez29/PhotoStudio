using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Domain.UnitTests.Galleries;

/// <summary>
/// Tests of <see cref="GalleryPolicy"/>: the photos each package includes and the price of an extra one.
/// </summary>
public sealed class GalleryPolicyTests
{
    /// <summary>
    /// The default policy is the one agreed with the photographer: 15, 30 and 50 photos, ₡3 000 per extra photo.
    /// </summary>
    [Fact]
    public void Default_IsFifteenThirtyFiftyAndThreeThousand()
    {
        var policy = GalleryPolicy.Default;

        policy.IncludedFor(PackageTier.Basic).ShouldBe(15);
        policy.IncludedFor(PackageTier.Standard).ShouldBe(30);
        policy.IncludedFor(PackageTier.Premium).ShouldBe(50);
        policy.ExtraPhotoPrice.ShouldBe(3_000m);
    }

    /// <summary>
    /// A custom package, any other arrangement and a booking from before packages had a size include no photos: the
    /// photographer sets them in the gallery.
    /// </summary>
    /// <param name="tier">The package, if the booking has one.</param>
    [Theory]
    [InlineData(PackageTier.Custom)]
    [InlineData(PackageTier.Other)]
    [InlineData(null)]
    public void IncludedFor_APackageWithoutASize_IsZero(PackageTier? tier) => GalleryPolicy.Default.IncludedFor(tier).ShouldBe(0);

    /// <summary>
    /// A policy built from other values answers with them.
    /// </summary>
    [Fact]
    public void Create_UsesTheValuesGiven()
    {
        var policy = GalleryPolicy.Create(10, 20, 40, 2_500.5m);

        policy.IncludedFor(PackageTier.Basic).ShouldBe(10);
        policy.IncludedFor(PackageTier.Standard).ShouldBe(20);
        policy.IncludedFor(PackageTier.Premium).ShouldBe(40);
        policy.ExtraPhotoPrice.ShouldBe(2_500.5m);
    }

    /// <summary>
    /// A policy with a count out of bounds or a negative price is refused.
    /// </summary>
    /// <param name="basic">Photos of the basic package.</param>
    /// <param name="standard">Photos of the standard package.</param>
    /// <param name="premium">Photos of the premium package.</param>
    /// <param name="price">Price of an extra photo.</param>
    [Theory]
    [InlineData(-1, 30, 50, 3000)]
    [InlineData(15, Gallery.MaxPhotos + 1, 50, 3000)]
    [InlineData(15, 30, -5, 3000)]
    [InlineData(15, 30, 50, -1)]
    public void Create_WithInvalidValues_IsRefused(int basic, int standard, int premium, int price) =>
        Should.Throw<DomainException>(() => GalleryPolicy.Create(basic, standard, premium, price)).Code.ShouldBe(DomainErrorCodes.GalleryInvalidTerms);
}
