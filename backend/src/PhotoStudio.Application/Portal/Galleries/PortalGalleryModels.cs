using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Portal.Galleries;

/// <summary>
/// What the link alone says about the gallery of a booking: only where it is, so the page knows whether to offer it. Nothing here
/// is personal or about the photos.
/// </summary>
/// <param name="Status">Where the gallery is. A gallery still waiting for its proofs is not reported at all.</param>
public sealed record PortalGallerySummaryResponse(GalleryStatus Status);

/// <summary>
/// A proof the client can look at.
/// </summary>
/// <param name="Id">Photo identifier, which the client sends back to choose it.</param>
/// <param name="Url">Signed address to show it; it expires in minutes and must not be logged.</param>
/// <param name="ExpiresAt">Instant the address stops working (UTC).</param>
public sealed record PortalProofResponse(Guid Id, Uri Url, DateTimeOffset ExpiresAt);

/// <summary>
/// The selection a client already sent.
/// </summary>
/// <param name="PhotoIds">The photos chosen, in the order they were chosen.</param>
/// <param name="SubmittedAt">Instant it was sent (UTC).</param>
/// <param name="ExtraPhotos">How many of them go beyond what the package includes.</param>
/// <param name="ExtraTotal">What those extra photos cost.</param>
public sealed record PortalSelectionResponse(IReadOnlyList<Guid> PhotoIds, DateTimeOffset SubmittedAt, int ExtraPhotos, MoneyResponse ExtraTotal);

/// <summary>
/// The gallery as its client sees it, once they confirmed the code: the proofs and the terms to choose them. It holds no file
/// name and nothing of the photographer.
/// </summary>
/// <param name="Status">Where the gallery is: choosing, or already sent.</param>
/// <param name="SelectablePhotos">Photos the client can choose without paying more.</param>
/// <param name="ExtraPhotoPrice">Price of each photo beyond that.</param>
/// <param name="Photos">The proofs, in the order they were uploaded, each with a signed address that lasts a few minutes.</param>
/// <param name="Selection">The selection that was sent, or <see langword="null"/> while the client has not sent one.</param>
public sealed record PortalGalleryResponse(
    GalleryStatus Status,
    int SelectablePhotos,
    MoneyResponse ExtraPhotoPrice,
    IReadOnlyList<PortalProofResponse> Photos,
    PortalSelectionResponse? Selection);
