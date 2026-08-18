using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Governance;

/// <summary>
/// The rules deciding whether a proposed action needs human sign-off, from whom, and how many.
/// <para>
/// A policy can only make gating <em>stricter</em> than the platform floor defined by
/// <see cref="ActionRiskClassExtensions.RequiresHumanApproval"/>. There is no configuration path
/// that removes a gate, because a governance control a customer can switch off is not a control.
/// </para>
/// </summary>
public sealed class ApprovalPolicy : AggregateRoot<ApprovalPolicyId>, IAuditable, ITenantOwned
{
    private readonly List<ApprovalRule> _rules = [];

    private ApprovalPolicy(ApprovalPolicyId id, TenantId tenantId, WorkspaceId workspaceId, string key, string displayName)
        : base(id)
    {
        TenantIdentifier = tenantId;
        WorkspaceId = workspaceId;
        Key = key;
        DisplayName = displayName;
        IsActive = true;
    }

    private ApprovalPolicy()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public WorkspaceId WorkspaceId { get; private set; }

    public string Key { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public IReadOnlyList<ApprovalRule> Rules => _rules.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static ApprovalPolicy Create(TenantId tenantId, WorkspaceId workspaceId, string key, string displayName)
    {
        Ensure.NotNullOrWhiteSpace(key);
        Ensure.NotNullOrWhiteSpace(displayName);

        return new ApprovalPolicy(
            ApprovalPolicyId.New(),
            tenantId,
            workspaceId,
            key.Trim().ToLowerInvariant(),
            displayName.Trim());
    }

    public Result AddRule(ApprovalRule rule)
    {
        Ensure.NotNull(rule);

        if (_rules.Any(r => r.Matches(rule.RiskClass, rule.ToolKey, rule.ThresholdAmount)))
        {
            return Result.Failure(Error.Conflict(
                "approval_policy.duplicate_rule",
                "An equivalent rule already exists in this policy."));
        }

        _rules.Add(rule);
        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Resolves the requirement for a proposed action.
    /// <para>
    /// The platform floor is applied first and unconditionally. Matching rules may then raise the
    /// approver count or narrow the required role; they can never lower either. When no rule
    /// matches, the floor stands on its own — an unconfigured policy is strict, not permissive.
    /// </para>
    /// </summary>
    public ApprovalRequirement Resolve(ActionRiskClass riskClass, string toolKey, Money? amount)
    {
        bool floorRequiresApproval = riskClass.RequiresHumanApproval();
        int approverCount = riskClass.MinimumApproverCount();
        string? requiredRole = floorRequiresApproval ? Access.BuiltInRoles.Approver : null;
        TimeSpan expiry = DefaultExpiryFor(riskClass);

        foreach (ApprovalRule rule in _rules.Where(r => r.AppliesTo(riskClass, toolKey, amount)))
        {
            floorRequiresApproval = true;
            approverCount = Math.Max(approverCount, rule.RequiredApproverCount);
            requiredRole = rule.RequiredRoleKey;
            expiry = rule.Expiry < expiry ? rule.Expiry : expiry;
        }

        if (!floorRequiresApproval)
        {
            return ApprovalRequirement.None;
        }

        return ApprovalRequirement.Required(Math.Max(1, approverCount), requiredRole ?? Access.BuiltInRoles.Approver, expiry);
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

    /// <summary>
    /// Shorter windows for higher risk. A pending irreversible action should not sit approvable for
    /// a week: the context that justified it goes stale, and a stale approval is a rubber stamp.
    /// </summary>
    private static TimeSpan DefaultExpiryFor(ActionRiskClass riskClass) => riskClass switch
    {
        ActionRiskClass.Irreversible => TimeSpan.FromHours(4),
        ActionRiskClass.Financial => TimeSpan.FromHours(24),
        ActionRiskClass.External => TimeSpan.FromHours(48),
        _ => TimeSpan.FromHours(72),
    };
}

/// <summary>The resolved outcome of evaluating a policy against one proposed action.</summary>
public sealed record ApprovalRequirement
{
    private ApprovalRequirement(bool isRequired, int approverCount, string? requiredRoleKey, TimeSpan expiry)
    {
        IsRequired = isRequired;
        ApproverCount = approverCount;
        RequiredRoleKey = requiredRoleKey;
        Expiry = expiry;
    }

    public bool IsRequired { get; }

    public int ApproverCount { get; }

    public string? RequiredRoleKey { get; }

    public TimeSpan Expiry { get; }

    public static ApprovalRequirement None { get; } = new(false, 0, null, TimeSpan.Zero);

    public static ApprovalRequirement Required(int approverCount, string requiredRoleKey, TimeSpan expiry)
        => new(true, approverCount, requiredRoleKey, expiry);
}
