using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Galleries;

/// <summary>
/// A proof of the gallery: a small photo, in low quality and with the watermark in the middle, that the client looks at to choose.
/// The bytes live in the object storage under a key built from the gallery and the photo, so the aggregate only keeps what the
/// photographer and the client see: the name of the file it came from (to find it in Lightroom) and how much it weighs.
/// </summary>
/// <param name="Id">Photo identifier; part of the key in the storage, never derived from the name.</param>
/// <param name="OriginalName">Name of the file the photographer chose, as a label.</param>
/// <param name="ProofBytes">Size of the stored proof in bytes.</param>
/// <param name="UploadedAt">Instant the proof was registered (UTC).</param>
/// <param name="FinalBytes">Size of the final (edited) photo in bytes, or <see langword="null"/> while the photographer has not uploaded it.</param>
/// <param name="FinalUploadedAt">Instant the final photo was registered (UTC), or <see langword="null"/> while it was not.</param>
/// <param name="FinalWidth">Width of the final in pixels, as read by the photographer's browser, or <see langword="null"/> when unknown.</param>
/// <param name="FinalHeight">Height of the final in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="FinalCapturedAt">Instant the final was taken, from its metadata, or <see langword="null"/> when unknown.</param>
/// <param name="FinalDownloads">How many times the client asked to download the final.</param>
public sealed record GalleryPhoto(
    Guid Id,
    string OriginalName,
    long ProofBytes,
    DateTimeOffset UploadedAt,
    long? FinalBytes = null,
    DateTimeOffset? FinalUploadedAt = null,
    int? FinalWidth = null,
    int? FinalHeight = null,
    DateTimeOffset? FinalCapturedAt = null,
    int FinalDownloads = 0)
{
    /// <summary>The most a final photo can weigh.</summary>
    public const long MaxFinalBytes = 52_428_800;

    /// <summary>Gets a value indicating whether the final photo was uploaded.</summary>
    public bool HasFinal => FinalBytes is not null;

    /// <summary>
    /// Checks the size of a final photo before it is accepted.
    /// </summary>
    /// <param name="finalBytes">Size of the final photo in bytes.</param>
    /// <exception cref="DomainException">When the size is not between 1 byte and <see cref="MaxFinalBytes"/>.</exception>
    public static void EnsureValidFinal(long finalBytes)
    {
        if (finalBytes is < 1 or > MaxFinalBytes)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, $"A final photo must weigh between 1 byte and {MaxFinalBytes} bytes.");
        }
    }

    /// <summary>The most a proof can weigh: they are deliberately small and of low quality.</summary>
    public const long MaxProofBytes = 1_048_576;

    /// <summary>The longest name accepted for the file a photo came from.</summary>
    public const int MaxNameLength = 200;

    /// <summary>
    /// Checks a name and a size before a photo is accepted.
    /// </summary>
    /// <param name="originalName">Name of the file.</param>
    /// <param name="proofBytes">Size of the proof in bytes.</param>
    /// <exception cref="DomainException">When the name is empty, too long or has control characters, or the size is not between 1 byte and <see cref="MaxProofBytes"/>.</exception>
    public static void EnsureValid(string? originalName, long proofBytes)
    {
        if (string.IsNullOrWhiteSpace(originalName)
            || originalName.Trim().Length > MaxNameLength
            || originalName.Any(char.IsControl))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, $"The name of a photo must have 1 to {MaxNameLength} characters and no control characters.");
        }

        if (proofBytes is < 1 or > MaxProofBytes)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, $"A proof must weigh between 1 byte and {MaxProofBytes} bytes.");
        }
    }
}
