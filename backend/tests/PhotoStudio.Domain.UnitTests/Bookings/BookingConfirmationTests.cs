using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;
using static PhotoStudio.Domain.UnitTests.Bookings.BookingTestData;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of transitions B2 to B6: contract, payments and automatic confirmation.
/// </summary>
public sealed class BookingConfirmationTests
{
    /// <summary>
    /// Signing first and paying later confirms on payment (in-person flow).
    /// </summary>
    [Fact]
    public void SignThenPayInPerson_ConfirmsOnPayment()
    {
        var booking = CreateTentative();

        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);
        booking.Status.ShouldBe(BookingStatus.Tentative);

        booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.Cash, "key-1", Now);

        booking.Status.ShouldBe(BookingStatus.Confirmed);
        booking.ExpiresAt.ShouldBeNull();
        booking.DomainEvents.OfType<BookingConfirmed>().ShouldHaveSingleItem();
    }

    /// <summary>
    /// Paying first and signing later confirms on signature (remote flow).
    /// </summary>
    [Fact]
    public void SubmitProofThenVerifyThenSign_ConfirmsOnSignature()
    {
        var booking = CreateTentative();
        var payment = booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.SinpeMovil, "key-1", Now);

        booking.VerifyPayment(payment.Id, Now);
        booking.Status.ShouldBe(BookingStatus.Tentative);

        booking.SignContract("María Pérez", "v1", Actor.Client, Channel.Portal, Now);

        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    /// <summary>
    /// A partial deposit does not confirm the booking.
    /// </summary>
    [Fact]
    public void PartialDeposit_DoesNotConfirm()
    {
        var booking = CreateTentative();
        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);

        booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(20_000m), PaymentMethod.Cash, "key-1", Now);

        booking.Status.ShouldBe(BookingStatus.Tentative);
        booking.IsDepositCovered.ShouldBeFalse();
    }

    /// <summary>
    /// A pending proof does not count toward the deposit until it is verified.
    /// </summary>
    [Fact]
    public void PendingProof_DoesNotCountTowardDeposit()
    {
        var booking = CreateTentative();
        booking.SignContract("María Pérez", "v1", Actor.Client, Channel.Portal, Now);

        booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.SinpeMovil, "key-1", Now);

        booking.Status.ShouldBe(BookingStatus.Tentative);
        booking.HasPendingPaymentVerification.ShouldBeTrue();
        booking.TotalVerifiedPaid.Amount.ShouldBe(0m);
    }

    /// <summary>
    /// A rejected proof stays recorded but never counts toward the balance.
    /// </summary>
    [Fact]
    public void RejectedProof_IsKeptAndDoesNotCount()
    {
        var booking = CreateTentative();
        var payment = booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.SinpeMovil, "key-1", Now);

        booking.RejectPayment(payment.Id, "Comprobante ilegible", Now);

        booking.Payments.ShouldHaveSingleItem().Status.ShouldBe(PaymentStatus.Rejected);
        booking.TotalVerifiedPaid.Amount.ShouldBe(0m);
    }

    /// <summary>
    /// Repeating an in-person payment with the same idempotency key does not duplicate it.
    /// </summary>
    [Fact]
    public void RecordInPersonPayment_WithRepeatedKey_IsIdempotent()
    {
        var booking = CreateTentative();

        var first = booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(10_000m), PaymentMethod.Cash, "same-key", Now);
        var second = booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(10_000m), PaymentMethod.Cash, "same-key", Now);

        second.Id.ShouldBe(first.Id);
        booking.Payments.Count.ShouldBe(1);
    }

    /// <summary>
    /// A client cannot sign through an in-person channel; that channel belongs to the photographer.
    /// </summary>
    [Fact]
    public void SignContract_ClientInPerson_Throws()
    {
        var booking = CreateTentative();

        var exception = Should.Throw<DomainException>(
            () => booking.SignContract("María Pérez", "v1", Actor.Client, Channel.InPerson, Now));

        exception.Code.ShouldBe(DomainErrorCodes.ActorNotAllowed);
    }

    /// <summary>
    /// The contract cannot be signed twice.
    /// </summary>
    [Fact]
    public void SignContract_Twice_Throws()
    {
        var booking = CreateTentative();
        booking.SignContract("María Pérez", "v1", Actor.Client, Channel.Portal, Now);

        var exception = Should.Throw<DomainException>(
            () => booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now));

        exception.Code.ShouldBe(DomainErrorCodes.ContractAlreadySigned);
    }

    /// <summary>
    /// Card payments cannot be uploaded as proofs; they go through the payment gateway.
    /// </summary>
    [Fact]
    public void SubmitPaymentProof_WithCard_Throws()
    {
        var booking = CreateTentative();

        var exception = Should.Throw<DomainException>(
            () => booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.Card, "key-1", Now));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidPaymentMethod);
    }

    /// <summary>
    /// Payments must use the currency of the booking.
    /// </summary>
    [Fact]
    public void RecordInPersonPayment_WithOtherCurrency_Throws()
    {
        var booking = CreateTentative();

        var exception = Should.Throw<DomainException>(
            () => booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(100m, "USD"), PaymentMethod.Cash, "key-1", Now));

        exception.Code.ShouldBe(DomainErrorCodes.CurrencyMismatch);
    }

    /// <summary>
    /// With a zero deposit policy, signing the contract alone confirms the booking.
    /// </summary>
    [Fact]
    public void ZeroDepositPolicy_ConfirmsOnSignature()
    {
        var booking = CreateTentative(Policy(depositPercentage: 0m));

        booking.SignContract("María Pérez", "v1", Actor.Client, Channel.Portal, Now);

        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }
}
