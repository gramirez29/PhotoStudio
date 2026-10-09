namespace PhotoStudio.Domain.Notifications;

/// <summary>
/// What a notification tells the photographer.
/// </summary>
public enum NotificationType
{
    /// <summary>The session of a confirmed booking is about to start: time to remind the client.</summary>
    SessionReminder,

    /// <summary>A session was completed and the client still owes money: time to collect.</summary>
    BalanceDue,
}

/// <summary>
/// Where a notification is in its life.
/// </summary>
public enum NotificationStatus
{
    /// <summary>Planned for a future instant; the photographer cannot see it yet.</summary>
    Scheduled,

    /// <summary>Its time came and it was still relevant, so it is in the photographer's inbox.</summary>
    Delivered,

    /// <summary>It stopped being relevant before its time came (the booking was cancelled, moved, expired or paid) and was dropped.</summary>
    Cancelled,
}
