using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Portal.SignContract;

/// <summary>
/// Signs the contract on behalf of the client who read it through the link. The portal session must be the one issued for the
/// booking of the link. The handler writes the contract text itself and keeps it with the signature, so what the client signed
/// can be shown again later even if the booking changes. The booking confirms by itself when the deposit is already covered (B6).
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="links">Link repository.</param>
/// <param name="users">User repository, to read the name of the photographer.</param>
/// <param name="tokens">Hashes the token taken from the URL.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class SignPortalContractHandler(
    IBookingRepository bookings,
    IBookingLinkRepository links,
    IUserRepository users,
    IRefreshTokenGenerator tokens,
    TimeProvider timeProvider) : ICommandHandler<SignPortalContractCommand, PortalBookingResponse>
{
    /// <summary>
    /// Signs the contract.
    /// </summary>
    /// <param name="command">Signature data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The booking as the client sees it after signing.</returns>
    /// <exception cref="NotFoundException">When the token does not open the booking the session was issued for.</exception>
    /// <exception cref="DomainException">When a value is not valid, the client did not accept, the contract changed since they read it, or the booking cannot be signed.</exception>
    /// <exception cref="ConflictException">When another write changed the booking first.</exception>
    public async Task<PortalBookingResponse> HandleAsync(SignPortalContractCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var link = await links.ResolveAsync(tokens, command.Token, now, cancellationToken);
        if (link.BookingId != command.AuthorizedBookingId)
        {
            throw new NotFoundException(nameof(Booking), Guid.Empty);
        }

        if (!command.AcceptsTerms)
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The client must accept the contract to sign it.");
        }

        if (!string.Equals(command.TemplateVersion?.Trim(), ContractTemplates.CurrentVersion, StringComparison.Ordinal))
        {
            throw new DomainException(DomainErrorCodes.ContractTemplateChanged, "The contract changed since it was read. Read it again before signing.");
        }

        if (string.IsNullOrWhiteSpace(command.SignerName))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The signer name is required.");
        }

        var booking = await bookings.GetBookingAsync(link, cancellationToken);
        var declaration = ClientDeclaration.Create(command.IdNumber, command.ParticipantCount, command.SessionLocation, command.ImageUse);
        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), booking, declaration, command.SignerName, now);

        booking.SignContract(command.SignerName, ContractTemplates.CurrentVersion, Actor.Client, Channel.Portal, now, declaration, text);
        await bookings.UpdateAsync(booking, cancellationToken);

        var photographer = await users.GetByIdAsync(booking.PhotographerId, cancellationToken);
        return booking.ToPortalResponse(photographer?.Name ?? string.Empty);
    }
}
