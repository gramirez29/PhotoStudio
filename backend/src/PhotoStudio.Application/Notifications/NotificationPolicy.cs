namespace PhotoStudio.Application.Notifications;

/// <summary>
/// When notifications are planned. Fixed for now; like the booking policy, it can become configurable per photographer.
/// </summary>
public static class NotificationPolicy
{
    /// <summary>
    /// How long before the session the reminder becomes due: the photographer gets a day to remind the client.
    /// </summary>
    public static readonly TimeSpan SessionReminderLeadTime = TimeSpan.FromHours(24);

    /// <summary>
    /// How long after the session was completed the balance notice becomes due, giving the client a day to pay on their own
    /// before the photographer is asked to chase it.
    /// </summary>
    public static readonly TimeSpan BalanceDueDelay = TimeSpan.FromHours(24);
}
