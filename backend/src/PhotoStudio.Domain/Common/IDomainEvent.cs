namespace PhotoStudio.Domain.Common;

/// <summary>
/// Marks a fact that happened inside an aggregate and that other modules may react to.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the instant (UTC) at which the event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}
