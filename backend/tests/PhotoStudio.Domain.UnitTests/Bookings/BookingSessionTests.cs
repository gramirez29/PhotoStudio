using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;
using static PhotoStudio.Domain.UnitTests.Bookings.BookingTestData;

namespace PhotoStudio.Domain.UnitTests.Bookings;

/// <summary>
/// Tests of transitions B9 (reschedule), B11 (complete), B12 (client absent) and B13 (revert client absent).
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
    /// The client can only be marked absent after the tolerance of the policy.
    /// </summary>
    [Fact]
    public void MarkClientAbsent_BeforeTolerance_Throws()
    {
        var booking = CreateConfirmed();

        var exception = Should.Throw<DomainException>(() => booking.MarkClientAbsent(SessionStart.AddMinutes(10)));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
    }

    /// <summary>
    /// Scenario 7 of the workbook: a client-absent mark made by mistake can be reverted with a reason.
    /// </summary>
    [Fact]
    public void MarkClientAbsent_ThenRevert_ReturnsToConfirmed()
    {
        var booking = CreateConfirmed();
        booking.MarkClientAbsent(SessionStart.AddMinutes(30));

        booking.RevertClientAbsent("Marcado por error", SessionStart.AddHours(2));

        booking.Status.ShouldBe(BookingStatus.Confirmed);
        booking.ClientAbsentMarkedAt.ShouldBeNull();
        booking.DomainEvents.OfType<ClientAbsenceReverted>().ShouldHaveSingleItem();
    }

    /// <summary>
    /// The client-absent mark cannot be reverted after the window of the policy.
    /// </summary>
    [Fact]
    public void RevertClientAbsent_AfterWindow_Throws()
    {
        var booking = CreateConfirmed();
        var markedAt = SessionStart.AddMinutes(30);
        booking.MarkClientAbsent(markedAt);

        var exception = Should.Throw<DomainException>(() => booking.RevertClientAbsent("Tarde", markedAt.AddDays(8)));

        exception.Code.ShouldBe(DomainErrorCodes.GuardFailed);
    }
}
