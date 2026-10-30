using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Storage;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries.Proofs;

/// <summary>
/// Command to get places to upload proofs.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery the proofs are for.</param>
/// <param name="Files">The files about to be uploaded.</param>
public sealed record RequestProofUploadsCommand(Guid PhotographerId, Guid GalleryId, IReadOnlyList<ProofFile> Files);

/// <summary>
/// Gives the photographer the signed addresses to upload proofs straight to the storage: the bytes never go through the API. Nothing
/// is stored in the gallery yet: a photo exists once the upload is registered, after the API has seen the object. The sizes the
/// photographer declares are checked here, and checked again against the real object when it is registered, because a signed address
/// cannot limit the size of what is sent to it.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
public sealed class RequestProofUploadsHandler(IGalleryRepository galleries, IObjectStorage storage)
    : ICommandHandler<RequestProofUploadsCommand, ProofUploadSlotsResponse>
{
    /// <summary>
    /// Most files a single request can ask places for.
    /// </summary>
    public const int MaxFilesPerRequest = 50;

    /// <summary>
    /// Creates the slots.
    /// </summary>
    /// <param name="command">The files.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>One slot per file, in order.</returns>
    /// <exception cref="Exceptions.NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When the gallery no longer takes proofs, they would not fit, or a file has a name or a size that is not valid.</exception>
    public async Task<ProofUploadSlotsResponse> HandleAsync(RequestProofUploadsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        if (gallery.Status != GalleryStatus.Pending)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {gallery.Status} no longer takes proofs.");
        }

        if (command.Files.Count is < 1 or > MaxFilesPerRequest)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, $"Ask for 1 to {MaxFilesPerRequest} files at a time.");
        }

        if (gallery.Photos.Count + command.Files.Count > Gallery.MaxPhotos)
        {
            throw new DomainException(DomainErrorCodes.GalleryFull, $"A gallery holds at most {Gallery.MaxPhotos} photos; this one has {gallery.Photos.Count}.");
        }

        foreach (var file in command.Files)
        {
            GalleryPhoto.EnsureValid(file.Name, file.Bytes);
        }

        var slots = new List<ProofUploadSlot>(command.Files.Count);
        foreach (var _ in command.Files)
        {
            var photoId = Guid.CreateVersion7();
            var url = await storage.CreateUploadUrlAsync(
                GalleryKeys.Proof(gallery.Id, photoId),
                GalleryKeys.ProofContentType,
                ObjectStorageLimits.MaxUploadLifetime,
                cancellationToken);
            slots.Add(new ProofUploadSlot(photoId, url.Url, url.RequiredHeaders, url.ExpiresAt));
        }

        return new ProofUploadSlotsResponse(slots);
    }
}
