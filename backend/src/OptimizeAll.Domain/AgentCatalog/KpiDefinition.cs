using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.AgentCatalog;

public enum KpiDirection
{
    /// <summary>Higher is better, e.g. approval rate.</summary>
    Increase = 1,

    /// <summary>Lower is better, e.g. cost per run.</summary>
    Decrease = 2,
}

/// <summary>
/// What an agent is measured on. Declared alongside the agent so that "is this agent working?" has
/// a stated answer before the agent is deployed rather than after someone asks.
/// </summary>
public sealed class KpiDefinition : ValueObject
{
    private KpiDefinition(string key, string displayName, string unit, KpiDirection direction, decimal? target)
    {
        Key = key;
        DisplayName = displayName;
        Unit = unit;
        Direction = direction;
        Target = target;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string Unit { get; }

    public KpiDirection Direction { get; }

    /// <summary>Null means the KPI is tracked but has no committed target yet.</summary>
    public decimal? Target { get; }

    public static KpiDefinition Create(
        string key,
        string displayName,
        string unit,
        KpiDirection direction,
        decimal? target = null)
    {
        Ensure.NotNullOrWhiteSpace(key);
        Ensure.NotNullOrWhiteSpace(displayName);
        Ensure.NotNullOrWhiteSpace(unit);

        return new KpiDefinition(
            key.Trim().ToLowerInvariant(),
            displayName.Trim(),
            unit.Trim(),
            direction,
            target);
    }

    /// <summary>Whether an observed value meets the committed target, accounting for direction.</summary>
    public bool MeetsTarget(decimal value) => Target is null
        || (Direction == KpiDirection.Increase ? value >= Target.Value : value <= Target.Value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Key;
        yield return DisplayName;
        yield return Unit;
        yield return Direction;
        yield return Target;
    }
}
