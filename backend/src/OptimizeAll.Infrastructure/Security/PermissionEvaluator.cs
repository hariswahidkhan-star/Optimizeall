using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.Common;
using OptimizeAll.Infrastructure.Persistence;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Infrastructure.Security;

/// <summary>
/// Resolves a principal's effective permissions in a specific scope.
/// <para>
/// The scope is always explicit. Resolving "what can this user do" without naming a workspace and
/// environment would produce a union across scopes, and a union is exactly the wrong answer: it
/// would let a Development grant authorise a Production action.
/// </para>
/// </summary>
public sealed class PermissionEvaluator(
    OptimizeAllDbContext context,
    IMemoryCache cache,
    IClock clock)
    : IPermissionEvaluator
{
    /// <summary>
    /// Short enough that a revoked role stops working promptly, long enough to keep the
    /// authorisation check off the database on every request. Revocation additionally evicts the
    /// entry directly, so this is a backstop rather than the mechanism.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        PrincipalRef principal,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        CancellationToken cancellationToken)
    {
        string cacheKey = BuildCacheKey(principal, tenantId, workspaceId, environment);

        if (cache.TryGetValue(cacheKey, out IReadOnlySet<string>? cached) && cached is not null)
        {
            return cached;
        }

        DateTimeOffset now = clock.UtcNow;

        // One join, one index seek. Assignments are filtered in the database by tenant and
        // principal; scope applicability is evaluated in memory because it involves nullable
        // widening semantics that read far more clearly as domain logic than as SQL.
        var assignments = await context.RoleAssignments
            .Where(a => a.TenantIdentifier == tenantId
                && a.PrincipalId == principal.Id
                && a.PrincipalType == principal.Type)
            .Join(
                context.Roles,
                assignment => assignment.RoleId,
                role => role.Id,
                (assignment, role) => new { Assignment = assignment, Role = role })
            .AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        HashSet<string> permissions = new(StringComparer.Ordinal);

        foreach (var pair in assignments)
        {
            if (!pair.Assignment.AppliesTo(workspaceId, environment, now))
            {
                continue;
            }

            foreach (string permission in pair.Role.Permissions)
            {
                permissions.Add(permission);
            }
        }

        cache.Set(cacheKey, (IReadOnlySet<string>)permissions, CacheDuration);
        return permissions;
    }

    public async Task<Result> AuthoriseAsync(
        PrincipalRef principal,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string requiredPermission,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredPermission);

        IReadOnlySet<string> held = await GetEffectivePermissionsAsync(
            principal, tenantId, workspaceId, environment, cancellationToken).ConfigureAwait(false);

        // The exact-match case is by far the most common, so it is checked first and the wildcard
        // interpretation is only reached when it fails.
        if (held.Contains(requiredPermission))
        {
            return Result.Success();
        }

        Permission required = Permission.Parse(requiredPermission);

        foreach (string candidate in held)
        {
            if (Permission.TryParse(candidate, out Permission? granted)
                && granted is not null
                && granted.Satisfies(required))
            {
                return Result.Success();
            }
        }

        return Result.Failure(Error.Forbidden(
            "auth.permission_denied",
            $"This operation requires '{requiredPermission}' in {environment}."));
    }

    private static string BuildCacheKey(
        PrincipalRef principal,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment)
        => $"perm:{tenantId}:{principal.Type}:{principal.Id}:{workspaceId}:{environment}";
}
