using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using static PhotoStudio.Domain.UnitTests.Bookings.BookingTestData;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of the client email of a booking and of the reference that comes with a payment proof.
/// </summary>
public sealed class ClientEmailAndProofReferenceTests
{
    /// <summary>
    /// The email is normalized like a user's: trimmed and in lower case.
    /// </summary>
    [Fact]
    public void ClientContact_NormalizesTheEmail()
    {
        var contact = ClientContact.Create("María Pérez", "+506 8888-8888", "  María@Example.COM ");

        contact.Email.ShouldBe("maría@example.com");
    }

    /// <summary>
    /// The email is optional: blank means the photographer did not give one.
    /// </summary>
    /// <param name="email">Value given for the email.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClientContact_WithoutAnEmail_HasNone(string? email)
    {
        ClientContact.Create("María Pérez", "+506 8888-8888", email).Email.ShouldBeNull();
    }

    /// <summary>
    /// An email that is given must have a valid shape.
    /// </summary>
    /// <param name="email">Malformed email.</param>
    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("two@@example.com")]
    [InlineData("with space@example.com")]
    public void ClientContact_WithAMalformedEmail_IsRejected(string email)
    {
        var exception = Should.Throw<DomainException>(() => ClientContact.Create("María Pérez", "+506 8888-8888", email));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidEmail);
    }

    /// <summary>
    /// The reference given with a proof is kept, trimmed, on the pending payment.
    /// </summary>
    [Fact]
    public void SubmitPaymentProof_KeepsTheTrimmedReference()
    {
        var booking = CreateTentative();

        var payment = booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.SinpeMovil, "key-1", Now, "  SINPE-123  ");

        payment.Reference.ShouldBe("SINPE-123");
        payment.Status.ShouldBe(PaymentStatus.PendingVerification);
    }

    /// <summary>
    /// A blank reference is stored as none.
    /// </summary>
    [Fact]
    public void SubmitPaymentProof_WithABlankReference_StoresNone()
    {
        var booking = CreateTentative();

        var payment = booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.SinpeMovil, "key-1", Now, "   ");

        payment.Reference.ShouldBeNull();
    }

    /// <summary>
    /// A reference longer than the limit is rejected.
    /// </summary>
    [Fact]
    public void SubmitPaymentProof_WithATooLongReference_IsRejected()
    {
        var booking = CreateTentative();
        var tooLong = new string('9', Payment.MaxReferenceLength + 1);

        var exception = Should.Throw<DomainException>(
            () => booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.SinpeMovil, "key-1", Now, tooLong));

        exception.Code.ShouldBe(DomainErrorCodes.RequiredValue);
        booking.Payments.ShouldBeEmpty();
    }

    /// <summary>
    /// A payment recorded in person has no reference.
    /// </summary>
    [Fact]
    public void RecordInPersonPayment_HasNoReference()
    {
        var booking = CreateTentative();

        var payment = booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.Cash, "key-1", Now);

        payment.Reference.ShouldBeNull();
    }
}
