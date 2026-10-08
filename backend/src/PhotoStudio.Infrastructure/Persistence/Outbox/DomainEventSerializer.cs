using System.Text.Json;
using System.Text.Json.Serialization;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Infrastructure.Persistence.Outbox;

/// <summary>
/// Converts domain events to and from the JSON stored in the outbox. Events are identified by their type name, so renaming
/// an event type requires keeping the old name readable for messages that are still pending.
/// </summary>
public static class DomainEventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Dictionary<string, Type> EventTypes = typeof(IDomainEvent).Assembly
        .GetTypes()
        .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IDomainEvent).IsAssignableFrom(type))
        .ToDictionary(type => type.Name);

    /// <summary>
    /// Serializes an event.
    /// </summary>
    /// <param name="domainEvent">Event to serialize.</param>
    /// <returns>The type name that identifies the event and its JSON payload.</returns>
    public static (string Type, string Payload) Serialize(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var type = domainEvent.GetType();
        return (type.Name, JsonSerializer.Serialize(domainEvent, type, Options));
    }

    /// <summary>
    /// Rebuilds an event from its stored form.
    /// </summary>
    /// <param name="type">Type name stored with the message.</param>
    /// <param name="payload">JSON payload stored with the message.</param>
    /// <returns>The event.</returns>
    /// <exception cref="InvalidOperationException">When the type name is unknown or the payload cannot be read.</exception>
    public static IDomainEvent Deserialize(string type, string payload)
    {
        if (!EventTypes.TryGetValue(type, out var eventType))
        {
            throw new InvalidOperationException($"Unknown domain event type '{type}'.");
        }

        try
        {
            return (IDomainEvent?)JsonSerializer.Deserialize(payload, eventType, Options)
                ?? throw new InvalidOperationException($"The payload of '{type}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The payload of '{type}' cannot be read.", exception);
        }
    }
}
