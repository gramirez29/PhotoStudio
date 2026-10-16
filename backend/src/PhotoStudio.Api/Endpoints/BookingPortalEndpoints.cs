using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.Auth;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.RejectPayment;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Bookings.VerifyPayment;
using PhotoStudio.Application.Portal;
using PhotoStudio.Application.Portal.CreateLink;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// HTTP body to reject a payment proof.
/// </summary>
/// <param name="Reason">Why the proof is rejected; required.</param>
public sealed record RejectPaymentRequest(string Reason);

/// <summary>
/// The photographer's side of the client portal: sharing the booking link and resolving the payment proofs the client sends
/// through it. Every endpoint requires a signed-in photographer and is scoped to the photographer of the access token.
/// </summary>
public static class BookingPortalEndpoints
{
    /// <summary>
    /// Maps the endpoints under <c>/api/bookings/{id}</c>.
    /// </summary>
    /// <param name="endpoints">Route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapBookingPortalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/bookings/{id:guid}").WithTags("Bookings").RequireAuthorization();

        group.MapPost("/link", CreateLinkAsync).WithName("CreateBookingLink");
        group.MapPost("/payments/{paymentId:guid}/verify", VerifyPaymentAsync).WithName("VerifyPayment");
        group.MapPost("/payments/{paymentId:guid}/reject", RejectPaymentAsync).WithName("RejectPayment");

        return endpoints;
    }

    /// <summary>
    /// Creates the link the client opens. The previous link of the booking, if any, stops working. The secret token is in
    /// this response only.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>201 with the token and the URL to share; 404 if the booking is not the photographer's.</returns>
    private static async Task<Created<BookingLinkResponse>> CreateLinkAsync(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<CreateBookingLinkCommand, BookingLinkResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Created(
            (string?)null,
            await handler.HandleAsync(new CreateBookingLinkCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Accepts a payment proof (B4).
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="paymentId">Payment waiting for verification.</param>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 409 <c>payment.invalid_status</c> if it is not pending; 422 <c>payment.not_found</c> if the payment does not exist.</returns>
    private static async Task<Ok<BookingResponse>> VerifyPaymentAsync(
        Guid id,
        Guid paymentId,
        ClaimsPrincipal user,
        ICommandHandler<VerifyPaymentCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new VerifyPaymentCommand(user.GetPhotographerId(), id, paymentId), cancellationToken));

    /// <summary>
    /// Rejects a payment proof (B4). The proof stays as evidence.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="paymentId">Payment waiting for verification.</param>
    /// <param name="request">Reason of the rejection.</param>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 422 <c>booking.reason_required</c> without a reason.</returns>
    private static async Task<Ok<BookingResponse>> RejectPaymentAsync(
        Guid id,
        Guid paymentId,
        RejectPaymentRequest request,
        ClaimsPrincipal user,
        ICommandHandler<RejectPaymentCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new RejectPaymentCommand(user.GetPhotographerId(), id, paymentId, request.Reason), cancellationToken));
}
