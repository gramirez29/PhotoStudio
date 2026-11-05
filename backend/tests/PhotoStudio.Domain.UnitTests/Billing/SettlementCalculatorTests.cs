using PhotoStudio.Domain.Billing;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.UnitTests.Bookings;

namespace PhotoStudio.Domain.UnitTests.Billing;

/// <summary>
/// Tests of <see cref="SettlementCalculator"/>: what a booking that ended owes, from its state.
/// </summary>
public sealed class SettlementCalculatorTests
{
    private static readonly DateTimeOffset Now = BookingTestData.Now;

    private static readonly DateTimeOffset SessionStart = BookingTestData.SessionStart;

    /// <summary>
    /// Builds a tentative booking with a verified payment in person (no contract, so it stays tentative).
    /// </summary>
    /// <param name="amount">Amount paid.</param>
    /// <returns>The booking.</returns>
    private static Booking TentativeWithPayment(decimal amount)
    {
        var booking = BookingTestData.CreateTentative();
        booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(amount), PaymentMethod.Cash, "pay-1", Now);
        return booking;
    }

    /// <summary>
    /// Builds a confirmed booking (deposit of 50 000 paid) with extra money paid beyond the deposit.
    /// </summary>
    /// <param name="extra">Amount paid on top of the deposit.</param>
    /// <returns>The booking.</returns>
    private static Booking ConfirmedWithExtra(decimal extra)
    {
        var booking = BookingTestData.CreateConfirmed();
        if (extra > 0m)
        {
            booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(extra), PaymentMethod.Cash, "extra", Now);
        }

        return booking;
    }

    /// <summary>
    /// A booking that is still active settles nothing.
    /// </summary>
    [Fact]
    public void Calculate_ForAnActiveOrCompletedBooking_ReturnsNull()
    {
        var confirmed = BookingTestData.CreateConfirmed();
        var completed = BookingTestData.CreateConfirmed();
        completed.Complete(SessionStart.AddHours(1));

        SettlementCalculator.Calculate(BookingTestData.CreateTentative()).ShouldBeNull();
        SettlementCalculator.Calculate(confirmed).ShouldBeNull();
        SettlementCalculator.Calculate(completed).ShouldBeNull();
    }

    /// <summary>
    /// When the photographer cancels, everything paid goes back, even a few hours before the session.
    /// </summary>
    [Fact]
    public void Calculate_WhenThePhotographerCancels_RefundsEverything()
    {
        var booking = BookingTestData.CreateConfirmed();
        booking.Cancel(Actor.Photographer, "Sick", SessionStart.AddHours(-5));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.Reason.ShouldBe(SettlementReason.PhotographerCancelled);
        outcome.Retained.ShouldBe(Money.Zero());
        outcome.Refund.ShouldBe(Money.Create(50_000m));
    }

    /// <summary>
    /// A client that cancels a tentative booking is not charged.
    /// </summary>
    [Fact]
    public void Calculate_WhenTheClientCancelsATentativeBooking_RefundsEverything()
    {
        var booking = TentativeWithPayment(20_000m);
        booking.Cancel(Actor.Client, null, SessionStart.AddHours(-1));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.Reason.ShouldBe(SettlementReason.TentativeCancelled);
        outcome.Retained.Amount.ShouldBe(0m);
        outcome.Refund.ShouldBe(Money.Create(20_000m));
    }

    /// <summary>
    /// A client that cancels a confirmed booking with enough notice is not charged.
    /// </summary>
    [Fact]
    public void Calculate_WhenTheClientCancelsInTime_RefundsEverything()
    {
        var booking = BookingTestData.CreateConfirmed();
        booking.Cancel(Actor.Client, "Plans changed", SessionStart.AddHours(-72));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.Reason.ShouldBe(SettlementReason.ClientCancelledInTime);
        outcome.Retained.Amount.ShouldBe(0m);
        outcome.Refund.ShouldBe(Money.Create(50_000m));
    }

    /// <summary>
    /// A client that cancels a confirmed booking too late loses the deposit.
    /// </summary>
    [Fact]
    public void Calculate_WhenTheClientCancelsLate_RetainsTheDeposit()
    {
        var booking = BookingTestData.CreateConfirmed();
        booking.Cancel(Actor.Client, "Plans changed", SessionStart.AddHours(-71));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.Reason.ShouldBe(SettlementReason.ClientCancelledLate);
        outcome.Retained.ShouldBe(Money.Create(50_000m));
        outcome.Refund.Amount.ShouldBe(0m);
    }

    /// <summary>
    /// Only the deposit is retained: what the client paid beyond it goes back.
    /// </summary>
    [Fact]
    public void Calculate_WhenTheClientCancelsLate_ReturnsWhatWasPaidBeyondTheDeposit()
    {
        var booking = ConfirmedWithExtra(30_000m);
        booking.Cancel(Actor.Client, "Plans changed", SessionStart.AddHours(-2));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.TotalPaid.ShouldBe(Money.Create(80_000m));
        outcome.Retained.ShouldBe(Money.Create(50_000m));
        outcome.Refund.ShouldBe(Money.Create(30_000m));
    }

    /// <summary>
    /// A tentative booking that expired with money paid gives it back.
    /// </summary>
    [Fact]
    public void Calculate_ForAnExpiredBookingWithPayments_RefundsEverything()
    {
        var booking = TentativeWithPayment(20_000m);
        booking.Expire(Now.AddDays(3));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.Reason.ShouldBe(SettlementReason.BookingExpired);
        outcome.Refund.ShouldBe(Money.Create(20_000m));
    }

    /// <summary>
    /// A client that did not show up loses the deposit.
    /// </summary>
    [Fact]
    public void Calculate_ForAClientAbsent_RetainsTheDeposit()
    {
        var booking = ConfirmedWithExtra(10_000m);
        booking.MarkClientAbsent(SessionStart.AddMinutes(BookingPolicy.Default.ClientAbsentToleranceMinutes));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.Reason.ShouldBe(SettlementReason.ClientAbsent);
        outcome.Retained.ShouldBe(Money.Create(50_000m));
        outcome.Refund.ShouldBe(Money.Create(10_000m));
    }

    /// <summary>
    /// What a client who cancels late loses is the share of the deposit the policy of the booking says, not always all of it; what
    /// was paid beyond the deposit still goes back.
    /// </summary>
    [Fact]
    public void Calculate_WhenTheClientCancelsLate_RetainsOnlyTheShareOfThePolicy()
    {
        var booking = ConfirmedWithPolicy(BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, lateCancellationRetention: 0.5m), extra: 30_000m);
        booking.Cancel(Actor.Client, "Plans changed", SessionStart.AddHours(-2));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.Reason.ShouldBe(SettlementReason.ClientCancelledLate);
        outcome.Retained.ShouldBe(Money.Create(25_000m));
        outcome.Refund.ShouldBe(Money.Create(55_000m));
    }

    /// <summary>
    /// A policy that keeps nothing when the client cancels late still reports the late reason but returns everything.
    /// </summary>
    [Fact]
    public void Calculate_WhenTheClientCancelsLateAndThePolicyKeepsNothing_RefundsEverything()
    {
        var booking = ConfirmedWithPolicy(BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, lateCancellationRetention: 0m), extra: 0m);
        booking.Cancel(Actor.Client, "Plans changed", SessionStart.AddHours(-2));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.Reason.ShouldBe(SettlementReason.ClientCancelledLate);
        outcome.Retained.Amount.ShouldBe(0m);
        outcome.Refund.ShouldBe(Money.Create(50_000m));
    }

    /// <summary>
    /// The share kept when the client does not show up is its own: it can differ from the one of a late cancellation.
    /// </summary>
    [Fact]
    public void Calculate_ForAClientAbsent_RetainsTheShareOfThePolicy_NotTheOneOfTheLateCancellation()
    {
        var policy = BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, lateCancellationRetention: 1m, clientAbsentRetention: 0.4m);
        var booking = ConfirmedWithPolicy(policy, extra: 10_000m);
        booking.MarkClientAbsent(SessionStart.AddMinutes(policy.ClientAbsentToleranceMinutes));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.Reason.ShouldBe(SettlementReason.ClientAbsent);
        outcome.Retained.ShouldBe(Money.Create(20_000m));
        outcome.Refund.ShouldBe(Money.Create(40_000m));
    }

    /// <summary>
    /// When the photographer cancels, the policy says which share of everything paid goes back; the rest is kept.
    /// </summary>
    [Fact]
    public void Calculate_WhenThePhotographerCancels_RefundsTheShareOfThePolicy()
    {
        var booking = ConfirmedWithPolicy(BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, photographerCancellationRefund: 0.5m), extra: 30_000m);
        booking.Cancel(Actor.Photographer, "Sick", SessionStart.AddHours(-5));

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.Reason.ShouldBe(SettlementReason.PhotographerCancelled);
        outcome.TotalPaid.ShouldBe(Money.Create(80_000m));
        outcome.Refund.ShouldBe(Money.Create(40_000m));
        outcome.Retained.ShouldBe(Money.Create(40_000m));
    }

    /// <summary>
    /// A booking is settled by the policy it was created with, whatever the policy of the photographer is now: two bookings of the
    /// same cancellation owe different amounts only because their frozen policies differ.
    /// </summary>
    [Fact]
    public void Calculate_UsesThePolicyFrozenInEachBooking()
    {
        var strict = ConfirmedWithPolicy(BookingPolicy.Default, extra: 0m);
        var gentle = ConfirmedWithPolicy(BookingPolicy.Create(48, 0.5m, 72, 48, 1, 30, 7, lateCancellationRetention: 0.2m), extra: 0m);
        strict.Cancel(Actor.Client, "Plans changed", SessionStart.AddHours(-2));
        gentle.Cancel(Actor.Client, "Plans changed", SessionStart.AddHours(-2));

        SettlementCalculator.Calculate(strict)!.Retained.ShouldBe(Money.Create(50_000m));
        SettlementCalculator.Calculate(gentle)!.Retained.ShouldBe(Money.Create(10_000m));
    }

    /// <summary>
    /// Builds a confirmed booking under a policy, with extra money paid beyond the deposit.
    /// </summary>
    /// <param name="policy">Policy frozen into the booking.</param>
    /// <param name="extra">Amount paid on top of the deposit.</param>
    /// <returns>The booking.</returns>
    private static Booking ConfirmedWithPolicy(BookingPolicy policy, decimal extra)
    {
        var booking = BookingTestData.CreateConfirmed(policy);
        if (extra > 0m)
        {
            booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(extra), PaymentMethod.Cash, "extra", Now);
        }

        return booking;
    }

    /// <summary>
    /// A booking that ended with nothing paid owes nothing either way.
    /// </summary>
    [Fact]
    public void Calculate_WithNothingPaid_OwesNothing()
    {
        var booking = BookingTestData.CreateTentative();
        booking.Cancel(Actor.Photographer, null, Now);

        var outcome = SettlementCalculator.Calculate(booking)!;

        outcome.TotalPaid.Amount.ShouldBe(0m);
        outcome.Retained.Amount.ShouldBe(0m);
        outcome.Refund.Amount.ShouldBe(0m);
    }

    /// <summary>
    /// A booking that was marked absent and then reverted is active again, so it settles nothing.
    /// </summary>
    [Fact]
    public void Calculate_AfterRevertingTheAbsence_ReturnsNull()
    {
        var booking = BookingTestData.CreateConfirmed();
        var markedAt = SessionStart.AddMinutes(BookingPolicy.Default.ClientAbsentToleranceMinutes);
        booking.MarkClientAbsent(markedAt);
        booking.RevertClientAbsent("Marked by mistake", markedAt.AddHours(1));

        SettlementCalculator.Calculate(booking).ShouldBeNull();
    }
}
