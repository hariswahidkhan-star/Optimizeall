namespace OptimizeAll.SharedKernel.Primitives;

/// <summary>
/// Marker for aggregate roots, allowing the unit of work to collect domain events without
/// knowing each root's identifier type.
/// </summary>
public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}

/// <summary>
/// The consistency boundary of a transaction. Only aggregate roots are loaded and saved; entities
/// inside an aggregate are reached through their root. One transaction should modify one aggregate,
/// which is what keeps the invariants inside it enforceable without distributed locking.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : struct
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <summary>Required by EF Core materialisation. Not for application use.</summary>
    protected AggregateRoot()
    {
    }

    /// <summary>Optimistic concurrency token. Incremented by the persistence layer on every save.</summary>
    public int Version { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Called by the unit of work once the events have been handed to the dispatcher.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
