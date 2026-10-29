using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Portal;
using PhotoStudio.Application.Portal.GetContract;
using PhotoStudio.Application.Portal.SignContract;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Portal;

/// <summary>
/// Tests of <see cref="GetPortalContractHandler"/> and <see cref="SignPortalContractHandler"/>.
/// </summary>
public sealed class PortalContractHandlerTests
{
    private readonly PortalFixture _fixture = new();

    /// <summary>
    /// Before signing, the client reads the contract with their blanks empty and the current version.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_BeforeSigning_ReturnsTheCurrentTemplateWithBlanks()
    {
        var response = await NewGetHandler().HandleAsync(new GetPortalContractQuery(PortalFixture.Token, _fixture.Booking.Id), TestContext.Current.CancellationToken);

        response.Signed.ShouldBeFalse();
        response.TemplateVersion.ShouldBe(ContractTemplates.CurrentVersion);
        response.Text.ShouldContain("María Pérez");
        response.Text.ShouldContain(ContractRenderer.Blank);
    }

    /// <summary>
    /// A token that opens nothing is a 404, like for the booking itself.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_WithAnUnknownToken_ThrowsNotFound()
    {
        _fixture.Links.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Domain.Portal.BookingLink?)null);

        await Should.ThrowAsync<NotFoundException>(() =>
            NewGetHandler().HandleAsync(new GetPortalContractQuery("nope", _fixture.Booking.Id), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A session issued for another booking cannot read this contract, even with a valid link token: the text holds personal data.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_WithASessionOfAnotherBooking_ThrowsNotFound()
    {
        var query = new GetPortalContractQuery(PortalFixture.Token, Guid.CreateVersion7());

        await Should.ThrowAsync<NotFoundException>(() => NewGetHandler().HandleAsync(query, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Once signed through the portal, the client reads back exactly the text they signed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_AfterSigning_ReturnsTheSignedText()
    {
        await NewSignHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);

        var response = await NewGetHandler().HandleAsync(new GetPortalContractQuery(PortalFixture.Token, _fixture.Booking.Id), TestContext.Current.CancellationToken);

        response.Signed.ShouldBeTrue();
        response.Text.ShouldBe(_fixture.Booking.Contract!.SignedText);
        response.Text.ShouldNotContain(ContractRenderer.Blank);
    }

    /// <summary>
    /// A contract signed in person has no text, and the portal says only that it is signed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_ForAContractSignedInPerson_ReturnsNoText()
    {
        _fixture.Booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, PortalFixture.Now);

        var response = await NewGetHandler().HandleAsync(new GetPortalContractQuery(PortalFixture.Token, _fixture.Booking.Id), TestContext.Current.CancellationToken);

        response.Signed.ShouldBeTrue();
        response.Text.ShouldBeEmpty();
    }

    /// <summary>
    /// Signing registers the signature with the declaration and the exact text, persists the booking and tells the portal it is signed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_RegistersTheSignatureWithTheDeclarationAndTheText()
    {
        var response = await NewSignHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);

        var contract = _fixture.Booking.Contract.ShouldNotBeNull();
        contract.SignedBy.ShouldBe(Actor.Client);
        contract.Channel.ShouldBe(Channel.Portal);
        contract.SignerName.ShouldBe("María Pérez");
        contract.TemplateVersion.ShouldBe(ContractTemplates.CurrentVersion);
        contract.SignedAt.ShouldBe(PortalFixture.Now);
        contract.Declaration.ShouldNotBeNull().IdNumber.ShouldBe("112345678");
        contract.Declaration.ParticipantCount.ShouldBe(3);
        contract.Declaration.ImageUse.ShouldBe(ImageUse.Authorized);
        contract.SignedText.ShouldNotBeNull().ShouldContain("cédula 112345678");
        response.ContractSigned.ShouldBeTrue();
        response.CanSignContract.ShouldBeFalse();
        await _fixture.Bookings.Received(1).UpdateAsync(_fixture.Booking, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The portal shows the name of the studio, never the name of the account that created the booking.
    /// </summary>
    [Fact]
    public void Get_Booking_SpeaksInTheNameOfTheStudioNotOfTheAccount()
    {
        _fixture.Booking.ToPortalResponse().PhotographerName.ShouldBe("PhotoSense Studio");
    }

    /// <summary>
    /// The portal offers to sign only while the contract is unsigned.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_Booking_OffersToSignOnlyWhileUnsigned()
    {
        var before = _fixture.Booking.ToPortalResponse();
        await NewSignHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);
        var after = _fixture.Booking.ToPortalResponse();

        before.CanSignContract.ShouldBeTrue();
        after.CanSignContract.ShouldBeFalse();
    }

    /// <summary>
    /// A session issued for another booking cannot sign this one, even with a valid link token.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_WithASessionOfAnotherBooking_ThrowsNotFound()
    {
        var command = Command() with { AuthorizedBookingId = Guid.CreateVersion7() };

        await Should.ThrowAsync<NotFoundException>(() => NewSignHandler().HandleAsync(command, TestContext.Current.CancellationToken));

        _fixture.Booking.Contract.ShouldBeNull();
        await _fixture.Bookings.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A client who did not accept the contract cannot sign it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_WithoutAcceptingTheTerms_IsRejected()
    {
        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewSignHandler().HandleAsync(Command() with { AcceptsTerms = false }, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.RequiredValue);
        _fixture.Booking.Contract.ShouldBeNull();
    }

    /// <summary>
    /// A client who read an older version is asked to read the current one before signing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_WithAnOutdatedVersion_IsRejectedAsAConflict()
    {
        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewSignHandler().HandleAsync(Command() with { TemplateVersion = "v0" }, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.ContractTemplateChanged);
        _fixture.Booking.Contract.ShouldBeNull();
    }

    /// <summary>
    /// A blank signer name is rejected.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_WithoutASignerName_IsRejected()
    {
        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewSignHandler().HandleAsync(Command() with { SignerName = "  " }, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.RequiredValue);
    }

    /// <summary>
    /// An invalid identity number is rejected before anything is written.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_WithAnInvalidIdNumber_IsRejected()
    {
        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewSignHandler().HandleAsync(Command() with { IdNumber = "123" }, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidIdNumber);
        await _fixture.Bookings.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A contract that is already signed cannot be signed again.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_Twice_IsRejectedAsAConflict()
    {
        await NewSignHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewSignHandler().HandleAsync(Command(), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.ContractAlreadySigned);
    }

    /// <summary>
    /// Signing with the deposit already paid confirms the booking by itself.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_WithTheDepositAlreadyPaid_ConfirmsTheBooking()
    {
        _fixture.Booking.RecordInPersonPayment(Guid.CreateVersion7(), Money.Create(50_000m, "CRC"), PaymentMethod.Cash, "deposit", PortalFixture.Now);

        var response = await NewSignHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);

        response.Status.ShouldBe(BookingStatus.Confirmed);
    }

    /// <summary>
    /// A booking that is no longer waiting for confirmation cannot be signed.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_ACancelledBooking_IsRejected()
    {
        _fixture.Booking.Cancel(Actor.Photographer, "motivo", PortalFixture.Now);

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewSignHandler().HandleAsync(Command(), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidTransition);
    }

    /// <summary>
    /// Builds a valid signing command for the booking of the fixture.
    /// </summary>
    /// <returns>The command.</returns>
    private SignPortalContractCommand Command() => new(
        PortalFixture.Token,
        _fixture.Booking.Id,
        ContractTemplates.CurrentVersion,
        "María Pérez",
        "1-1234-5678",
        3,
        "Parque La Sabana",
        ImageUse.Authorized,
        AcceptsTerms: true);

    /// <summary>
    /// Builds the handler that reads the contract.
    /// </summary>
    /// <returns>The handler.</returns>
    private GetPortalContractHandler NewGetHandler() => new(_fixture.Bookings, _fixture.Links, _fixture.Tokens, _fixture.Clock);

    /// <summary>
    /// Builds the handler that signs the contract.
    /// </summary>
    /// <returns>The handler.</returns>
    private SignPortalContractHandler NewSignHandler() => new(_fixture.Bookings, _fixture.Links, _fixture.Tokens, _fixture.Clock);
}
