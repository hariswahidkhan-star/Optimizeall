namespace OptimizeAll.SharedKernel.Primitives;

/// <summary>
/// A fact about something that has already happened inside the domain. Domain events are raised
/// by aggregates and dispatched after the transaction commits, so a handler can never observe a
/// state that was subsequently rolled back.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }

    /// <summary>Stable dotted name used for audit and outbox routing, e.g. <c>approval.decided</c>.</summary>
    string EventType { get; }
}

/// <summary>Convenience base assigning identity and timestamp at construction.</summary>
public abstract record DomainEvent(DateTimeOffset OccurredAt) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();

    public abstract string EventType { get; }
}
