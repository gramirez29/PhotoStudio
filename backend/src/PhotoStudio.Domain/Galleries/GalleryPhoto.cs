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
public sealed record GalleryPhoto(Guid Id, string OriginalName, long ProofBytes, DateTimeOffset UploadedAt)
{
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
