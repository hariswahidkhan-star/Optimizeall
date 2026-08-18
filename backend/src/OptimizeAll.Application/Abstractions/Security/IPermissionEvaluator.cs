using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Abstractions.Security;

/// <summary>
/// Answers "may this principal do this, here?" — always with an explicit scope, never against an
/// implied one.
/// </summary>
public interface IPermissionEvaluator
{
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        PrincipalRef principal,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        CancellationToken cancellationToken);

    Task<Result> AuthoriseAsync(
        PrincipalRef principal,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string requiredPermission,
        CancellationToken cancellationToken);
}

/// <summary>
/// Confirms the caller re-authenticated recently. Used for role changes, policy edits, key rotation
/// and high-risk approval decisions.
/// </summary>
public interface IStepUpVerifier
{
    Task<bool> IsVerifiedAsync(PrincipalRef principal, TimeSpan maxAge, CancellationToken cancellationToken);
}
