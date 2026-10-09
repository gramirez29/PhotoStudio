using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Billing;

/// <summary>
/// What a booking that ended owes: how much of the verified payments is kept and how much goes back to the client.
/// </summary>
/// <param name="Reason">Why money is kept or returned.</param>
/// <param name="TotalPaid">Sum of the verified payments.</param>
/// <param name="Retained">Part of the deposit the photographer keeps.</param>
/// <param name="Refund">Part of the payments that goes back to the client.</param>
public sealed record SettlementOutcome(SettlementReason Reason, Money TotalPaid, Money Retained, Money Refund);

/// <summary>
/// Decides the economic outcome of a booking from its current state. It reads the state, not an event, so the answer is the
/// same whatever the order in which events are delivered and however many times each one is.
/// <para>
/// Rules (the defaults of the business, see the design document): a booking that ends because of the photographer, or
/// without a confirmed commitment of the client, gives everything back; a client that cancels a confirmed booking with less
/// notice than the policy allows, or does not show up, loses the deposit (and only the deposit: anything paid beyond it
/// goes back).
/// </para>
/// </summary>
public static class SettlementCalculator
{
    /// <summary>
    /// Share of the deposit retained when the client cancels a confirmed booking too late.
    /// </summary>
    public const decimal LateCancellationRetention = 1m;

    /// <summary>
    /// Share of the deposit retained when the client does not show up.
    /// </summary>
    public const decimal ClientAbsentRetention = 1m;

    /// <summary>
    /// Calculates what the booking owes.
    /// </summary>
    /// <param name="booking">Booking to evaluate.</param>
    /// <returns>The outcome, or <see langword="null"/> when the booking is not in a state that settles money
    /// (it is active, or its session was delivered).</returns>
    public static SettlementOutcome? Calculate(Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        return booking.Status switch
        {
            BookingStatus.Cancelled => CalculateCancelled(booking),
            BookingStatus.Expired => Build(booking, SettlementReason.BookingExpired, 0m),
            BookingStatus.ClientAbsent => Build(booking, SettlementReason.ClientAbsent, ClientAbsentRetention),
            _ => null,
        };
    }

    /// <summary>
    /// Calculates the outcome of a cancelled booking from who cancelled it, when and in which state it was.
    /// </summary>
    /// <param name="booking">A cancelled booking.</param>
    /// <returns>The outcome.</returns>
    private static SettlementOutcome CalculateCancelled(Booking booking)
    {
        var cancellation = booking.History.LastOrDefault(transition => transition.To == BookingStatus.Cancelled);
        if (cancellation is null || cancellation.Actor == Actor.Photographer)
        {
            return Build(booking, SettlementReason.PhotographerCancelled, 0m);
        }

        if (cancellation.From == BookingStatus.Tentative)
        {
            return Build(booking, SettlementReason.TentativeCancelled, 0m);
        }

        var hoursBeforeSession = (booking.Slot.Start - cancellation.OccurredAt).TotalHours;
        return hoursBeforeSession >= booking.Policy.FreeCancellationWindowHours
            ? Build(booking, SettlementReason.ClientCancelledInTime, 0m)
            : Build(booking, SettlementReason.ClientCancelledLate, LateCancellationRetention);
    }

    /// <summary>
    /// Builds the outcome: the retained part is a share of the deposit, never more than what was paid; the rest goes back.
    /// </summary>
    /// <param name="booking">Booking being settled.</param>
    /// <param name="reason">Why money is kept or returned.</param>
    /// <param name="retentionShare">Share of the deposit that is kept, from 0 to 1.</param>
    /// <returns>The outcome.</returns>
    private static SettlementOutcome Build(Booking booking, SettlementReason reason, decimal retentionShare)
    {
        var paid = booking.TotalVerifiedPaid;
        var wanted = booking.DepositRequired.MultiplyBy(retentionShare);
        var retained = wanted.Amount < paid.Amount ? wanted : paid;
        return new SettlementOutcome(reason, paid, retained, paid.Subtract(retained));
    }
}
