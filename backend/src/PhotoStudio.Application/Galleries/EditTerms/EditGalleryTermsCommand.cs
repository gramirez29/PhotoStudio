namespace PhotoStudio.Application.Galleries.EditTerms;

/// <summary>
/// Command to change the terms of a gallery.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
/// <param name="GalleryId">Gallery to change.</param>
/// <param name="IncludedPhotos">Photos the package includes.</param>
/// <param name="BonusPhotos">Photos given on top.</param>
/// <param name="ExtraPhotoPrice">Price of each extra photo, in the currency of the booking.</param>
public sealed record EditGalleryTermsCommand(Guid PhotographerId, Guid GalleryId, int IncludedPhotos, int BonusPhotos, decimal ExtraPhotoPrice);
