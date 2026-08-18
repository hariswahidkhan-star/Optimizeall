using OptimizeAll.Domain.Common;

namespace OptimizeAll.Application.Abstractions.Security;

/// <summary>
/// The scope the current operation runs in, resolved once per request from the validated token and
/// immutable thereafter.
/// <para>
/// Nothing downstream may widen this. Every tenant-scoped query and every secret lookup keys off it,
/// so a handler that could reassign it could read another tenant's data with no other change.
/// </para>
/// </summary>
public interface ITenantContext
{
    TenantId TenantId { get; }

    WorkspaceId? WorkspaceId { get; }

    EnvironmentTier Environment { get; }

    /// <summary>
    /// True only inside an audited, time-boxed platform-operator escalation. When set, row-level
    /// security is relaxed by a distinct database role — never by a flag on the normal path.
    /// </summary>
    bool IsPlatformEscalation { get; }

    string CorrelationId { get; }
}

/// <summary>Who is acting. Distinguishes humans from agents, which several domain rules depend on.</summary>
public interface ICurrentPrincipal
{
    PrincipalRef Principal { get; }

    bool IsAuthenticated { get; }

    /// <summary>Effective permissions in the current scope, already flattened across role assignments.</summary>
    IReadOnlySet<string> Permissions { get; }

    /// <summary>True when the caller re-authenticated recently enough for a privileged operation.</summary>
    bool StepUpVerified { get; }

    string? IpAddress { get; }
}
