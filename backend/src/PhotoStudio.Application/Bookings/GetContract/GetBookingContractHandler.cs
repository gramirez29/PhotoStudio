using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.Exceptions;

namespace PhotoStudio.Application.Bookings.GetContract;

/// <summary>
/// Reads the contract of a booking for its photographer, so the app can show it before the client signs on the phone. Before
/// signing, the text has the booking data filled in and what only the client knows left blank; after a signature with a text, it is
/// the exact text that was signed; a contract registered on paper has no text.
/// </summary>
/// <param name="repository">Booking repository.</param>
public sealed class GetBookingContractHandler(IBookingRepository repository) : IQueryHandler<GetBookingContractQuery, BookingContractResponse>
{
    /// <summary>
    /// Reads the contract.
    /// </summary>
    /// <param name="query">Photographer and booking.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The contract text and whether it is signed.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist or is not the photographer's.</exception>
    public async Task<BookingContractResponse> HandleAsync(GetBookingContractQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var booking = await repository.GetOwnedAsync(query.BookingId, query.PhotographerId, cancellationToken);

        if (booking.Contract is { } signature)
        {
            return new BookingContractResponse(signature.TemplateVersion, signature.SignedText ?? string.Empty, Signed: true);
        }

        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), booking, declaration: null, signerName: null, signedAt: null);
        return new BookingContractResponse(ContractTemplates.CurrentVersion, text, Signed: false);
    }
}
