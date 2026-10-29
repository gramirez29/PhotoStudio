using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.GetContract;
using PhotoStudio.Application.Bookings.SignContractInPerson;
using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="GetBookingContractHandler"/> and of <see cref="SignContractInPersonHandler"/> when the client signs on the
/// photographer's phone.
/// </summary>
public sealed class ContractOnThePhoneHandlerTests
{
    private static readonly InPersonDeclaration Declaration = new("1-1234-5678", 3, "Parque La Sabana", ImageUse.Authorized);

    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();
    private readonly Booking _booking = BookingFactory.Tentative();

    /// <summary>
    /// Before signing, the photographer reads the contract with the booking data filled in and the client's blanks empty.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_BeforeSigning_ReturnsTheCurrentTemplateWithBlanks()
    {
        GivenTheBookingIsStored();

        var response = await NewGetHandler().HandleAsync(new GetBookingContractQuery(_booking.PhotographerId, _booking.Id), TestContext.Current.CancellationToken);

        response.Signed.ShouldBeFalse();
        response.TemplateVersion.ShouldBe(ContractTemplates.CurrentVersion);
        response.Text.ShouldContain("María Pérez");
        response.Text.ShouldContain(ContractRenderer.Blank);
    }

    /// <summary>
    /// Another photographer cannot read the contract of this booking: the answer is the same as for a booking that does not exist.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_ForAnotherPhotographer_ThrowsNotFound()
    {
        GivenTheBookingIsStored();

        await Should.ThrowAsync<NotFoundException>(() =>
            NewGetHandler().HandleAsync(new GetBookingContractQuery(Guid.CreateVersion7(), _booking.Id), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A contract signed on the phone reads back as the exact text that was signed, and one on paper has no text.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Get_AfterSigning_ReturnsTheSignedTextOrNothingForPaper()
    {
        GivenTheBookingIsStored();
        await NewSignHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);
        var onThePhone = await NewGetHandler().HandleAsync(new GetBookingContractQuery(_booking.PhotographerId, _booking.Id), TestContext.Current.CancellationToken);

        var paper = BookingFactory.Tentative();
        paper.SignContract("María Pérez", "v1", Actor.Photographer, Channel.External, BookingFactory.CreatedAt);
        _repository.GetByIdAsync(paper.Id, Arg.Any<CancellationToken>()).Returns(paper);
        var onPaper = await NewGetHandler().HandleAsync(new GetBookingContractQuery(paper.PhotographerId, paper.Id), TestContext.Current.CancellationToken);

        onThePhone.Signed.ShouldBeTrue();
        onThePhone.Text.ShouldBe(_booking.Contract!.SignedText);
        onThePhone.Text.ShouldNotContain(ContractRenderer.Blank);
        onPaper.Signed.ShouldBeTrue();
        onPaper.Text.ShouldBeEmpty();
    }

    /// <summary>
    /// Signing on the phone keeps the declaration and the exact text, with the photographer as who registered it and the channel in person.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_OnThePhone_KeepsTheDeclarationAndTheText()
    {
        GivenTheBookingIsStored();

        var response = await NewSignHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);

        var contract = _booking.Contract.ShouldNotBeNull();
        contract.SignedBy.ShouldBe(Actor.Photographer);
        contract.Channel.ShouldBe(Channel.InPerson);
        contract.TemplateVersion.ShouldBe(ContractTemplates.CurrentVersion);
        contract.Declaration.ShouldNotBeNull().IdNumber.ShouldBe("112345678");
        contract.Declaration.ParticipantCount.ShouldBe(3);
        contract.SignedText.ShouldNotBeNull().ShouldContain("cédula 112345678");
        contract.SignedText.ShouldContain("[x] Autorizo");
        response.Contract.ShouldNotBeNull().Declaration.ShouldNotBeNull().IdNumber.ShouldBe("112345678");
        await _repository.Received(1).UpdateAsync(_booking, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A client who read another version of the contract is asked to read the current one before signing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_OnThePhone_WithAnOutdatedVersion_IsRejected()
    {
        GivenTheBookingIsStored();

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewSignHandler().HandleAsync(Command() with { TemplateVersion = "v0" }, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.ContractTemplateChanged);
        _booking.Contract.ShouldBeNull();
    }

    /// <summary>
    /// An invalid identity number, or a blank signer name, is rejected before anything is written.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_OnThePhone_WithInvalidValues_IsRejected()
    {
        GivenTheBookingIsStored();

        var badId = await Should.ThrowAsync<DomainException>(() =>
            NewSignHandler().HandleAsync(Command() with { Declaration = Declaration with { IdNumber = "123" } }, TestContext.Current.CancellationToken));
        var noName = await Should.ThrowAsync<DomainException>(() =>
            NewSignHandler().HandleAsync(Command() with { SignerName = "  " }, TestContext.Current.CancellationToken));

        badId.Code.ShouldBe(DomainErrorCodes.InvalidIdNumber);
        noName.Code.ShouldBe(DomainErrorCodes.RequiredValue);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A contract on paper takes no declaration: it keeps neither the declaration nor a text, whatever the request carries.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_OnPaper_IgnoresTheDeclarationAndKeepsNoText()
    {
        GivenTheBookingIsStored();

        await NewSignHandler().HandleAsync(Command() with { IsPaperContract = true, TemplateVersion = "anything" }, TestContext.Current.CancellationToken);

        var contract = _booking.Contract.ShouldNotBeNull();
        contract.Channel.ShouldBe(Channel.External);
        contract.Declaration.ShouldBeNull();
        contract.SignedText.ShouldBeNull();
    }

    /// <summary>
    /// Signing in person without a declaration (the old way) still registers the signature, with no text.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_InPersonWithoutADeclaration_KeepsNoText()
    {
        GivenTheBookingIsStored();

        await NewSignHandler().HandleAsync(Command() with { Declaration = null }, TestContext.Current.CancellationToken);

        var contract = _booking.Contract.ShouldNotBeNull();
        contract.Channel.ShouldBe(Channel.InPerson);
        contract.SignedText.ShouldBeNull();
    }

    /// <summary>
    /// Another photographer cannot sign a booking that is not theirs.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Sign_ForAnotherPhotographer_ThrowsNotFound()
    {
        GivenTheBookingIsStored();

        await Should.ThrowAsync<NotFoundException>(() =>
            NewSignHandler().HandleAsync(Command() with { PhotographerId = Guid.CreateVersion7() }, TestContext.Current.CancellationToken));

        _booking.Contract.ShouldBeNull();
    }

    /// <summary>
    /// Makes the repository return the booking of the test.
    /// </summary>
    private void GivenTheBookingIsStored() => _repository.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(_booking);

    /// <summary>
    /// Builds a valid command to sign the booking of the test on the phone.
    /// </summary>
    /// <returns>The command.</returns>
    private SignContractInPersonCommand Command() => new(
        _booking.PhotographerId,
        _booking.Id,
        "María Pérez",
        ContractTemplates.CurrentVersion,
        IsPaperContract: false,
        Declaration);

    /// <summary>
    /// Builds the handler that reads the contract.
    /// </summary>
    /// <returns>The handler.</returns>
    private GetBookingContractHandler NewGetHandler() => new(_repository);

    /// <summary>
    /// Builds the handler that signs the contract.
    /// </summary>
    /// <returns>The handler.</returns>
    private SignContractInPersonHandler NewSignHandler() => new(_repository, new FixedTimeProvider(BookingFactory.CreatedAt));
}
