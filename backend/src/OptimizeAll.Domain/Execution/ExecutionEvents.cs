using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Execution;

public sealed record AgentRunQueued(
    AgentRunId RunId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    EnvironmentTier Environment,
    string AgentKey,
    Guid CorrelationId,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "agent.run_queued";
}

public sealed record AgentRunStarted(
    AgentRunId RunId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    string AgentKey,
    string WorkerId,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "agent.run_started";
}

public sealed record AgentRunSuspended(
    AgentRunId RunId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    ApprovalRequestId BlockingApprovalId,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "agent.run_suspended";
}

public sealed record AgentRunResumed(
    AgentRunId RunId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "agent.run_resumed";
}

public sealed record AgentRunCompleted(
    AgentRunId RunId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    string AgentKey,
    AgentRunStatus Status,
    Money Cost,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "agent.run_completed";
}

public sealed record ToolInvocationDenied(
    AgentRunId RunId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    string ToolKey,
    string Reason,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "security.tool_invocation_denied";
}
