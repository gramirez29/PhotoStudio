namespace PhotoStudio.Application.Galleries;

/// <summary>
/// Where the photos of a gallery live in the object storage. Keys are built only from identifiers the backend generated (never from a
/// name the photographer typed or a file name), so they are always valid and cannot climb out of their prefix.
/// </summary>
public static class GalleryKeys
{
    /// <summary>Content type of a proof; the storage refuses an upload that does not declare exactly this.</summary>
    public const string ProofContentType = "image/jpeg";

    /// <summary>
    /// Builds the key of a proof.
    /// </summary>
    /// <param name="galleryId">Gallery the photo belongs to.</param>
    /// <param name="photoId">Photo identifier.</param>
    /// <returns>The key, for example <c>galleries/0197.../proofs/0197....jpg</c>.</returns>
    public static string Proof(Guid galleryId, Guid photoId) => $"galleries/{galleryId:D}/proofs/{photoId:D}.jpg";
}
