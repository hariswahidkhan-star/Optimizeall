using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Orchestration;

public sealed record WorkflowStarted(
    WorkflowRunId WorkflowRunId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    EnvironmentTier Environment,
    string ObjectiveTitle,
    Guid CorrelationId,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "workflow.started";
}

public sealed record WorkflowActivated(
    WorkflowRunId WorkflowRunId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    int TaskCount,
    Money EstimatedCost,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "workflow.activated";
}

public sealed record WorkflowTaskBlocked(
    WorkflowRunId WorkflowRunId,
    TenantId TenantId,
    WorkTaskId TaskId,
    string Reason,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "workflow.task_blocked";
}

public sealed record WorkflowCompleted(
    WorkflowRunId WorkflowRunId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    WorkflowStatus Status,
    Money ActualCost,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "workflow.completed";
}
