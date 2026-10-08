using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Bookings.CreateBooking;

/// <summary>
/// Creates a tentative booking after checking that the photographer's slot is free.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="policyProvider">Provider of the photographer's current policy.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class CreateBookingHandler(
    IBookingRepository repository,
    IBookingPolicyProvider policyProvider,
    TimeProvider timeProvider) : ICommandHandler<CreateBookingCommand, BookingResponse>
{
    /// <summary>
    /// Creates the booking.
    /// </summary>
    /// <param name="command">Booking data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created booking as seen by the photographer.</returns>
    /// <exception cref="ConflictException">When the slot overlaps another active booking.</exception>
    public async Task<BookingResponse> HandleAsync(CreateBookingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var slot = TimeSlot.Create(command.SessionStart, command.SessionEnd);

        if (await repository.HasOverlappingActiveBookingAsync(command.PhotographerId, slot, cancellationToken))
        {
            throw new ConflictException(ApplicationErrorCodes.SlotUnavailable, "The photographer already has a booking in that slot.");
        }

        var policy = await policyProvider.GetPolicyAsync(command.PhotographerId, cancellationToken);

        var booking = Booking.Create(
            Guid.CreateVersion7(now),
            command.PhotographerId,
            ClientContact.Create(command.ClientName, command.ClientPhone),
            command.PackageName,
            Money.Create(command.PackagePrice, command.Currency),
            slot,
            policy,
            now);

        await repository.AddAsync(booking, cancellationToken);
        return booking.ToResponse(Actor.Photographer, now);
    }
}
