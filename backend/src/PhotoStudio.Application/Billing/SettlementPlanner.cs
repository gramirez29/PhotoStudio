using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Billing;

namespace PhotoStudio.Application.Billing;

/// <summary>
/// Brings the settlement of a booking in line with what the booking owes now. It works from the current state of the booking,
/// not from the event that triggered it, so it gives the same result whatever the order in which events are delivered and
/// however many times each one is: the outbox delivers at least once and does not guarantee order between events.
/// </summary>
/// <param name="bookings">Booking repository.</param>
/// <param name="settlements">Settlement repository.</param>
public sealed class SettlementPlanner(IBookingRepository bookings, ISettlementRepository settlements)
{
    /// <summary>
    /// Attempts made when another request changes the settlement at the same time.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Makes the settlement of the booking match its state: opens it when the booking ended with money paid, recalculates it
    /// if the booking ended differently since, and undoes it when the booking is active again.
    /// </summary>
    /// <param name="bookingId">Booking to reconcile.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the settlement matches the booking.</returns>
    /// <exception cref="ConflictException">When the settlement kept changing under it after <see cref="MaxAttempts"/> attempts;
    /// the outbox retries the event.</exception>
    public async Task ReconcileAsync(Guid bookingId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await ReconcileOnceAsync(bookingId, now, cancellationToken);
                return;
            }
            catch (ConflictException) when (attempt < MaxAttempts)
            {
                // Two events for the same booking ran at once and the other one got there first: read it again and redo.
            }
        }
    }

    /// <summary>
    /// One attempt to reconcile.
    /// </summary>
    /// <param name="bookingId">Booking to reconcile.</param>
    /// <param name="now">Current instant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the settlement matches the booking.</returns>
    private async Task ReconcileOnceAsync(Guid bookingId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var booking = await bookings.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null)
        {
            return;
        }

        var outcome = SettlementCalculator.Calculate(booking);
        var existing = await settlements.GetByBookingIdAsync(bookingId, cancellationToken);

        if (outcome is null)
        {
            // The booking is active (or delivered): whatever was settled for an earlier ending no longer applies.
            if (existing is not null && existing.Void(now))
            {
                await settlements.UpdateAsync(existing, cancellationToken);
            }

            return;
        }

        if (existing is null)
        {
            // Nothing was paid, so nothing is kept or returned.
            if (outcome.TotalPaid.Amount == 0m)
            {
                return;
            }

            var snapshot = new SettlementSnapshot(booking.Client.Name, booking.Client.Phone, booking.PackageName, booking.Slot.Start);
            var opened = BookingSettlement.Open(booking.PhotographerId, booking.Id, snapshot, outcome, now);
            if (!await settlements.TryAddAsync(opened, cancellationToken))
            {
                throw new ConflictException(
                    ApplicationErrorCodes.ConcurrencyConflict,
                    $"The settlement of booking {bookingId} was created by another request.");
            }

            return;
        }

        if (existing.Apply(outcome, now))
        {
            await settlements.UpdateAsync(existing, cancellationToken);
        }
    }
}
