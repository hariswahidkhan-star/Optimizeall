using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Tenancy;

public sealed record WorkspaceCreated(
    WorkspaceId WorkspaceId,
    TenantId TenantId,
    string Slug,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "workspace.created";
}

public sealed record KillSwitchEngaged(
    WorkspaceId WorkspaceId,
    TenantId TenantId,
    string Reason,
    Guid EngagedBy,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "workspace.kill_switch_engaged";
}

public sealed record KillSwitchReleased(
    WorkspaceId WorkspaceId,
    TenantId TenantId,
    Guid ReleasedBy,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "workspace.kill_switch_released";
}
