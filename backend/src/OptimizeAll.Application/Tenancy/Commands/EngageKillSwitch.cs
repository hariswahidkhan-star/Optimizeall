using FluentValidation;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.Audit;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Tenancy;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Application.Tenancy.Commands;

/// <summary>
/// The emergency stop. Halts every external action by every agent in a workspace.
/// <para>
/// Engaging is intentionally easier than releasing: engaging needs one permission and a reason,
/// while releasing additionally requires step-up authentication. Under pressure the safe action
/// should be the fast one.
/// </para>
/// </summary>
public sealed record EngageKillSwitchCommand
    : ICommand<KillSwitchResult>, IRequirePermission, IScopedRequest, IAuditableRequest
{
    public required Guid WorkspaceId { get; init; }

    public required EnvironmentTier Environment { get; init; }

    public required string Reason { get; init; }

    public string RequiredPermission => Permissions.Workspace.EngageKillSwitch;

    public string AuditAction => AuditActions.KillSwitchEngaged;

    public string AuditResourceType => nameof(Workspace);

    public Guid? AuditResourceId => WorkspaceId;
}

public sealed record ReleaseKillSwitchCommand
    : ICommand<KillSwitchResult>, IRequirePermission, IScopedRequest, IAuditableRequest
{
    public required Guid WorkspaceId { get; init; }

    public required EnvironmentTier Environment { get; init; }

    /// <summary>Confirms the operator considered why the stop was engaged before lifting it.</summary>
    public required string Acknowledgement { get; init; }

    public string RequiredPermission => Permissions.Workspace.EngageKillSwitch;

    public string AuditAction => AuditActions.KillSwitchReleased;

    public string AuditResourceType => nameof(Workspace);

    public Guid? AuditResourceId => WorkspaceId;
}

public sealed record KillSwitchResult(Guid WorkspaceId, bool Engaged, DateTimeOffset At);

public sealed class EngageKillSwitchCommandValidator : AbstractValidator<EngageKillSwitchCommand>
{
    public EngageKillSwitchCommandValidator()
    {
        RuleFor(c => c.WorkspaceId).NotEmpty();

        RuleFor(c => c.Reason)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(2000)
            .WithMessage("Record why the emergency stop is being engaged, so the next operator can judge when it is safe to lift.");
    }
}

public sealed class ReleaseKillSwitchCommandValidator : AbstractValidator<ReleaseKillSwitchCommand>
{
    public ReleaseKillSwitchCommandValidator()
    {
        RuleFor(c => c.WorkspaceId).NotEmpty();
        RuleFor(c => c.Acknowledgement).NotEmpty().MinimumLength(10).MaximumLength(2000);
    }
}

public sealed class EngageKillSwitchCommandHandler(
    IWorkspaceRepository workspaces,
    ICurrentPrincipal principal,
    IClock clock)
    : IRequestHandler<EngageKillSwitchCommand, KillSwitchResult>
{
    public async Task<Result<KillSwitchResult>> HandleAsync(
        EngageKillSwitchCommand command,
        CancellationToken cancellationToken)
    {
        Workspace? workspace = await workspaces
            .FindAsync(WorkspaceId.From(command.WorkspaceId), cancellationToken).ConfigureAwait(false);

        if (workspace is null)
        {
            return Result.Failure<KillSwitchResult>(Error.NotFound("workspace.not_found", "No such workspace."));
        }

        Result engaged = workspace.EngageKillSwitch(command.Reason, principal.Principal.Id, clock.UtcNow);

        return engaged.IsFailure
            ? Result.Failure<KillSwitchResult>(engaged.Error)
            : Result.Success(new KillSwitchResult(workspace.Id.Value, true, clock.UtcNow));
    }
}

public sealed class ReleaseKillSwitchCommandHandler(
    IWorkspaceRepository workspaces,
    ICurrentPrincipal principal,
    IStepUpVerifier stepUpVerifier,
    IClock clock)
    : IRequestHandler<ReleaseKillSwitchCommand, KillSwitchResult>
{
    private static readonly TimeSpan StepUpMaxAge = TimeSpan.FromMinutes(15);

    public async Task<Result<KillSwitchResult>> HandleAsync(
        ReleaseKillSwitchCommand command,
        CancellationToken cancellationToken)
    {
        bool verified = principal.StepUpVerified
            || await stepUpVerifier.IsVerifiedAsync(principal.Principal, StepUpMaxAge, cancellationToken)
                .ConfigureAwait(false);

        if (!verified)
        {
            return Result.Failure<KillSwitchResult>(Error.Forbidden(
                "workspace.step_up_required",
                "Releasing the emergency stop requires step-up authentication."));
        }

        Workspace? workspace = await workspaces
            .FindAsync(WorkspaceId.From(command.WorkspaceId), cancellationToken).ConfigureAwait(false);

        if (workspace is null)
        {
            return Result.Failure<KillSwitchResult>(Error.NotFound("workspace.not_found", "No such workspace."));
        }

        Result released = workspace.ReleaseKillSwitch(principal.Principal.Id, clock.UtcNow);

        return released.IsFailure
            ? Result.Failure<KillSwitchResult>(released.Error)
            : Result.Success(new KillSwitchResult(workspace.Id.Value, false, clock.UtcNow));
    }
}
