using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.Auth;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.CancelBooking;
using PhotoStudio.Application.Bookings.CompleteBooking;
using PhotoStudio.Application.Bookings.CreateBooking;
using PhotoStudio.Application.Bookings.GetBooking;
using PhotoStudio.Application.Bookings.GetCalendar;
using PhotoStudio.Application.Bookings.GetStats;
using PhotoStudio.Application.Bookings.ListBookings;
using PhotoStudio.Application.Bookings.MarkClientAbsent;
using PhotoStudio.Application.Bookings.RecordInPersonPayment;
using PhotoStudio.Application.Bookings.RescheduleBooking;
using PhotoStudio.Application.Bookings.EditBookingDetails;
using PhotoStudio.Application.Bookings.GetContract;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Bookings.RevertClientAbsent;
using PhotoStudio.Application.Bookings.SignContractInPerson;
using PhotoStudio.Application.Pagination;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// Photographer-facing booking endpoints. Every one requires a signed-in photographer, and every operation is scoped to the
/// photographer of the access token. Client-portal endpoints (token based) will live in a separate group.
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

        var group = endpoints.MapGroup("/api/bookings").WithTags("Bookings").RequireAuthorization();

        group.MapPost("/", CreateAsync).WithName("CreateBooking");
        group.MapGet("/", ListAsync).WithName("ListBookings");
        group.MapGet("/calendar", CalendarAsync).WithName("GetBookingCalendar");
        group.MapGet("/stats", StatsAsync).WithName("GetBookingStats");
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetBooking");
        group.MapPost("/{id:guid}/details", EditDetailsAsync).WithName("EditBookingDetails");
        group.MapGet("/{id:guid}/contract", GetContractAsync).WithName("GetBookingContract");
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
    /// <param name="user">Authenticated photographer; becomes the owner of the booking.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>201 with the created booking.</returns>
    private static async Task<Created<BookingResponse>> CreateAsync(
        CreateBookingRequest request,
        ClaimsPrincipal user,
        ICommandHandler<CreateBookingCommand, BookingResponse> handler,
        CancellationToken cancellationToken)
    {
        var response = await handler.HandleAsync(request.ToCommand(user.GetPhotographerId()), cancellationToken);
        return TypedResults.Created($"/api/bookings/{response.Id}", response);
    }

    /// <summary>
    /// Lists one page of the bookings of the authenticated photographer, the most recently created first.
    /// </summary>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <param name="cursor">The <c>nextCursor</c> of the previous page; omitted for the first page. 400 when it is not valid.</param>
    /// <param name="limit">How many bookings the page has (5 by default, at most 50).</param>
    /// <param name="status">Only the bookings in this status; 400 when it is not a booking status.</param>
    /// <param name="name">Only the bookings whose client name contains this text, ignoring case.</param>
    /// <returns>200 with the page of booking summaries and the cursor of the next one, if any.</returns>
    private static async Task<Ok<BookingListResponse>> ListAsync(
        ClaimsPrincipal user,
        IQueryHandler<ListBookingsQuery, BookingListResponse> handler,
        CancellationToken cancellationToken,
        string? cursor = null,
        int? limit = null,
        string? status = null,
        string? name = null) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListBookingsQuery(
                user.GetPhotographerId(),
                PageCursors.Read(cursor),
                PageSizes.Resolve(limit),
                BookingListFilter.From(ReadStatus(status), name)),
            cancellationToken));

    /// <summary>
    /// Lists the bookings whose session overlaps a range of time, for the calendar of the app: no pages, ordered by the start of the
    /// session. By default only the tentative and the confirmed ones; <c>includeEnded=true</c> adds the ones that are over.
    /// </summary>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <param name="from">Start of the range, ISO 8601 with its offset. Required.</param>
    /// <param name="to">End of the range, exclusive, ISO 8601 with its offset. Required; after <paramref name="from"/> and at most 62 days from it.</param>
    /// <param name="includeEnded">Also the completed, client-absent, cancelled and expired bookings; <see langword="false"/> by default.</param>
    /// <returns>200 with the bookings of the range, and whether some were left out for being more than 500; 400 when the range is missing, inverted or too long.</returns>
    /// <exception cref="BadHttpRequestException">The range is missing, empty, inverted or longer than the limit.</exception>
    private static async Task<Ok<CalendarResponse>> CalendarAsync(
        ClaimsPrincipal user,
        IQueryHandler<GetCalendarQuery, CalendarResponse> handler,
        CancellationToken cancellationToken,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        bool includeEnded = false)
    {
        if (from is null || to is null)
        {
            throw new BadHttpRequestException("The range needs 'from' and 'to'.");
        }

        if (to <= from || to - from > TimeSpan.FromDays(CalendarLimits.MaxRangeDays))
        {
            throw new BadHttpRequestException($"The range must be positive and at most {CalendarLimits.MaxRangeDays} days.");
        }

        return TypedResults.Ok(await handler.HandleAsync(new GetCalendarQuery(user.GetPhotographerId(), from.Value, to.Value, includeEnded), cancellationToken));
    }

    /// <summary>
    /// Counts the bookings of the photographer by status, for the home screen. A booking belongs to a period by the start of its session.
    /// Without <paramref name="from"/> and <paramref name="to"/> it counts all of them.
    /// </summary>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <param name="from">Start of the period, ISO 8601 with its offset; optional.</param>
    /// <param name="to">End of the period, exclusive, ISO 8601 with its offset; optional.</param>
    /// <returns>200 with the total and one count per status, zero included; 400 when the period ends before it starts.</returns>
    /// <exception cref="BadHttpRequestException">The period is empty or inverted.</exception>
    private static async Task<Ok<BookingStatsResponse>> StatsAsync(
        ClaimsPrincipal user,
        IQueryHandler<GetBookingStatsQuery, BookingStatsResponse> handler,
        CancellationToken cancellationToken,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        if (from is not null && to is not null && to <= from)
        {
            throw new BadHttpRequestException("The period must end after it starts.");
        }

        return TypedResults.Ok(await handler.HandleAsync(new GetBookingStatsQuery(user.GetPhotographerId(), from, to), cancellationToken));
    }

    /// <summary>
    /// Reads the status filter of the query string.
    /// </summary>
    /// <param name="status">The text received, or <see langword="null"/> when there is no status filter.</param>
    /// <returns>The status, or <see langword="null"/> when none was asked for.</returns>
    /// <exception cref="BadHttpRequestException">The text is not the name of a booking status.</exception>
    private static BookingStatus? ReadStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return Enum.TryParse<BookingStatus>(status, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new BadHttpRequestException("The status filter is not a booking status.");
    }

    /// <summary>
    /// Reads a booking.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must belong to them, otherwise the answer is 404.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the booking.</returns>
    private static async Task<Ok<BookingResponse>> GetByIdAsync(
        Guid id,
        ClaimsPrincipal user,
        IQueryHandler<GetBookingQuery, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetBookingQuery(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Corrects the contact data of the client of a booking and, while it is still possible, its package and price.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must belong to them, otherwise the answer is 404.</param>
    /// <param name="request">Every editable value.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 409 <c>booking.invalid_transition</c> when the booking can no longer be edited, or <c>booking.guard_failed</c> when the package or the price changed after the contract was signed or a payment was recorded; 422 for invalid values.</returns>
    private static async Task<Ok<BookingResponse>> EditDetailsAsync(
        Guid id,
        ClaimsPrincipal user,
        EditBookingDetailsRequest request,
        ICommandHandler<EditBookingDetailsCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Reads the contract of a booking, with the booking data filled in, so the client can read it on the phone before signing.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must belong to them, otherwise the answer is 404.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the contract text and whether it is signed.</returns>
    private static async Task<Ok<BookingContractResponse>> GetContractAsync(
        Guid id,
        ClaimsPrincipal user,
        IQueryHandler<GetBookingContractQuery, BookingContractResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetBookingContractQuery(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Registers a contract signed in person or on paper.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must belong to them, otherwise the answer is 404.</param>
    /// <param name="request">Signature data.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking.</returns>
    private static async Task<Ok<BookingResponse>> SignContractInPersonAsync(
        Guid id,
        ClaimsPrincipal user,
        SignContractInPersonRequest request,
        ICommandHandler<SignContractInPersonCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Moves a confirmed booking to another slot. Photographer-only until the client portal exists.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must belong to them, otherwise the answer is 404.</param>
    /// <param name="request">New slot.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 409 when the slot is taken or the booking is not confirmed.</returns>
    private static async Task<Ok<BookingResponse>> RescheduleAsync(
        Guid id,
        ClaimsPrincipal user,
        RescheduleBookingRequest request,
        ICommandHandler<RescheduleBookingCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Cancels a booking. Photographer-only until the client portal exists.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must belong to them, otherwise the answer is 404.</param>
    /// <param name="request">JSON body with the reason; the reason is mandatory once the booking is confirmed, otherwise send <c>{}</c>.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 422 when the reason is missing; 409 when it cannot be cancelled.</returns>
    private static async Task<Ok<BookingResponse>> CancelAsync(
        Guid id,
        ClaimsPrincipal user,
        CancelBookingRequest request,
        ICommandHandler<CancelBookingCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Marks a confirmed booking as completed once its session has started.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must belong to them, otherwise the answer is 404.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 409 when the booking is not confirmed or the session has not started.</returns>
    private static async Task<Ok<BookingResponse>> CompleteAsync(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<CompleteBookingCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new CompleteBookingCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Records that the client did not show up, once the tolerance of the policy has passed.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must belong to them, otherwise the answer is 404.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 409 when the booking is not confirmed or the tolerance has not passed.</returns>
    private static async Task<Ok<BookingResponse>> MarkClientAbsentAsync(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<MarkClientAbsentCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new MarkClientAbsentCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Reverts a client-absent mark made by mistake, within the window of the policy.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must belong to them, otherwise the answer is 404.</param>
    /// <param name="request">Reason for the reversal; required.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking; 409 when the window closed or another booking took the slot.</returns>
    private static async Task<Ok<BookingResponse>> RevertClientAbsentAsync(
        Guid id,
        ClaimsPrincipal user,
        RevertClientAbsentRequest request,
        ICommandHandler<RevertClientAbsentCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Records a payment received face to face.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="user">Authenticated photographer; the booking must belong to them, otherwise the answer is 404.</param>
    /// <param name="request">Payment data.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the updated booking.</returns>
    private static async Task<Ok<BookingResponse>> RecordInPersonPaymentAsync(
        Guid id,
        ClaimsPrincipal user,
        RecordInPersonPaymentRequest request,
        ICommandHandler<RecordInPersonPaymentCommand, BookingResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(request.ToCommand(user.GetPhotographerId(), id), cancellationToken));
}
