using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.Auth;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Galleries.EditTerms;
using PhotoStudio.Application.Galleries.GetGallery;
using PhotoStudio.Application.Galleries.ListGalleries;
using PhotoStudio.Application.Pagination;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// Body to change the terms of a gallery.
/// </summary>
/// <param name="IncludedPhotos">Photos the package includes.</param>
/// <param name="BonusPhotos">Photos given on top.</param>
/// <param name="ExtraPhotoPrice">Price of each photo beyond those, in the currency of the booking.</param>
public sealed record EditGalleryTermsRequest(int IncludedPhotos, int BonusPhotos, decimal ExtraPhotoPrice);

/// <summary>
/// Photographer-facing gallery endpoints. A gallery is created by the system when a session is completed, so there is no
/// endpoint to create one. Every operation is scoped to the photographer of the access token, and the gallery of another
/// photographer answers 404, exactly like one that does not exist.
/// </summary>
public static class GalleryEndpoints
{
    /// <summary>
    /// Maps the gallery endpoints under <c>/api/galleries</c>.
    /// </summary>
    /// <param name="endpoints">Route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapGalleryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/galleries").WithTags("Galleries").RequireAuthorization();

        group.MapGet("/", ListAsync).WithName("ListGalleries");
        group.MapGet("/by-booking/{bookingId:guid}", GetByBookingAsync).WithName("GetGalleryByBooking");
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetGallery");
        group.MapPost("/{id:guid}/terms", EditTermsAsync).WithName("EditGalleryTerms");

        return endpoints;
    }

    /// <summary>
    /// Lists one page of the galleries of the authenticated photographer, the most recently created first.
    /// </summary>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <param name="cursor">The <c>nextCursor</c> of the previous page; omitted for the first page. 400 when it is not valid.</param>
    /// <param name="limit">How many galleries the page has (5 by default, at most 50).</param>
    /// <returns>200 with the page of galleries and the cursor of the next one, if any.</returns>
    private static async Task<Ok<GalleryListResponse>> ListAsync(
        ClaimsPrincipal user,
        IQueryHandler<ListGalleriesQuery, GalleryListResponse> handler,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListGalleriesQuery(user.GetPhotographerId(), PageCursors.Read(cursor), PageSizes.Resolve(limit)),
            cancellationToken));

    /// <summary>
    /// Reads a gallery.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="user">Authenticated photographer; the gallery must be theirs, otherwise the answer is 404.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the gallery.</returns>
    private static async Task<Ok<GalleryResponse>> GetByIdAsync(
        Guid id,
        ClaimsPrincipal user,
        IQueryHandler<GetGalleryQuery, GalleryResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetGalleryQuery(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Reads the gallery of a booking.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must be theirs, otherwise the answer is 404.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the gallery; 404 when the booking has none yet (its session was not completed).</returns>
    private static async Task<Ok<GalleryResponse>> GetByBookingAsync(
        Guid bookingId,
        ClaimsPrincipal user,
        IQueryHandler<GetGalleryByBookingQuery, GalleryResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetGalleryByBookingQuery(user.GetPhotographerId(), bookingId), cancellationToken));

    /// <summary>
    /// Changes the terms of a gallery: included photos, gift photos and the price of an extra photo.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="request">The new terms.</param>
    /// <param name="user">Authenticated photographer; the gallery must be theirs, otherwise the answer is 404.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the gallery; 409 <c>gallery.invalid_state</c> once the client sent the selection; 422 <c>gallery.invalid_terms</c> or <c>money.invalid_amount</c> for invalid values.</returns>
    private static async Task<Ok<GalleryResponse>> EditTermsAsync(
        Guid id,
        EditGalleryTermsRequest request,
        ClaimsPrincipal user,
        ICommandHandler<EditGalleryTermsCommand, GalleryResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new EditGalleryTermsCommand(user.GetPhotographerId(), id, request.IncludedPhotos, request.BonusPhotos, request.ExtraPhotoPrice),
            cancellationToken));
}
