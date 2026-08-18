using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Insights;

public enum KpiGranularity
{
    Hourly = 1,
    Daily = 2,
    Weekly = 3,
    Monthly = 4,
    Quarterly = 5,
}

/// <summary>
/// An immutable measurement of one KPI over one period.
/// <para>
/// Recomputation writes a new row rather than updating an old one, and dashboards read the most
/// recently computed row for a period. Overwriting history would make last quarter's board report
/// irreproducible the moment a metric definition changed.
/// </para>
/// </summary>
public sealed class KpiSnapshot : AggregateRoot<KpiSnapshotId>, ITenantOwned
{
    private KpiSnapshot(
        KpiSnapshotId id,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string kpiKey,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        KpiGranularity granularity,
        decimal value,
        string unit,
        string dimensionsJson,
        DateTimeOffset computedAt)
        : base(id)
    {
        TenantIdentifier = tenantId;
        WorkspaceId = workspaceId;
        Environment = environment;
        KpiKey = kpiKey;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Granularity = granularity;
        Value = value;
        Unit = unit;
        DimensionsJson = dimensionsJson;
        ComputedAt = computedAt;
    }

    private KpiSnapshot()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public WorkspaceId WorkspaceId { get; private set; }

    public EnvironmentTier Environment { get; private set; }

    public string KpiKey { get; private set; } = null!;

    public DateTimeOffset PeriodStart { get; private set; }

    public DateTimeOffset PeriodEnd { get; private set; }

    public KpiGranularity Granularity { get; private set; }

    public decimal Value { get; private set; }

    public string Unit { get; private set; } = null!;

    /// <summary>Slice this measurement applies to, e.g. <c>{"agentKey":"seo-agent"}</c>.</summary>
    public string DimensionsJson { get; private set; } = "{}";

    public DateTimeOffset ComputedAt { get; private set; }

    public static KpiSnapshot Record(
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string kpiKey,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        KpiGranularity granularity,
        decimal value,
        string unit,
        DateTimeOffset computedAt,
        string dimensionsJson = "{}")
    {
        Ensure.NotNullOrWhiteSpace(kpiKey);
        Ensure.NotNullOrWhiteSpace(unit);

        if (periodEnd <= periodStart)
        {
            throw new ArgumentException("A KPI period must end after it starts.", nameof(periodEnd));
        }

        return new KpiSnapshot(
            KpiSnapshotId.New(),
            tenantId,
            workspaceId,
            environment,
            kpiKey.Trim().ToLowerInvariant(),
            periodStart,
            periodEnd,
            granularity,
            value,
            unit.Trim(),
            string.IsNullOrWhiteSpace(dimensionsJson) ? "{}" : dimensionsJson,
            computedAt);
    }
}
