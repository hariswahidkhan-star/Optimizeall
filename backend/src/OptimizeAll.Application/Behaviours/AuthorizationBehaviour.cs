using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Behaviours;

/// <summary>
/// Enforces the permission a request declares, against the scope it declares.
/// <para>
/// This runs after tenant resolution — you cannot authorise against an unknown scope — and before
/// any transaction opens, so an unauthorised request never touches data. It is the single place
/// authorisation happens for commands and queries alike; a handler that forgets to check is
/// impossible, because handlers do not check at all.
/// </para>
/// </summary>
public sealed class AuthorizationBehaviour<TRequest, TResponse>(
    ICurrentPrincipal principal,
    ITenantContext tenantContext,
    IPermissionEvaluator permissionEvaluator,
    ILogger<AuthorizationBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehaviour<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IRequirePermission permissionRequirement)
        {
            // A request that declares no permission is unauthenticated-safe by construction
            // (health checks, token exchange). Anything else must declare one.
            return await next().ConfigureAwait(false);
        }

        if (!principal.IsAuthenticated)
        {
            return Result.Failure<TResponse>(Error.Unauthorized(
                "auth.unauthenticated",
                "Authentication is required for this operation."));
        }

        // The scope comes from the request when it names one, so that a caller authorised in one
        // workspace cannot use that authorisation to act in another.
        WorkspaceId workspaceId;
        EnvironmentTier environment;

        if (request is IScopedRequest scoped)
        {
            workspaceId = WorkspaceId.From(scoped.WorkspaceId);
            environment = scoped.Environment;
        }
        else if (tenantContext.WorkspaceId is { } ambientWorkspace)
        {
            workspaceId = ambientWorkspace;
            environment = tenantContext.Environment;
        }
        else
        {
            return Result.Failure<TResponse>(Error.Forbidden(
                "auth.scope_unresolved",
                "The operation requires a workspace scope, and none was supplied or resolved."));
        }

        Result authorised = await permissionEvaluator.AuthoriseAsync(
            principal.Principal,
            tenantContext.TenantId,
            workspaceId,
            environment,
            permissionRequirement.RequiredPermission,
            cancellationToken).ConfigureAwait(false);

        if (authorised.IsFailure)
        {
            // Logged at warning, not error: a denied request is the control working, not a fault.
            // The audit behaviour records it as a Denied outcome for the security stream.
            logger.LogWarning(
                "Authorization denied for {Principal} on {Request} requiring {Permission} in {Workspace}/{Environment}.",
                principal.Principal,
                typeof(TRequest).Name,
                permissionRequirement.RequiredPermission,
                workspaceId,
                environment);

            return Result.Failure<TResponse>(authorised.Error);
        }

        return await next().ConfigureAwait(false);
    }
}
