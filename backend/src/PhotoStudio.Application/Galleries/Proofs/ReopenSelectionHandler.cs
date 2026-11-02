using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Galleries.Proofs;

/// <summary>
/// Command to reopen the selection of a client.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery whose selection is reopened.</param>
/// <param name="Reason">Why; required.</param>
public sealed record ReopenSelectionCommand(Guid PhotographerId, Guid GalleryId, string? Reason);

/// <summary>
/// Reopens the selection (G5) so the client can choose again, and tells the client by email, without a link, as with the proofs.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class ReopenSelectionHandler(IGalleryRepository galleries, TimeProvider timeProvider)
    : ICommandHandler<ReopenSelectionCommand, GalleryResponse>
{
    /// <summary>
    /// Reopens the selection.
    /// </summary>
    /// <param name="command">The gallery and the reason.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery, in selection again.</returns>
    /// <exception cref="Exceptions.NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="Domain.Common.DomainException">When the gallery is not in editing, or the reason is missing.</exception>
    public async Task<GalleryResponse> HandleAsync(ReopenSelectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetOwnedAsync(command.GalleryId, command.PhotographerId, cancellationToken);
        gallery.ReopenSelection(command.Reason, timeProvider.GetUtcNow());
        await galleries.UpdateAsync(gallery, cancellationToken);
        return gallery.ToResponse();
    }
}
