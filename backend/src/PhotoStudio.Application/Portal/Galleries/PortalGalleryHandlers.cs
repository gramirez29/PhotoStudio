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
public sealed record SubmitPortalSelectionCommand(string Token, Guid AuthorizedBookingId, IReadOnlyList<Guid> PhotoIds);

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

        if (gallery.SubmitSelection(command.PhotoIds, now))
        {
            await galleries.UpdateAsync(gallery, cancellationToken);
        }

        return await gallery.ToPortalResponseAsync(storage, cancellationToken);
    }
}
