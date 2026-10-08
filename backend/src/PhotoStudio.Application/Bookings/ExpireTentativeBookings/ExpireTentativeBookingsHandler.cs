using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Bookings.ExpireTentativeBookings;

/// <summary>
/// B7. Expires tentative bookings whose hold ended. The domain re-evaluates every guard when it runs, so the job is
/// idempotent and safe to repeat; saving an expired booking releases its slot.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class ExpireTentativeBookingsHandler(
    IBookingRepository repository,
    TimeProvider timeProvider) : ICommandHandler<ExpireTentativeBookingsCommand, ExpireTentativeBookingsResult>
{
    /// <summary>
    /// Expires every due booking in the batch. A booking that cannot expire right now is skipped and examined again on the
    /// next run: a pending proof of payment blocks expiration, and a concurrent write (a payment verified at the same
    /// moment) wins the race through optimistic concurrency.
    /// </summary>
    /// <param name="command">Batch size.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>How many bookings expired and how many were skipped.</returns>
    public async Task<ExpireTentativeBookingsResult> HandleAsync(ExpireTentativeBookingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var due = await repository.ListExpiredTentativeAsync(now, command.BatchSize, cancellationToken);

        var expired = 0;
        var skipped = 0;
        foreach (var booking in due)
        {
            try
            {
                booking.Expire(now);
                await repository.UpdateAsync(booking, cancellationToken);
                expired++;
            }
            catch (Exception exception) when (exception is DomainException or ConflictException)
            {
                skipped++;
            }
        }

        return new ExpireTentativeBookingsResult(expired, skipped);
    }
}
