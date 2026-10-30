using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries.Proofs;

/// <summary>
/// Command to register proofs that were uploaded.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery the proofs belong to.</param>
/// <param name="Photos">The uploads to register.</param>
public sealed record RegisterProofsCommand(Guid PhotographerId, Guid GalleryId, IReadOnlyList<ProofRegistration> Photos);

/// <summary>
/// Registers the proofs that were uploaded, after looking at what is really in the storage: the object must exist, declare the
/// content type of a JPEG and weigh between 1 byte and the limit. An object that fails any of that is deleted and reported with its
/// reason, so nothing the photographer did not mean to publish, and nothing too big, stays. Registering what is already registered
/// does nothing, so a request that is repeated is safe. Two batches registered at the same time may collide on the version of the
/// gallery; the registration is additive, so the handler reloads and tries again.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RegisterProofsHandler(IGalleryRepository galleries, IObjectStorage storage, TimeProvider timeProvider)
    : ICommandHandler<RegisterProofsCommand, RegisterProofsResponse>
{
    private const int Attempts = 3;

    /// <summary>
    /// Registers the proofs.
    /// </summary>
    /// <param name="command">The uploads.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery and the uploads that were not accepted.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When the gallery no longer takes proofs or the request has too many files.</exception>
    /// <exception cref="ConflictException">When the gallery kept changing under the request.</exception>
    public async Task<RegisterProofsResponse> HandleAsync(RegisterProofsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Photos.Count is < 1 or > RequestProofUploadsHandler.MaxFilesPerRequest)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, $"Register 1 to {RequestProofUploadsHandler.MaxFilesPerRequest} files at a time.");
        }

        // What the storage says about each object does not change between attempts, so it is read once.
        var inspected = new List<(ProofRegistration Photo, string? Reason, long Bytes)>(command.Photos.Count);
        foreach (var photo in command.Photos.DistinctBy(registration => registration.PhotoId))
        {
            inspected.Add(await InspectAsync(command.GalleryId, photo, cancellationToken));
        }

        for (var attempt = 1; ; attempt++)
        {
            var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
            if (gallery.Status != GalleryStatus.Pending)
            {
                throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {gallery.Status} no longer takes proofs.");
            }

            var rejected = new List<ProofRejection>();
            var added = false;
            foreach (var (photo, reason, bytes) in inspected)
            {
                if (reason is not null)
                {
                    rejected.Add(new ProofRejection(photo.PhotoId, reason));
                    continue;
                }

                try
                {
                    added |= gallery.RegisterProof(photo.PhotoId, photo.Name, bytes, timeProvider.GetUtcNow());
                }
                catch (DomainException exception) when (exception.Code is DomainErrorCodes.GalleryFull or DomainErrorCodes.GalleryInvalidPhoto)
                {
                    rejected.Add(new ProofRejection(photo.PhotoId, exception.Code == DomainErrorCodes.GalleryFull ? "gallery_full" : "invalid_name"));
                }
            }

            try
            {
                if (added)
                {
                    await galleries.UpdateAsync(gallery, cancellationToken);
                }
            }
            catch (ConflictException) when (attempt < Attempts)
            {
                continue;
            }

            // What could not be kept is not left in the storage.
            foreach (var rejection in rejected)
            {
                await storage.DeleteAsync(GalleryKeys.Proof(command.GalleryId, rejection.PhotoId), cancellationToken);
            }

            return new RegisterProofsResponse(gallery.ToResponse(), rejected);
        }
    }

    /// <summary>
    /// Looks at the object an upload left in the storage.
    /// </summary>
    /// <param name="galleryId">Gallery the photo belongs to.</param>
    /// <param name="photo">The upload.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The upload, the reason it cannot be kept (or <see langword="null"/> when it can) and the size of the object.</returns>
    private async Task<(ProofRegistration Photo, string? Reason, long Bytes)> InspectAsync(Guid galleryId, ProofRegistration photo, CancellationToken cancellationToken)
    {
        if (photo.PhotoId == Guid.Empty)
        {
            return (photo, "missing", 0);
        }

        var info = await storage.GetInfoAsync(GalleryKeys.Proof(galleryId, photo.PhotoId), cancellationToken);
        if (info is null)
        {
            return (photo, "missing", 0);
        }

        if (!string.Equals(info.ContentType, GalleryKeys.ProofContentType, StringComparison.OrdinalIgnoreCase))
        {
            return (photo, "wrong_type", info.Size);
        }

        if (info.Size < 1)
        {
            return (photo, "empty", info.Size);
        }

        if (info.Size > GalleryPhoto.MaxProofBytes)
        {
            return (photo, "too_big", info.Size);
        }

        try
        {
            GalleryPhoto.EnsureValid(photo.Name, info.Size);
        }
        catch (DomainException)
        {
            return (photo, "invalid_name", info.Size);
        }

        return (photo, null, info.Size);
    }
}
