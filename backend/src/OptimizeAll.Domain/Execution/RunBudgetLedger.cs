using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Execution;

/// <summary>
/// Running consumption for a single agent run, checked against the definition's
/// <see cref="BudgetPolicy"/> <em>before</em> each step rather than after.
/// <para>
/// The distinction is the whole point: a limit checked after the fact reports an overspend, while a
/// limit checked before the call prevents one. Every method here answers "may I do the next thing?",
/// never "did I go too far?".
/// </para>
/// </summary>
public sealed class RunBudgetLedger : ValueObject
{
    private RunBudgetLedger(
        long promptTokens,
        long completionTokens,
        decimal costAmount,
        string costCurrency,
        int toolCallCount,
        int iterationCount)
    {
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        CostAmount = costAmount;
        CostCurrency = costCurrency;
        ToolCallCount = toolCallCount;
        IterationCount = iterationCount;
    }

    public long PromptTokens { get; }

    public long CompletionTokens { get; }

    public long TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// Cost is held as a scalar amount and currency rather than as a nested <see cref="Money"/>
    /// so that month-to-date spend across thousands of runs is a plain SQL <c>SUM</c> over one
    /// column, and budget enforcement never needs to materialise the runs to add them up.
    /// </summary>
    public decimal CostAmount { get; }

    public string CostCurrency { get; } = "USD";

    public Money CostIncurred => Money.Of(CostAmount, CostCurrency);

    public int ToolCallCount { get; }

    public int IterationCount { get; }

    public static RunBudgetLedger Empty(string currency) => new(0, 0, 0m, Money.Zero(currency).Currency, 0, 0);

    public RunBudgetLedger RecordCompletion(long promptTokens, long completionTokens, Money cost)
    {
        ArgumentNullException.ThrowIfNull(cost);

        // Adding through Money rather than adding the raw decimals keeps the currency-mismatch
        // guard in force: a provider misconfigured to bill in a different currency fails loudly
        // here instead of silently inflating the ledger.
        Money combined = CostIncurred.Add(cost);

        return new RunBudgetLedger(
            PromptTokens + promptTokens,
            CompletionTokens + completionTokens,
            combined.Amount,
            combined.Currency,
            ToolCallCount,
            IterationCount + 1);
    }

    public RunBudgetLedger RecordToolCall() => new(
        PromptTokens, CompletionTokens, CostAmount, CostCurrency, ToolCallCount + 1, IterationCount);

    /// <summary>
    /// Whether another reasoning iteration may begin.
    /// <para>
    /// <paramref name="projectedCost"/> is the estimated cost of the call about to be made, so a
    /// call that would breach the ceiling is refused rather than made and then regretted.
    /// </para>
    /// </summary>
    public Result CanStartIteration(BudgetPolicy policy, Money projectedCost, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(projectedCost);

        if (IterationCount >= policy.MaxIterations)
        {
            return Result.Failure(Error.Exhausted(
                "run.iteration_limit",
                $"The run reached its iteration limit of {policy.MaxIterations}."));
        }

        if (TotalTokens >= policy.MaxTotalTokens)
        {
            return Result.Failure(Error.Exhausted(
                "run.token_limit",
                $"The run reached its token budget of {policy.MaxTotalTokens:N0}."));
        }

        if (elapsed >= policy.MaxWallClock)
        {
            return Result.Failure(Error.Exhausted(
                "run.wall_clock_limit",
                $"The run exceeded its wall-clock budget of {policy.MaxWallClock}."));
        }

        if (CostIncurred.Add(projectedCost).IsGreaterThan(policy.MaxCost))
        {
            return Result.Failure(Error.Exhausted(
                "run.cost_limit",
                $"The next call would take the run past its cost ceiling of {policy.MaxCost}."));
        }

        return Result.Success();
    }

    public Result CanCallTool(BudgetPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return ToolCallCount >= policy.MaxToolCalls
            ? Result.Failure(Error.Exhausted(
                "run.tool_call_limit",
                $"The run reached its tool-call limit of {policy.MaxToolCalls}."))
            : Result.Success();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PromptTokens;
        yield return CompletionTokens;
        yield return CostAmount;
        yield return CostCurrency;
        yield return ToolCallCount;
        yield return IterationCount;
    }
}
