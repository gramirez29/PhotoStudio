using PhotoStudio.Domain.Billing;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.UnitTests.Billing;

/// <summary>
/// Tests of <see cref="BookingSettlement"/>.
/// </summary>
public sealed class BookingSettlementTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly SettlementSnapshot Snapshot = new("María Pérez", "+50688888888", "Retrato", Now.AddDays(5));

    /// <summary>
    /// Builds an outcome.
    /// </summary>
    /// <param name="reason">Reason.</param>
    /// <param name="paid">Total paid.</param>
    /// <param name="retained">Retained.</param>
    /// <returns>The outcome; the refund is what was not retained.</returns>
    private static SettlementOutcome Outcome(SettlementReason reason, decimal paid, decimal retained) =>
        new(reason, Money.Create(paid), Money.Create(retained), Money.Create(paid - retained));

    /// <summary>
    /// Opens a settlement for a late cancellation: deposit kept, the rest owed back.
    /// </summary>
    /// <returns>The settlement.</returns>
    private static BookingSettlement OpenLate() =>
        BookingSettlement.Open(Guid.CreateVersion7(), Guid.CreateVersion7(), Snapshot, Outcome(SettlementReason.ClientCancelledLate, 80_000m, 50_000m), Now);

    /// <summary>
    /// Opening derives the retention and refund states from the amounts.
    /// </summary>
    [Fact]
    public void Open_DerivesTheStatesFromTheAmounts()
    {
        var late = OpenLate();
        var full = BookingSettlement.Open(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Snapshot, Outcome(SettlementReason.PhotographerCancelled, 50_000m, 0m), Now);
        var onlyRetained = BookingSettlement.Open(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Snapshot, Outcome(SettlementReason.ClientAbsent, 50_000m, 50_000m), Now);

        late.RetentionStatus.ShouldBe(RetentionStatus.Applied);
        late.RefundStatus.ShouldBe(RefundStatus.Pending);
        full.RetentionStatus.ShouldBe(RetentionStatus.None);
        full.RefundStatus.ShouldBe(RefundStatus.Pending);
        onlyRetained.RetentionStatus.ShouldBe(RetentionStatus.Applied);
        onlyRetained.RefundStatus.ShouldBe(RefundStatus.None);
    }

    /// <summary>
    /// Missing identifiers are rejected.
    /// </summary>
    [Fact]
    public void Open_WithoutIdentifiers_Throws()
    {
        Should.Throw<DomainException>(
            () => BookingSettlement.Open(Guid.Empty, Guid.CreateVersion7(), Snapshot, Outcome(SettlementReason.ClientAbsent, 1m, 1m), Now))
            .Code.ShouldBe(DomainErrorCodes.RequiredValue);
    }

    /// <summary>
    /// Applying the same outcome again changes nothing; a different one updates the amounts.
    /// </summary>
    [Fact]
    public void Apply_IsIdempotentAndUpdatesWhenTheOutcomeChanges()
    {
        var settlement = OpenLate();

        settlement.Apply(Outcome(SettlementReason.ClientCancelledLate, 80_000m, 50_000m), Now.AddHours(1)).ShouldBeFalse();
        settlement.Apply(Outcome(SettlementReason.PhotographerCancelled, 80_000m, 0m), Now.AddHours(2)).ShouldBeTrue();

        settlement.Reason.ShouldBe(SettlementReason.PhotographerCancelled);
        settlement.RetentionStatus.ShouldBe(RetentionStatus.None);
        settlement.RefundAmount.ShouldBe(Money.Create(80_000m));
        settlement.UpdatedAt.ShouldBe(Now.AddHours(2));
    }

    /// <summary>
    /// A settlement whose refund was given back never changes again.
    /// </summary>
    [Fact]
    public void ApplyAndVoid_AfterTheRefundWasCompleted_DoNothing()
    {
        var settlement = OpenLate();
        settlement.CompleteRefund(PaymentMethod.SinpeMovil, "ref 123", Now.AddDays(1));

        settlement.Apply(Outcome(SettlementReason.PhotographerCancelled, 80_000m, 0m), Now.AddDays(2)).ShouldBeFalse();
        settlement.Void(Now.AddDays(2)).ShouldBeFalse();

        settlement.Reason.ShouldBe(SettlementReason.ClientCancelledLate);
        settlement.RefundStatus.ShouldBe(RefundStatus.Completed);
        settlement.RetentionStatus.ShouldBe(RetentionStatus.Applied);
    }

    /// <summary>
    /// Voiding reverses the retention and drops a refund that was not paid yet; doing it twice changes nothing more.
    /// </summary>
    [Fact]
    public void Void_ReversesTheRetentionAndDropsThePendingRefund()
    {
        var settlement = OpenLate();

        settlement.Void(Now.AddHours(1)).ShouldBeTrue();
        settlement.Void(Now.AddHours(2)).ShouldBeFalse();

        settlement.RetentionStatus.ShouldBe(RetentionStatus.Reversed);
        settlement.RefundStatus.ShouldBe(RefundStatus.Voided);
    }

    /// <summary>
    /// A voided settlement comes back to life if the booking ends again.
    /// </summary>
    [Fact]
    public void Apply_AfterVoiding_ReopensTheSettlement()
    {
        var settlement = OpenLate();
        settlement.Void(Now.AddHours(1));

        settlement.Apply(Outcome(SettlementReason.ClientAbsent, 80_000m, 50_000m), Now.AddHours(2)).ShouldBeTrue();

        settlement.RetentionStatus.ShouldBe(RetentionStatus.Applied);
        settlement.RefundStatus.ShouldBe(RefundStatus.Pending);
    }

    /// <summary>
    /// Completing a refund stores how and when it was given back, trimming the note.
    /// </summary>
    [Fact]
    public void CompleteRefund_StoresTheMethodNoteAndInstant()
    {
        var settlement = OpenLate();

        settlement.CompleteRefund(PaymentMethod.SinpeMovil, "  ref 123  ", Now.AddDays(1));

        settlement.RefundStatus.ShouldBe(RefundStatus.Completed);
        settlement.RefundMethod.ShouldBe(PaymentMethod.SinpeMovil);
        settlement.RefundNote.ShouldBe("ref 123");
        settlement.RefundCompletedAt.ShouldBe(Now.AddDays(1));
    }

    /// <summary>
    /// A blank note is stored as no note.
    /// </summary>
    [Fact]
    public void CompleteRefund_WithABlankNote_StoresNoNote()
    {
        var settlement = OpenLate();

        settlement.CompleteRefund(PaymentMethod.Cash, "   ", Now);

        settlement.RefundNote.ShouldBeNull();
    }

    /// <summary>
    /// A refund can be completed once, only with cash or SINPE Móvil, and with a note of reasonable length.
    /// </summary>
    [Fact]
    public void CompleteRefund_RejectsRepeatsCardAndLongNotes()
    {
        var settlement = OpenLate();

        Should.Throw<DomainException>(() => settlement.CompleteRefund(PaymentMethod.Card, null, Now))
            .Code.ShouldBe(DomainErrorCodes.InvalidPaymentMethod);
        Should.Throw<DomainException>(() => settlement.CompleteRefund(PaymentMethod.Cash, new string('a', BookingSettlement.MaxNoteLength + 1), Now));

        settlement.CompleteRefund(PaymentMethod.Cash, null, Now);
        Should.Throw<DomainException>(() => settlement.CompleteRefund(PaymentMethod.Cash, null, Now))
            .Code.ShouldBe(DomainErrorCodes.InvalidTransition);
    }

    /// <summary>
    /// There is nothing to complete when no money goes back.
    /// </summary>
    [Fact]
    public void CompleteRefund_WithoutAPendingRefund_Throws()
    {
        var settlement = BookingSettlement.Open(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Snapshot, Outcome(SettlementReason.ClientAbsent, 50_000m, 50_000m), Now);

        Should.Throw<DomainException>(() => settlement.CompleteRefund(PaymentMethod.Cash, null, Now))
            .Code.ShouldBe(DomainErrorCodes.InvalidTransition);
    }
}
