using FluentValidation;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Application.Orchestration.Planning;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Audit;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Governance;
using OptimizeAll.Domain.Orchestration;
using OptimizeAll.Domain.Tenancy;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Application.Orchestration.Commands;

/// <summary>Accepts a business objective, plans it, and activates the resulting task graph.</summary>
public sealed record StartWorkflowCommand
    : ICommand<StartWorkflowResult>, IRequirePermission, IScopedRequest, IAuditableRequest
{
    public required Guid WorkspaceId { get; init; }

    public required EnvironmentTier Environment { get; init; }

    public required string ObjectiveTitle { get; init; }

    public required string ObjectivePayloadJson { get; init; }

    public DateTimeOffset? Deadline { get; init; }

    public string RequiredPermission => Permissions.Workflow.Create;

    public string AuditAction => AuditActions.WorkflowStarted;

    public string AuditResourceType => nameof(WorkflowRun);

    public Guid? AuditResourceId => null;
}

public sealed record StartWorkflowResult(
    Guid WorkflowRunId,
    int TaskCount,
    decimal EstimatedCost,
    string Currency,
    IReadOnlyList<string> UnstaffedSteps);

public sealed class StartWorkflowCommandValidator : AbstractValidator<StartWorkflowCommand>
{
    public StartWorkflowCommandValidator()
    {
        RuleFor(c => c.WorkspaceId).NotEmpty();
        RuleFor(c => c.Environment).IsInEnum();
        RuleFor(c => c.ObjectiveTitle).NotEmpty().MaximumLength(300);

        RuleFor(c => c.ObjectivePayloadJson)
            .NotEmpty()
            .Must(CanonicalJson.IsValidJson)
            .WithMessage("The objective payload must be a valid JSON document.");

        RuleFor(c => c.Deadline)
            .Must(d => d is null || d.Value > DateTimeOffset.UtcNow)
            .WithMessage("A deadline must be in the future.");
    }
}

public sealed class StartWorkflowCommandHandler(
    IWorkflowRepository workflows,
    IWorkspaceRepository workspaces,
    IAgentDefinitionRepository definitions,
    IObjectivePlanner planner,
    ITenantContext tenantContext,
    IClock clock)
    : IRequestHandler<StartWorkflowCommand, StartWorkflowResult>
{
    public async Task<Result<StartWorkflowResult>> HandleAsync(
        StartWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        WorkspaceId workspaceId = WorkspaceId.From(command.WorkspaceId);

        Workspace? workspace = await workspaces.FindAsync(workspaceId, cancellationToken).ConfigureAwait(false);

        if (workspace is null)
        {
            return Result.Failure<StartWorkflowResult>(Error.NotFound("workspace.not_found", "No such workspace."));
        }

        if (workspace.KillSwitchEngaged)
        {
            return Result.Failure<StartWorkflowResult>(Error.Conflict(
                "workspace.kill_switch_engaged",
                "The workspace emergency stop is engaged; no new workflows can be started."));
        }

        IReadOnlyList<AgentDefinition> available = await definitions
            .ListForWorkspaceAsync(workspaceId, cancellationToken).ConfigureAwait(false);

        AgentCapability[] capabilities =
        [
            .. available
                .Where(d => d.IsExecutable)
                .Select(d => new AgentCapability(
                    d.AgentKey,
                    d.DisplayName,
                    d.Mission,
                    [.. d.ToolGrants.Select(g => g.ToolKey)],
                    d.MaxRiskClass)),
        ];

        if (capabilities.Length == 0)
        {
            return Result.Failure<StartWorkflowResult>(Error.Conflict(
                "workflow.no_agents",
                "This workspace has no published agents, so no objective can be planned."));
        }

        Result<ObjectivePlan> planned = await planner.PlanAsync(
            tenantContext.TenantId,
            workspaceId,
            command.Environment,
            command.ObjectiveTitle,
            command.ObjectivePayloadJson,
            capabilities,
            cancellationToken).ConfigureAwait(false);

        if (planned.IsFailure)
        {
            return Result.Failure<StartWorkflowResult>(planned.Error);
        }

        ObjectivePlan plan = planned.Value;

        if (plan.Tasks.Count == 0)
        {
            return Result.Failure<StartWorkflowResult>(Error.Invariant(
                "workflow.empty_plan",
                "The planner produced no tasks for this objective."));
        }

        Result<WorkflowRun> started = WorkflowRun.Start(
            tenantContext.TenantId,
            workspaceId,
            command.Environment,
            command.ObjectiveTitle,
            command.ObjectivePayloadJson,
            Guid.TryParse(tenantContext.CorrelationId, out Guid correlationId) ? correlationId : Guid.CreateVersion7(),
            clock.UtcNow,
            command.Deadline,
            workspace.MonthlyBudgetCap.Currency);

        if (started.IsFailure)
        {
            return Result.Failure<StartWorkflowResult>(started.Error);
        }

        WorkflowRun workflow = started.Value;

        Result materialised = MaterialisePlan(workflow, plan);

        if (materialised.IsFailure)
        {
            return Result.Failure<StartWorkflowResult>(materialised.Error);
        }

        Result activated = workflow.Activate(plan.EstimatedCost, clock.UtcNow);

        if (activated.IsFailure)
        {
            return Result.Failure<StartWorkflowResult>(activated.Error);
        }

        workflows.Add(workflow);

        return Result.Success(new StartWorkflowResult(
            workflow.Id.Value,
            workflow.Tasks.Count,
            plan.EstimatedCost.Amount,
            plan.EstimatedCost.Currency,
            plan.UnstaffedSteps));
    }

    /// <summary>
    /// Converts the planner's references into real tasks and edges.
    /// <para>
    /// The dependency edges are added through the aggregate, which rejects a cycle. That check is
    /// not defensive nicety: a planner is model-driven, and a model can and eventually will emit a
    /// graph with a loop in it. A cyclic plan would deadlock the orchestrator with no timeout able
    /// to distinguish it from slow work.
    /// </para>
    /// </summary>
    private static Result MaterialisePlan(WorkflowRun workflow, ObjectivePlan plan)
    {
        Dictionary<string, WorkTaskId> byReference = new(StringComparer.Ordinal);

        foreach (PlannedTask planned in plan.Tasks)
        {
            Result<WorkTask> added = workflow.AddTask(
                planned.Title, planned.AgentKey, planned.InputJson, planned.MaxAttempts);

            if (added.IsFailure)
            {
                return Result.Failure(added.Error);
            }

            if (!byReference.TryAdd(planned.Reference, added.Value.Id))
            {
                return Result.Failure(Error.Validation(
                    "workflow.duplicate_task_reference",
                    $"The plan uses the reference '{planned.Reference}' more than once."));
            }
        }

        foreach (PlannedTask planned in plan.Tasks)
        {
            WorkTaskId taskId = byReference[planned.Reference];

            foreach (string dependency in planned.DependsOn)
            {
                if (!byReference.TryGetValue(dependency, out WorkTaskId dependsOnId))
                {
                    return Result.Failure(Error.Validation(
                        "workflow.unknown_dependency",
                        $"Task '{planned.Reference}' depends on '{dependency}', which is not part of the plan."));
                }

                Result edge = workflow.AddDependency(taskId, dependsOnId);

                if (edge.IsFailure)
                {
                    return Result.Failure(edge.Error);
                }
            }
        }

        return Result.Success();
    }
}
