using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Tenancy;

public sealed record TenantProvisioned(TenantId TenantId, string Slug, string Region, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt)
{
    public override string EventType => "tenant.provisioned";
}

public sealed record TenantActivated(TenantId TenantId, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "tenant.activated";
}

public sealed record TenantSuspended(TenantId TenantId, string Reason, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt)
{
    public override string EventType => "tenant.suspended";
}

public sealed record TenantDeletionRequested(TenantId TenantId, DateTimeOffset PurgeAfter, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt)
{
    public override string EventType => "tenant.deletion_requested";
}

public sealed record TenantRestored(TenantId TenantId, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "tenant.restored";
}
