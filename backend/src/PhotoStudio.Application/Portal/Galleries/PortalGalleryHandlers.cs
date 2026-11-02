using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Portal.Galleries;

/// <summary>
/// Query for what the link alone says about the gallery of its booking.
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
public sealed record GetPortalGallerySummaryQuery(string Token);

/// <summary>
/// Query for the gallery of the booking a link opens.
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
/// <param name="AuthorizedBookingId">Booking the portal session is valid for; it must be the booking of the link.</param>
public sealed record GetPortalGalleryQuery(string Token, Guid AuthorizedBookingId);

/// <summary>
/// Command for the client to send the photos they chose.
/// </summary>
/// <param name="Token">Secret token taken from the URL.</param>
/// <param name="AuthorizedBookingId">Booking the portal session is valid for; it must be the booking of the link.</param>
/// <param name="PhotoIds">Identifiers of the photos chosen.</param>
/// <param name="AcceptExtras">Whether the client accepted that the studio contacts them to agree on the payment of the photos beyond the package.</param>
public sealed record SubmitPortalSelectionCommand(string Token, Guid AuthorizedBookingId, IReadOnlyList<Guid> PhotoIds, bool AcceptExtras = false);

/// <summary>
/// Tells whether the client has a gallery to look at and where it is. It needs only the link, like the booking itself, and says
/// nothing about the photos.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class GetPortalGallerySummaryHandler(
    IGalleryRepository galleries,
    IBookingLinkRepository links,
    IRefreshTokenGenerator tokens,
    TimeProvider timeProvider) : IQueryHandler<GetPortalGallerySummaryQuery, PortalGallerySummaryResponse>
{
    /// <summary>
    /// Reads the summary.
    /// </summary>
    /// <param name="query">Token of the link.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Where the gallery is.</returns>
    /// <exception cref="NotFoundException">When the token opens nothing, or there is no gallery for the client to see yet.</exception>
    public async Task<PortalGallerySummaryResponse> HandleAsync(GetPortalGallerySummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var link = await links.ResolveAsync(tokens, query.Token, timeProvider.GetUtcNow(), cancellationToken);
        var gallery = await galleries.GetVisibleAsync(link, cancellationToken);
        return new PortalGallerySummaryResponse(gallery.Status);
    }
}

/// <summary>
/// Gives the client their gallery: the proofs, each with a signed address that lasts a few minutes, and the terms to choose them.
/// It needs the portal session, not only the link, because the photos are the thing the client has not paid for yet.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="storage">Object storage that signs the addresses.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class GetPortalGalleryHandler(
    IGalleryRepository galleries,
    IBookingLinkRepository links,
    IRefreshTokenGenerator tokens,
    IObjectStorage storage,
    TimeProvider timeProvider) : IQueryHandler<GetPortalGalleryQuery, PortalGalleryResponse>
{
    /// <summary>
    /// Reads the gallery.
    /// </summary>
    /// <param name="query">Token of the link and the booking of the session.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery as the client sees it.</returns>
    /// <exception cref="NotFoundException">When the token does not open the booking the session was issued for, or there is no gallery yet.</exception>
    public async Task<PortalGalleryResponse> HandleAsync(GetPortalGalleryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var link = await links.ResolveAsync(tokens, query.Token, timeProvider.GetUtcNow(), cancellationToken);
        if (link.BookingId != query.AuthorizedBookingId)
        {
            throw new NotFoundException(nameof(Gallery), Guid.Empty);
        }

        var gallery = await galleries.GetVisibleAsync(link, cancellationToken);
        return await gallery.ToPortalResponseAsync(storage, cancellationToken);
    }
}

