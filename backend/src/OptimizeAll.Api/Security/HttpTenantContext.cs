using System.Security.Claims;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Common;

namespace OptimizeAll.Api.Security;

/// <summary>
/// Claim types the platform reads from the access token. Named constants so a typo is a compile
/// error rather than a silently absent scope that fails open.
/// </summary>
public static class OptimizeAllClaims
{
    public const string TenantId = "oa_tenant";
    public const string WorkspaceId = "oa_workspace";
    public const string Environment = "oa_environment";
    public const string PrincipalType = "oa_principal_type";
    public const string AuthTime = "auth_time";
}

/// <summary>
/// Resolves the request's scope from the validated access token, once, at construction.
/// <para>
/// Reading the scope from headers or query parameters would let a caller name any tenant. It comes
/// from claims the identity provider signed, and it is immutable for the life of the request.
/// </para>
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    public HttpTenantContext(IHttpContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        HttpContext? http = accessor.HttpContext;
        ClaimsPrincipal? user = http?.User;

        TenantId = ReadGuidClaim(user, OptimizeAllClaims.TenantId) is { } tenant
            ? Domain.Common.TenantId.From(tenant)
            : Domain.Common.TenantId.From(Guid.Empty);

        WorkspaceId = ReadGuidClaim(user, OptimizeAllClaims.WorkspaceId) is { } workspace
            ? Domain.Common.WorkspaceId.From(workspace)
            : null;

        Environment = Enum.TryParse(
            user?.FindFirst(OptimizeAllClaims.Environment)?.Value,
            ignoreCase: true,
            out EnvironmentTier tier)
            ? tier
            // Defaults to the most restrictive tier. A token that fails to state its environment
            // must not be treated as production-capable.
            : EnvironmentTier.Development;

        CorrelationId = http?.TraceIdentifier ?? Guid.CreateVersion7().ToString();
    }

    public TenantId TenantId { get; }

    public WorkspaceId? WorkspaceId { get; }

    public EnvironmentTier Environment { get; }

    /// <summary>
    /// Always false on the HTTP surface. Cross-tenant access runs through a separate,
    /// platform-scoped code path with its own database role — there is no header that turns it on.
    /// </summary>
    public bool IsPlatformEscalation => false;

    public string CorrelationId { get; }

    private static Guid? ReadGuidClaim(ClaimsPrincipal? user, string claimType)
        => Guid.TryParse(user?.FindFirst(claimType)?.Value, out Guid value) ? value : null;
}

/// <summary>The authenticated actor, projected from the token.</summary>
public sealed class HttpCurrentPrincipal : ICurrentPrincipal
{
    /// <summary>How recently the identity provider must have authenticated the user for step-up.</summary>
    private static readonly TimeSpan StepUpWindow = TimeSpan.FromMinutes(15);

    public HttpCurrentPrincipal(IHttpContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        HttpContext? http = accessor.HttpContext;
        ClaimsPrincipal? user = http?.User;

        IsAuthenticated = user?.Identity?.IsAuthenticated ?? false;

        PrincipalType principalType = Enum.TryParse(
            user?.FindFirst(OptimizeAllClaims.PrincipalType)?.Value,
            ignoreCase: true,
            out PrincipalType parsed)
            ? parsed
            : PrincipalType.User;

        Guid subjectId = Guid.TryParse(
            user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user?.FindFirst("sub")?.Value,
            out Guid id)
            ? id
            : Guid.Empty;

        Principal = new PrincipalRef(principalType, subjectId);

        Permissions = user?.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        StepUpVerified = IsRecentlyAuthenticated(user);
        IpAddress = http?.Connection.RemoteIpAddress?.ToString();
    }

    public PrincipalRef Principal { get; }

    public bool IsAuthenticated { get; }

    public IReadOnlySet<string> Permissions { get; }

    public bool StepUpVerified { get; }

    public string? IpAddress { get; }

    /// <summary>
    /// Reads the OIDC <c>auth_time</c> claim rather than trusting a client-supplied flag. Step-up is
    /// an assertion by the identity provider that the human re-authenticated recently; a request
    /// cannot assert it about itself.
    /// </summary>
    private static bool IsRecentlyAuthenticated(ClaimsPrincipal? user)
    {
        string? authTime = user?.FindFirst(OptimizeAllClaims.AuthTime)?.Value;

        if (!long.TryParse(authTime, out long unixSeconds))
        {
            return false;
        }

        return DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unixSeconds) <= StepUpWindow;
    }
}
