using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.CreateBooking;
using PhotoStudio.Application.Bookings.GetBooking;
using PhotoStudio.Application.Bookings.RecordInPersonPayment;
using PhotoStudio.Application.Bookings.Responses;
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
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetBooking");
        group.MapPost("/{id:guid}/contract/in-person", SignContractInPersonAsync).WithName("SignContractInPerson");
        group.MapPost("/{id:guid}/payments/in-person", RecordInPersonPaymentAsync).WithName("RecordInPersonPayment");

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
