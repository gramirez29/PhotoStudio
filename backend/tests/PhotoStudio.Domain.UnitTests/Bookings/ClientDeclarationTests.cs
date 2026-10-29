using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="ClientDeclaration"/> and of how <see cref="Booking.SignContract"/> keeps it.
/// </summary>
public sealed class ClientDeclarationTests
{
    /// <summary>
    /// Hyphens and spaces typed in the identity number are ignored, so the stored value is digits only.
    /// </summary>
    /// <param name="typed">Identity number as the client typed it.</param>
    /// <param name="expected">Digits kept.</param>
    [Theory]
    [InlineData("1-1234-5678", "112345678")]
    [InlineData("1 1234 5678", "112345678")]
    [InlineData("112345678", "112345678")]
    [InlineData("15600012345", "15600012345")]
    [InlineData("155600012345", "155600012345")]
    public void Create_AcceptsACedulaOrADimexAndKeepsDigitsOnly(string typed, string expected)
    {
        var declaration = ClientDeclaration.Create(typed, 3, null, ImageUse.Authorized);

        declaration.IdNumber.ShouldBe(expected);
    }

    /// <summary>
    /// An identity number with letters or a length that is neither 9, 11 nor 12 digits is rejected.
    /// </summary>
    /// <param name="typed">Identity number as the client typed it.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("1234567890123")]
    [InlineData("11234567A")]
    public void Create_WithAnInvalidIdNumber_IsRejected(string typed)
    {
        var exception = Should.Throw<DomainException>(() => ClientDeclaration.Create(typed, 3, null, ImageUse.Authorized));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidIdNumber);
    }

    /// <summary>
    /// The number of people must be between 1 and the maximum.
    /// </summary>
    /// <param name="count">Number of people.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ClientDeclaration.MaxParticipants + 1)]
    public void Create_WithAnInvalidParticipantCount_IsRejected(int count)
    {
        var exception = Should.Throw<DomainException>(() => ClientDeclaration.Create("112345678", count, null, ImageUse.Authorized));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidContractDetails);
    }

    /// <summary>
    /// A blank location is stored as nothing, and a written one is trimmed.
    /// </summary>
    /// <param name="typed">Location as typed.</param>
    /// <param name="expected">Location kept.</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Parque La Sabana  ", "Parque La Sabana")]
    public void Create_NormalizesTheLocation(string? typed, string? expected)
    {
        var declaration = ClientDeclaration.Create("112345678", 2, typed, ImageUse.NotAuthorized);

        declaration.SessionLocation.ShouldBe(expected);
    }

    /// <summary>
    /// A location longer than the maximum is rejected.
    /// </summary>
    [Fact]
    public void Create_WithATooLongLocation_IsRejected()
    {
        var location = new string('a', ClientDeclaration.MaxLocationLength + 1);

        var exception = Should.Throw<DomainException>(() => ClientDeclaration.Create("112345678", 2, location, ImageUse.Authorized));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidContractDetails);
    }

    /// <summary>
    /// A value outside the image use enum is rejected, so a malformed request cannot store a choice nobody made.
    /// </summary>
    [Fact]
    public void Create_WithAnUndefinedImageUse_IsRejected()
    {
        var exception = Should.Throw<DomainException>(() => ClientDeclaration.Create("112345678", 2, null, (ImageUse)99));

        exception.Code.ShouldBe(DomainErrorCodes.RequiredValue);
    }

    /// <summary>
    /// A contract signed by the client through the portal keeps what the client declared and the text they signed.
    /// </summary>
    [Fact]
    public void SignContract_ByTheClient_KeepsTheDeclarationAndTheSignedText()
    {
        var booking = BookingTestData.CreateTentative();
        var declaration = ClientDeclaration.Create("1-1234-5678", 4, "Estudio", ImageUse.Authorized);

        booking.SignContract("María Pérez", "v1", Actor.Client, Channel.Portal, BookingTestData.Now, declaration, "texto firmado");

        booking.Contract.ShouldNotBeNull();
        booking.Contract.Declaration.ShouldBe(declaration);
        booking.Contract.SignedText.ShouldBe("texto firmado");
    }

    /// <summary>
    /// A contract registered by the photographer keeps no declaration and no text: only the client declares them.
    /// </summary>
    [Fact]
    public void SignContract_ByThePhotographer_KeepsNoDeclaration()
    {
        var booking = BookingTestData.CreateTentative();

        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, BookingTestData.Now);

        booking.Contract.ShouldNotBeNull();
        booking.Contract.Declaration.ShouldBeNull();
        booking.Contract.SignedText.ShouldBeNull();
    }
}
