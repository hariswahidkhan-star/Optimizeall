using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Tenancy;

/// <summary>
/// A subdivision of a tenant — a brand, business unit, or region — and the unit at which budgets,
/// agent configuration, and the emergency stop apply.
/// </summary>
public sealed class Workspace : AggregateRoot<WorkspaceId>, IAuditable, ISoftDeletable, ITenantOwned
{
    private Workspace(WorkspaceId id, TenantId tenantId, Slug slug, string displayName, Money monthlyBudgetCap)
        : base(id)
    {
        TenantIdentifier = tenantId;
        Slug = slug;
        DisplayName = displayName;
        MonthlyBudgetCap = monthlyBudgetCap;
    }

    private Workspace()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public Slug Slug { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    /// <summary>
    /// Ceiling on AI provider spend per calendar month. Enforced before each provider call, so an
    /// agent cannot exceed it and then report the overage.
    /// </summary>
    public Money MonthlyBudgetCap { get; private set; } = null!;

    /// <summary>
    /// Emergency stop. When engaged, no agent in this workspace may perform any external effect.
    /// Deliberately a plain column on the aggregate: the check runs immediately before every
    /// external action, and a check expensive enough to be worth caching is not a kill switch.
    /// </summary>
    public bool KillSwitchEngaged { get; private set; }

    public string? KillSwitchReason { get; private set; }

    public DateTimeOffset? KillSwitchEngagedAt { get; private set; }

    public Guid? KillSwitchEngagedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static Workspace Create(
        TenantId tenantId,
        Slug slug,
        string displayName,
        Money monthlyBudgetCap,
        DateTimeOffset now)
    {
        Ensure.NotEmpty(tenantId.Value);
        Ensure.NotNull(slug);
        Ensure.MaxLength(Ensure.NotNullOrWhiteSpace(displayName), 200);
        Ensure.NotNull(monthlyBudgetCap);
        Ensure.NotNegative(monthlyBudgetCap.Amount);

        Workspace workspace = new(WorkspaceId.New(), tenantId, slug, displayName.Trim(), monthlyBudgetCap);
        workspace.Raise(new WorkspaceCreated(workspace.Id, tenantId, slug.Value, now));
        return workspace;
    }

    public Result ChangeBudgetCap(Money newCap)
    {
        Ensure.NotNull(newCap);

        if (newCap.Amount < 0)
        {
            return Result.Failure(Error.Validation(
                "workspace.negative_budget",
                "A budget cap cannot be negative."));
        }

        MonthlyBudgetCap = newCap;
        return Result.Success();
    }

    /// <summary>
    /// Halts all agent execution in this workspace. Requires a reason because an unexplained global
    /// stop is impossible to safely reverse — the next operator has no way to know whether the
    /// original cause is resolved.
    /// </summary>
    public Result EngageKillSwitch(string reason, Guid engagedBy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation(
                "workspace.kill_switch_reason_required",
                "Engaging the emergency stop requires a recorded reason."));
        }

        if (KillSwitchEngaged)
        {
            return Result.Success();
        }

        KillSwitchEngaged = true;
        KillSwitchReason = reason.Trim();
        KillSwitchEngagedAt = now;
        KillSwitchEngagedBy = engagedBy;
        Raise(new KillSwitchEngaged(Id, TenantIdentifier, KillSwitchReason, engagedBy, now));
        return Result.Success();
    }

    public Result ReleaseKillSwitch(Guid releasedBy, DateTimeOffset now)
    {
        if (!KillSwitchEngaged)
        {
            return Result.Success();
        }

        KillSwitchEngaged = false;
        KillSwitchReason = null;
        KillSwitchEngagedAt = null;
        KillSwitchEngagedBy = null;
        Raise(new KillSwitchReleased(Id, TenantIdentifier, releasedBy, now));
        return Result.Success();
    }

    public void Rename(string displayName)
        => DisplayName = Ensure.MaxLength(Ensure.NotNullOrWhiteSpace(displayName), 200).Trim();

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

    public void MarkDeleted(DateTimeOffset at) => DeletedAt = at;
}
