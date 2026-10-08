using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;
using static PhotoStudio.Domain.UnitTests.Bookings.BookingTestData;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of transitions B7 (expire), B8 and B10 (cancel).
/// </summary>
public sealed class BookingExpirationAndCancellationTests
{
    /// <summary>
    /// A tentative booking expires once the hold has ended.
    /// </summary>
    [Fact]
    public void Expire_AfterHold_MovesToExpired()
    {
        var booking = CreateTentative();

        booking.Expire(Now.AddHours(48));

        booking.Status.ShouldBe(BookingStatus.Expired);
        booking.DomainEvents.OfType<BookingExpired>().ShouldHaveSingleItem();
    }

    /// <summary>
    /// The background job cannot expire a booking before the hold ends.
    /// </summary>
    [Fact]
    public void Expire_BeforeHold_Throws()
    {
        var booking = CreateTentative();

        var exception = Should.Throw<DomainException>(() => booking.Expire(Now.AddHours(1)));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
    }

    /// <summary>
    /// Scenario 3 of the workbook: a proof waiting for verification prevents the expiration.
    /// </summary>
    [Fact]
    public void Expire_WithPendingProof_Throws()
    {
        var booking = CreateTentative();
        booking.SubmitPaymentProof(Guid.CreateVersion7(), Money.Create(50_000m), PaymentMethod.SinpeMovil, "key-1", Now.AddHours(47));

        var exception = Should.Throw<DomainException>(() => booking.Expire(Now.AddHours(48)));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
        booking.Status.ShouldBe(BookingStatus.Tentative);
    }

    /// <summary>
    /// A tentative booking can be cancelled without a reason.
    /// </summary>
    [Fact]
    public void Cancel_Tentative_WithoutReason_Succeeds()
    {
        var booking = CreateTentative();

        booking.Cancel(Actor.Client, null, Now);

        booking.Status.ShouldBe(BookingStatus.Cancelled);
        booking.DomainEvents.OfType<BookingCancelled>().ShouldHaveSingleItem().WasConfirmed.ShouldBeFalse();
    }

    /// <summary>
    /// Cancelling a confirmed booking requires a reason.
    /// </summary>
    [Fact]
    public void Cancel_Confirmed_WithoutReason_Throws()
    {
        var booking = CreateConfirmed();

        var exception = Should.Throw<DomainException>(() => booking.Cancel(Actor.Client, "  ", Now));

        exception.Code.ShouldBe(DomainErrorCodes.ReasonRequired);
    }

    /// <summary>
    /// The cancellation event carries the notice given, so billing can apply the copied policy.
    /// </summary>
    [Fact]
    public void Cancel_Confirmed_RaisesEventWithNotice()
    {
        var booking = CreateConfirmed();
        var cancelledAt = SessionStart.AddHours(-24);

        booking.Cancel(Actor.Client, "Viaje imprevisto", cancelledAt);

        var cancelled = booking.DomainEvents.OfType<BookingCancelled>().ShouldHaveSingleItem();
        cancelled.WasConfirmed.ShouldBeTrue();
        cancelled.HoursBeforeSession.ShouldBe(24d);
        cancelled.CancelledBy.ShouldBe(Actor.Client);
    }

    /// <summary>
    /// The system actor cannot cancel; only participants can.
    /// </summary>
    [Fact]
    public void Cancel_BySystem_Throws()
    {
        var booking = CreateTentative();

        var exception = Should.Throw<DomainException>(() => booking.Cancel(Actor.System, "job", Now));

        exception.Code.ShouldBe(DomainErrorCodes.ActorNotAllowed);
    }

    /// <summary>
    /// Final states reject further transitions.
    /// </summary>
    [Fact]
    public void Cancel_AfterExpired_Throws()
    {
        var booking = CreateTentative();
        booking.Expire(Now.AddHours(48));

        var exception = Should.Throw<DomainException>(() => booking.Cancel(Actor.Client, null, Now.AddHours(49)));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidTransition);
    }
}
