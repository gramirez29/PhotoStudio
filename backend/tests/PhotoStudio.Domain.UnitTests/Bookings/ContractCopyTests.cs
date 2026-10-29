using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="Booking.MarkContractCopySent"/>: the record that keeps the copy of the contract from being emailed twice.
/// </summary>
public sealed class ContractCopyTests
{
    /// <summary>
    /// Marking the copy as sent stores the instant, and a signed booking starts without it.
    /// </summary>
    [Fact]
    public void MarkContractCopySent_StoresTheInstant()
    {
        var booking = SignedByTheClient();
        booking.Contract!.CopySentAt.ShouldBeNull();

        booking.MarkContractCopySent(BookingTestData.Now.AddMinutes(5));

        booking.Contract.CopySentAt.ShouldBe(BookingTestData.Now.AddMinutes(5));
    }

    /// <summary>
    /// Marking it twice keeps the first instant, so a repeated delivery does not rewrite history.
    /// </summary>
    [Fact]
    public void MarkContractCopySent_Twice_KeepsTheFirstInstant()
    {
        var booking = SignedByTheClient();

        booking.MarkContractCopySent(BookingTestData.Now.AddMinutes(5));
        booking.MarkContractCopySent(BookingTestData.Now.AddMinutes(30));

        booking.Contract!.CopySentAt.ShouldBe(BookingTestData.Now.AddMinutes(5));
    }

    /// <summary>
    /// It changes nothing else of the signature: who signed, the text and the declaration stay as they were.
    /// </summary>
    [Fact]
    public void MarkContractCopySent_DoesNotChangeTheRestOfTheSignature()
    {
        var booking = SignedByTheClient();
        var before = booking.Contract!;

        booking.MarkContractCopySent(BookingTestData.Now.AddMinutes(5));

        booking.Contract!.SignerName.ShouldBe(before.SignerName);
        booking.Contract.SignedText.ShouldBe(before.SignedText);
        booking.Contract.Declaration.ShouldBe(before.Declaration);
        booking.DomainEvents.OfType<PhotoStudio.Domain.Bookings.Events.ContractSigned>().Count().ShouldBe(1);
    }

    /// <summary>
    /// There is no copy to send before the contract is signed.
    /// </summary>
    [Fact]
    public void MarkContractCopySent_WithoutAContract_IsRejected()
    {
        var booking = BookingTestData.CreateTentative();

        var exception = Should.Throw<DomainException>(() => booking.MarkContractCopySent(BookingTestData.Now));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
    }

    /// <summary>
    /// A signature restored with the instant keeps it, which is how it comes back from the database.
    /// </summary>
    [Fact]
    public void Create_WithACopySentInstant_KeepsIt()
    {
        var signature = ContractSignature.Create("María Pérez", "v1", Actor.Client, Channel.Portal, BookingTestData.Now, copySentAt: BookingTestData.Now.AddHours(1));

        signature.CopySentAt.ShouldBe(BookingTestData.Now.AddHours(1));
    }

    /// <summary>
    /// Creates a booking the client signed through the portal.
    /// </summary>
    /// <returns>The booking.</returns>
    private static Booking SignedByTheClient()
    {
        var booking = BookingTestData.CreateTentative();
        var declaration = ClientDeclaration.Create("112345678", 2, null, ImageUse.Authorized);
        booking.SignContract("María Pérez", "v1", Actor.Client, Channel.Portal, BookingTestData.Now, declaration, "texto firmado");
        return booking;
    }
}
