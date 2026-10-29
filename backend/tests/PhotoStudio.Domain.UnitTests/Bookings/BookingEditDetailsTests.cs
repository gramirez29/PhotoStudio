using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using static PhotoStudio.Domain.UnitTests.Bookings.BookingTestData;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of <see cref="Booking.EditDetails"/>: what the photographer can correct, and when.
/// </summary>
public sealed class BookingEditDetailsTests
{
    private static readonly DateTimeOffset Later = Now.AddHours(1);

    /// <summary>
    /// The contact data of the client can be corrected, and the change is kept in the history.
    /// </summary>
    [Fact]
    public void EditDetails_CorrectsTheClient_AndKeepsItInTheHistory()
    {
        var booking = CreateTentative();
        var client = ClientContact.Create("María Pérez Solano", "+506 7018-9220", "maria@example.com");

        booking.EditDetails(client, booking.PackageName, booking.PackagePrice, Later);

        booking.Client.ShouldBe(client);
        booking.History.Last().ShouldBe(new StatusTransition(BookingStatus.Tentative, BookingStatus.Tentative, Actor.Photographer, Channel.PhotographerApp, "ClientDetailsEdited", Later));
    }

    /// <summary>
    /// Sending the same values changes nothing and adds nothing to the history.
    /// </summary>
    [Fact]
    public void EditDetails_WithTheSameValues_ChangesNothing()
    {
        var booking = CreateTentative();
        var historyBefore = booking.History.Count;

        booking.EditDetails(booking.Client, booking.PackageName, booking.PackagePrice, Later);

        booking.History.Count.ShouldBe(historyBefore);
        booking.Version.ShouldBe(0);
    }

    /// <summary>
    /// While nothing depends on them, the package and the price can change, and so does the deposit that is worked out from them.
    /// </summary>
    [Fact]
    public void EditDetails_BeforeTheContractAndAnyPayment_ChangesThePackageAndThePrice()
    {
        var booking = CreateTentative();

        booking.EditDetails(booking.Client, "  Pre-Boda  ", Money.Create(200_000m), Later);

        booking.PackageName.ShouldBe("Pre-Boda");
        booking.PackagePrice.Amount.ShouldBe(200_000m);
        booking.DepositRequired.Amount.ShouldBe(100_000m);
        booking.History.Last().Reason.ShouldBe("PackageEdited");
    }

    /// <summary>
    /// Once the contract is signed, the package and the price cannot change, because the contract text has them, but the client can.
    /// </summary>
    [Fact]
    public void EditDetails_AfterTheContractIsSigned_RefusesThePackageButAcceptsTheClient()
    {
        var booking = CreateTentative();
        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);

