using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;

namespace PhotoStudio.Application.Portal.GetBooking;

/// <summary>
/// Reads the booking a link opens, as its client sees it. The link is the only credential, so this exposes no personal data
/// of the client (phone, email) and none of the photographer's internal records.
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="users">User repository, to read the name of the photographer.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class GetPortalBookingHandler(
    IBookingRepository bookings,
    IBookingLinkRepository links,
    IUserRepository users,
    IRefreshTokenGenerator tokens,
    TimeProvider timeProvider) : IQueryHandler<GetPortalBookingQuery, PortalBookingResponse>
{
    /// <summary>
    /// Reads the booking.
    /// </summary>
    /// <param name="query">Token of the link.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The booking as the client sees it.</returns>
    /// <exception cref="NotFoundException">When the token does not open a booking.</exception>
    public async Task<PortalBookingResponse> HandleAsync(GetPortalBookingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var link = await links.ResolveAsync(tokens, query.Token, timeProvider.GetUtcNow(), cancellationToken);
        var booking = await bookings.GetBookingAsync(link, cancellationToken);
        var photographer = await users.GetByIdAsync(booking.PhotographerId, cancellationToken);

        return booking.ToPortalResponse(photographer?.Name ?? string.Empty);
    }
}