/// <summary>
/// Records the photos the client chose (G3). From then on the selection is locked. Sending the same selection twice answers the
/// same way and records it once.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="storage">Object storage that signs the addresses.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class SubmitPortalSelectionHandler(
    IGalleryRepository galleries,
    IBookingLinkRepository links,
    IRefreshTokenGenerator tokens,
    IObjectStorage storage,
    TimeProvider timeProvider) : ICommandHandler<SubmitPortalSelectionCommand, PortalGalleryResponse>
{
    /// <summary>
    /// Records the selection.
    /// </summary>
    /// <param name="command">Token of the link, the booking of the session and the photos chosen.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The gallery after the selection.</returns>
    /// <exception cref="NotFoundException">When the token does not open the booking the session was issued for, or there is no gallery yet.</exception>
    /// <exception cref="DomainException">When the selection is not open, or the photos are none, repeated or not of this gallery.</exception>
    /// <exception cref="ConflictException">When another write changed the gallery first.</exception>
    public async Task<PortalGalleryResponse> HandleAsync(SubmitPortalSelectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var link = await links.ResolveAsync(tokens, command.Token, now, cancellationToken);
        if (link.BookingId != command.AuthorizedBookingId)
        {
            throw new NotFoundException(nameof(Gallery), Guid.Empty);
        }

        var gallery = await galleries.GetVisibleAsync(link, cancellationToken);
        if (command.PhotoIds is null || command.PhotoIds.Count > Gallery.MaxPhotos)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidSelection, "The selection is not valid.");
        }

        // The extra photos are not charged by the system: the studio agrees on their payment with the client, so the client must say yes first.
        if (command.PhotoIds.Count > gallery.SelectablePhotos && !command.AcceptExtras)
        {
            throw new DomainException(DomainErrorCodes.GalleryExtrasNotAccepted, "The selection has extra photos and the client did not accept them.");
        }

        if (gallery.SubmitSelection(command.PhotoIds, now))
        {
            await galleries.UpdateAsync(gallery, cancellationToken);
        }

        return await gallery.ToPortalResponseAsync(storage, cancellationToken);
    }
}

/// <summary>
/// Gives the client the addresses to download the final photos they ask for. Every photo asked for counts as a download, so the
/// photographer knows what the client took. Only photos the client may download now are given; if one is not, none is.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="storage">Object storage that signs the addresses.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RequestPortalDownloadsHandler(
    IGalleryRepository galleries,
    IBookingLinkRepository links,
    IRefreshTokenGenerator tokens,
    IObjectStorage storage,
    TimeProvider timeProvider) : ICommandHandler<RequestPortalDownloadsCommand, PortalDownloadsResponse>
{
    private const int Attempts = 3;

    /// <summary>
    /// Counts the downloads and signs the addresses.
    /// </summary>
    /// <param name="command">Token of the link, the booking of the session and the photos asked for.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>One address per photo.</returns>
    /// <exception cref="NotFoundException">When the token does not open the booking the session was issued for, or there is no gallery.</exception>
    /// <exception cref="DomainException">When the gallery is not delivered or a photo is not one the client may download.</exception>
    /// <exception cref="ConflictException">When the gallery kept changing under the request.</exception>
    public async Task<PortalDownloadsResponse> HandleAsync(RequestPortalDownloadsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var link = await links.ResolveAsync(tokens, command.Token, now, cancellationToken);
        if (link.BookingId != command.AuthorizedBookingId)
        {
            throw new NotFoundException(nameof(Gallery), Guid.Empty);
        }

        if (command.PhotoIds is null || command.PhotoIds.Count is < 1 or > Gallery.MaxPhotos)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, "Ask for 1 to " + Gallery.MaxPhotos + " photos.");
        }

        for (var attempt = 1; ; attempt++)
        {
            var gallery = await galleries.GetVisibleAsync(link, cancellationToken);
            gallery.RecordFinalDownloads(command.PhotoIds, now);
            try
            {
                await galleries.UpdateAsync(gallery, cancellationToken);
            }
            catch (ConflictException) when (attempt < Attempts)
            {
                continue;
            }

            var items = new List<PortalDownloadResponse>(command.PhotoIds.Count);
            foreach (var photoId in command.PhotoIds.Distinct())
            {
                var photo = gallery.Photos.First(candidate => candidate.Id == photoId);
                var fileName = PortalGalleryLookup.FinalFileName(photo);
                var url = await storage.CreateDownloadUrlAsync(PhotoStudio.Application.Galleries.GalleryKeys.Final(gallery.Id, photoId), PortalGalleryLookup.FinalUrlLifetime, fileName, cancellationToken);
                items.Add(new PortalDownloadResponse(photoId, fileName, url.Url, url.ExpiresAt));
            }

            return new PortalDownloadsResponse(items);
        }
    }
}

