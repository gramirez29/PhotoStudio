namespace PhotoStudio.Application.Maintenance.RunMaintenance;

/// <summary>
/// Command to run one maintenance pass: expire the tentative bookings whose hold ended, deliver the pending outbox
/// messages and deliver the notifications that came due. Issued by the daily scheduled worker and by the photographer on demand.
/// </summary>
public sealed record RunMaintenanceCommand;

/// <summary>
/// Outcome of a maintenance pass.
/// </summary>
/// <param name="BookingsExpired">Bookings that moved to <c>Expired</c>.</param>
/// <param name="BookingsSkipped">Bookings left untouched because a guard failed or another write won the race.</param>
/// <param name="EventsProcessed">Outbox messages that were claimed for delivery.</param>
/// <param name="NotificationsDelivered">Notifications that reached a photographer's inbox.</param>
/// <param name="MoreWorkPending">Whether the pass stopped at its size limit and a new pass would find more to do.</param>
public sealed record MaintenanceResponse(int BookingsExpired, int BookingsSkipped, int EventsProcessed, int NotificationsDelivered, bool MoreWorkPending);
