using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.SignContractInPerson;

/// <summary>
/// Registers a contract signed in person; confirms the booking when the deposit is already covered.
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
    /// <exception cref="NotFoundException">When the booking does not exist.</exception>
    public async Task<BookingResponse> HandleAsync(SignContractInPersonCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetByIdAsync(command.BookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), command.BookingId);

        var now = timeProvider.GetUtcNow();
        var channel = command.IsPaperContract ? Channel.External : Channel.InPerson;

        booking.SignContract(command.SignerName, command.TemplateVersion, Actor.Photographer, channel, now);
        await repository.UpdateAsync(booking, cancellationToken);
        return booking.ToResponse(Actor.Photographer, now);
    }
}
