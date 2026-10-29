using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Portal.GetContract;

/// <summary>
/// Reads the contract of a booking as its client sees it. Unlike the booking page, the text holds personal data (the phone the
/// client gave and, once signed, the identity number), so it needs the portal session that proves the client controls the email
/// the photographer registered; the link alone is not enough. A contract signed through the portal returns exactly the text that
/// was signed; one signed in person returns no text.
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class GetPortalContractHandler(
    IBookingRepository bookings,
    IBookingLinkRepository links,
    IRefreshTokenGenerator tokens,
    TimeProvider timeProvider) : IQueryHandler<GetPortalContractQuery, PortalContractResponse>
{
    /// <summary>
    /// Reads the contract.
    /// </summary>
    /// <param name="query">Token of the link and the booking the session is valid for.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The contract text and whether it is signed.</returns>
    /// <exception cref="NotFoundException">When the token does not open the booking the session was issued for.</exception>
    public async Task<PortalContractResponse> HandleAsync(GetPortalContractQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var link = await links.ResolveAsync(tokens, query.Token, timeProvider.GetUtcNow(), cancellationToken);
        if (link.BookingId != query.AuthorizedBookingId)
        {
            throw new NotFoundException(nameof(Booking), Guid.Empty);
        }

        var booking = await bookings.GetBookingAsync(link, cancellationToken);

        if (booking.Contract is { } signature)
        {
            return new PortalContractResponse(signature.TemplateVersion, signature.SignedText ?? string.Empty, Signed: true);
        }

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), booking, declaration: null, signerName: null, signedAt: null);
        return new PortalContractResponse(ContractTemplates.CurrentVersion, text, Signed: false);
    }
}
