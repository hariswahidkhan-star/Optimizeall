namespace OptimizeAll.SharedKernel.Primitives;

/// <summary>
/// Creation and modification provenance, stamped by the persistence layer rather than by callers.
/// Leaving this to individual handlers guarantees that some of them will forget.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    Guid? CreatedBy { get; }

    DateTimeOffset? UpdatedAt { get; }

    Guid? UpdatedBy { get; }

    void StampCreated(DateTimeOffset at, Guid? by);

    void StampUpdated(DateTimeOffset at, Guid? by);
}

/// <summary>Recoverable deletion. Read paths filter these rows out by default.</summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }

    void MarkDeleted(DateTimeOffset at);
}

/// <summary>Ownership by a tenant. Every row carrying this is subject to row-level security.</summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
