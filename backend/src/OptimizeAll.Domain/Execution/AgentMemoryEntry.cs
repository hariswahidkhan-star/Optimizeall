using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Execution;

/// <summary>How a prior run turned out. This is the signal an agent learns from.</summary>
public enum OutcomeSignal
{
    Approved = 1,
    Rejected = 2,
    Revised = 3,
    Succeeded = 4,
    Failed = 5,
}

/// <summary>
/// A durable memory an agent may recall on later runs.
/// <para>
/// Keyed on <see cref="AgentKey"/> rather than on a definition id, so that learning survives a
/// version bump. Keying on the version would make every prompt tweak amnesiac, which is the
/// opposite of the requirement that agents improve over time.
/// </para>
/// </summary>
public sealed class AgentMemoryEntry : AggregateRoot<MemoryEntryId>, IAuditable, ITenantOwned
{
    private AgentMemoryEntry(
        MemoryEntryId id,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string agentKey,
        MemoryTier tier,
        string content,
        float importance)
        : base(id)
    {
        TenantIdentifier = tenantId;
        WorkspaceId = workspaceId;
        Environment = environment;
        AgentKey = agentKey;
        Tier = tier;
        Content = content;
        Importance = importance;
    }

    private AgentMemoryEntry()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public WorkspaceId WorkspaceId { get; private set; }

    /// <summary>Memory never crosses environments: a Development experiment must not teach Production.</summary>
    public EnvironmentTier Environment { get; private set; }

    public string AgentKey { get; private set; } = null!;

    public MemoryTier Tier { get; private set; }

    public AgentRunId? SourceRunId { get; private set; }

    public string Content { get; private set; } = null!;

    /// <summary>Retrieval weight in [0,1]. Rejections score high: a mistake is worth remembering.</summary>
    public float Importance { get; private set; }

    public OutcomeSignal? Outcome { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static AgentMemoryEntry RecordEpisode(
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string agentKey,
        AgentRunId sourceRunId,
        string content,
        OutcomeSignal outcome,
        DateTimeOffset now,
        TimeSpan? retention = null)
    {
        Ensure.NotNullOrWhiteSpace(agentKey);
        Ensure.NotNullOrWhiteSpace(content);

        AgentMemoryEntry entry = new(
            MemoryEntryId.New(),
            tenantId,
            workspaceId,
            environment,
            agentKey.Trim().ToLowerInvariant(),
            MemoryTier.Episodic,
            content.Trim(),
            ImportanceOf(outcome))
        {
            SourceRunId = sourceRunId,
            Outcome = outcome,
            ExpiresAt = retention is null ? null : now.Add(retention.Value),
        };

        return entry;
    }

    public void Reweight(float importance)
        => Importance = Math.Clamp(importance, 0f, 1f);

    public bool IsLiveAt(DateTimeOffset instant) => ExpiresAt is null || instant < ExpiresAt.Value;

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
    /// Corrective outcomes are weighted above confirmatory ones. An agent that only remembers its
    /// successes learns nothing; the rejections are where the information is.
    /// </summary>
    private static float ImportanceOf(OutcomeSignal outcome) => outcome switch
    {
        OutcomeSignal.Rejected => 0.95f,
        OutcomeSignal.Failed => 0.85f,
        OutcomeSignal.Revised => 0.75f,
        OutcomeSignal.Approved => 0.55f,
        OutcomeSignal.Succeeded => 0.50f,
        _ => 0.50f,
    };
}
