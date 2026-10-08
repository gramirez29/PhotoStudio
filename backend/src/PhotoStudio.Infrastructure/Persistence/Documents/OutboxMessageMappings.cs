using PhotoStudio.Domain.Common;
using PhotoStudio.Infrastructure.Persistence.Outbox;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// Mapping from domain events to outbox documents.
/// </summary>
public static class OutboxMessageMappings
{
    /// <summary>
    /// Builds the pending outbox message for a domain event. The message is due immediately.
    /// </summary>
    /// <param name="domainEvent">Event to store.</param>
    /// <returns>The outbox document.</returns>
    public static OutboxMessageDocument ToOutboxMessage(this IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var (type, payload) = DomainEventSerializer.Serialize(domainEvent);
        return new OutboxMessageDocument
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Payload = payload,
            OccurredAt = domainEvent.OccurredAt.UtcDateTime,
            Status = OutboxMessageDocument.PendingStatus,
            NextAttemptAt = DateTime.UnixEpoch,
        };
    }
}
