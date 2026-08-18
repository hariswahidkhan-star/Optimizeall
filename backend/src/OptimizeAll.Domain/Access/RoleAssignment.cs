using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Access;

/// <summary>
/// A grant of a role to a principal, scoped to a workspace and environment.
/// <para>
/// Scope is the whole point. A Production approver is not automatically a Development approver, and
/// an engineer with broad Development rights must not inherit them in Production. A null workspace
/// means tenant-wide; a null environment means all environments — both are deliberate, explicit
/// widenings rather than defaults.
/// </para>
/// </summary>
public sealed class RoleAssignment : AggregateRoot<RoleAssignmentId>, IAuditable, ITenantOwned
{
    private RoleAssignment(
        RoleAssignmentId id,
        TenantId tenantId,
        PrincipalRef principal,
        RoleId roleId,
        WorkspaceId? workspaceId,
        EnvironmentTier? environment,
        Guid grantedBy,
        DateTimeOffset? expiresAt)
        : base(id)
    {
        TenantIdentifier = tenantId;
        PrincipalType = principal.Type;
        PrincipalId = principal.Id;
        RoleId = roleId;
        WorkspaceId = workspaceId;
        Environment = environment;
        GrantedBy = grantedBy;
        ExpiresAt = expiresAt;
    }

    private RoleAssignment()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public PrincipalType PrincipalType { get; private set; }

    public Guid PrincipalId { get; private set; }

    public RoleId RoleId { get; private set; }

    /// <summary>Null means the grant applies across every workspace in the tenant.</summary>
    public WorkspaceId? WorkspaceId { get; private set; }

    /// <summary>Null means the grant applies in every environment.</summary>
    public EnvironmentTier? Environment { get; private set; }

    public Guid GrantedBy { get; private set; }

    /// <summary>Null means the grant does not expire. Time-boxing is encouraged for elevated roles.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Result<RoleAssignment> Grant(
        TenantId tenantId,
        PrincipalRef principal,
        RoleId roleId,
        WorkspaceId? workspaceId,
        EnvironmentTier? environment,
        Guid grantedBy,
        DateTimeOffset? expiresAt,
        DateTimeOffset now)
    {
        Ensure.NotEmpty(tenantId.Value);
        Ensure.NotEmpty(roleId.Value);

        if (expiresAt is not null && expiresAt.Value <= now)
        {
            return Result.Failure<RoleAssignment>(Error.Validation(
                "role_assignment.expiry_in_past",
                "A role grant cannot expire in the past."));
        }

        if (principal.Type == Common.PrincipalType.System)
        {
            return Result.Failure<RoleAssignment>(Error.Forbidden(
                "role_assignment.system_principal",
                "The system principal's authority is intrinsic and cannot be granted or revoked."));
        }

        RoleAssignment assignment = new(
            RoleAssignmentId.New(),
            tenantId,
            principal,
            roleId,
            workspaceId,
            environment,
            grantedBy,
            expiresAt);

        assignment.Raise(new RoleGranted(assignment.Id, tenantId, principal, roleId, workspaceId, environment, now));
        return Result.Success(assignment);
    }

    public bool IsActiveAt(DateTimeOffset instant) => ExpiresAt is null || instant < ExpiresAt.Value;

    /// <summary>
    /// Whether this grant applies to a request in the given scope. A null scope column on the grant
    /// widens it; a null scope on the *request* never widens anything, because the caller must always
    /// know which workspace and environment it is acting in.
    /// </summary>
    public bool AppliesTo(WorkspaceId workspaceId, EnvironmentTier environment, DateTimeOffset instant)
    {
        if (!IsActiveAt(instant))
        {
            return false;
        }

        bool workspaceMatches = WorkspaceId is null || WorkspaceId.Value == workspaceId;
        bool environmentMatches = Environment is null || Environment.Value == environment;

        return workspaceMatches && environmentMatches;
    }

    public void StampCreated(DateTimeOffset at, Guid? by)
    {
        CreatedAt = at;
        CreatedBy = by;
    }

    public void StampUpdated(DateTimeOffset at, Guid? by)
    {
        UpdatedAt = at;
        UpdatedBy = by;
    }
}

public sealed record RoleGranted(
    RoleAssignmentId AssignmentId,
    TenantId TenantId,
    PrincipalRef Principal,
    RoleId RoleId,
    WorkspaceId? WorkspaceId,
    EnvironmentTier? Environment,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "access.role_granted";
}
