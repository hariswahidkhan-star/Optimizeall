using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Orchestration.Planning;

/// <summary>A single step the planner proposes, before it becomes a persisted task.</summary>
public sealed record PlannedTask
{
    public required string Reference { get; init; }

    public required string Title { get; init; }

    /// <summary>The agent to assign. Null when no capable agent exists and a human must design the step.</summary>
    public string? AgentKey { get; init; }

    public required string InputJson { get; init; }

    /// <summary>References of steps that must succeed first. Must not contain a cycle.</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = [];

    public int MaxAttempts { get; init; } = 3;
}

public sealed record ObjectivePlan
{
    public required IReadOnlyList<PlannedTask> Tasks { get; init; }

    public required Money EstimatedCost { get; init; }

    public required string Rationale { get; init; }

    /// <summary>Steps the planner could not staff. Surfaced to administrators rather than silently dropped.</summary>
    public IReadOnlyList<string> UnstaffedSteps { get; init; } = [];
}

/// <summary>
/// Turns a business objective into a task graph.
/// <para>
/// Behind this port sits a model-driven planner, but the port exists so the orchestrator does not
/// depend on that. Planning is the one place where an LLM decides the shape of the work, which makes
/// it exactly the place worth being able to swap for a deterministic template when a tenant needs
/// reproducibility.
/// </para>
/// </summary>
public interface IObjectivePlanner
{
    Task<Result<ObjectivePlan>> PlanAsync(
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string objectiveTitle,
        string objectivePayloadJson,
        IReadOnlyList<AgentCapability> availableAgents,
        CancellationToken cancellationToken);
}

/// <summary>What an agent can do, as the planner sees it.</summary>
public sealed record AgentCapability(
    string AgentKey,
    string DisplayName,
    string Mission,
    IReadOnlyList<string> Tools,
    ActionRiskClass MaxRiskClass);
