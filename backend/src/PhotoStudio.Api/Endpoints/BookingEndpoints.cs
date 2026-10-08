using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.CancelBooking;
using PhotoStudio.Application.Bookings.CompleteBooking;
using PhotoStudio.Application.Bookings.CreateBooking;
using PhotoStudio.Application.Bookings.GetBooking;
using PhotoStudio.Application.Bookings.ListBookings;
using PhotoStudio.Application.Bookings.MarkClientAbsent;
using PhotoStudio.Application.Bookings.RecordInPersonPayment;
using PhotoStudio.Application.Bookings.RescheduleBooking;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Bookings.RevertClientAbsent;
using PhotoStudio.Application.Bookings.SignContractInPerson;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// Photographer-facing booking endpoints. Client-portal endpoints (token based) will live in a separate group.
/// </summary>
public static class BookingEndpoints
{
    /// <summary>
    /// Maps the booking endpoints under <c>/api/bookings</c>.
    /// </summary>
    /// <param name="endpoints">Route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/bookings").WithTags("Bookings");

        group.MapPost("/", CreateAsync).WithName("CreateBooking");
        group.MapGet("/", ListAsync).WithName("ListBookings");
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetBooking");
        group.MapPost("/{id:guid}/contract/in-person", SignContractInPersonAsync).WithName("SignContractInPerson");
        group.MapPost("/{id:guid}/payments/in-person", RecordInPersonPaymentAsync).WithName("RecordInPersonPayment");
        group.MapPost("/{id:guid}/reschedule", RescheduleAsync).WithName("RescheduleBooking");
        group.MapPost("/{id:guid}/cancel", CancelAsync).WithName("CancelBooking");
        group.MapPost("/{id:guid}/complete", CompleteAsync).WithName("CompleteBooking");
        group.MapPost("/{id:guid}/client-absent", MarkClientAbsentAsync).WithName("MarkClientAbsent");
        group.MapPost("/{id:guid}/client-absent/revert", RevertClientAbsentAsync).WithName("RevertClientAbsent");

        return endpoints;
    }

    /// <summary>
    /// Creates a tentative booking.
    /// </summary>
    /// <param name="request">Booking data.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>201 with the created booking.</returns>
    private static async Task<Created<BookingResponse>> CreateAsync(
        CreateBookingRequest request,
        ICommandHandler<CreateBookingCommand, BookingResponse> handler,
        CancellationToken cancellationToken)
    {
        var response = await handler.HandleAsync(request.ToCommand(), cancellationToken);
        return TypedResults.Created($"/api/bookings/{response.Id}", response);
    }

    /// <summary>
    /// Lists the photographer's bookings. The photographer comes from the query string until authentication exists.
    /// </summary>
    /// <param name="photographerId">Photographer (tenant) identifier; required.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the booking summaries, earliest session first.</returns>
    private static async Task<Ok<IReadOnlyList<BookingSummaryResponse>>> ListAsync(
        Guid photographerId,
        IQueryHandler<ListBookingsQuery, IReadOnlyList<BookingSummaryResponse>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new ListBookingsQuery(photographerId), cancellationToken));

    /// <summary>
    /// Reads a booking.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the booking.</returns>
    private static async Task<Ok<BookingResponse>> GetByIdAsync(
        Guid id,
        IQueryHandler<GetBookingQuery, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetBookingQuery(id), cancellationToken));

    /// <summary>
    /// Registers a contract signed in person or on paper.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="request">Signature data.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking.</returns>
    private static async Task<Ok<BookingResponse>> SignContractInPersonAsync(
        Guid id,
        SignContractInPersonRequest request,
        ICommandHandler<SignContractInPersonCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(id), cancellationToken));

    /// <summary>
    /// Moves a confirmed booking to another slot. Photographer-only until authentication and the client portal exist.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="request">New slot.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 409 when the slot is taken or the booking is not confirmed.</returns>
    private static async Task<Ok<BookingResponse>> RescheduleAsync(
        Guid id,
        RescheduleBookingRequest request,
        ICommandHandler<RescheduleBookingCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(id), cancellationToken));

    /// <summary>
    /// Cancels a booking. Photographer-only until authentication and the client portal exist.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="request">JSON body with the reason; the reason is mandatory once the booking is confirmed, otherwise send <c>{}</c>.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 422 when the reason is missing; 409 when it cannot be cancelled.</returns>
    private static async Task<Ok<BookingResponse>> CancelAsync(
        Guid id,
        CancelBookingRequest request,
        ICommandHandler<CancelBookingCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(id), cancellationToken));

    /// <summary>
    /// Marks a confirmed booking as completed once its session has started.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 409 when the booking is not confirmed or the session has not started.</returns>
    private static async Task<Ok<BookingResponse>> CompleteAsync(
        Guid id,
        ICommandHandler<CompleteBookingCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new CompleteBookingCommand(id), cancellationToken));

    /// <summary>
    /// Records that the client did not show up, once the tolerance of the policy has passed.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 409 when the booking is not confirmed or the tolerance has not passed.</returns>
    private static async Task<Ok<BookingResponse>> MarkClientAbsentAsync(
        Guid id,
        ICommandHandler<MarkClientAbsentCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new MarkClientAbsentCommand(id), cancellationToken));

    /// <summary>
    /// Reverts a client-absent mark made by mistake, within the window of the policy.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="request">Reason for the reversal; required.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 409 when the window closed or another booking took the slot.</returns>
    private static async Task<Ok<BookingResponse>> RevertClientAbsentAsync(
        Guid id,
        RevertClientAbsentRequest request,
        ICommandHandler<RevertClientAbsentCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(id), cancellationToken));

    /// <summary>
    /// Records a payment received face to face.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="request">Payment data.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking.</returns>
    private static async Task<Ok<BookingResponse>> RecordInPersonPaymentAsync(
        Guid id,
        RecordInPersonPaymentRequest request,
        ICommandHandler<RecordInPersonPaymentCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(id), cancellationToken));
}
