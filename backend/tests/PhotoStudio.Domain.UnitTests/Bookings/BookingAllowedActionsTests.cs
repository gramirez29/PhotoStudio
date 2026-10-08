using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;
using static PhotoStudio.Domain.UnitTests.Bookings.BookingTestData;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of the allowed actions that the clients render as buttons.
/// </summary>
public sealed class BookingAllowedActionsTests
{
    /// <summary>
    /// A tentative booking offers signing, paying and cancelling to the client.
    /// </summary>
    [Fact]
    public void Tentative_ForClient_OffersSignPayCancel()
    {
        var booking = CreateTentative();

        var actions = booking.GetAllowedActions(Actor.Client, Now);

        BookingAction[] expected = [BookingAction.SignContract, BookingAction.SubmitPaymentProof, BookingAction.Cancel];
        actions.ShouldBe(expected);
    }

    /// <summary>
    /// The photographer sees the verify action only while a proof is pending.
    /// </summary>
    [Fact]
    public void Tentative_ForPhotographer_OffersVerifyOnlyWithPendingProof()
    {
        var booking = CreateTentative();
        booking.GetAllowedActions(Actor.Photographer, Now).ShouldNotContain(BookingAction.VerifyPayment);

        booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.SinpeMovil, "key-1", Now);

        booking.GetAllowedActions(Actor.Photographer, Now).ShouldContain(BookingAction.VerifyPayment);
    }

    /// <summary>
    /// Complete and client-absent only appear once the session has started and the tolerance passed.
    /// </summary>
    [Fact]
    public void Confirmed_ForPhotographer_SessionActionsDependOnTime()
    {
        var booking = CreateConfirmed();

        booking.GetAllowedActions(Actor.Photographer, Now).ShouldNotContain(BookingAction.Complete);

        var afterTolerance = booking.GetAllowedActions(Actor.Photographer, SessionStart.AddMinutes(30));
        afterTolerance.ShouldContain(BookingAction.Complete);
        afterTolerance.ShouldContain(BookingAction.MarkClientAbsent);
    }

    /// <summary>
    /// Final states offer no actions.
    /// </summary>
    [Fact]
    public void Cancelled_OffersNothing()
    {
        var booking = CreateTentative();
        booking.Cancel(Actor.Client, null, Now);

        booking.GetAllowedActions(Actor.Photographer, Now).ShouldBeEmpty();
        booking.GetAllowedActions(Actor.Client, Now).ShouldBeEmpty();
    }
}
