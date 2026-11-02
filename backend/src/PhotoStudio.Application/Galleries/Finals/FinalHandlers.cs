using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Galleries.Proofs;
using PhotoStudio.Application.Storage;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries.Finals;

/// <summary>
/// One final photo the photographer is about to upload.
/// </summary>
/// <param name="PhotoId">Identifier of the chosen photo the final belongs to (the identifier of its proof).</param>
/// <param name="Bytes">Size of the file in bytes, as declared by the photographer.</param>
public sealed record FinalFile(Guid PhotoId, long Bytes);

/// <summary>
/// A signed place to upload one final photo.
/// </summary>
/// <param name="PhotoId">Identifier of the chosen photo the final belongs to.</param>
/// <param name="Url">Signed address to send the file to with a PUT.</param>
/// <param name="Headers">Headers that must go with the request.</param>
/// <param name="ExpiresAt">Instant the address stops working (UTC).</param>
public sealed record FinalUploadSlot(Guid PhotoId, Uri Url, IReadOnlyDictionary<string, string> Headers, DateTimeOffset ExpiresAt);

/// <summary>
/// The places to upload a batch of final photos, in the order they were asked.
/// </summary>
/// <param name="Slots">One slot per file.</param>
public sealed record FinalUploadSlotsResponse(IReadOnlyList<FinalUploadSlot> Slots);

/// <summary>
/// A final photo that was not kept, and why.
/// </summary>
/// <param name="PhotoId">Identifier of the chosen photo.</param>
/// <param name="Reason">Short code: <c>missing</c>, <c>wrong_type</c>, <c>empty</c>, <c>too_big</c>, <c>not_chosen</c> or <c>locked</c>.</param>
public sealed record FinalRejection(Guid PhotoId, string Reason);

/// <summary>
/// The result of registering a batch of final photos.
/// </summary>
/// <param name="Gallery">The gallery after registering.</param>
/// <param name="Rejected">The uploads that were not kept.</param>
public sealed record RegisterFinalsResponse(GalleryResponse Gallery, IReadOnlyList<FinalRejection> Rejected);

/// <summary>
/// Command to get places to upload final photos.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery the finals are for.</param>
/// <param name="Files">The files about to be uploaded.</param>
public sealed record RequestFinalUploadsCommand(Guid PhotographerId, Guid GalleryId, IReadOnlyList<FinalFile> Files);

/// <summary>
/// Command to register final photos that were uploaded.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery the finals belong to.</param>
/// <param name="PhotoIds">The chosen photos whose finals were uploaded.</param>
public sealed record RegisterFinalsCommand(Guid PhotographerId, Guid GalleryId, IReadOnlyList<Guid> PhotoIds);

/// <summary>
/// Command to publish the final photos of the package (G6).
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery to publish.</param>
public sealed record PublishFinalsCommand(Guid PhotographerId, Guid GalleryId);

/// <summary>
/// Command to let the client download the extra photos.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery whose extras are released.</param>
public sealed record ReleaseExtrasCommand(Guid PhotographerId, Guid GalleryId);

/// <summary>
/// Gives the photographer the signed addresses to upload final photos straight to the storage, like the proofs: the bytes never go
/// through the API. A final belongs to a photo the client chose; after the package was published, only the extras can still get
/// theirs. The size the photographer declares is checked here and again against the real object when it is registered.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
public sealed class RequestFinalUploadsHandler(IGalleryRepository galleries, IObjectStorage storage)
    : ICommandHandler<RequestFinalUploadsCommand, FinalUploadSlotsResponse>
{
    /// <summary>
    /// Creates the slots.
    /// </summary>
    /// <param name="command">The files.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>One slot per file, in order.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When the gallery does not take finals, a photo was not chosen, or a size is not valid.</exception>
    public async Task<FinalUploadSlotsResponse> HandleAsync(RequestFinalUploadsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        if (!gallery.GetAllowedActions().Contains(GalleryAction.UploadFinals))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {gallery.Status} does not take final photos.");
        }

        if (command.Files.Count is < 1 or > RequestProofUploadsHandler.MaxFilesPerRequest)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, $"Ask for 1 to {RequestProofUploadsHandler.MaxFilesPerRequest} files at a time.");
        }

        var slots = new List<FinalUploadSlot>(command.Files.Count);
        foreach (var file in command.Files)
        {
            if (!gallery.SelectedPhotoIds.Contains(file.PhotoId))
            {
                throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, "Only a photo the client chose can have a final photo.");
            }

            if (gallery.Status != GalleryStatus.InEditing && !gallery.IsExtra(file.PhotoId))
            {
                throw new DomainException(DomainErrorCodes.GalleryInvalidState, "The final photos of the package cannot change once they are published.");
            }

            GalleryPhoto.EnsureValidFinal(file.Bytes);
            var url = await storage.CreateUploadUrlAsync(
                GalleryKeys.Final(gallery.Id, file.PhotoId),
                GalleryKeys.ProofContentType,
                ObjectStorageLimits.MaxUploadLifetime,
                cancellationToken);
            slots.Add(new FinalUploadSlot(file.PhotoId, url.Url, url.RequiredHeaders, url.ExpiresAt));
        }

        return new FinalUploadSlotsResponse(slots);
    }
}

