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
    /// Builds the key of the thumbnail of a final: the small image the galleries show. It uses the identifier of the photo.
    /// </summary>
    /// <param name="galleryId">Gallery the photo belongs to.</param>
    /// <param name="photoId">Identifier of the chosen photo.</param>
    /// <returns>The key in the object storage.</returns>
    public static string Thumb(Guid galleryId, Guid photoId) => $"galleries/{galleryId:D}/thumbs/{photoId:D}.jpg";

    /// <summary>
    /// Content type of the ZIP with the photos. The signed address of the storage only accepts this one.
    /// </summary>
    public const string ZipContentType = "application/zip";

    /// <summary>
    /// Name the client saves the ZIP with.
    /// </summary>
    public const string ZipFileName = "PhotoSense-fotos.zip";

    /// <summary>
    /// Builds the key of the ZIP of a gallery. A gallery has one, so uploading another replaces it.
    /// </summary>
    /// <param name="galleryId">Gallery the ZIP belongs to.</param>
    /// <returns>The key in the object storage.</returns>
    public static string Zip(Guid galleryId) => $"galleries/{galleryId:D}/zip/photos.zip";

    /// <summary>
    /// Builds the key of a proof.
    /// </summary>
    /// <param name="galleryId">Gallery the photo belongs to.</param>
    /// <param name="photoId">Photo identifier.</param>
    /// <returns>The key, for example <c>galleries/0197.../proofs/0197....jpg</c>.</returns>
    public static string Proof(Guid galleryId, Guid photoId) => $"galleries/{galleryId:D}/proofs/{photoId:D}.jpg";

    /// <summary>
    /// Builds the key of a final photo. It uses the identifier of the proof it belongs to, so a photo and its final share a name apart
    /// from the folder; the key never comes from the name of the file.
    /// </summary>
    /// <param name="galleryId">Gallery the photo belongs to.</param>
    /// <param name="photoId">Identifier of the chosen photo.</param>
    /// <returns>The key in the object storage.</returns>
    public static string Final(Guid galleryId, Guid photoId) => $"galleries/{galleryId:D}/finals/{photoId:D}.jpg";
}
