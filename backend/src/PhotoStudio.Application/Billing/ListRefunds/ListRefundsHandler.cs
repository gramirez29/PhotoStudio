using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Billing.ListRefunds;

/// <summary>
/// Lists the pending refunds of a photographer: money that belongs to a client and has not been given back yet.
/// </summary>
/// <param name="settlements">Settlement repository.</param>
public sealed class ListRefundsHandler(ISettlementRepository settlements)
    : IQueryHandler<ListRefundsQuery, RefundListResponse>
{
    /// <summary>
    /// Maximum number of refunds returned.
    /// </summary>
    public const int MaxItems = 100;

    /// <summary>
    /// Reads the pending refunds.
    /// </summary>
    /// <param name="query">Photographer whose refunds are read.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The pending refunds and how many there are.</returns>
    public async Task<RefundListResponse> HandleAsync(ListRefundsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pending = await settlements.ListPendingRefundsAsync(query.PhotographerId, MaxItems, cancellationToken);
        var count = await settlements.CountPendingRefundsAsync(query.PhotographerId, cancellationToken);

        return new RefundListResponse([.. pending.Select(settlement => settlement.ToResponse())], count);
    }
}
