using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Governance;

/// <summary>
/// One clause of an approval policy: "actions of this risk class (optionally this tool, optionally
/// above this amount) need this many approvers holding this role, deciding within this window."
/// </summary>
public sealed class ApprovalRule : Entity<Guid>
{
    private ApprovalRule(
        Guid id,
        ApprovalPolicyId policyId,
        ActionRiskClass riskClass,
        string? toolKey,
        Money? thresholdAmount,
        string requiredRoleKey,
        int requiredApproverCount,
        TimeSpan expiry)
        : base(id)
    {
        PolicyId = policyId;
        RiskClass = riskClass;
        ToolKey = toolKey;
        ThresholdAmount = thresholdAmount;
        RequiredRoleKey = requiredRoleKey;
        RequiredApproverCount = requiredApproverCount;
        Expiry = expiry;
    }

    private ApprovalRule()
    {
    }

    public ApprovalPolicyId PolicyId { get; private set; }

    public ActionRiskClass RiskClass { get; private set; }

    /// <summary>Null applies the rule to every tool in the risk class.</summary>
    public string? ToolKey { get; private set; }

    /// <summary>Null means the rule applies regardless of amount. Otherwise it applies at or above this value.</summary>
    public Money? ThresholdAmount { get; private set; }

    public string RequiredRoleKey { get; private set; } = null!;

    public int RequiredApproverCount { get; private set; }

    public TimeSpan Expiry { get; private set; }

    public static ApprovalRule Create(
        ApprovalPolicyId policyId,
        ActionRiskClass riskClass,
        string requiredRoleKey,
        int requiredApproverCount = 1,
        string? toolKey = null,
        Money? thresholdAmount = null,
        TimeSpan? expiry = null)
    {
        Ensure.NotNullOrWhiteSpace(requiredRoleKey);
        Ensure.Positive(requiredApproverCount);

        if (toolKey is not null && !AgentCatalog.ToolRegistry.IsKnown(toolKey))
        {
            throw new ArgumentException($"'{toolKey}' is not a registered tool.", nameof(toolKey));
        }

        TimeSpan window = expiry ?? TimeSpan.FromHours(24);

        if (window <= TimeSpan.Zero || window > TimeSpan.FromDays(7))
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiry), window, "An approval window must be positive and no longer than seven days.");
        }

        return new ApprovalRule(
            Guid.CreateVersion7(),
            policyId,
            riskClass,
            toolKey,
            thresholdAmount,
            requiredRoleKey,
            requiredApproverCount,
            window);
    }

    public bool AppliesTo(ActionRiskClass riskClass, string toolKey, Money? amount)
    {
        if (RiskClass != riskClass)
        {
            return false;
        }

        if (ToolKey is not null && !string.Equals(ToolKey, toolKey, StringComparison.Ordinal))
        {
            return false;
        }

        if (ThresholdAmount is null)
        {
            return true;
        }

        // A rule with a monetary threshold cannot be satisfied by an action carrying no amount, and
        // a currency mismatch is treated as non-matching rather than being coerced: converting
        // currencies to evaluate a governance threshold would silently invent an exchange rate.
        return amount is not null
            && string.Equals(amount.Currency, ThresholdAmount.Currency, StringComparison.Ordinal)
            && amount.IsGreaterThanOrEqual(ThresholdAmount);
    }

    internal bool Matches(ActionRiskClass riskClass, string? toolKey, Money? thresholdAmount)
        => RiskClass == riskClass
            && string.Equals(ToolKey, toolKey, StringComparison.Ordinal)
            && Equals(ThresholdAmount, thresholdAmount);
}
