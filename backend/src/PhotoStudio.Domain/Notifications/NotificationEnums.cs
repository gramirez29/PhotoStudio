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

    /// <summary>A client uploaded a proof of payment that waits for the photographer to verify it.</summary>
    PaymentProofSubmitted,

    /// <summary>A client sent the photos they chose from their gallery: time to edit them.</summary>
    SelectionSubmitted,

    /// <summary>A gallery was delivered long enough ago to free its space: the photographer decides whether to release it.</summary>
    GalleryArchiveDue,

    /// <summary>The photographer is close to the deadline to publish the final photos of a selection the client sent.</summary>
    DeliveryDeadlineApproaching,
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
