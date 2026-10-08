using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Provides the current booking policy of a photographer, which is copied into each new booking.
/// </summary>
public interface IBookingPolicyProvider
{
    /// <summary>
    /// Gets the policy currently configured for the photographer.
    /// </summary>
    /// <param name="photographerId">Photographer identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The policy to apply to new bookings.</returns>
    Task<BookingPolicy> GetPolicyAsync(Guid photographerId, CancellationToken cancellationToken);
}
