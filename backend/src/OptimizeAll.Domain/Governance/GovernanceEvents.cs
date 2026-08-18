using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Governance;

public sealed record ApprovalRequested(
    ApprovalRequestId RequestId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    EnvironmentTier Environment,
    ActionRiskClass RiskClass,
    string Title,
    PrincipalRef RequestedBy,
    DateTimeOffset ExpiresAt,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "approval.requested";
}

public sealed record ApprovalGranted(
    ApprovalRequestId RequestId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    string PayloadFingerprint,
    int ApproverCount,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "approval.granted";
}

public sealed record ApprovalRejected(
    ApprovalRequestId RequestId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    PrincipalRef RejectedBy,
    string Rationale,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "approval.rejected";
}

public sealed record ApprovalExpired(
    ApprovalRequestId RequestId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "approval.expired";
}

public sealed record ApprovalCancelled(
    ApprovalRequestId RequestId,
    TenantId TenantId,
    string Reason,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "approval.cancelled";
}
