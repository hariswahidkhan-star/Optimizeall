using FluentValidation;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.Audit;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.Domain.Governance;
using OptimizeAll.Domain.Notifications;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Application.Governance.Commands;

/// <summary>
/// A human's decision on a gated action.
/// <para>
/// This is the most security-sensitive command in the platform, and almost all of its rules live in
/// the <see cref="ApprovalRequest"/> aggregate rather than here. The handler's job is to load,
/// delegate, and deal with the consequences — deliberately, so that a second caller of the
/// aggregate cannot bypass a check this handler happened to perform locally.
/// </para>
/// </summary>
public sealed record DecideApprovalCommand : ICommand<DecideApprovalResult>, IRequirePermission, IAuditableRequest
{
    public required Guid ApprovalRequestId { get; init; }

    public required ApprovalDecisionKind Decision { get; init; }

    /// <summary>Mandatory when rejecting.</summary>
    public string? Rationale { get; init; }

    public string RequiredPermission => Permissions.Approval.Decide;

    public string AuditAction => AuditActions.ApprovalDecided;

    public string AuditResourceType => nameof(ApprovalRequest);

    public Guid? AuditResourceId => ApprovalRequestId;
}

public sealed record DecideApprovalResult(
    Guid ApprovalRequestId,
    ApprovalStatus Status,
    int ApprovalsReceived,
    int ApprovalsRequired,
    bool RunResumed);

public sealed class DecideApprovalCommandValidator : AbstractValidator<DecideApprovalCommand>
{
    public DecideApprovalCommandValidator()
    {
        RuleFor(c => c.ApprovalRequestId).NotEmpty();
        RuleFor(c => c.Decision).IsInEnum();

        RuleFor(c => c.Rationale)
            .NotEmpty()
            .When(c => c.Decision == ApprovalDecisionKind.Reject)
            .WithMessage("A rejection must record why the action was refused.");

        RuleFor(c => c.Rationale).MaximumLength(4000);
    }
}

public sealed class DecideApprovalCommandHandler(
    IApprovalRepository approvals,
    IAgentRunRepository runs,
    INotificationRepository notifications,
    ICurrentPrincipal principal,
    IPermissionEvaluator permissions,
    IStepUpVerifier stepUpVerifier,
    IRunQueue runQueue,
    IClock clock)
    : IRequestHandler<DecideApprovalCommand, DecideApprovalResult>
{
    /// <summary>How recently the approver must have re-authenticated for a high-risk decision.</summary>
    private static readonly TimeSpan StepUpMaxAge = TimeSpan.FromMinutes(15);

    public async Task<Result<DecideApprovalResult>> HandleAsync(
        DecideApprovalCommand command,
        CancellationToken cancellationToken)
    {
        ApprovalRequest? request = await approvals
            .FindAsync(ApprovalRequestId.From(command.ApprovalRequestId), cancellationToken).ConfigureAwait(false);

        if (request is null)
        {
            return Result.Failure<DecideApprovalResult>(Error.NotFound(
                "approval.not_found",
                "No such approval request."));
        }

        // Financial and Irreversible decisions need their own permission on top of approval:decide.
        // Being an approver is not the same as being authorised to commit money.
        Result elevated = await CheckElevatedAuthorityAsync(request, cancellationToken).ConfigureAwait(false);

        if (elevated.IsFailure)
        {
            return Result.Failure<DecideApprovalResult>(elevated.Error);
        }

        bool stepUpVerified = principal.StepUpVerified
            || await stepUpVerifier.IsVerifiedAsync(principal.Principal, StepUpMaxAge, cancellationToken)
                .ConfigureAwait(false);

        Result decided = request.Decide(
            principal.Principal,
            command.Decision,
            command.Rationale,
            stepUpVerified,
            clock.UtcNow);

        if (decided.IsFailure)
        {
            return Result.Failure<DecideApprovalResult>(decided.Error);
        }

        bool runResumed = false;

        if (request.AgentRunId is { } runId)
        {
            runResumed = await SettleBlockedRunAsync(request, runId, cancellationToken).ConfigureAwait(false);
        }

        NotifyRequesterIfHuman(request);

        return Result.Success(new DecideApprovalResult(
            request.Id.Value,
            request.Status,
            request.ApprovalsReceived,
            request.RequiredApproverCount,
            runResumed));
    }

    private async Task<Result> CheckElevatedAuthorityAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        string? elevatedPermission = request.RiskClass switch
        {
            ActionRiskClass.Financial => Permissions.Approval.DecideFinancial,
            ActionRiskClass.Irreversible => Permissions.Approval.DecideIrreversible,
            _ => null,
        };

        if (elevatedPermission is null)
        {
            return Result.Success();
        }

        return await permissions.AuthoriseAsync(
            principal.Principal,
            request.TenantIdentifier,
            request.WorkspaceId,
            request.Environment,
            elevatedPermission,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases the run this approval was blocking. An approved request re-queues it; a refused one
    /// cancels it, because a run whose only remaining action was rejected has nothing left to do.
    /// </summary>
    private async Task<bool> SettleBlockedRunAsync(
        ApprovalRequest request,
        AgentRunId runId,
        CancellationToken cancellationToken)
    {
        if (request.Status == ApprovalStatus.Pending)
        {
            // More approvers are still required; the run stays suspended.
            return false;
        }

        AgentRun? run = await runs.FindAsync(runId, cancellationToken).ConfigureAwait(false);

        if (run is null || run.Status != AgentRunStatus.AwaitingApproval)
        {
            return false;
        }

        if (request.Status == ApprovalStatus.Approved)
        {
            run.Resume(clock.UtcNow);
            await runQueue.EnqueueAsync(run.Id, run.WorkspaceId, cancellationToken).ConfigureAwait(false);
            return true;
        }

        run.Cancel(
            $"The required approval was {request.Status.ToString().ToLowerInvariant()}.",
            clock.UtcNow);

        return false;
    }

    private void NotifyRequesterIfHuman(ApprovalRequest request)
    {
        // An agent has no inbox. Notifying one would create an unread notification nobody ever sees.
        if (request.RequestedByType != PrincipalType.User || request.Status == ApprovalStatus.Pending)
        {
            return;
        }

        notifications.Add(Notification.Create(
            request.TenantIdentifier,
            UserId.From(request.RequestedById),
            NotificationCategory.ApprovalResolved,
            request.Status == ApprovalStatus.Approved
                ? NotificationSeverity.Informational
                : NotificationSeverity.Warning,
            $"Approval {request.Status.ToString().ToLowerInvariant()}: {request.Title}",
            request.Status == ApprovalStatus.Approved
                ? "Your request was approved and the action will now proceed."
                : "Your request was refused. See the approver's rationale for the reason.",
            clock.UtcNow,
            $"/approvals/{request.Id.Value}",
            [NotificationChannel.InApp, NotificationChannel.Email]));
    }
}
