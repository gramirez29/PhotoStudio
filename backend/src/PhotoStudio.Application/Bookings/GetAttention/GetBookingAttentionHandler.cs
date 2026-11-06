using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Bookings.GetAttention;

/// <summary>
/// Counts what in the bookings of a photographer is waiting for them: proofs of payment to verify and tentative bookings about to expire.
/// </summary>
/// <param name="repository">Booking repository.</param>
/// <param name="timeProvider">Clock.</param>
public sealed class GetBookingAttentionHandler(IBookingRepository repository, TimeProvider timeProvider)
    : IQueryHandler<GetBookingAttentionQuery, BookingAttentionResponse>
{
    /// <summary>
    /// Counts both things.
    /// </summary>
    /// <param name="query">The photographer.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The two counts.</returns>
    public async Task<BookingAttentionResponse> HandleAsync(GetBookingAttentionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = timeProvider.GetUtcNow();
        var pendingProofs = await repository.CountWithPendingProofAsync(query.PhotographerId, cancellationToken);
        var expiringSoon = await repository.CountExpiringTentativeAsync(
            query.PhotographerId,
            now.AddHours(AttentionLimits.ExpiringSoonHours),
            cancellationToken);

        return new BookingAttentionResponse(pendingProofs, expiringSoon);
    }
}
