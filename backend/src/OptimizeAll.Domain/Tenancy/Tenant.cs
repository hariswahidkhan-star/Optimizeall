using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Tenancy;

public enum TenantStatus
{
    Provisioning = 1,
    Active = 2,
    Suspended = 3,
    PendingDeletion = 4,
}

/// <summary>
/// A customer organisation, and the root of every isolation decision in the platform.
/// Nothing outside this aggregate may assume a tenant exists; nothing inside another tenant may
/// ever observe it.
/// </summary>
public sealed class Tenant : AggregateRoot<TenantId>, IAuditable, ISoftDeletable
{
    /// <summary>How long a tenant stays recoverable after deletion is requested.</summary>
    public static readonly TimeSpan DeletionGracePeriod = TimeSpan.FromDays(30);

    private Tenant(TenantId id, Slug slug, string displayName, string region, string planCode)
        : base(id)
    {
        Slug = slug;
        DisplayName = displayName;
        Region = region;
        PlanCode = planCode;
        Status = TenantStatus.Provisioning;
    }

    private Tenant()
    {
    }

    public Slug Slug { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    /// <summary>Data residency pin. Immutable after provisioning: moving data across regions is a migration, not an edit.</summary>
    public string Region { get; private set; } = null!;

    public string PlanCode { get; private set; } = null!;

    public TenantStatus Status { get; private set; }

    public string? SuspensionReason { get; private set; }

    /// <summary>When a soft-deleted tenant becomes eligible for irreversible purge.</summary>
    public DateTimeOffset? PurgeAfter { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static Tenant Provision(Slug slug, string displayName, string region, string planCode, DateTimeOffset now)
    {
        Ensure.NotNull(slug);
        Ensure.MaxLength(Ensure.NotNullOrWhiteSpace(displayName), 200);
        Ensure.NotNullOrWhiteSpace(region);
        Ensure.NotNullOrWhiteSpace(planCode);

        Tenant tenant = new(TenantId.New(), slug, displayName.Trim(), region.Trim(), planCode.Trim());
        tenant.Raise(new TenantProvisioned(tenant.Id, slug.Value, region, now));
        return tenant;
    }

    public Result Activate(DateTimeOffset now)
    {
        if (Status == TenantStatus.Active)
        {
            return Result.Success();
        }

        if (Status == TenantStatus.PendingDeletion)
        {
            return Result.Failure(Error.Conflict(
                "tenant.pending_deletion",
                "A tenant scheduled for deletion must be restored before it can be activated."));
        }

        Status = TenantStatus.Active;
        SuspensionReason = null;
        Raise(new TenantActivated(Id, now));
        return Result.Success();
    }

    public Result Suspend(string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation(
                "tenant.suspension_reason_required",
                "Suspending a tenant requires a recorded reason."));
        }

        if (Status == TenantStatus.PendingDeletion)
        {
            return Result.Failure(Error.Conflict(
                "tenant.pending_deletion",
                "A tenant scheduled for deletion cannot be suspended."));
        }

        Status = TenantStatus.Suspended;
        SuspensionReason = reason.Trim();
        Raise(new TenantSuspended(Id, SuspensionReason, now));
        return Result.Success();
    }

    /// <summary>
    /// Begins the two-phase deletion. The tenant becomes inaccessible immediately but remains
    /// recoverable until <see cref="PurgeAfter"/>, because an irreversible delete triggered by a
    /// mistaken click is the single worst outcome this platform can produce for a customer.
    /// </summary>
    public Result RequestDeletion(DateTimeOffset now)
    {
        if (Status == TenantStatus.PendingDeletion)
        {
            return Result.Success();
        }

        Status = TenantStatus.PendingDeletion;
        DeletedAt = now;
        PurgeAfter = now.Add(DeletionGracePeriod);
        Raise(new TenantDeletionRequested(Id, PurgeAfter.Value, now));
        return Result.Success();
    }

    public Result Restore(DateTimeOffset now)
    {
        if (Status != TenantStatus.PendingDeletion)
        {
            return Result.Failure(Error.Conflict(
                "tenant.not_pending_deletion",
                "Only a tenant scheduled for deletion can be restored."));
        }

        if (PurgeAfter is not null && now >= PurgeAfter.Value)
        {
            return Result.Failure(Error.Conflict(
                "tenant.grace_period_elapsed",
                "The recovery window for this tenant has elapsed."));
        }

        Status = TenantStatus.Suspended;
        DeletedAt = null;
        PurgeAfter = null;
        Raise(new TenantRestored(Id, now));
        return Result.Success();
    }

    public void Rename(string displayName)
        => DisplayName = Ensure.MaxLength(Ensure.NotNullOrWhiteSpace(displayName), 200).Trim();

    public void ChangePlan(string planCode)
        => PlanCode = Ensure.NotNullOrWhiteSpace(planCode).Trim();

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
