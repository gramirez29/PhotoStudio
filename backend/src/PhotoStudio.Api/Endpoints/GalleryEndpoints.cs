using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.Auth;
using PhotoStudio.Api.RateLimiting;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Galleries.EditTerms;
using PhotoStudio.Application.Galleries.Finals;
using PhotoStudio.Application.Galleries.GetGallery;
using PhotoStudio.Application.Galleries.ListGalleries;
using PhotoStudio.Application.Galleries.Proofs;
using PhotoStudio.Application.Galleries.Selection;
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
/// HTTP body to reopen the selection of a client.
/// </summary>
/// <param name="Reason">Why the selection is reopened; required.</param>
public sealed record ReopenSelectionRequest(string? Reason);

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

        // What the photographer does with a gallery from the app. The same handlers serve the web area; here the session is the
        // token of the app instead of the cookie.
        group.MapGet("/{id:guid}/proofs", ListProofsAsync).WithName("AppListProofs");
        group.MapGet("/{id:guid}/selection", GetSelectionAsync).WithName("AppGetSelection");
        group.MapPost("/{id:guid}/proofs/{photoId:guid}/remove", RemoveProofAsync).WithName("AppRemoveProof");
        group.MapPost("/{id:guid}/selection/open", OpenSelectionAsync).WithName("AppOpenSelection");
        group.MapPost("/{id:guid}/selection/reopen", ReopenSelectionAsync).WithName("AppReopenSelection");
        group.MapPost("/{id:guid}/finals/publish", PublishFinalsAsync).WithName("AppPublishFinals");
        group.MapPost("/{id:guid}/extras/release", ReleaseExtrasAsync).WithName("AppReleaseExtras");
        group.MapPost("/{id:guid}/zip/remove", RemoveZipAsync).WithName("AppRemoveZip");
        group.MapPost("/{id:guid}/release", StudioEndpoints.ReleaseAsync).WithName("AppReleaseGallery");
        group.MapGet("/{id:guid}/finals/{photoId:guid}/download", GetFinalDownloadAsync).WithName("AppGetFinalDownload");

        // Uploading files from the phone: the phone prepares the photos (a proof is reduced and marked on the device) and the bytes go
        // straight to the storage with the signed addresses these routes give; the API only looks at what arrived. The handlers are
        // the ones of the web area, so both surfaces follow the same rules.
        group.MapPost("/{id:guid}/proofs/uploads", StudioEndpoints.RequestUploadsAsync).WithName("AppRequestProofUploads").RequireRateLimiting(RateLimitingExtensions.StudioUploadPolicy);
        group.MapPost("/{id:guid}/proofs", StudioEndpoints.RegisterProofsAsync).WithName("AppRegisterProofs").RequireRateLimiting(RateLimitingExtensions.StudioUploadPolicy);
        group.MapPost("/{id:guid}/finals/uploads", StudioEndpoints.RequestFinalUploadsAsync).WithName("AppRequestFinalUploads").RequireRateLimiting(RateLimitingExtensions.StudioUploadPolicy);
        group.MapPost("/{id:guid}/finals", StudioEndpoints.RegisterFinalsAsync).WithName("AppRegisterFinals").RequireRateLimiting(RateLimitingExtensions.StudioUploadPolicy);
        group.MapPost("/{id:guid}/zip/upload", StudioEndpoints.RequestZipUploadAsync).WithName("AppRequestZipUpload").RequireRateLimiting(RateLimitingExtensions.StudioUploadPolicy);
        group.MapPost("/{id:guid}/zip", StudioEndpoints.RegisterZipAsync).WithName("AppRegisterZip").RequireRateLimiting(RateLimitingExtensions.StudioUploadPolicy);

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
    /// Lists the proofs of a gallery, each with a signed address that works for a few minutes.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the proofs; 404 when the gallery does not exist or is another photographer's.</returns>
    private static async Task<Ok<ProofListResponse>> ListProofsAsync(
        Guid id,
        ClaimsPrincipal user,
        IQueryHandler<ListProofsQuery, ProofListResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new ListProofsQuery(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Reads what the client chose, with the names of the files, the details of each final and its downloads.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the selection (empty until the client sends it); 404 when the gallery does not exist or is another photographer's.</returns>
    private static async Task<Ok<SelectionResponse>> GetSelectionAsync(
        Guid id,
        ClaimsPrincipal user,
        IQueryHandler<GetSelectionQuery, SelectionResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetSelectionQuery(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Removes a proof, while the selection is not open.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="photoId">Photo identifier.</param>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the gallery; 404 when the gallery or the photo does not exist; 409 <c>gallery.invalid_state</c> once the selection is open.</returns>
    private static async Task<Ok<GalleryResponse>> RemoveProofAsync(
        Guid id,
        Guid photoId,
        ClaimsPrincipal user,
        ICommandHandler<RemoveProofCommand, GalleryResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new RemoveProofCommand(user.GetPhotographerId(), id, photoId), cancellationToken));

    /// <summary>
    /// Opens the selection: the proofs are published, the client can choose, and the client is told by email.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the gallery; 409 <c>gallery.no_photos</c> without proofs, or <c>gallery.invalid_state</c> when it is already open.</returns>
    private static async Task<Ok<GalleryResponse>> OpenSelectionAsync(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<OpenSelectionCommand, GalleryResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new OpenSelectionCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Reopens the selection of the client, so they can choose again. The client is told by email.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="request">Why it is reopened.</param>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the gallery, in selection again; 409 <c>gallery.invalid_state</c> when it is not in editing; 422 <c>booking.reason_required</c> without a reason.</returns>
    private static async Task<Ok<GalleryResponse>> ReopenSelectionAsync(
        Guid id,
        ReopenSelectionRequest request,
        ClaimsPrincipal user,
        ICommandHandler<ReopenSelectionCommand, GalleryResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new ReopenSelectionCommand(user.GetPhotographerId(), id, request.Reason), cancellationToken));

    /// <summary>
    /// Publishes the final photos of the package. The client downloads them as soon as the balance is zero.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the gallery; 409 <c>gallery.invalid_state</c> or <c>gallery.finals_incomplete</c> when a photo of the package has no final.</returns>
    private static async Task<Ok<GalleryResponse>> PublishFinalsAsync(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<PublishFinalsCommand, GalleryResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new PublishFinalsCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Lets the client download the extra photos, once the photographer agreed on their payment with the client.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the gallery; 409 <c>gallery.invalid_state</c> before the finals are published, or <c>gallery.finals_incomplete</c> when no extra has its final.</returns>
    private static async Task<Ok<GalleryResponse>> ReleaseExtrasAsync(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<ReleaseExtrasCommand, GalleryResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new ReleaseExtrasCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Deletes the ZIP, from the gallery and from the storage.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the gallery.</returns>
    private static async Task<Ok<GalleryResponse>> RemoveZipAsync(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<RemoveZipCommand, GalleryResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new RemoveZipCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Gives the photographer the address to download one final photo. It is not counted as a download of the client.
    /// </summary>
    /// <param name="id">Gallery identifier.</param>
    /// <param name="photoId">Photo identifier.</param>
    /// <param name="user">The principal of the session.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the address; 404 when the gallery or the final does not exist or is another photographer's.</returns>
    private static async Task<Ok<FinalDownloadResponse>> GetFinalDownloadAsync(
        Guid id,
        Guid photoId,
        ClaimsPrincipal user,
        IQueryHandler<GetFinalDownloadQuery, FinalDownloadResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetFinalDownloadQuery(user.GetPhotographerId(), id, photoId), cancellationToken));

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
