using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Reacts to a domain event delivered by the outbox. The outbox guarantees at-least-once delivery, so every implementation
/// must be idempotent: handling the same event twice must leave the system in the same state as handling it once.
/// </summary>
/// <typeparam name="TEvent">Type of the event the handler reacts to.</typeparam>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles the event. An exception makes the outbox retry the delivery later.
    /// </summary>
    /// <param name="domainEvent">Event to handle.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the event has been handled.</returns>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
