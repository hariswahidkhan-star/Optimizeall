using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Scheduling;

public enum ScheduleTargetType
{
    Agent = 1,
    Workflow = 2,
}

/// <summary>What to do about occurrences that were due while the scheduler was unavailable.</summary>
public enum MissPolicy
{
    /// <summary>Forget them. Correct for "the daily 06:00 report" — yesterday's is worthless today.</summary>
    Skip = 1,

    /// <summary>Run the most recent missed occurrence once. Correct for state-reconciling jobs.</summary>
    RunOnceOnRecovery = 2,

    /// <summary>Run every missed occurrence. Correct only when each occurrence has independent value.</summary>
    BackfillAll = 3,
}

/// <summary>
/// A recurring trigger expressed in the tenant's own timezone.
/// <para>
/// The timezone is stored as an IANA identifier rather than a fixed offset, because "09:00 London"
/// is a different UTC instant in January and July. Storing an offset would silently shift every
/// schedule by an hour twice a year.
/// </para>
/// </summary>
public sealed class ScheduleDefinition : AggregateRoot<ScheduleDefinitionId>, IAuditable, ISoftDeletable, ITenantOwned
{
    private ScheduleDefinition(
        ScheduleDefinitionId id,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string key,
        string cronExpression,
        string timeZoneId,
        ScheduleTargetType targetType,
        string targetKey,
        string payloadJson,
        MissPolicy missPolicy)
        : base(id)
    {
        TenantIdentifier = tenantId;
        WorkspaceId = workspaceId;
        Environment = environment;
        Key = key;
        CronExpression = cronExpression;
        TimeZoneId = timeZoneId;
        TargetType = targetType;
        TargetKey = targetKey;
        PayloadJson = payloadJson;
        MissPolicy = missPolicy;
        IsEnabled = true;
    }

    private ScheduleDefinition()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public WorkspaceId WorkspaceId { get; private set; }

    public EnvironmentTier Environment { get; private set; }

    public string Key { get; private set; } = null!;

    public string CronExpression { get; private set; } = null!;

    /// <summary>IANA timezone identifier, e.g. <c>Europe/London</c>.</summary>
    public string TimeZoneId { get; private set; } = null!;

    public ScheduleTargetType TargetType { get; private set; }

    public string TargetKey { get; private set; } = null!;

    public string PayloadJson { get; private set; } = "{}";

    public MissPolicy MissPolicy { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset? NextOccurrenceUtc { get; private set; }

    public DateTimeOffset? LastOccurrenceUtc { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>
    /// Creates a schedule. Cron syntax is validated by the caller through the scheduling port, since
    /// parsing belongs to the library that will also compute occurrences — validating it twice with
    /// two different parsers would be worse than validating it once with the right one.
    /// </summary>
    public static Result<ScheduleDefinition> Create(
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string key,
        string cronExpression,
        string timeZoneId,
        ScheduleTargetType targetType,
        string targetKey,
        string payloadJson = "{}",
        MissPolicy missPolicy = MissPolicy.Skip)
    {
        Ensure.NotNullOrWhiteSpace(key);
        Ensure.NotNullOrWhiteSpace(cronExpression);
        Ensure.NotNullOrWhiteSpace(timeZoneId);
        Ensure.NotNullOrWhiteSpace(targetKey);

        if (!Governance.CanonicalJson.IsValidJson(payloadJson))
        {
            return Result.Failure<ScheduleDefinition>(Error.Validation(
                "schedule.invalid_payload",
                "The schedule payload must be a valid JSON document."));
        }

        return Result.Success(new ScheduleDefinition(
            ScheduleDefinitionId.New(),
            tenantId,
            workspaceId,
            environment,
            key.Trim().ToLowerInvariant(),
            cronExpression.Trim(),
            timeZoneId.Trim(),
            targetType,
            targetKey.Trim().ToLowerInvariant(),
            payloadJson,
            missPolicy));
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public void RecordOccurrenceDispatched(DateTimeOffset occurrenceUtc, DateTimeOffset? nextOccurrenceUtc)
    {
        LastOccurrenceUtc = occurrenceUtc;
        NextOccurrenceUtc = nextOccurrenceUtc;
    }

    public void SetNextOccurrence(DateTimeOffset? nextOccurrenceUtc) => NextOccurrenceUtc = nextOccurrenceUtc;

    public bool IsDue(DateTimeOffset now)
        => IsEnabled && DeletedAt is null && NextOccurrenceUtc is not null && now >= NextOccurrenceUtc.Value;

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
