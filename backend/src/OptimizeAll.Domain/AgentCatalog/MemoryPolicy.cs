using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.AgentCatalog;

public enum MemoryTier
{
    /// <summary>Lives only for the duration of one run.</summary>
    Short = 1,

    /// <summary>Outcomes of this agent's prior runs, including reviewer feedback.</summary>
    Episodic = 2,

    /// <summary>The tenant knowledge base, retrieved semantically.</summary>
    Semantic = 3,
}

/// <summary>
/// What an agent is allowed to remember, and for how long.
/// <para>
/// Some agents must not accumulate memory at all. A publishing agent that "learns" would introduce
/// variability into an operation whose entire value is that it does exactly what was approved.
/// </para>
/// </summary>
public sealed class MemoryPolicy : ValueObject
{
    private MemoryPolicy(
        bool episodicEnabled,
        bool semanticEnabled,
        int episodicRecallLimit,
        int semanticRecallLimit,
        TimeSpan? episodicRetention,
        bool redactPersonalData)
    {
        EpisodicEnabled = episodicEnabled;
        SemanticEnabled = semanticEnabled;
        EpisodicRecallLimit = episodicRecallLimit;
        SemanticRecallLimit = semanticRecallLimit;
        EpisodicRetention = episodicRetention;
        RedactPersonalData = redactPersonalData;
    }

    public bool EpisodicEnabled { get; }

    public bool SemanticEnabled { get; }

    /// <summary>Maximum episodic entries injected into a prompt.</summary>
    public int EpisodicRecallLimit { get; }

    /// <summary>Maximum knowledge chunks injected into a prompt.</summary>
    public int SemanticRecallLimit { get; }

    /// <summary>Null means retain until the tenant's global policy expires it.</summary>
    public TimeSpan? EpisodicRetention { get; }

    /// <summary>
    /// Strips detected personal data before anything is written to durable memory. Mandatory for
    /// agents handling candidate or customer records, where memory would otherwise become an
    /// unmanaged copy of personal data outside the erasure path.
    /// </summary>
    public bool RedactPersonalData { get; }

    public static MemoryPolicy Create(
        bool episodicEnabled = true,
        bool semanticEnabled = true,
        int episodicRecallLimit = 10,
        int semanticRecallLimit = 8,
        TimeSpan? episodicRetention = null,
        bool redactPersonalData = true)
    {
        if (episodicRecallLimit is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(episodicRecallLimit), episodicRecallLimit, "Episodic recall limit must be between 0 and 100.");
        }

        if (semanticRecallLimit is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(semanticRecallLimit), semanticRecallLimit, "Semantic recall limit must be between 0 and 100.");
        }

        return new MemoryPolicy(
            episodicEnabled,
            semanticEnabled,
            episodicRecallLimit,
            semanticRecallLimit,
            episodicRetention,
            redactPersonalData);
    }

    /// <summary>For agents that must behave identically on every run.</summary>
    public static MemoryPolicy Stateless() => new(
        episodicEnabled: false,
        semanticEnabled: false,
        episodicRecallLimit: 0,
        semanticRecallLimit: 0,
        episodicRetention: TimeSpan.Zero,
        redactPersonalData: true);

    public bool IsEnabled(MemoryTier tier) => tier switch
    {
        MemoryTier.Short => true,
        MemoryTier.Episodic => EpisodicEnabled,
        MemoryTier.Semantic => SemanticEnabled,
        _ => false,
    };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EpisodicEnabled;
        yield return SemanticEnabled;
        yield return EpisodicRecallLimit;
        yield return SemanticRecallLimit;
        yield return EpisodicRetention;
        yield return RedactPersonalData;
    }
}
