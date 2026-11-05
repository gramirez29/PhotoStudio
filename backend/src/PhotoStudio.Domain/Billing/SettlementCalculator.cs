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
/// Rules: a booking that ends without a confirmed commitment of the client gives everything back; one the photographer
/// cancels gives back the share of what was paid that the policy says; a client that cancels a confirmed booking with less
/// notice than the policy allows, or does not show up, loses the share of the deposit that the policy says (and only of the
/// deposit: anything paid beyond it goes back). Every share is read from the policy that was frozen into the booking when it
/// was created, so changing the photographer's policy later never changes what an existing booking owes. The defaults of the
/// business are 100% in each case.
/// </para>
/// </summary>
public static class SettlementCalculator
{
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
            BookingStatus.ClientAbsent => Build(booking, SettlementReason.ClientAbsent, booking.Policy.ClientAbsentRetention),
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
            return BuildPhotographerCancelled(booking);
        }

        if (cancellation.From == BookingStatus.Tentative)
        {
            return Build(booking, SettlementReason.TentativeCancelled, 0m);
        }

        var hoursBeforeSession = (booking.Slot.Start - cancellation.OccurredAt).TotalHours;
        return hoursBeforeSession >= booking.Policy.FreeCancellationWindowHours
            ? Build(booking, SettlementReason.ClientCancelledInTime, 0m)
            : Build(booking, SettlementReason.ClientCancelledLate, booking.Policy.LateCancellationRetention);
    }

    /// <summary>
    /// Builds the outcome of a booking the photographer cancelled: the policy says which share of everything that was paid goes back,
    /// and the rest is kept. It is all of it unless the photographer's policy says otherwise.
    /// </summary>
    /// <param name="booking">A booking the photographer cancelled.</param>
    /// <returns>The outcome.</returns>
    private static SettlementOutcome BuildPhotographerCancelled(Booking booking)
    {
        var paid = booking.TotalVerifiedPaid;
        var refund = paid.MultiplyBy(booking.Policy.PhotographerCancellationRefund);
        return new SettlementOutcome(SettlementReason.PhotographerCancelled, paid, paid.Subtract(refund), refund);
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
