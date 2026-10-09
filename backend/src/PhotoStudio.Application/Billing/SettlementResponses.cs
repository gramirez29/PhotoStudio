using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Domain.Billing;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Billing;

/// <summary>
/// The money side of a booking that ended, as exposed by the API.
/// </summary>
/// <param name="Id">Settlement identifier.</param>
/// <param name="BookingId">Booking it is about.</param>
/// <param name="ClientName">Name of the client.</param>
/// <param name="ClientPhone">Phone of the client.</param>
/// <param name="PackageName">Name of the booked package.</param>
/// <param name="SessionStart">Start of the session (UTC).</param>
/// <param name="Reason">Why money is kept or returned.</param>
/// <param name="TotalPaid">Sum of the verified payments.</param>
/// <param name="Retained">Part of the deposit the photographer keeps.</param>
/// <param name="RetentionStatus">State of the retention.</param>
/// <param name="RefundAmount">Amount that goes back to the client.</param>
/// <param name="RefundStatus">State of the refund.</param>
/// <param name="RefundMethod">How the money was given back, once completed.</param>
/// <param name="RefundNote">Note left when completing the refund.</param>
/// <param name="RefundCompletedAt">Instant the refund was completed (UTC).</param>
/// <param name="CreatedAt">Instant the settlement was opened (UTC).</param>
public sealed record SettlementResponse(
    Guid Id,
    Guid BookingId,
    string ClientName,
    string ClientPhone,
    string PackageName,
    DateTimeOffset SessionStart,
    SettlementReason Reason,
    MoneyResponse TotalPaid,
    MoneyResponse Retained,
    RetentionStatus RetentionStatus,
    MoneyResponse RefundAmount,
    RefundStatus RefundStatus,
    PaymentMethod? RefundMethod,
    string? RefundNote,
    DateTimeOffset? RefundCompletedAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// The refunds a photographer still has to give back.
/// </summary>
/// <param name="Items">Settlements with a pending refund, the oldest first.</param>
/// <param name="PendingCount">How many refunds are pending (not only the ones listed).</param>
public sealed record RefundListResponse(IReadOnlyList<SettlementResponse> Items, int PendingCount);

/// <summary>
/// Mapping from settlements to their API responses.
/// </summary>
public static class SettlementResponseMappings
{
    /// <summary>
    /// Maps a settlement to its response.
    /// </summary>
    /// <param name="settlement">Settlement to map.</param>
    /// <returns>The response.</returns>
    public static SettlementResponse ToResponse(this BookingSettlement settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        return new SettlementResponse(
            settlement.Id,
            settlement.BookingId,
            settlement.Snapshot.ClientName,
            settlement.Snapshot.ClientPhone,
            settlement.Snapshot.PackageName,
            settlement.Snapshot.SessionStart,
            settlement.Reason,
            new MoneyResponse(settlement.TotalPaid.Amount, settlement.TotalPaid.Currency),
            new MoneyResponse(settlement.Retained.Amount, settlement.Retained.Currency),
            settlement.RetentionStatus,
            new MoneyResponse(settlement.RefundAmount.Amount, settlement.RefundAmount.Currency),
            settlement.RefundStatus,
            settlement.RefundMethod,
            settlement.RefundNote,
            settlement.RefundCompletedAt,
            settlement.CreatedAt);
    }
}
