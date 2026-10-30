using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries.Proofs;

/// <summary>
/// Command to remove a proof.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery the proof belongs to.</param>
/// <param name="PhotoId">Photo to remove.</param>
public sealed record RemoveProofCommand(Guid PhotographerId, Guid GalleryId, Guid PhotoId);

/// <summary>
/// Removes a proof that was uploaded by mistake, while the selection is not open. The gallery forgets it first and the object is
/// deleted after: if the deletion fails the photo is already gone for everybody and the object is only an orphan, which the cleanup
/// of the storage removes, whereas the other order could leave a photo that points to nothing.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="storage">Object storage.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RemoveProofHandler(IGalleryRepository galleries, IObjectStorage storage, TimeProvider timeProvider)
    : ICommandHandler<RemoveProofCommand, GalleryResponse>
{
    /// <summary>
    /// Removes the proof.
    /// </summary>
    /// <param name="command">Gallery and photo.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery without the proof.</returns>
    /// <exception cref="NotFoundException">When the gallery or the photo does not exist, or the gallery is not the photographer's.</exception>
    /// <exception cref="Domain.Common.DomainException">When the gallery no longer allows it.</exception>
    public async Task<GalleryResponse> HandleAsync(RemoveProofCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        if (!gallery.RemoveProof(command.PhotoId, timeProvider.GetUtcNow()))
        {
            throw new NotFoundException(nameof(GalleryPhoto), command.PhotoId);
        }

        await galleries.UpdateAsync(gallery, cancellationToken);
        try
        {
            await storage.DeleteAsync(GalleryKeys.Proof(gallery.Id, command.PhotoId), cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            // The photo is already gone; the object is an orphan that the cleanup of the storage will remove.
        }

        return gallery.ToResponse();
    }
}
