using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.AgentCatalog;

/// <summary>
/// The hard ceilings on a single agent run.
/// <para>
/// Every limit here is checked <em>before</em> the step that would breach it, never after. An agent
/// that discovers it has overspent has already spent the money; the only useful limit is one that
/// prevents the call.
/// </para>
/// </summary>
public sealed class BudgetPolicy : ValueObject
{
    private BudgetPolicy(
        long maxTotalTokens,
        Money maxCost,
        int maxToolCalls,
        int maxIterations,
        TimeSpan maxWallClock)
    {
        MaxTotalTokens = maxTotalTokens;
        MaxCost = maxCost;
        MaxToolCalls = maxToolCalls;
        MaxIterations = maxIterations;
        MaxWallClock = maxWallClock;
    }

    public long MaxTotalTokens { get; }

    public Money MaxCost { get; }

    public int MaxToolCalls { get; }

    /// <summary>Reasoning-loop depth. The primary guard against an agent that never converges.</summary>
    public int MaxIterations { get; }

    public TimeSpan MaxWallClock { get; }

    public static BudgetPolicy Create(
        long maxTotalTokens,
        Money maxCost,
        int maxToolCalls,
        int maxIterations,
        TimeSpan maxWallClock)
    {
        if (maxTotalTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTotalTokens), maxTotalTokens, "Token budget must be positive.");
        }

        Ensure.NotNull(maxCost);
        Ensure.NotNegative(maxCost.Amount);
        Ensure.Positive(maxToolCalls);
        Ensure.Positive(maxIterations);

        if (maxWallClock <= TimeSpan.Zero || maxWallClock > TimeSpan.FromHours(6))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxWallClock),
                maxWallClock,
                "Wall-clock budget must be positive and no greater than six hours.");
        }

        return new BudgetPolicy(maxTotalTokens, maxCost, maxToolCalls, maxIterations, maxWallClock);
    }

    /// <summary>Conservative defaults, applied when an agent definition does not state its own.</summary>
    public static BudgetPolicy Default(string currency = "USD") => Create(
        maxTotalTokens: 200_000,
        maxCost: Money.Of(5.00m, currency),
        maxToolCalls: 50,
        maxIterations: 25,
        maxWallClock: TimeSpan.FromMinutes(30));

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MaxTotalTokens;
        yield return MaxCost;
        yield return MaxToolCalls;
        yield return MaxIterations;
        yield return MaxWallClock;
    }
}
