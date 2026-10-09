using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Billing;

namespace PhotoStudio.Application.Billing.GetSettlement;

/// <summary>
/// Reads the settlement of a booking. A booking that has none, or one that belongs to another photographer, answers not
/// found, so the identifier of someone else's booking is not confirmed to exist.
/// </summary>
/// <param name="settlements">Settlement repository.</param>
public sealed class GetSettlementHandler(ISettlementRepository settlements)
    : IQueryHandler<GetSettlementQuery, SettlementResponse>
{
    /// <summary>
    /// Reads the settlement.
    /// </summary>
    /// <param name="query">Photographer and booking.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The settlement.</returns>
    /// <exception cref="NotFoundException">When the booking has no settlement or it is not the photographer's.</exception>
    public async Task<SettlementResponse> HandleAsync(GetSettlementQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settlement = await settlements.GetByBookingIdAsync(query.BookingId, cancellationToken);
        if (settlement is null || settlement.PhotographerId != query.PhotographerId)
        {
            throw new NotFoundException(nameof(BookingSettlement), query.BookingId);
        }

        return settlement.ToResponse();
    }
}
