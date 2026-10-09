namespace PhotoStudio.Application.Notifications.MarkRead;

/// <summary>
/// Command to mark one notification as read.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer; the notification must be theirs.</param>
/// <param name="NotificationId">Notification identifier.</param>
public sealed record MarkNotificationReadCommand(Guid PhotographerId, Guid NotificationId);

/// <summary>
/// Command to mark every notification of a photographer as read.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
public sealed record MarkAllNotificationsReadCommand(Guid PhotographerId);
