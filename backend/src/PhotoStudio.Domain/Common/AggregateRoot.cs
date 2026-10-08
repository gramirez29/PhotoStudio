namespace PhotoStudio.Domain.Common;

/// <summary>
/// Base type for aggregate roots. Tracks the persisted version used for optimistic concurrency
/// and accumulates the domain events raised during the current unit of work.
/// </summary>
/// <typeparam name="TId">Type of the aggregate identifier.</typeparam>
public abstract class AggregateRoot<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateRoot{TId}"/> class.
    /// </summary>
    /// <param name="id">Aggregate identifier.</param>
    /// <param name="version">Version loaded from persistence; zero for a new aggregate.</param>
    protected AggregateRoot(TId id, long version)
    {
        Id = id;
        Version = version;
    }

    /// <summary>
    /// Gets the aggregate identifier.
    /// </summary>
    public TId Id { get; }

    /// <summary>
    /// Gets the version that was loaded from persistence. Repositories compare it to detect concurrent writes.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets the domain events raised since the aggregate was loaded or created.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Removes every pending domain event, typically after they have been stored in the outbox.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Records a domain event to be dispatched after the aggregate is persisted.
    /// </summary>
    /// <param name="domainEvent">The event to record.</param>
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
