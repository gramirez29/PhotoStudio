using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.EditBookingDetails;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="EditBookingDetailsHandler"/>.
/// </summary>
public sealed class EditBookingDetailsHandlerTests
{
    private readonly IBookingRepository _repository = Substitute.For<IBookingRepository>();
    private readonly IPortalOtpRepository _codes = Substitute.For<IPortalOtpRepository>();
    private readonly Booking _booking = Booking.Create(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        ClientContact.Create("María Pérez", "+506 8888-8888", "vieja@example.com"),
        "Retrato familiar",
        Money.Create(100_000m, "CRC"),
        TimeSlot.Create(BookingFactory.SessionStart, BookingFactory.SessionStart.AddHours(2)),
        BookingPolicy.Default,
        BookingFactory.CreatedAt);

    /// <summary>
    /// The new values are applied, saved and returned.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_AppliesAndSavesTheNewValues()
    {
        GivenTheBookingIsStored();

        var response = await NewHandler().HandleAsync(Command() with { ClientName = "María Pérez Solano", PackageName = "Pre-Boda", PackagePrice = 150_000m }, TestContext.Current.CancellationToken);

        response.ClientName.ShouldBe("María Pérez Solano");
        response.PackageName.ShouldBe("Pre-Boda");
        response.PackagePrice.Amount.ShouldBe(150_000m);
        response.DepositRequired.Amount.ShouldBe(75_000m);
        await _repository.Received(1).UpdateAsync(_booking, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An email that is added or changed cancels the code the portal may have sent to the old address, and one that stays does not.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheEmailChanges_CancelsThePendingPortalCode()
    {
        GivenTheBookingIsStored();

        await NewHandler().HandleAsync(Command() with { ClientEmail = " Nueva@Example.com " }, TestContext.Current.CancellationToken);

        _booking.Client.Email.ShouldBe("nueva@example.com");
        await _codes.Received(1).DeleteAsync(_booking.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Correcting something else leaves the portal code alone.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WhenTheEmailStays_KeepsThePortalCode()
    {
        GivenTheBookingIsStored();

        await NewHandler().HandleAsync(Command() with { ClientName = "María P." }, TestContext.Current.CancellationToken);

        await _codes.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A blank email removes the email of the booking.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithABlankEmail_RemovesIt()
    {
        GivenTheBookingIsStored();

        var response = await NewHandler().HandleAsync(Command() with { ClientEmail = "  " }, TestContext.Current.CancellationToken);

        response.ClientEmail.ShouldBeNull();
        await _codes.Received(1).DeleteAsync(_booking.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A package change that the booking no longer allows is refused and nothing is saved.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ChangingThePackageAfterTheContractIsSigned_IsRefusedAndNothingIsSaved()
    {
        GivenTheBookingIsStored();
        _booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, BookingFactory.CreatedAt);

        var exception = await Should.ThrowAsync<DomainException>(() =>
            NewHandler().HandleAsync(Command() with { PackagePrice = 90_000m }, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
        await _codes.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An email without a valid shape, or a blank name, is rejected.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_WithInvalidValues_IsRejected()
    {
        GivenTheBookingIsStored();

        var badEmail = await Should.ThrowAsync<DomainException>(() =>
            NewHandler().HandleAsync(Command() with { ClientEmail = "no-es-un-correo" }, TestContext.Current.CancellationToken));
        var noName = await Should.ThrowAsync<DomainException>(() =>
            NewHandler().HandleAsync(Command() with { ClientName = " " }, TestContext.Current.CancellationToken));
        var badPrice = await Should.ThrowAsync<DomainException>(() =>
            NewHandler().HandleAsync(Command() with { PackagePrice = -1m }, TestContext.Current.CancellationToken));

        badEmail.Code.ShouldBe(DomainErrorCodes.InvalidEmail);
        noName.Code.ShouldBe(DomainErrorCodes.RequiredValue);
        badPrice.Code.ShouldBe(DomainErrorCodes.InvalidAmount);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Another photographer cannot edit a booking that is not theirs: the answer is the same as for one that does not exist.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task HandleAsync_ForAnotherPhotographer_ThrowsNotFound()
    {
        GivenTheBookingIsStored();

        await Should.ThrowAsync<NotFoundException>(() =>
            NewHandler().HandleAsync(Command() with { PhotographerId = Guid.CreateVersion7() }, TestContext.Current.CancellationToken));

        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Makes the repository return the booking of the test.
    /// </summary>
    private void GivenTheBookingIsStored() => _repository.GetByIdAsync(_booking.Id, Arg.Any<CancellationToken>()).Returns(_booking);

    /// <summary>
    /// Builds a command that changes nothing: it carries the current values of the booking of the test.
    /// </summary>
    /// <returns>The command.</returns>
    private EditBookingDetailsCommand Command() => new(
        _booking.PhotographerId,
        _booking.Id,
        _booking.Client.Name,
        _booking.Client.Phone,
        _booking.Client.Email,
        _booking.PackageName,
        _booking.PackagePrice.Amount);

    /// <summary>
    /// Builds the handler under test.
    /// </summary>
    /// <returns>The handler.</returns>
    private EditBookingDetailsHandler NewHandler() => new(_repository, _codes, new FixedTimeProvider(BookingFactory.CreatedAt));
}
