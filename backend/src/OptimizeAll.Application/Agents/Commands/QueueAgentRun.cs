using FluentValidation;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Audit;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.Domain.Governance;
using OptimizeAll.Domain.Tenancy;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Application.Agents.Commands;

/// <summary>Queues one agent run. The run is executed later by a worker, never inline on the request thread.</summary>
public sealed record QueueAgentRunCommand
    : ICommand<QueueAgentRunResult>, IRequirePermission, IScopedRequest, IAuditableRequest
{
    public required Guid WorkspaceId { get; init; }

    public required EnvironmentTier Environment { get; init; }

    public required string AgentKey { get; init; }

    /// <summary>The agent's typed input, validated against the definition's input schema.</summary>
    public required string InputJson { get; init; }

    /// <summary>Stubs every external-effect tool: fully recorded, nothing applied.</summary>
    public bool IsDryRun { get; init; }

    public Guid? TaskId { get; init; }

    public string RequiredPermission => Permissions.Agent.Invoke;

    public string AuditAction => AuditActions.RunQueued;

    public string AuditResourceType => nameof(AgentRun);

    public Guid? AuditResourceId => null;
}

public sealed record QueueAgentRunResult(Guid RunId, string AgentKey, int DefinitionVersion, AgentRunStatus Status);

public sealed class QueueAgentRunCommandValidator : AbstractValidator<QueueAgentRunCommand>
{
    public QueueAgentRunCommandValidator()
    {
        RuleFor(c => c.WorkspaceId).NotEmpty();
        RuleFor(c => c.Environment).IsInEnum();
        RuleFor(c => c.AgentKey).NotEmpty().MaximumLength(100);

        RuleFor(c => c.InputJson)
            .NotEmpty()
            .Must(CanonicalJson.IsValidJson)
            .WithMessage("Run input must be a valid JSON document.");
    }
}

public sealed class QueueAgentRunCommandHandler(
    IAgentDefinitionRepository definitions,
    IAgentRunRepository runs,
    IWorkspaceRepository workspaces,
    IRunQueue runQueue,
    ICurrentPrincipal principal,
    ITenantContext tenantContext,
    IClock clock)
    : IRequestHandler<QueueAgentRunCommand, QueueAgentRunResult>
{
    public async Task<Result<QueueAgentRunResult>> HandleAsync(
        QueueAgentRunCommand command,
        CancellationToken cancellationToken)
    {
        WorkspaceId workspaceId = WorkspaceId.From(command.WorkspaceId);

        Workspace? workspace = await workspaces.FindAsync(workspaceId, cancellationToken).ConfigureAwait(false);

        if (workspace is null)
        {
            return Result.Failure<QueueAgentRunResult>(Error.NotFound(
                "workspace.not_found",
                "No such workspace."));
        }

        // Refusing to queue while the emergency stop is engaged is deliberate. Queueing runs that
        // will be blocked at their first external action produces a backlog that fires all at once
        // the moment the switch is released — precisely when an operator least wants a surge.
        if (workspace.KillSwitchEngaged)
        {
            return Result.Failure<QueueAgentRunResult>(Error.Conflict(
                "workspace.kill_switch_engaged",
                "The workspace emergency stop is engaged; no new runs can be queued."));
        }

        AgentDefinition? definition = await definitions
            .FindPublishedAsync(workspaceId, command.AgentKey, cancellationToken).ConfigureAwait(false);

        if (definition is null)
        {
            return Result.Failure<QueueAgentRunResult>(Error.NotFound(
                "agent.not_published",
                $"No published version of agent '{command.AgentKey}' exists in this workspace."));
        }

        Result budgetCheck = await CheckWorkspaceBudgetAsync(workspace, cancellationToken).ConfigureAwait(false);

        if (budgetCheck.IsFailure)
        {
            return Result.Failure<QueueAgentRunResult>(budgetCheck.Error);
        }

        Result<AgentRun> queued = AgentRun.Queue(
            definition,
            command.Environment,
            RunTriggerType.Manual,
            principal.Principal.Id,
            command.InputJson,
            Guid.TryParse(tenantContext.CorrelationId, out Guid correlationId) ? correlationId : Guid.CreateVersion7(),
            clock.UtcNow,
            command.IsDryRun,
            command.TaskId is { } taskId ? WorkTaskId.From(taskId) : null,
            workspace.MonthlyBudgetCap.Currency);

        if (queued.IsFailure)
        {
            return Result.Failure<QueueAgentRunResult>(queued.Error);
        }

        runs.Add(queued.Value);

        // Enqueued after the aggregate is added but inside the same transaction. The queue is
        // at-least-once, so a worker that picks up a run before the commit lands simply finds
        // nothing and the message is redelivered — safe. The reverse order would not be.
        await runQueue.EnqueueAsync(queued.Value.Id, workspaceId, cancellationToken).ConfigureAwait(false);

        return Result.Success(new QueueAgentRunResult(
            queued.Value.Id.Value,
            definition.AgentKey,
            definition.DefinitionVersion,
            queued.Value.Status));
    }

    /// <summary>
    /// Refuses to queue once the workspace has consumed its monthly cap. Checked here as well as
    /// per-run, because a hundred individually-affordable runs still add up to an overspend.
    /// </summary>
    private async Task<Result> CheckWorkspaceBudgetAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        if (workspace.MonthlyBudgetCap.IsZero)
        {
            return Result.Success();
        }

        decimal spent = await runs
            .GetMonthToDateCostAsync(workspace.Id, clock.UtcNow, cancellationToken).ConfigureAwait(false);

        if (spent >= workspace.MonthlyBudgetCap.Amount)
        {
            return Result.Failure(Error.Exhausted(
                "workspace.budget_exhausted",
                $"This workspace has consumed its monthly budget of {workspace.MonthlyBudgetCap}."));
        }

        return Result.Success();
    }
}
