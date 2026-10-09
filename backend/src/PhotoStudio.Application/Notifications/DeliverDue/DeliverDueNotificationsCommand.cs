namespace PhotoStudio.Application.Notifications.DeliverDue;

/// <summary>
/// Command, issued by a background job, to deliver the notifications that came due.
/// </summary>
/// <param name="BatchSize">Maximum number of notifications to examine in one run.</param>
public sealed record DeliverDueNotificationsCommand(int BatchSize);

/// <summary>
/// Outcome of one delivery run.
/// </summary>
/// <param name="Delivered">Notifications that reached an inbox.</param>
/// <param name="Cancelled">Notifications dropped because they were no longer relevant.</param>
/// <param name="Skipped">Notifications left for the next run because another write changed them first.</param>
public sealed record DeliverDueNotificationsResult(int Delivered, int Cancelled, int Skipped);
