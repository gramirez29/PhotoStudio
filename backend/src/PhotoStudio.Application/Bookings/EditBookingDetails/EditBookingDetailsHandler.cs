using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Bookings.EditBookingDetails;

/// <summary>
/// Corrects the contact data of the client of a booking and, while it is still possible, its package and price. When the email
/// changes, the code the portal may have sent to the previous address is cancelled, so an address that was wrong cannot go on
/// opening the booking with a code it already received.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="codes">Store of the codes the portal sends by email.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class EditBookingDetailsHandler(
    IBookingRepository repository,
    IPortalOtpRepository codes,
    TimeProvider timeProvider) : ICommandHandler<EditBookingDetailsCommand, BookingResponse>
{
    /// <summary>
    /// Edits the booking.
    /// </summary>
    /// <param name="command">New values.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated booking as seen by the photographer.</returns>
    /// <exception cref="NotFoundException">When the booking does not exist or is not the photographer's.</exception>
    /// <exception cref="DomainException">When a value is not valid, or the booking does not allow the change.</exception>
    /// <exception cref="ConflictException">When another write changed the booking first.</exception>
    public async Task<BookingResponse> HandleAsync(EditBookingDetailsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var booking = await repository.GetOwnedAsync(command.BookingId, command.PhotographerId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var client = ClientContact.Create(command.ClientName, command.ClientPhone, command.ClientEmail);
        var price = Money.Create(command.PackagePrice, booking.PackagePrice.Currency);
        var emailChanged = !string.Equals(client.Email, booking.Client.Email, StringComparison.Ordinal);

        booking.EditDetails(client, command.PackageName, price, now);
        await repository.UpdateAsync(booking, cancellationToken);

        if (emailChanged)
        {
            await codes.DeleteAsync(booking.Id, cancellationToken);
        }

        return booking.ToResponse(Actor.Photographer, now);
    }
}
