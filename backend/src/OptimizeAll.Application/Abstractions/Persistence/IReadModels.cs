using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.Domain.Governance;

namespace OptimizeAll.Application.Abstractions.Persistence;

/// <summary>
/// Read-side projections.
/// <para>
/// Separated from the repositories because reads and writes want different shapes: a list screen
/// needs a flat, paged projection across several aggregates, and forcing that through
/// aggregate-shaped repositories produces either N+1 loads or an <c>IQueryable</c> escape hatch
/// that lets a caller compose a query with no tenant filter.
/// </para>
/// </summary>
public interface IApprovalReadModel
{
    Task<PagedResult<ApprovalSummary>> ListPendingAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<ApprovalDetail?> GetDetailAsync(ApprovalRequestId id, CancellationToken cancellationToken);
}

public interface IRunReadModel
{
    Task<PagedResult<RunSummary>> ListAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string? agentKey,
        AgentRunStatus? status,
        PageRequest page,
        CancellationToken cancellationToken);

    Task<RunDetail?> GetDetailAsync(AgentRunId id, CancellationToken cancellationToken);
}

public interface IDashboardReadModel
{
    Task<ExecutiveDashboard> GetExecutiveDashboardAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

/// <summary>
/// Paging is mandatory on every list endpoint. An unbounded list works fine on a demo dataset and
/// then times out on the first real tenant.
/// </summary>
public sealed record PageRequest(int Page = 1, int PageSize = 25)
{
    public const int MaxPageSize = 100;

    public int Skip => (Math.Max(1, Page) - 1) * Take;

    public int Take => Math.Clamp(PageSize, 1, MaxPageSize);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public long TotalPages => PageSize == 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;

    public bool HasNextPage => Page < TotalPages;
}

public sealed record ApprovalSummary(
    Guid Id,
    string Title,
    ActionRiskClass RiskClass,
    string RequestedByAgentKey,
    int ApprovalsReceived,
    int ApprovalsRequired,
    decimal? EstimatedCostAmount,
    string? EstimatedCostCurrency,
    DateTimeOffset ExpiresAt,
    DateTimeOffset RequestedAt);

public sealed record ApprovalDetail(
    Guid Id,
    string Title,
    ActionRiskClass RiskClass,
    ApprovalStatus Status,
    string PayloadJson,
    string PayloadFingerprint,
    decimal? EstimatedCostAmount,
    string? EstimatedCostCurrency,
    Guid? AgentRunId,
    string? AgentKey,
    Guid? WorkflowRunId,
    string? ObjectiveTitle,
    int ApprovalsReceived,
    int ApprovalsRequired,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<ApprovalDecisionView> Decisions);

public sealed record ApprovalDecisionView(
    Guid ApproverUserId,
    string ApproverDisplayName,
    ApprovalDecisionKind Decision,
    string? Rationale,
    DateTimeOffset DecidedAt);

public sealed record RunSummary(
    Guid Id,
    string AgentKey,
    int DefinitionVersion,
    AgentRunStatus Status,
    RunTriggerType TriggerType,
    long TotalTokens,
    decimal CostAmount,
    string CostCurrency,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    bool IsDryRun);

public sealed record RunDetail(
    Guid Id,
    string AgentKey,
    int DefinitionVersion,
    AgentRunStatus Status,
    string InputJson,
    string? OutputJson,
    string? ErrorJson,
    string? Provider,
    string? Model,
    long PromptTokens,
    long CompletionTokens,
    decimal CostAmount,
    string CostCurrency,
    int IterationCount,
    Guid CorrelationId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<RunStepView> Steps,
    IReadOnlyList<ToolInvocationView> ToolInvocations);

public sealed record RunStepView(
    int Sequence,
    RunStepType StepType,
    string ContentJson,
    long Tokens,
    int LatencyMilliseconds,
    DateTimeOffset OccurredAt);

public sealed record ToolInvocationView(
    Guid Id,
    string ToolKey,
    ActionRiskClass RiskClass,
    ToolInvocationStatus Status,
    string ArgumentsJson,
    string? ResultJson,
    string? DenialReason,
    Guid? ApprovalRequestId,
    int? DurationMilliseconds,
    DateTimeOffset RequestedAt);

public sealed record ExecutiveDashboard(
    long RunsCompleted,
    long RunsFailed,
    long ApprovalsPending,
    long ApprovalsApproved,
    long ApprovalsRejected,
    double ApprovalMedianMinutes,
    decimal SpendAmount,
    string SpendCurrency,
    decimal BudgetCapAmount,
    IReadOnlyList<AgentPerformance> AgentPerformance,
    IReadOnlyList<SpendPoint> SpendTrend);

public sealed record AgentPerformance(
    string AgentKey,
    string DisplayName,
    long RunCount,
    double SuccessRate,
    double ApprovalRate,
    decimal AverageCost,
    double AverageDurationSeconds);

public sealed record SpendPoint(DateTimeOffset Date, decimal Amount);
