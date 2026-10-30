using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries.EditTerms;

/// <summary>
/// Changes how many photos a gallery includes, how many are a gift and what an extra one costs. Only the gallery of the
/// photographer can be changed, and only until the client sends the selection.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class EditGalleryTermsHandler(IGalleryRepository galleries, TimeProvider timeProvider)
    : ICommandHandler<EditGalleryTermsCommand, GalleryResponse>
{
    /// <summary>
    /// Changes the terms.
    /// </summary>
    /// <param name="command">The new terms.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery with its new terms.</returns>
    /// <exception cref="NotFoundException">When the gallery does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When the gallery no longer allows it or a value is not valid.</exception>
    /// <exception cref="ConflictException">When another request changed the gallery first.</exception>
    public async Task<GalleryResponse> HandleAsync(EditGalleryTermsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gallery = await galleries.GetByIdAsync(command.GalleryId, cancellationToken);
        if (gallery is null || gallery.PhotographerId != command.PhotographerId)
        {
            throw new NotFoundException(nameof(Gallery), command.GalleryId);
        }

        gallery.EditTerms(
            command.IncludedPhotos,
            command.BonusPhotos,
            Money.Create(command.ExtraPhotoPrice, gallery.ExtraPhotoPrice.Currency),
            timeProvider.GetUtcNow());
        await galleries.UpdateAsync(gallery, cancellationToken);

        return gallery.ToResponse();
    }
}
