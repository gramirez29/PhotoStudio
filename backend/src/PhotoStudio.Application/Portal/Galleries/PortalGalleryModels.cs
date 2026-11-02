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
/// A final photo the client can download, with an address that lasts a few minutes.
/// </summary>
/// <param name="Id">Photo identifier, the same as its proof.</param>
/// <param name="FileName">Name to save the file with.</param>
/// <param name="Bytes">Size of the file in bytes.</param>
/// <param name="IsExtra">Whether it is one of the extra photos.</param>
/// <param name="ThumbUrl">Signed address of a small image of the photo, to look at before downloading it; <see langword="null"/> when it has none. It is not a download and is not counted.</param>
public sealed record PortalFinalResponse(Guid Id, string FileName, long Bytes, bool IsExtra, Uri? ThumbUrl = null);

/// <summary>
/// An address to look at one final photo in its real quality, before or without downloading it.
/// </summary>
/// <param name="Url">Signed address of the photo, to show it; lasts a few minutes. It is not a download and is not counted.</param>
/// <param name="ExpiresAt">Instant the address stops working (UTC).</param>
public sealed record PortalFinalViewResponse(Uri Url, DateTimeOffset ExpiresAt);

/// <summary>
/// Query for the client to look at one final photo in its real quality.
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
/// <param name="AuthorizedBookingId">Booking the portal session is valid for.</param>
/// <param name="PhotoId">The photo.</param>
public sealed record GetPortalFinalViewQuery(string Token, Guid AuthorizedBookingId, Guid PhotoId);

/// <summary>
/// The ZIP with the photos, as the client sees it before asking for it.
/// </summary>
/// <param name="Bytes">Size of the ZIP, so the client knows what they are about to download.</param>
public sealed record PortalZipResponse(long Bytes);

/// <summary>
/// An address to download one final photo, given when the client asks for it.
/// </summary>
/// <param name="Id">Photo identifier.</param>
/// <param name="FileName">Name to save the file with.</param>
/// <param name="Url">Signed address to download it; lasts a few minutes.</param>
/// <param name="ExpiresAt">Instant the address stops working (UTC).</param>
public sealed record PortalDownloadResponse(Guid Id, string FileName, Uri Url, DateTimeOffset ExpiresAt);

/// <summary>
/// The addresses to download the photos the client asked for.
/// </summary>
/// <param name="Items">One address per photo, in the order they were asked.</param>
public sealed record PortalDownloadsResponse(IReadOnlyList<PortalDownloadResponse> Items);

/// <summary>
/// The address to download the ZIP.
/// </summary>
/// <param name="FileName">Name to save the file with.</param>
/// <param name="Url">Signed address to download it; lasts a few minutes.</param>
/// <param name="ExpiresAt">Instant the address stops working (UTC).</param>
/// <param name="Bytes">Size of the ZIP.</param>
public sealed record PortalZipDownloadResponse(string FileName, Uri Url, DateTimeOffset ExpiresAt, long Bytes);

/// <summary>
/// Command for the client to ask for the addresses to download some final photos. Asking counts as a download.
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
/// <param name="AuthorizedBookingId">Booking the portal session is valid for.</param>
/// <param name="PhotoIds">The photos the client wants.</param>
public sealed record RequestPortalDownloadsCommand(string Token, Guid AuthorizedBookingId, IReadOnlyList<Guid> PhotoIds);

/// <summary>
/// Command for the client to ask for the address to download the ZIP. Asking counts as a download.
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
/// <param name="AuthorizedBookingId">Booking the portal session is valid for.</param>
public sealed record RequestPortalZipDownloadCommand(string Token, Guid AuthorizedBookingId);

/// <summary>
/// The gallery as its client sees it, once they confirmed the code: the proofs and the terms to choose them. It holds no file
/// name and nothing of the photographer.
/// </summary>
/// <param name="Status">Where the gallery is: choosing, or already sent.</param>
/// <param name="SelectablePhotos">Photos the client can choose without paying more.</param>
/// <param name="ExtraPhotoPrice">Price of each photo beyond that.</param>
/// <param name="Photos">The proofs, in the order they were uploaded, each with a signed address that lasts a few minutes.</param>
/// <param name="Selection">The selection that was sent, or <see langword="null"/> while the client has not sent one.</param>
/// <param name="Finals">The final photos the client can download now: none until the gallery is delivered; extras only once released.</param>
/// <param name="ExtrasReleased">Whether the photographer released the extra photos.</param>
/// <param name="Zip">The ZIP with the photos, when the gallery is delivered and has one.</param>
public sealed record PortalGalleryResponse(
    GalleryStatus Status,
    int SelectablePhotos,
    MoneyResponse ExtraPhotoPrice,
    IReadOnlyList<PortalProofResponse> Photos,
    PortalSelectionResponse? Selection,
    IReadOnlyList<PortalFinalResponse>? Finals = null,
    bool ExtrasReleased = false,
    PortalZipResponse? Zip = null);
