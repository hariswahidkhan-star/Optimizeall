using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Application.Abstractions.Tools;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.Domain.Governance;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Application.Agents.Runtime;

/// <summary>The disposition of a tool call after the platform has decided what to do with it.</summary>
public enum ToolDispositionKind
{
    Executed = 1,
    Denied = 2,

    /// <summary>An approval request was raised; the run must suspend until a human decides.</summary>
    GatedOnApproval = 3,

    /// <summary>Recorded but not applied, because the run is a dry run.</summary>
    SkippedDryRun = 4,

    Failed = 5,
}

public sealed record ToolDisposition(
    ToolDispositionKind Kind,
    ToolInvocation Invocation,
    string? ResultJson,
    ApprovalRequestId? ApprovalRequestId,
    string? Reason);

/// <summary>
/// The single chokepoint every tool call passes through.
/// <para>
/// A model asking to call a tool is an intent, not a permission. This service turns that intent
/// into a decision, applying the checks in an order chosen so that the cheapest and most absolute
/// come first, and so that no check can be skipped by an unusual code path — because there is only
/// one path.
/// </para>
/// </summary>
public sealed class ToolInvocationService(
    IToolCatalog toolCatalog,
    IWorkspaceRepository workspaces,
    IApprovalRepository approvals,
    IDataRedactor redactor,
    IClock clock,
    ILogger<ToolInvocationService> logger)
{
    /// <summary>
    /// Decides and, where permitted, performs a tool call.
    /// <para>
    /// Order of checks: registration, grant, budget, kill switch, approval, dry run, execute.
    /// Grant precedes everything else because an ungranted call should never even reach a
    /// kill-switch lookup; the kill switch precedes approval because a halted workspace must not
    /// accumulate pending approvals it will never be allowed to act on.
    /// </para>
    /// </summary>
    public async Task<ToolDisposition> InvokeAsync(
        AgentRun run,
        AgentDefinition definition,
        ToolCallRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset now = clock.UtcNow;

        Result<ToolInvocation> proposed = ToolInvocation.Propose(
            run.Id, request.ToolKey, request.ArgumentsJson, now);

        if (proposed.IsFailure)
        {
            ToolInvocation rejected = ToolInvocation.Propose(run.Id, ToolRegistry.KnowledgeSearch, "{}", now).Value;
            rejected.Deny(proposed.Error.Message, now);
            return new ToolDisposition(ToolDispositionKind.Denied, rejected, null, null, proposed.Error.Message);
        }

        ToolInvocation invocation = proposed.Value;
        run.AttachToolInvocation(invocation);

        // 1. Grant. Absence of a grant is denial; there is no default-allow branch.
        if (!definition.IsToolGranted(request.ToolKey))
        {
            return Deny(
                run,
                invocation,
                $"Agent '{definition.AgentKey}' has no grant for tool '{request.ToolKey}'.",
                now,
                isSecurityEvent: true);
        }

        // 2. Budget. Checked before the call, so a breach is prevented rather than reported.
        Result budgetCheck = run.Budget.CanCallTool(definition.BudgetPolicy);

        if (budgetCheck.IsFailure)
        {
            return Deny(run, invocation, budgetCheck.Error.Message, now, isSecurityEvent: false);
        }

        // 3. Emergency stop, read immediately before the effect rather than at run start, so a
        //    kill switch engaged mid-run actually stops the next action.
        if (invocation.RiskClass.RequiresHumanApproval())
        {
            bool halted = await workspaces
                .IsKillSwitchEngagedAsync(run.WorkspaceId, cancellationToken).ConfigureAwait(false);

            if (halted)
            {
                return Deny(
                    run,
                    invocation,
                    "The workspace emergency stop is engaged; no external action may be taken.",
                    now,
                    isSecurityEvent: false);
            }
        }

        // 4. Approval. Either an approval already covers this exact payload, or one is raised now.
        if (invocation.RequiresApproval)
        {
            ToolDisposition? gated = await ApplyApprovalGateAsync(
                run, definition, invocation, request, now, cancellationToken).ConfigureAwait(false);

            if (gated is not null)
            {
                return gated;
            }
        }

        // 5. Dry run. Recorded in full, applied not at all.
        if (run.IsDryRun && invocation.RiskClass != ActionRiskClass.Read)
        {
            // A dry run consumes the same budget as a real one. If it did not, a dry run would be a
            // poor rehearsal: it would succeed on a plan that the real run could not afford.
            run.CountToolCall();
            invocation.SkipAsDryRun(now);
            return new ToolDisposition(ToolDispositionKind.SkippedDryRun, invocation, invocation.ResultJson, null, null);
        }

        // 6. Execute.
        IToolExecutor? executor = toolCatalog.Find(request.ToolKey);

        if (executor is null)
        {
            return Deny(run, invocation, $"No executor is registered for tool '{request.ToolKey}'.", now, false);
        }

        invocation.Authorise();
        run.CountToolCall();

        ToolExecutionContext context = new()
        {
            TenantId = run.TenantIdentifier,
            WorkspaceId = run.WorkspaceId,
            Environment = run.Environment,
            RunId = run.Id,
            AgentKey = run.AgentKey,
            ConstraintsJson = definition.FindGrant(request.ToolKey)?.ConstraintsJson ?? "{}",
            IsDryRun = run.IsDryRun,
            IdempotencyKey = request.IdempotencyKey,
            CorrelationId = run.CorrelationId,
        };

        invocation.AssignIdempotencyKey(request.IdempotencyKey);

        try
        {
            Result<ToolOutcome> outcome = await executor
                .ExecuteAsync(request.ArgumentsJson, context, cancellationToken).ConfigureAwait(false);

            if (outcome.IsFailure)
            {
                string errorJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    code = outcome.Error.Code,
                    message = outcome.Error.Message,
                });

                invocation.FailWith(errorJson, clock.UtcNow, 0);
                return new ToolDisposition(ToolDispositionKind.Failed, invocation, errorJson, null, outcome.Error.Message);
            }

            string redactedResult = redactor.Redact(outcome.Value.ResultJson);
            invocation.Complete(redactedResult, clock.UtcNow, outcome.Value.DurationMilliseconds);

            return new ToolDisposition(ToolDispositionKind.Executed, invocation, redactedResult, null, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A tool implementation throwing must not abort the whole run: the model may well be
            // able to recover, and the failure is recorded either way.
            logger.LogError(exception, "Tool {ToolKey} threw during run {RunId}.", request.ToolKey, run.Id);

            string errorJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                code = "tool.unhandled_exception",
                message = "The tool failed unexpectedly.",
            });

            invocation.FailWith(errorJson, clock.UtcNow, 0);
            return new ToolDisposition(ToolDispositionKind.Failed, invocation, errorJson, null, "Tool failed unexpectedly.");
        }
    }

    /// <summary>
    /// Returns a disposition when the call cannot proceed yet, or null when an existing approval
    /// authorises it. The payload is re-fingerprinted here rather than trusted from the approval
    /// record, which is what closes the time-of-check/time-of-use gap.
    /// </summary>
    private async Task<ToolDisposition?> ApplyApprovalGateAsync(
        AgentRun run,
        AgentDefinition definition,
        ToolInvocation invocation,
        ToolCallRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (request.ExistingApprovalId is { } approvalId)
        {
            ApprovalRequest? approval = await approvals.FindAsync(approvalId, cancellationToken).ConfigureAwait(false);

            if (approval is null)
            {
                return Deny(run, invocation, "The referenced approval does not exist.", now, isSecurityEvent: true);
            }

            Result authorised = approval.AuthoriseExecution(request.ArgumentsJson, now);

            if (authorised.IsFailure)
            {
                // A mismatch here means the payload changed between approval and execution. That is
                // a security event, not a validation error.
                logger.LogWarning(
                    "Execution refused for run {RunId}: {Reason} (approval {ApprovalId}).",
                    run.Id,
                    authorised.Error.Code,
                    approvalId);

                return Deny(run, invocation, authorised.Error.Message, now, isSecurityEvent: true);
            }

            // The approval covers exactly this payload. Proceed to execution.
            return null;
        }

        ApprovalPolicy? policy = definition.ApprovalPolicyId is { } policyId
            ? await approvals.FindPolicyAsync(policyId, cancellationToken).ConfigureAwait(false)
            : null;

        // A missing or inactive policy does not open the gate. The platform floor still applies,
        // because gating must not depend on configuration being present and correct.
        ApprovalRequirement requirement = policy is { IsActive: true }
            ? policy.Resolve(invocation.RiskClass, invocation.ToolKey, request.EstimatedCost)
            : ApprovalRequirement.Required(
                invocation.RiskClass.MinimumApproverCount(),
                Domain.Access.BuiltInRoles.Approver,
                TimeSpan.FromHours(24));

        Result<ApprovalRequest> raised = ApprovalRequest.Raise(
            run.TenantIdentifier,
            run.WorkspaceId,
            run.Environment,
            invocation.RiskClass,
            $"{definition.DisplayName}: {invocation.ToolKey}",
            request.ArgumentsJson,
            PrincipalRef.ForAgent(definition.Id),
            requirement,
            now,
            request.EstimatedCost,
            run.Id,
            invocation.Id);

        if (raised.IsFailure)
        {
            return Deny(run, invocation, raised.Error.Message, now, isSecurityEvent: false);
        }

        approvals.Add(raised.Value);
        invocation.GateOnApproval(raised.Value.Id);

        return new ToolDisposition(
            ToolDispositionKind.GatedOnApproval,
            invocation,
            null,
            raised.Value.Id,
            "Awaiting human approval.");
    }

    private ToolDisposition Deny(
        AgentRun run,
        ToolInvocation invocation,
        string reason,
        DateTimeOffset now,
        bool isSecurityEvent)
    {
        invocation.Deny(reason, now);

        if (isSecurityEvent)
        {
            logger.LogWarning(
                "Tool call denied for run {RunId}, agent {AgentKey}, tool {ToolKey}: {Reason}",
                run.Id,
                run.AgentKey,
                invocation.ToolKey,
                reason);
        }

        return new ToolDisposition(ToolDispositionKind.Denied, invocation, null, null, reason);
    }
}

/// <summary>A model's request to call a tool, after the runtime has attached execution metadata.</summary>
public sealed record ToolCallRequest
{
    public required string ToolKey { get; init; }

    public required string ArgumentsJson { get; init; }

    /// <summary>Deduplicates the external effect across retries of the same logical call.</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Set when resuming after approval, so the gate verifies rather than re-raises.</summary>
    public ApprovalRequestId? ExistingApprovalId { get; init; }

    /// <summary>Drives threshold rules for Financial-class actions. Null when the action costs nothing.</summary>
    public Money? EstimatedCost { get; init; }
}
