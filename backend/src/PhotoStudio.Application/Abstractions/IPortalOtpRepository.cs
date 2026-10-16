using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="PortalOtp"/>. Implemented in the Infrastructure layer.
/// </summary>
/// <remarks>
/// A guess is counted with an atomic increment before it is compared, so many guesses sent in parallel each get their own
/// number and only the first few are ever evaluated. Reading, comparing and then writing would let an attacker try many
/// codes at once before the counter moved.
/// </remarks>
public interface IPortalOtpRepository
{
    /// <summary>
    /// Loads the current code record of a booking.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The record, or <see langword="null"/> when no code was requested or it was spent.</returns>
    Task<PortalOtp?> GetAsync(Guid bookingId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the record of a newly sent code, replacing the previous one of the booking.
    /// </summary>
    /// <param name="otp">Record to store.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the record is stored.</returns>
    Task SaveAsync(PortalOtp otp, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically counts one more guess and returns the record as it is after counting.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The record after counting, or <see langword="null"/> when there is no code to guess.</returns>
    Task<PortalOtp?> RegisterAttemptAsync(Guid bookingId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the code of a booking: it was used or it was spent by too many guesses.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the code is gone.</returns>
    Task DeleteAsync(Guid bookingId, CancellationToken cancellationToken);
}
