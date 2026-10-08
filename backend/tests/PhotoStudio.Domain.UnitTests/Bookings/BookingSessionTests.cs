using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;
using static PhotoStudio.Domain.UnitTests.Bookings.BookingTestData;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of transitions B9 (reschedule), B11 (complete), B12 (no-show) and B13 (revert no-show).
/// </summary>
public sealed class BookingSessionTests
{
    /// <summary>
    /// The client can reschedule once with enough notice.
    /// </summary>
    [Fact]
    public void Reschedule_ByClientWithinPolicy_MovesSlot()
    {
        var booking = CreateConfirmed();
        var newStart = SessionStart.AddDays(3);

        booking.Reschedule(TimeSlot.Create(newStart, newStart.AddHours(2)), Actor.Client, Now);

        booking.Slot.Start.ShouldBe(newStart);
        booking.RescheduleCount.ShouldBe(1);
        booking.DomainEvents.OfType<BookingRescheduled>().ShouldHaveSingleItem();
    }

    /// <summary>
    /// Scenario 13 of the workbook: the client cannot exceed the reschedule limit, the photographer can.
    /// </summary>
    [Fact]
    public void Reschedule_ClientOverLimit_Throws_ButPhotographerCan()
    {
        var booking = CreateConfirmed();
        var firstStart = SessionStart.AddDays(3);
        var secondStart = SessionStart.AddDays(5);
        booking.Reschedule(TimeSlot.Create(firstStart, firstStart.AddHours(2)), Actor.Client, Now);

        var exception = Should.Throw<DomainException>(
            () => booking.Reschedule(TimeSlot.Create(secondStart, secondStart.AddHours(2)), Actor.Client, Now));
        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);

        booking.Reschedule(TimeSlot.Create(secondStart, secondStart.AddHours(2)), Actor.Photographer, Now);
        booking.Slot.Start.ShouldBe(secondStart);
    }

    /// <summary>
    /// The client cannot reschedule with less notice than the policy requires.
    /// </summary>
    [Fact]
    public void Reschedule_ClientWithShortNotice_Throws()
    {
        var booking = CreateConfirmed();
        var newStart = SessionStart.AddDays(3);

        var exception = Should.Throw<DomainException>(
            () => booking.Reschedule(TimeSlot.Create(newStart, newStart.AddHours(2)), Actor.Client, SessionStart.AddHours(-10)));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
    }

    /// <summary>
    /// The session can be completed once it has started, and the event feeds the gallery module.
    /// </summary>
    [Fact]
    public void Complete_AfterStart_MovesToCompleted()
    {
        var booking = CreateConfirmed();

        booking.Complete(SessionStart);

        booking.Status.ShouldBe(BookingStatus.Completed);
        booking.DomainEvents.OfType<SessionCompleted>().ShouldHaveSingleItem();
    }

    /// <summary>
    /// The session cannot be completed before it starts.
    /// </summary>
    [Fact]
    public void Complete_BeforeStart_Throws()
    {
        var booking = CreateConfirmed();

        var exception = Should.Throw<DomainException>(() => booking.Complete(SessionStart.AddMinutes(-1)));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
    }

    /// <summary>
    /// A no-show can only be marked after the tolerance of the policy.
    /// </summary>
    [Fact]
    public void MarkNoShow_BeforeTolerance_Throws()
    {
        var booking = CreateConfirmed();

        var exception = Should.Throw<DomainException>(() => booking.MarkNoShow(SessionStart.AddMinutes(10)));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
    }

    /// <summary>
    /// Scenario 7 of the workbook: a no-show marked by mistake can be reverted with a reason.
    /// </summary>
    [Fact]
    public void MarkNoShow_ThenRevert_ReturnsToConfirmed()
    {
        var booking = CreateConfirmed();
        booking.MarkNoShow(SessionStart.AddMinutes(30));

        booking.RevertNoShow("Marcado por error", SessionStart.AddHours(2));

        booking.Status.ShouldBe(BookingStatus.Confirmed);
        booking.NoShowMarkedAt.ShouldBeNull();
        booking.DomainEvents.OfType<NoShowReverted>().ShouldHaveSingleItem();
    }

    /// <summary>
    /// The no-show cannot be reverted after the window of the policy.
    /// </summary>
    [Fact]
    public void RevertNoShow_AfterWindow_Throws()
    {
        var booking = CreateConfirmed();
        var markedAt = SessionStart.AddMinutes(30);
        booking.MarkNoShow(markedAt);

        var exception = Should.Throw<DomainException>(() => booking.RevertNoShow("Tarde", markedAt.AddDays(8)));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
    }
}
