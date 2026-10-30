using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Galleries.Proofs;

/// <summary>
/// Command to open the selection.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery to publish.</param>
public sealed record OpenSelectionCommand(Guid PhotographerId, Guid GalleryId);

/// <summary>
/// Opens the selection (G2): the proofs are published and the client can choose from them. The event that tells the client is stored
/// with the change, in the same transaction, and sent by the outbox.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class OpenSelectionHandler(IGalleryRepository galleries, TimeProvider timeProvider)
    : ICommandHandler<OpenSelectionCommand, GalleryResponse>
{
    /// <summary>
    /// Opens the selection.
    /// </summary>
    /// <param name="command">Photographer and gallery.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery, now in selection.</returns>
    /// <exception cref="Exceptions.NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="Domain.Common.DomainException">When the gallery is not waiting for its proofs, or has none.</exception>
    /// <exception cref="Exceptions.ConflictException">When another request changed the gallery first.</exception>
    public async Task<GalleryResponse> HandleAsync(OpenSelectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        gallery.OpenSelection(timeProvider.GetUtcNow());
        await galleries.UpdateAsync(gallery, cancellationToken);
        return gallery.ToResponse();
    }
}
