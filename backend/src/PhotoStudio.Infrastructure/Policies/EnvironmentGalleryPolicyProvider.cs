using System.Globalization;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Infrastructure.Policies;

/// <summary>
/// Reads the gallery policy from environment variables, falling back to <see cref="GalleryPolicy.Default"/>: 15, 30 and 50
/// photos (<c>GALLERY_INCLUDED_PHOTOS_BASIC</c>, <c>_STANDARD</c>, <c>_PREMIUM</c>) and ₡3 000 per extra photo
/// (<c>GALLERY_EXTRA_PHOTO_PRICE</c>). Temporary until each photographer can configure their own policy from the app.
/// </summary>
public sealed class EnvironmentGalleryPolicyProvider : IGalleryPolicyProvider
{
    private readonly GalleryPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentGalleryPolicyProvider"/> class and reads the variables once.
    /// </summary>
    /// <exception cref="InvalidOperationException">When a variable is set to something that is not a valid number.</exception>
    public EnvironmentGalleryPolicyProvider()
    {
        var defaults = GalleryPolicy.Default;
        try
        {
            _policy = GalleryPolicy.Create(
                ReadInt("GALLERY_INCLUDED_PHOTOS_BASIC", defaults.IncludedBasic),
                ReadInt("GALLERY_INCLUDED_PHOTOS_STANDARD", defaults.IncludedStandard),
                ReadInt("GALLERY_INCLUDED_PHOTOS_PREMIUM", defaults.IncludedPremium),
                ReadDecimal("GALLERY_EXTRA_PHOTO_PRICE", defaults.ExtraPhotoPrice));
        }
        catch (DomainException exception)
        {
            throw new InvalidOperationException($"The gallery policy variables are not valid: {exception.Message}", exception);
        }
    }

    /// <inheritdoc />
    public Task<GalleryPolicy> GetPolicyAsync(Guid photographerId, CancellationToken cancellationToken) => Task.FromResult(_policy);

    /// <summary>
    /// Reads an integer variable.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="fallback">Value used when the variable is missing.</param>
    /// <returns>The parsed value or the fallback.</returns>
    /// <exception cref="InvalidOperationException">When the variable is present but not an integer.</exception>
    private static int ReadInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"The environment variable '{name}' must be an integer.");
    }

    /// <summary>
    /// Reads a decimal variable using the invariant culture (dot as decimal separator).
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="fallback">Value used when the variable is missing.</param>
    /// <returns>The parsed value or the fallback.</returns>
    /// <exception cref="InvalidOperationException">When the variable is present but not a number.</exception>
    private static decimal ReadDecimal(string name, decimal fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"The environment variable '{name}' must be a number.");
    }
}
