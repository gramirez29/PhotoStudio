using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Domain.UnitTests.Notifications;

/// <summary>
/// Tests of <see cref="Notification"/>.
/// </summary>
public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly NotificationDetails Details = new("María Pérez", "+50688888888", "Retrato", Now.AddDays(1), null);

    /// <summary>
    /// Builds a scheduled reminder.
    /// </summary>
    /// <returns>The notification.</returns>
    private static Notification NewReminder()
    {
        var bookingId = Guid.CreateVersion7();
        return Notification.Schedule(
            Guid.CreateVersion7(),
            bookingId,
            NotificationType.SessionReminder,
            Notification.SessionReminderKey(bookingId, Now.AddDays(2)),
            Now.AddDays(1),
            Now);
    }

    /// <summary>
    /// A new notification is scheduled and not yet due.
    /// </summary>
    [Fact]
    public void Schedule_CreatesAScheduledNotification()
    {
        var notification = NewReminder();

        notification.Status.ShouldBe(NotificationStatus.Scheduled);
        notification.IsDueAt(Now).ShouldBeFalse();
        notification.IsDueAt(Now.AddDays(1)).ShouldBeTrue();
        notification.RetainUntil.ShouldBe(notification.DueAt + Notification.Retention);
    }

    /// <summary>
    /// Missing identifiers or key are rejected.
    /// </summary>
    [Fact]
    public void Schedule_WithoutRequiredValues_Throws()
    {
        var exception = Should.Throw<DomainException>(
            () => Notification.Schedule(Guid.Empty, Guid.CreateVersion7(), NotificationType.BalanceDue, "key", Now, Now));

        exception.Code.ShouldBe(DomainErrorCodes.RequiredValue);
        Should.Throw<DomainException>(
            () => Notification.Schedule(Guid.CreateVersion7(), Guid.CreateVersion7(), NotificationType.BalanceDue, " ", Now, Now));
    }

    /// <summary>
    /// Moving the session changes the reminder key; the same start gives the same key.
    /// </summary>
    [Fact]
    public void SessionReminderKey_DependsOnTheSessionStart()
    {
        var bookingId = Guid.CreateVersion7();

        Notification.SessionReminderKey(bookingId, Now).ShouldBe(Notification.SessionReminderKey(bookingId, Now.ToOffset(TimeSpan.FromHours(-6))));
        Notification.SessionReminderKey(bookingId, Now).ShouldNotBe(Notification.SessionReminderKey(bookingId, Now.AddHours(1)));
    }

    /// <summary>
    /// Delivering stores the snapshot and a delivered notification is no longer due.
    /// </summary>
    [Fact]
    public void Deliver_StoresTheSnapshot()
    {
        var notification = NewReminder();

        notification.Deliver(Details, Now.AddDays(1));

        notification.Status.ShouldBe(NotificationStatus.Delivered);
        notification.Details.ShouldBe(Details);
        notification.DeliveredAt.ShouldBe(Now.AddDays(1));
        notification.IsDueAt(Now.AddDays(2)).ShouldBeFalse();
    }

    /// <summary>
    /// A delivered or cancelled notification cannot be delivered or cancelled again.
    /// </summary>
    [Fact]
    public void DeliverAndCancel_WhenNotScheduled_Throw()
    {
        var delivered = NewReminder();
        delivered.Deliver(Details, Now);
        var cancelled = NewReminder();
        cancelled.Cancel(Now);

        Should.Throw<DomainException>(() => delivered.Cancel(Now)).Code.ShouldBe(DomainErrorCodes.InvalidTransition);
        Should.Throw<DomainException>(() => delivered.Deliver(Details, Now));
        Should.Throw<DomainException>(() => cancelled.Deliver(Details, Now)).Code.ShouldBe(DomainErrorCodes.InvalidTransition);
        cancelled.CancelledAt.ShouldBe(Now);
    }

    /// <summary>
    /// Reading is idempotent: the first read instant is kept.
    /// </summary>
    [Fact]
    public void MarkRead_KeepsTheFirstReadInstant()
    {
        var notification = NewReminder();
        notification.Deliver(Details, Now);

        notification.MarkRead(Now.AddHours(1));
        notification.MarkRead(Now.AddHours(2));

        notification.ReadAt.ShouldBe(Now.AddHours(1));
    }

    /// <summary>
    /// A notice that was never delivered cannot be read.
    /// </summary>
    [Fact]
    public void MarkRead_BeforeDelivery_Throws()
    {
        Should.Throw<DomainException>(() => NewReminder().MarkRead(Now)).Code.ShouldBe(DomainErrorCodes.InvalidTransition);
    }
}
