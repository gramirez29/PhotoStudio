using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Bookings.Events;

namespace PhotoStudio.Application.Billing;

/// <summary>
/// Settles the money of a booking when it ends without delivering its session: cancelled, expired or the client did not
/// show up, and undoes that when the absence mark is reverted. Every event does the same thing, which is to ask the planner
/// to reconcile from the current state of the booking, so the handler is idempotent and does not depend on the order of the
/// events.
/// </summary>
/// <param name="planner">Plans the settlement of a booking.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class BookingSettlementHandler(SettlementPlanner planner, TimeProvider timeProvider) :
    IDomainEventHandler<BookingCancelled>,
    IDomainEventHandler<BookingExpired>,
    IDomainEventHandler<ClientMarkedAbsent>,
    IDomainEventHandler<ClientAbsenceReverted>
{
    /// <summary>
    /// Settles a cancelled booking.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the settlement is in line with the booking.</returns>
    public Task HandleAsync(BookingCancelled domainEvent, CancellationToken cancellationToken) =>
        Reconcile(domainEvent.BookingId, cancellationToken);

    /// <summary>
    /// Settles an expired booking that had money paid.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the settlement is in line with the booking.</returns>
    public Task HandleAsync(BookingExpired domainEvent, CancellationToken cancellationToken) =>
        Reconcile(domainEvent.BookingId, cancellationToken);

    /// <summary>
    /// Retains the deposit of a booking whose client did not show up.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the settlement is in line with the booking.</returns>
    public Task HandleAsync(ClientMarkedAbsent domainEvent, CancellationToken cancellationToken) =>
        Reconcile(domainEvent.BookingId, cancellationToken);

    /// <summary>
    /// Undoes the retention of a booking whose absence mark was reverted.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the settlement is in line with the booking.</returns>
    public Task HandleAsync(ClientAbsenceReverted domainEvent, CancellationToken cancellationToken) =>
        Reconcile(domainEvent.BookingId, cancellationToken);

    /// <summary>
    /// Asks the planner to reconcile the settlement of a booking.
    /// </summary>
    /// <param name="bookingId">Booking to reconcile.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the settlement is in line with the booking.</returns>
    private Task Reconcile(Guid bookingId, CancellationToken cancellationToken) =>
        planner.ReconcileAsync(bookingId, timeProvider.GetUtcNow(), cancellationToken);
}
