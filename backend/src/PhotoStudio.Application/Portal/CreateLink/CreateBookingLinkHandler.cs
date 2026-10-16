using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.Portal.CreateLink;

/// <summary>
/// Creates the link of a booking. The secret token is generated here and returned once; only its hash is stored. Any link the
/// booking had before stops working, which is how a link sent to the wrong person is cancelled.
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="tokens">Generates the secret token and its hash.</param>
/// <param name="settings">Portal address.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class CreateBookingLinkHandler(
    IBookingRepository bookings,
    IBookingLinkRepository links,
    IRefreshTokenGenerator tokens,
    PortalSettings settings,
    TimeProvider timeProvider) : ICommandHandler<CreateBookingLinkCommand, BookingLinkResponse>
{
    /// <summary>
    /// Creates the link.
    /// </summary>
    /// <param name="command">Photographer and booking.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The token and the URL to share.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist or belongs to another photographer.</exception>
    public async Task<BookingLinkResponse> HandleAsync(CreateBookingLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await bookings.GetOwnedAsync(command.BookingId, command.PhotographerId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var generated = tokens.Generate();
        var anchor = booking.Slot.End > now ? booking.Slot.End : now;
        var link = BookingLink.Issue(booking.Id, booking.PhotographerId, generated.Hash, now, anchor + PortalSettings.LinkLifetimeAfterSession);
        await links.ReplaceActiveAsync(link, now, cancellationToken);

        return new BookingLinkResponse(generated.Secret, settings.LinkUrl(generated.Secret), link.ExpiresAt);
    }
}
