using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Access;

/// <summary>
/// A named bundle of permissions. Roles carry no hierarchy: "what can this role do" is answered by
/// reading one list, not by walking an inheritance tree whose effective grants nobody can predict.
/// </summary>
public sealed class Role : AggregateRoot<RoleId>, IAuditable, ITenantOwned
{
    private readonly HashSet<string> _permissions = new(StringComparer.Ordinal);

    private Role(RoleId id, TenantId tenantId, string key, string displayName, string description, bool isBuiltIn)
        : base(id)
    {
        TenantIdentifier = tenantId;
        Key = key;
        DisplayName = displayName;
        Description = description;
        IsBuiltIn = isBuiltIn;
    }

    private Role()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public string Key { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    /// <summary>Platform-maintained roles cannot be edited or deleted by tenants.</summary>
    public bool IsBuiltIn { get; private set; }

    public IReadOnlySet<string> Permissions => _permissions;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Role CreateBuiltIn(TenantId tenantId, string key, IEnumerable<string> permissions)
    {
        Role role = new(RoleId.New(), tenantId, key, key, $"Platform-managed role '{key}'.", isBuiltIn: true);

        foreach (string permission in permissions)
        {
            role._permissions.Add(permission);
        }

        return role;
    }

    /// <summary>
    /// Creates a tenant-defined role. The granter's own permission set is required so the platform
    /// can refuse privilege escalation: an administrator must not be able to mint a role holding
    /// permissions they do not themselves hold and then assign it to themselves.
    /// </summary>
    public static Result<Role> CreateCustom(
        TenantId tenantId,
        string key,
        string displayName,
        string description,
        IReadOnlyCollection<string> permissions,
        IReadOnlySet<string> granterPermissions)
    {
        Ensure.NotNullOrWhiteSpace(key);
        Ensure.NotNullOrWhiteSpace(displayName);
        Ensure.NotNull(permissions);
        Ensure.NotNull(granterPermissions);

        string normalisedKey = key.Trim().ToLowerInvariant();

        if (BuiltInRoles.Definitions.ContainsKey(normalisedKey))
        {
            return Result.Failure<Role>(Error.Conflict(
                "role.reserved_key",
                $"'{normalisedKey}' is a built-in role key and cannot be redefined."));
        }

        string[] unknown = [.. permissions.Where(p => !Access.Permissions.All.Contains(p))];

        if (unknown.Length > 0)
        {
            return Result.Failure<Role>(Error.Validation(
                "role.unknown_permission",
                $"Unknown permissions: {string.Join(", ", unknown)}."));
        }

        string[] escalating = [.. permissions.Where(p => !granterPermissions.Contains(p))];

        if (escalating.Length > 0)
        {
            return Result.Failure<Role>(Error.Forbidden(
                "role.privilege_escalation",
                $"Cannot grant permissions the granting principal does not hold: {string.Join(", ", escalating)}."));
        }

        Role role = new(
            RoleId.New(),
            tenantId,
            normalisedKey,
            displayName.Trim(),
            description?.Trim() ?? string.Empty,
            isBuiltIn: false);

        foreach (string permission in permissions)
        {
            role._permissions.Add(permission);
        }

        return Result.Success(role);
    }

    public Result UpdatePermissions(IReadOnlyCollection<string> permissions, IReadOnlySet<string> granterPermissions)
    {
        if (IsBuiltIn)
        {
            return Result.Failure(Error.Forbidden(
                "role.builtin_immutable",
                "Built-in roles cannot be modified."));
        }

        string[] unknown = [.. permissions.Where(p => !Access.Permissions.All.Contains(p))];

        if (unknown.Length > 0)
        {
            return Result.Failure(Error.Validation(
                "role.unknown_permission",
                $"Unknown permissions: {string.Join(", ", unknown)}."));
        }

        string[] escalating = [.. permissions.Where(p => !granterPermissions.Contains(p))];

        if (escalating.Length > 0)
        {
            return Result.Failure(Error.Forbidden(
                "role.privilege_escalation",
                $"Cannot grant permissions the granting principal does not hold: {string.Join(", ", escalating)}."));
        }

        _permissions.Clear();

        foreach (string permission in permissions)
        {
            _permissions.Add(permission);
        }

        return Result.Success();
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