/// <summary>
/// Registers the final photos that were uploaded, after looking at what is really in the storage: the object must exist, declare the
/// content type of a JPEG and weigh between 1 byte and the limit. An object that fails is deleted and reported with its reason, and
/// if it replaced a final that was already registered, that final is forgotten too, because the object it pointed to is gone.
/// Registering what is already registered does nothing. Two batches at the same time may collide on the version of the gallery; the
/// handler reloads and tries again.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RegisterFinalsHandler(IGalleryRepository galleries, IObjectStorage storage, TimeProvider timeProvider)
    : ICommandHandler<RegisterFinalsCommand, RegisterFinalsResponse>
{
    private const int Attempts = 3;

    /// <summary>
    /// Registers the finals.
    /// </summary>
    /// <param name="command">The uploads.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery and the uploads that were not kept.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When the request has too many files.</exception>
    /// <exception cref="ConflictException">When the gallery kept changing under the request.</exception>
    public async Task<RegisterFinalsResponse> HandleAsync(RegisterFinalsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.PhotoIds.Count is < 1 or > RequestProofUploadsHandler.MaxFilesPerRequest)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, $"Register 1 to {RequestProofUploadsHandler.MaxFilesPerRequest} files at a time.");
        }

        var inspected = new List<(Guid PhotoId, string? Reason, long Bytes)>(command.PhotoIds.Count);
        foreach (var photoId in command.PhotoIds.Distinct())
        {
            inspected.Add(await InspectAsync(command.GalleryId, photoId, cancellationToken));
        }

        for (var attempt = 1; ; attempt++)
        {
            var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var rejected = new List<FinalRejection>();
            var changed = false;
            foreach (var (photoId, reason, bytes) in inspected)
            {
                var failure = reason;
                try
                {
                    if (failure is null)
                    {
                        changed |= gallery.RegisterFinal(photoId, bytes, now);
                    }
                    else
                    {
                        changed |= gallery.RemoveFinal(photoId, now);
                    }
                }
                catch (DomainException exception) when (exception.Code is DomainErrorCodes.GalleryInvalidPhoto or DomainErrorCodes.GalleryInvalidState)
                {
                    failure = exception.Code == DomainErrorCodes.GalleryInvalidPhoto ? "not_chosen" : "locked";
                }

                if (failure is not null)
                {
                    rejected.Add(new FinalRejection(photoId, failure));
                }
            }

            try
            {
                if (changed)
                {
                    await galleries.UpdateAsync(gallery, cancellationToken);
                }
            }
            catch (ConflictException) when (attempt < Attempts)
            {
                continue;
            }

            // What could not be kept is not left in the storage. A "locked" upload is the exception: it was refused because the
            // final is frozen, and the object at that key is the one the client may be downloading.
            foreach (var rejection in rejected.Where(rejection => rejection.Reason is not "locked"))
            {
                await storage.DeleteAsync(GalleryKeys.Final(command.GalleryId, rejection.PhotoId), cancellationToken);
            }

            return new RegisterFinalsResponse(gallery.ToResponse(), rejected);
        }
    }

    /// <summary>
    /// Looks at the object an upload left in the storage.
    /// </summary>
    /// <param name="galleryId">Gallery the photo belongs to.</param>
    /// <param name="photoId">Identifier of the chosen photo.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The photo, the reason it cannot be kept (or <see langword="null"/> when it can) and the size of the object.</returns>
    private async Task<(Guid PhotoId, string? Reason, long Bytes)> InspectAsync(Guid galleryId, Guid photoId, CancellationToken cancellationToken)
    {
        if (photoId == Guid.Empty)
        {
            return (photoId, "missing", 0);
        }

        var info = await storage.GetInfoAsync(GalleryKeys.Final(galleryId, photoId), cancellationToken);
        if (info is null)
        {
            return (photoId, "missing", 0);
        }

        if (!string.Equals(info.ContentType, GalleryKeys.ProofContentType, StringComparison.OrdinalIgnoreCase))
        {
            return (photoId, "wrong_type", info.Size);
        }

        if (info.Size < 1)
        {
            return (photoId, "empty", info.Size);
        }

        return info.Size > GalleryPhoto.MaxFinalBytes ? (photoId, "too_big", info.Size) : (photoId, null, info.Size);
    }
}

/// <summary>
/// Publishes the final photos of the package (G6). The gallery is delivered by the system when the balance is zero.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class PublishFinalsHandler(IGalleryRepository galleries, TimeProvider timeProvider)
    : ICommandHandler<PublishFinalsCommand, GalleryResponse>
{
    /// <summary>
    /// Publishes the finals.
    /// </summary>
    /// <param name="command">The gallery.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery after publishing.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When the gallery is not being edited, or a photo of the package has no final.</exception>
    public async Task<GalleryResponse> HandleAsync(PublishFinalsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        gallery.PublishFinals(timeProvider.GetUtcNow());
        await galleries.UpdateAsync(gallery, cancellationToken);
        return gallery.ToResponse();
    }
}

/// <summary>
/// Lets the client download the extra photos, when the photographer says the client has paid them. The system never charges them.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class ReleaseExtrasHandler(IGalleryRepository galleries, TimeProvider timeProvider)
    : ICommandHandler<ReleaseExtrasCommand, GalleryResponse>
{
    /// <summary>
    /// Releases the extras.
    /// </summary>
    /// <param name="command">The gallery.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery after releasing.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When the final photos are not published, or no extra has its final.</exception>
    public async Task<GalleryResponse> HandleAsync(ReleaseExtrasCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        if (gallery.ReleaseExtras(timeProvider.GetUtcNow()))
        {
            await galleries.UpdateAsync(gallery, cancellationToken);
        }

        return gallery.ToResponse();
    }
}
