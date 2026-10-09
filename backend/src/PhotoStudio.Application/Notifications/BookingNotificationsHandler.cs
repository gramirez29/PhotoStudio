using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Bookings.Events;

namespace PhotoStudio.Application.Notifications;

/// <summary>
/// Keeps the session reminder of a booking in line with it whenever the booking changes in a way that affects it: it is
/// scheduled when the booking is confirmed, moved when the session is rescheduled, and dropped when the booking is
/// cancelled, expires or the client is marked absent. Every event does the same thing, which is to ask the planner to
/// reconcile, so the handler is idempotent and does not depend on the order of the events.
/// </summary>
/// <param name="planner">Plans the notifications of a booking.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class BookingNotificationsHandler(NotificationPlanner planner, TimeProvider timeProvider) :
    IDomainEventHandler<BookingConfirmed>,
    IDomainEventHandler<BookingRescheduled>,
    IDomainEventHandler<BookingCancelled>,
    IDomainEventHandler<BookingExpired>,
    IDomainEventHandler<ClientMarkedAbsent>
{
    /// <summary>
    /// Schedules the reminder of the confirmed booking.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the reminder is planned.</returns>
    public Task HandleAsync(BookingConfirmed domainEvent, CancellationToken cancellationToken) =>
        Reconcile(domainEvent.BookingId, cancellationToken);

    /// <summary>
    /// Moves the reminder to the new session time.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the reminder is planned.</returns>
    public Task HandleAsync(BookingRescheduled domainEvent, CancellationToken cancellationToken) =>
        Reconcile(domainEvent.BookingId, cancellationToken);

    /// <summary>
    /// Drops the reminder of the cancelled booking.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the reminder is planned.</returns>
    public Task HandleAsync(BookingCancelled domainEvent, CancellationToken cancellationToken) =>
        Reconcile(domainEvent.BookingId, cancellationToken);

    /// <summary>
    /// Drops the reminder of the expired booking.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the reminder is planned.</returns>
    public Task HandleAsync(BookingExpired domainEvent, CancellationToken cancellationToken) =>
        Reconcile(domainEvent.BookingId, cancellationToken);

    /// <summary>
    /// Drops the reminder of the booking whose client did not show up.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the reminder is planned.</returns>
    public Task HandleAsync(ClientMarkedAbsent domainEvent, CancellationToken cancellationToken) =>
        Reconcile(domainEvent.BookingId, cancellationToken);

    /// <summary>
    /// Brings the reminder of a booking in line with its current state.
    /// </summary>
    /// <param name="bookingId">Booking that changed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the reminder is planned.</returns>
    private Task Reconcile(Guid bookingId, CancellationToken cancellationToken) =>
        planner.ReconcileSessionReminderAsync(bookingId, timeProvider.GetUtcNow(), cancellationToken);
}