        var exception = Should.Throw<DomainException>(() => booking.EditDetails(booking.Client, booking.PackageName, Money.Create(1m), Later));
        booking.EditDetails(ClientContact.Create("María Pérez", "+506 7018-9220", "nuevo@example.com"), booking.PackageName, booking.PackagePrice, Later);

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
        booking.Client.Email.ShouldBe("nuevo@example.com");
        booking.PackagePrice.Amount.ShouldBe(PackagePrice);
    }

    /// <summary>
    /// Once a payment is recorded, even a pending or rejected proof, the package and the price cannot change.
    /// </summary>
    [Fact]
    public void EditDetails_AfterAPayment_RefusesThePackage()
    {
        var booking = CreateTentative();
        booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(10_000m), PaymentMethod.SinpeMovil, "key-1", Now);

        var exception = Should.Throw<DomainException>(() => booking.EditDetails(booking.Client, "Otro", booking.PackagePrice, Later));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
        booking.PackageName.ShouldBe("Retrato familiar");
    }

    /// <summary>
    /// A confirmed booking can have its client corrected but not its package, and a completed one too.
    /// </summary>
    [Fact]
    public void EditDetails_WhenConfirmedOrCompleted_AcceptsOnlyTheClient()
    {
        var confirmed = CreateConfirmed();
        var completed = CreateConfirmed();
        completed.Complete(SessionStart.AddMinutes(1));
        var client = ClientContact.Create("María Pérez", "+506 7018-9220", "nuevo@example.com");

        confirmed.EditDetails(client, confirmed.PackageName, confirmed.PackagePrice, Later);
        completed.EditDetails(client, completed.PackageName, completed.PackagePrice, Later);

        confirmed.Client.ShouldBe(client);
        completed.Client.ShouldBe(client);
        Should.Throw<DomainException>(() => confirmed.EditDetails(client, "Otro", confirmed.PackagePrice, Later)).Code.ShouldBe(DomainErrorCodes.GuardFailed);
        Should.Throw<DomainException>(() => completed.EditDetails(client, "Otro", completed.PackagePrice, Later)).Code.ShouldBe(DomainErrorCodes.GuardFailed);
    }

    /// <summary>
    /// A booking that ended cannot be edited.
    /// </summary>
    [Fact]
    public void EditDetails_WhenCancelled_IsRejected()
    {
        var booking = CreateTentative();
        booking.Cancel(Actor.Photographer, null, Now);

        var exception = Should.Throw<DomainException>(() => booking.EditDetails(booking.Client, booking.PackageName, booking.PackagePrice, Later));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidTransition);
    }

    /// <summary>
    /// The package needs a name, and the price must be in the currency of the booking.
    /// </summary>
    [Fact]
    public void EditDetails_WithAnInvalidPackage_IsRejected()
    {
        var booking = CreateTentative();

        var noName = Should.Throw<DomainException>(() => booking.EditDetails(booking.Client, "  ", booking.PackagePrice, Later));
        var dollars = Should.Throw<DomainException>(() => booking.EditDetails(booking.Client, booking.PackageName, Money.Create(100m, "USD"), Later));

        noName.Code.ShouldBe(DomainErrorCodes.RequiredValue);
        dollars.Code.ShouldBe(DomainErrorCodes.CurrencyMismatch);
    }

    /// <summary>
    /// Only the photographer is offered the actions that edit, and the package one only while it is possible.
    /// </summary>
    [Fact]
    public void AllowedActions_OfferTheEditsToThePhotographerOnly()
    {
        var booking = CreateTentative();

        booking.GetAllowedActions(Actor.Photographer, Now).ShouldContain(BookingAction.EditDetails);
        booking.GetAllowedActions(Actor.Photographer, Now).ShouldContain(BookingAction.EditPackage);
        booking.GetAllowedActions(Actor.Client, Now).ShouldNotContain(BookingAction.EditDetails);
        booking.GetAllowedActions(Actor.Client, Now).ShouldNotContain(BookingAction.EditPackage);

        booking.SignContract("María Pérez", "v1", Actor.Photographer, Channel.InPerson, Now);
        var afterContract = booking.GetAllowedActions(Actor.Photographer, Now);
        afterContract.ShouldContain(BookingAction.EditDetails);
        afterContract.ShouldNotContain(BookingAction.EditPackage);
    }

    /// <summary>
    /// A confirmed and a completed booking can be corrected, a cancelled one cannot, and none of them offers to change the package.
    /// </summary>
    [Fact]
    public void AllowedActions_FollowTheStatus()
    {
        var confirmed = CreateConfirmed();
        var completed = CreateConfirmed();
        completed.Complete(SessionStart.AddMinutes(1));
        var cancelled = CreateTentative();
        cancelled.Cancel(Actor.Photographer, null, Now);

        confirmed.GetAllowedActions(Actor.Photographer, Now).ShouldContain(BookingAction.EditDetails);
        confirmed.GetAllowedActions(Actor.Photographer, Now).ShouldNotContain(BookingAction.EditPackage);
        completed.GetAllowedActions(Actor.Photographer, SessionStart.AddHours(3)).ShouldContain(BookingAction.EditDetails);
        cancelled.GetAllowedActions(Actor.Photographer, Now).ShouldBeEmpty();
    }
}
