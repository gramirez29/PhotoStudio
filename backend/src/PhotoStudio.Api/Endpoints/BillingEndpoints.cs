using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.Auth;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Billing;
using PhotoStudio.Application.Billing.CompleteRefund;
using PhotoStudio.Application.Billing.GetSettlement;
using PhotoStudio.Application.Billing.ListRefunds;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// Body to record that a refund was given back.
/// </summary>
/// <param name="Method">How the money was given back: <c>Cash</c> or <c>SinpeMovil</c>.</param>
/// <param name="Note">Optional note, for example the SINPE reference (up to 200 characters).</param>
public sealed record CompleteRefundRequest(PaymentMethod Method, string? Note);

/// <summary>
/// The money side of the bookings that ended without delivering their session: what the photographer keeps and what still
/// has to go back to the client. Every operation is scoped to the photographer of the token.
/// </summary>
public static class BillingEndpoints
{
    /// <summary>
    /// Maps the billing endpoints under <c>/api/billing</c>.
    /// </summary>
    /// <param name="endpoints">Route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/billing").WithTags("Billing").RequireAuthorization();

        group.MapGet("/refunds", ListRefundsAsync).WithName("ListRefunds");
        group.MapGet("/settlements/{bookingId:guid}", GetSettlementAsync).WithName("GetSettlement");
        group.MapPost("/settlements/{bookingId:guid}/refund/complete", CompleteRefundAsync).WithName("CompleteRefund");

        return endpoints;
    }

    /// <summary>
    /// Lists the refunds the photographer still has to give back.
    /// </summary>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the pending refunds.</returns>
    private static async Task<Ok<RefundListResponse>> ListRefundsAsync(
        ClaimsPrincipal user,
        IQueryHandler<ListRefundsQuery, RefundListResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new ListRefundsQuery(user.GetPhotographerId()), cancellationToken));

    /// <summary>
    /// Reads the settlement of a booking.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must be theirs, otherwise the answer is 404.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the settlement.</returns>
    private static async Task<Ok<SettlementResponse>> GetSettlementAsync(
        Guid bookingId,
        ClaimsPrincipal user,
        IQueryHandler<GetSettlementQuery, SettlementResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetSettlementQuery(user.GetPhotographerId(), bookingId), cancellationToken));

    /// <summary>
    /// Records that the refund of a booking was given back to the client.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="request">Method and optional note.</param>
    /// <param name="user">Authenticated photographer; the booking must be theirs, otherwise the answer is 404.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the settlement.</returns>
    private static async Task<Ok<SettlementResponse>> CompleteRefundAsync(
        Guid bookingId,
        CompleteRefundRequest request,
        ClaimsPrincipal user,
        ICommandHandler<CompleteRefundCommand, SettlementResponse> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new CompleteRefundCommand(user.GetPhotographerId(), bookingId, request.Method, request.Note);
        return TypedResults.Ok(await handler.HandleAsync(command, cancellationToken));
    }
}
