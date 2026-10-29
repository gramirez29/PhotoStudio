using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Bookings.SignContractInPerson;

/// <summary>
/// Registers a contract signed in person or on paper; confirms the booking when the deposit is already covered. When the client
/// signed on the photographer's phone, the handler writes the contract text itself with what the client declared and keeps it with
/// the signature, so what was signed can be shown again and emailed later, exactly as for a signature through the portal.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class SignContractInPersonHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : ICommandHandler<SignContractInPersonCommand, BookingResponse>
{
    /// <summary>
    /// Registers the signature.
    /// </summary>
    /// <param name="command">Signature data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated booking as seen by the photographer.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When a value is not valid, the contract changed since the client read it, or the booking cannot be signed.</exception>
    public async Task<BookingResponse> HandleAsync(SignContractInPersonCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetOwnedAsync(command.BookingId, command.PhotographerId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        if (command.IsPaperContract || command.Declaration is null)
        {
            var channel = command.IsPaperContract ? Channel.External : Channel.InPerson;
            booking.SignContract(command.SignerName, command.TemplateVersion, Actor.Photographer, channel, now);
        }
        else
        {
            SignOnThePhone(booking, command, command.Declaration, now);
        }

        await repository.UpdateAsync(booking, cancellationToken);
        return booking.ToResponse(Actor.Photographer, now);
    }

    /// <summary>
    /// Signs on the photographer's phone: validates the declaration, writes the text the client read with their data and signs.
    /// </summary>
    /// <param name="booking">Booking being signed.</param>
    /// <param name="command">Signature data.</param>
    /// <param name="input">What the client declared.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the client read another version of the contract, or a value is not valid.</exception>
    private static void SignOnThePhone(Booking booking, SignContractInPersonCommand command, InPersonDeclaration input, DateTimeOffset now)
    {
        if (!string.Equals(command.TemplateVersion?.Trim(), ContractTemplates.CurrentVersion, StringComparison.Ordinal))
        {
            throw new DomainException(DomainErrorCodes.ContractTemplateChanged, "The contract changed since it was read. Read it again before signing.");
        }

        if (string.IsNullOrWhiteSpace(command.SignerName))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The signer name is required.");
        }

        var declaration = ClientDeclaration.Create(input.IdNumber, input.ParticipantCount, input.SessionLocation, input.ImageUse);
        var text = ContractRenderer.Render(ContractTemplates.GetCurrent(), booking, declaration, command.SignerName, now);
        booking.SignContract(command.SignerName, ContractTemplates.CurrentVersion, Actor.Photographer, Channel.InPerson, now, declaration, text);
    }
}
