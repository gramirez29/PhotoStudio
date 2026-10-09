using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Notifications;
using PhotoStudio.Application.Notifications.List;
using PhotoStudio.Application.Notifications.MarkRead;
using PhotoStudio.Application.UnitTests.Support;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.UnitTests.Notifications;

/// <summary>
/// Tests of the inbox handlers: listing and marking as read, always scoped to the photographer.
/// </summary>
public sealed class NotificationInboxHandlerTests
{
    private static readonly DateTimeOffset Now = BookingFactory.CreatedAt.AddDays(5);

    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();

    /// <summary>
    /// Builds a delivered notification for a photographer.
    /// </summary>
    /// <param name="photographerId">Owner.</param>
    /// <returns>The notification.</returns>
    private static Notification Delivered(Guid photographerId)
    {
        var bookingId = Guid.CreateVersion7();
        var notification = Notification.Schedule(
            photographerId, bookingId, NotificationType.SessionReminder, Notification.SessionReminderKey(bookingId, Now), Now, Now);
        notification.Deliver(new NotificationDetails("María", "+50688888888", "Retrato", Now.AddDays(1), null), Now);
        return notification;
    }

    /// <summary>
    /// The inbox returns the photographer's notifications and the unread count.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task List_ReturnsTheInboxAndTheUnreadCount()
    {
        var photographer = Guid.CreateVersion7();
        var notification = Delivered(photographer);
        _notifications.ListDeliveredAsync(photographer, ListNotificationsHandler.MaxItems, Arg.Any<CancellationToken>()).Returns([notification]);
        _notifications.CountUnreadAsync(photographer, Arg.Any<CancellationToken>()).Returns(1);

        var response = await new ListNotificationsHandler(_notifications)
            .HandleAsync(new ListNotificationsQuery(photographer), TestContext.Current.CancellationToken);

        response.UnreadCount.ShouldBe(1);
        response.Items.Single().Id.ShouldBe(notification.Id);
    }

    /// <summary>
    /// Marking a notification as read stores the instant.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task MarkRead_StoresTheReadInstant()
    {
        var photographer = Guid.CreateVersion7();
        var notification = Delivered(photographer);
        _notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        var response = await new MarkNotificationReadHandler(_notifications, new FixedTimeProvider(Now.AddHours(1)))
            .HandleAsync(new MarkNotificationReadCommand(photographer, notification.Id), TestContext.Current.CancellationToken);

        response.ReadAt.ShouldBe(Now.AddHours(1));
        await _notifications.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A notification of another photographer, or a missing one, looks like it does not exist.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task MarkRead_OfAnotherPhotographerOrMissing_ThrowsNotFound()
    {
        var notification = Delivered(Guid.CreateVersion7());
        _notifications.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
        var handler = new MarkNotificationReadHandler(_notifications, new FixedTimeProvider(Now));

        await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(new MarkNotificationReadCommand(Guid.CreateVersion7(), notification.Id), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<NotFoundException>(
            () => handler.HandleAsync(new MarkNotificationReadCommand(notification.PhotographerId, Guid.CreateVersion7()), TestContext.Current.CancellationToken));
        await _notifications.DidNotReceiveWithAnyArgs().UpdateAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Marking the whole inbox reports how many were unread.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task MarkAllRead_ReportsHowManyWereUnread()
    {
        var photographer = Guid.CreateVersion7();
        _notifications.MarkAllReadAsync(photographer, Now, Arg.Any<CancellationToken>()).Returns(3);

        var response = await new MarkAllNotificationsReadHandler(_notifications, new FixedTimeProvider(Now))
            .HandleAsync(new MarkAllNotificationsReadCommand(photographer), TestContext.Current.CancellationToken);

        response.Marked.ShouldBe(3);
    }
}
