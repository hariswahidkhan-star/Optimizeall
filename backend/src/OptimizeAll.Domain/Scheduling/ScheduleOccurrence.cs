using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Scheduling;

public enum OccurrenceStatus
{
    Claimed = 1,
    Dispatched = 2,
    Failed = 3,
    Skipped = 4,
}

/// <summary>
/// A single firing of a schedule.
/// <para>
/// Exactly-once firing across an arbitrary number of scheduler replicas is achieved by one unique
/// constraint on <c>(schedule_id, occurrence_utc)</c>: every replica may try to insert, and exactly
/// one insert wins. No distributed lock, no leader election in the hot path, and no window in which
/// two replicas both believe they are the leader.
/// </para>
/// </summary>
public sealed class ScheduleOccurrence : AggregateRoot<ScheduleOccurrenceId>, ITenantOwned
{
    private ScheduleOccurrence(
        ScheduleOccurrenceId id,
        TenantId tenantId,
        ScheduleDefinitionId scheduleId,
        DateTimeOffset occurrenceUtc,
        DateTimeOffset claimedAt)
        : base(id)
    {
        TenantIdentifier = tenantId;
        ScheduleId = scheduleId;
        OccurrenceUtc = occurrenceUtc;
        ClaimedAt = claimedAt;
        Status = OccurrenceStatus.Claimed;
    }

    private ScheduleOccurrence()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public ScheduleDefinitionId ScheduleId { get; private set; }

    /// <summary>The logical instant this occurrence represents, not the instant it was processed.</summary>
    public DateTimeOffset OccurrenceUtc { get; private set; }

    public OccurrenceStatus Status { get; private set; }

    public DateTimeOffset ClaimedAt { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public Guid? DispatchedRunId { get; private set; }

    public string? FailureReason { get; private set; }

    public static ScheduleOccurrence Claim(
        TenantId tenantId,
        ScheduleDefinitionId scheduleId,
        DateTimeOffset occurrenceUtc,
        DateTimeOffset now)
        => new(ScheduleOccurrenceId.New(), tenantId, scheduleId, occurrenceUtc, now);

    public void MarkDispatched(Guid runId, DateTimeOffset now)
    {
        Status = OccurrenceStatus.Dispatched;
        DispatchedRunId = runId;
        DispatchedAt = now;
    }

    public void MarkFailed(string reason, DateTimeOffset now)
    {
        Status = OccurrenceStatus.Failed;
        FailureReason = reason;
        DispatchedAt = now;
    }

    public void MarkSkipped(string reason, DateTimeOffset now)
    {
        Status = OccurrenceStatus.Skipped;
        FailureReason = reason;
        DispatchedAt = now;
    }
}
