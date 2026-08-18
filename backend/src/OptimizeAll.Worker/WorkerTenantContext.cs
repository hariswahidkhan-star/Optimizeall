using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Common;

namespace OptimizeAll.Worker;

/// <summary>
/// The scope a background operation runs in.
/// <para>
/// Workers have no request to derive scope from, so each unit of work sets it explicitly from the
/// record it picked up — the run's own tenant, workspace and environment. Mutable by design, but
/// only through <see cref="SetScope"/>, and only between units of work: a worker processes one item
/// at a time inside its own dependency-injection scope, so two items can never share this instance.
/// </para>
/// </summary>
public sealed class WorkerTenantContext : ITenantContext
{
    public TenantId TenantId { get; private set; } = TenantId.From(Guid.Empty);

    public WorkspaceId? WorkspaceId { get; private set; }

    public EnvironmentTier Environment { get; private set; } = EnvironmentTier.Development;

    public bool IsPlatformEscalation { get; private set; }

    public string CorrelationId { get; private set; } = Guid.Empty.ToString();

    public void SetScope(
        TenantId tenantId,
        WorkspaceId? workspaceId,
        EnvironmentTier environment,
        Guid correlationId)
    {
        TenantId = tenantId;
        WorkspaceId = workspaceId;
        Environment = environment;
        CorrelationId = correlationId.ToString();
    }

    /// <summary>
    /// Enables cross-tenant reads for maintenance sweeps that must scan every tenant — the lease
    /// reclaimer and the approval-expiry sweep. Used with a database role holding BYPASSRLS, and
    /// never while a unit of work is acting on a specific tenant's data.
    /// </summary>
    public void EnablePlatformScope(Guid correlationId)
    {
        IsPlatformEscalation = true;
        CorrelationId = correlationId.ToString();
    }
}

/// <summary>The identity background work acts under.</summary>
public sealed class SystemPrincipal : ICurrentPrincipal
{
    public PrincipalRef Principal => PrincipalRef.System;

    public bool IsAuthenticated => true;

    /// <summary>
    /// Empty by design. The system principal performs no permission-gated operation: workers
    /// execute work that a human or an agent already caused, and each unit re-checks its own rules.
    /// Granting the worker a blanket permission set would make it the widest hole in the platform.
    /// </summary>
    public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(StringComparer.Ordinal);

    public bool StepUpVerified => false;

    public string? IpAddress => null;
}
