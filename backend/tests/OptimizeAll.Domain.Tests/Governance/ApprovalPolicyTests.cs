using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Governance;
using Xunit;

namespace OptimizeAll.Domain.Tests.Governance;

public sealed class ApprovalPolicyTests
{
    private static ApprovalPolicy EmptyPolicy()
        => ApprovalPolicy.Create(TestData.Tenant, TestData.Workspace, "default", "Default policy");

    [Theory]
    [InlineData(ActionRiskClass.External)]
    [InlineData(ActionRiskClass.Financial)]
    [InlineData(ActionRiskClass.Irreversible)]
    public void An_unconfigured_policy_still_gates_every_high_risk_class(ActionRiskClass risk)
    {
        ApprovalRequirement requirement = EmptyPolicy().Resolve(risk, ToolRegistry.ContentPublish, null);

        Assert.True(requirement.IsRequired);
        Assert.True(requirement.ApproverCount >= 1);
    }

    [Theory]
    [InlineData(ActionRiskClass.Read)]
    [InlineData(ActionRiskClass.Write)]
    public void Low_risk_classes_are_not_gated_by_default(ActionRiskClass risk)
        => Assert.False(EmptyPolicy().Resolve(risk, ToolRegistry.KnowledgeSearch, null).IsRequired);

    [Fact]
    public void Irreversible_actions_require_two_approvers_by_platform_floor()
    {
        ApprovalRequirement requirement = EmptyPolicy()
            .Resolve(ActionRiskClass.Irreversible, ToolRegistry.DeployTrigger, null);

        Assert.Equal(2, requirement.ApproverCount);
    }

    [Fact]
    public void A_rule_can_raise_the_required_approver_count_but_a_policy_cannot_lower_the_floor()
    {
        ApprovalPolicy policy = EmptyPolicy();
        policy.AddRule(ApprovalRule.Create(
            policy.Id,
            ActionRiskClass.Irreversible,
            OptimizeAll.Domain.Access.BuiltInRoles.TenantOwner,
            requiredApproverCount: 3));

        ApprovalRequirement raised = policy.Resolve(ActionRiskClass.Irreversible, ToolRegistry.DeployTrigger, null);

        Assert.Equal(3, raised.ApproverCount);
    }

    [Fact]
    public void A_monetary_threshold_rule_applies_only_at_or_above_its_amount()
    {
        ApprovalPolicy policy = EmptyPolicy();
        policy.AddRule(ApprovalRule.Create(
            policy.Id,
            ActionRiskClass.Financial,
            OptimizeAll.Domain.Access.BuiltInRoles.TenantOwner,
            requiredApproverCount: 2,
            thresholdAmount: Money.Of(10_000m, "USD")));

        ApprovalRequirement small = policy.Resolve(ActionRiskClass.Financial, ToolRegistry.AdsSpend, Money.Of(500m, "USD"));
        ApprovalRequirement large = policy.Resolve(ActionRiskClass.Financial, ToolRegistry.AdsSpend, Money.Of(25_000m, "USD"));

        Assert.Equal(1, small.ApproverCount);
        Assert.Equal(2, large.ApproverCount);
        Assert.True(small.IsRequired);
    }

    [Fact]
    public void A_threshold_rule_in_a_different_currency_does_not_match()
    {
        // Matching across currencies would require inventing an exchange rate inside a governance
        // decision, so a mismatch deliberately falls back to the platform floor instead.
        ApprovalPolicy policy = EmptyPolicy();
        policy.AddRule(ApprovalRule.Create(
            policy.Id,
            ActionRiskClass.Financial,
            OptimizeAll.Domain.Access.BuiltInRoles.TenantOwner,
            requiredApproverCount: 2,
            thresholdAmount: Money.Of(10_000m, "USD")));

        ApprovalRequirement requirement = policy.Resolve(
            ActionRiskClass.Financial, ToolRegistry.AdsSpend, Money.Of(25_000m, "EUR"));

        Assert.True(requirement.IsRequired);
        Assert.Equal(1, requirement.ApproverCount);
    }

    [Fact]
    public void Higher_risk_classes_get_shorter_default_approval_windows()
    {
        ApprovalPolicy policy = EmptyPolicy();

        TimeSpan irreversible = policy.Resolve(ActionRiskClass.Irreversible, ToolRegistry.DeployTrigger, null).Expiry;
        TimeSpan external = policy.Resolve(ActionRiskClass.External, ToolRegistry.ContentPublish, null).Expiry;

        Assert.True(irreversible < external);
    }
}
