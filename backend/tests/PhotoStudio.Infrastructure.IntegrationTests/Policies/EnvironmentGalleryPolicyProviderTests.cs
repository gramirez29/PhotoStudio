using PhotoStudio.Domain.Bookings;
using PhotoStudio.Infrastructure.IntegrationTests.Storage;
using PhotoStudio.Infrastructure.Policies;

namespace PhotoStudio.Infrastructure.IntegrationTests.Policies;

/// <summary>
/// Tests of <see cref="EnvironmentGalleryPolicyProvider"/>: the gallery policy read from environment variables.
/// </summary>
[Collection(EnvironmentCollection.Name)]
public sealed class EnvironmentGalleryPolicyProviderTests : IDisposable
{
    private static readonly string[] Variables =
    [
        "GALLERY_INCLUDED_PHOTOS_BASIC",
        "GALLERY_INCLUDED_PHOTOS_STANDARD",
        "GALLERY_INCLUDED_PHOTOS_PREMIUM",
        "GALLERY_EXTRA_PHOTO_PRICE",
    ];

    private readonly Dictionary<string, string?> _saved = Variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentGalleryPolicyProviderTests"/> class, clearing the variables.
    /// </summary>
    public EnvironmentGalleryPolicyProviderTests()
    {
        foreach (var name in Variables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    /// <summary>
    /// Puts the variables back as they were.
    /// </summary>
    public void Dispose()
    {
        foreach (var (name, value) in _saved)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    /// <summary>
    /// With no variable set the policy is the agreed one: 15, 30, 50 photos and ₡3 000.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WithNothingSet_IsTheDefaultPolicy()
    {
        var policy = await new EnvironmentGalleryPolicyProvider().GetPolicyAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        policy.IncludedFor(PackageTier.Basic).ShouldBe(15);
        policy.IncludedFor(PackageTier.Standard).ShouldBe(30);
        policy.IncludedFor(PackageTier.Premium).ShouldBe(50);
        policy.ExtraPhotoPrice.ShouldBe(3_000m);
    }

    /// <summary>
    /// The variables replace the defaults, with a dot as the decimal separator.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task TheVariablesReplaceTheDefaults()
    {
        Environment.SetEnvironmentVariable("GALLERY_INCLUDED_PHOTOS_BASIC", "10");
        Environment.SetEnvironmentVariable("GALLERY_INCLUDED_PHOTOS_STANDARD", "25");
        Environment.SetEnvironmentVariable("GALLERY_INCLUDED_PHOTOS_PREMIUM", "60");
        Environment.SetEnvironmentVariable("GALLERY_EXTRA_PHOTO_PRICE", "2500.50");

        var policy = await new EnvironmentGalleryPolicyProvider().GetPolicyAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        policy.IncludedFor(PackageTier.Basic).ShouldBe(10);
        policy.IncludedFor(PackageTier.Standard).ShouldBe(25);
        policy.IncludedFor(PackageTier.Premium).ShouldBe(60);
        policy.ExtraPhotoPrice.ShouldBe(2_500.5m);
    }

    /// <summary>
    /// A variable that is not a number, or a value out of bounds, stops the process at startup instead of being ignored.
    /// </summary>
    /// <param name="name">The variable.</param>
    /// <param name="value">Its wrong value.</param>
    [Theory]
    [InlineData("GALLERY_INCLUDED_PHOTOS_BASIC", "quince")]
    [InlineData("GALLERY_INCLUDED_PHOTOS_STANDARD", "151")]
    [InlineData("GALLERY_INCLUDED_PHOTOS_PREMIUM", "-1")]
    [InlineData("GALLERY_EXTRA_PHOTO_PRICE", "tres mil")]
    [InlineData("GALLERY_EXTRA_PHOTO_PRICE", "-1")]
    public void WithAnInvalidValue_FailsAtStartup(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);

        Should.Throw<InvalidOperationException>(() => new EnvironmentGalleryPolicyProvider());
    }
}