/// <summary>
/// Gives the client the address to download the ZIP with the photos, and counts it as a download.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="storage">Object storage that signs the address.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RequestPortalZipDownloadHandler(
    IGalleryRepository galleries,
    IBookingLinkRepository links,
    IRefreshTokenGenerator tokens,
    IObjectStorage storage,
    TimeProvider timeProvider) : ICommandHandler<RequestPortalZipDownloadCommand, PortalZipDownloadResponse>
{
    private const int Attempts = 3;

    /// <summary>
    /// Counts the download and signs the address.
    /// </summary>
    /// <param name="command">Token of the link and the booking of the session.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The address of the ZIP.</returns>
    /// <exception cref="NotFoundException">When the token does not open the booking the session was issued for, or there is no gallery.</exception>
    /// <exception cref="DomainException">When the gallery is not delivered or has no ZIP.</exception>
    /// <exception cref="ConflictException">When the gallery kept changing under the request.</exception>
    public async Task<PortalZipDownloadResponse> HandleAsync(RequestPortalZipDownloadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var link = await links.ResolveAsync(tokens, command.Token, now, cancellationToken);
        if (link.BookingId != command.AuthorizedBookingId)
        {
            throw new NotFoundException(nameof(Gallery), Guid.Empty);
        }

        for (var attempt = 1; ; attempt++)
        {
            var gallery = await galleries.GetVisibleAsync(link, cancellationToken);
            gallery.RecordZipDownload(now);
            try
            {
                await galleries.UpdateAsync(gallery, cancellationToken);
            }
            catch (ConflictException) when (attempt < Attempts)
            {
                continue;
            }

            var url = await storage.CreateDownloadUrlAsync(PhotoStudio.Application.Galleries.GalleryKeys.Zip(gallery.Id), PortalGalleryLookup.FinalUrlLifetime, PhotoStudio.Application.Galleries.GalleryKeys.ZipFileName, cancellationToken);
            return new PortalZipDownloadResponse(PhotoStudio.Application.Galleries.GalleryKeys.ZipFileName, url.Url, url.ExpiresAt, gallery.ZipBytes ?? 0);
        }
    }
}

/// <summary>
/// Gives the client the address to look at a final photo in its real quality, as the photographer edited it. Looking is not
/// downloading and is not counted, but it only works for what the client may download: the gallery is delivered, so the balance is
/// zero, and an extra only once the photographer released it.
/// </summary>
/// <param name="galleries">Gallery repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="storage">Object storage that signs the address.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class GetPortalFinalViewHandler(
    IGalleryRepository galleries,
    IBookingLinkRepository links,
    IRefreshTokenGenerator tokens,
    IObjectStorage storage,
    TimeProvider timeProvider) : IQueryHandler<GetPortalFinalViewQuery, PortalFinalViewResponse>
{
    /// <summary>
    /// Signs the address.
    /// </summary>
    /// <param name="query">Token of the link, the booking of the session and the photo.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The address.</returns>
    /// <exception cref="NotFoundException">When the token does not open the booking the session was issued for, or there is no gallery.</exception>
    /// <exception cref="DomainException">When the gallery is not delivered or the photo is not one the client may take.</exception>
    public async Task<PortalFinalViewResponse> HandleAsync(GetPortalFinalViewQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = timeProvider.GetUtcNow();
        var link = await links.ResolveAsync(tokens, query.Token, now, cancellationToken);
        if (link.BookingId != query.AuthorizedBookingId)
        {
            throw new NotFoundException(nameof(Gallery), Guid.Empty);
        }

        var gallery = await galleries.GetVisibleAsync(link, cancellationToken);
        if (gallery.Status != GalleryStatus.Delivered)
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidState, $"A gallery in state {gallery.Status} cannot be looked at in its final quality.");
        }

        if (!gallery.IsDownloadable(query.PhotoId))
        {
            throw new DomainException(DomainErrorCodes.GalleryInvalidPhoto, "Only the final photos that were delivered can be looked at.");
        }

        // No file name: the address answers to be shown, not saved, so an image tag or a viewer can use it.
        var url = await storage.CreateDownloadUrlAsync(PhotoStudio.Application.Galleries.GalleryKeys.Final(gallery.Id, query.PhotoId), PortalGalleryLookup.FinalUrlLifetime, null, cancellationToken);
        return new PortalFinalViewResponse(url.Url, url.ExpiresAt);
    }
}
