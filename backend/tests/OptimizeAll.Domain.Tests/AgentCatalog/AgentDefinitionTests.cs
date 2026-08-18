using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;
using Xunit;

namespace OptimizeAll.Domain.Tests.AgentCatalog;

public sealed class AgentDefinitionTests
{
    private static AgentDefinition Draft(ActionRiskClass ceiling = ActionRiskClass.Write)
        => AgentDefinition.CreateDraft(
            TestData.Tenant,
            TestData.Workspace,
            "seo-agent",
            version: 1,
            displayName: "SEO Agent",
            mission: "Grow qualified organic traffic.",
            systemPrompt: "You are the SEO agent.",
            ModelPolicy.Create(AiProvider.OpenAi, "gpt-5"),
            MemoryPolicy.Create(),
            BudgetPolicy.Default(),
            ceiling).Value;

    [Fact]
    public void A_tool_riskier_than_the_declared_ceiling_cannot_be_granted()
    {
        AgentDefinition definition = Draft(ActionRiskClass.Write);

        Result grant = definition.GrantTool(ToolRegistry.ContentPublish);

        Assert.True(grant.IsFailure);
        Assert.Equal("agent.risk_ceiling_exceeded", grant.Error.Code);
        Assert.False(definition.IsToolGranted(ToolRegistry.ContentPublish));
    }

    [Fact]
    public void An_unregistered_tool_cannot_be_granted()
    {
        Result grant = Draft().GrantTool("totally.made.up");

        Assert.True(grant.IsFailure);
        Assert.Equal("agent.unknown_tool", grant.Error.Code);
    }

    [Fact]
    public void An_ungranted_tool_is_denied_by_default()
    {
        AgentDefinition definition = Draft();
        definition.GrantTool(ToolRegistry.WebSearch);

        Assert.True(definition.IsToolGranted(ToolRegistry.WebSearch));
        Assert.False(definition.IsToolGranted(ToolRegistry.KnowledgeWrite));
    }

    [Fact]
    public void A_published_definition_cannot_be_modified()
    {
        AgentDefinition definition = Draft();
        definition.GrantTool(ToolRegistry.WebSearch);
        definition.Publish(TestData.Now);

        Result grant = definition.GrantTool(ToolRegistry.WebFetch);
        Result revoke = definition.RevokeTool(ToolRegistry.WebSearch);

        Assert.True(grant.IsFailure);
        Assert.Equal("agent.published_immutable", grant.Error.Code);
        Assert.True(revoke.IsFailure);
        Assert.True(definition.IsToolGranted(ToolRegistry.WebSearch));
    }

    [Fact]
    public void An_agent_that_can_propose_gated_actions_cannot_publish_without_an_approval_policy()
    {
        AgentDefinition definition = Draft(ActionRiskClass.External);
        definition.GrantTool(ToolRegistry.ContentPublish);

        Result published = definition.Publish(TestData.Now);

        Assert.True(published.IsFailure);
        Assert.Equal("agent.approval_policy_required", published.Error.Code);
        Assert.Equal(AgentDefinitionStatus.Draft, definition.Status);
    }

    [Fact]
    public void Attaching_an_approval_policy_allows_a_gated_agent_to_publish()
    {
        AgentDefinition definition = Draft(ActionRiskClass.External);
        definition.GrantTool(ToolRegistry.ContentPublish);
        definition.AttachApprovalPolicy(ApprovalPolicyId.New());

        Assert.True(definition.Publish(TestData.Now).IsSuccess);
        Assert.Equal(AgentDefinitionStatus.Published, definition.Status);
        Assert.True(definition.IsExecutable);
    }

    [Fact]
    public void An_agent_claiming_a_risk_ceiling_above_read_must_actually_be_granted_something()
    {
        Result published = Draft(ActionRiskClass.Write).Publish(TestData.Now);

        Assert.True(published.IsFailure);
        Assert.Equal("agent.no_tools_granted", published.Error.Code);
    }

    [Fact]
    public void A_grant_records_the_risk_class_as_it_stood_at_grant_time()
    {
        AgentDefinition definition = Draft();
        definition.GrantTool(ToolRegistry.ContentDraft);

        ToolGrant? grant = definition.FindGrant(ToolRegistry.ContentDraft);

        Assert.NotNull(grant);
        Assert.Equal(ActionRiskClass.Write, grant.RiskClass);
    }

    [Fact]
    public void A_deprecated_definition_is_no_longer_executable()
    {
        AgentDefinition definition = Draft();
        definition.GrantTool(ToolRegistry.WebSearch);
        definition.Publish(TestData.Now);
        definition.Deprecate(TestData.Now);

        Assert.False(definition.IsExecutable);
    }
}

public sealed class ToolRegistryTests
{
    [Fact]
    public void An_unknown_tool_throws_rather_than_defaulting_to_a_low_risk_class()
        => Assert.Throws<ArgumentException>(() => ToolRegistry.RiskOf("unknown.tool"));

    [Theory]
    [InlineData(ToolRegistry.ContentPublish, ActionRiskClass.External)]
    [InlineData(ToolRegistry.EmailSend, ActionRiskClass.External)]
    [InlineData(ToolRegistry.LinkedInMessage, ActionRiskClass.External)]
    [InlineData(ToolRegistry.AdsSpend, ActionRiskClass.Financial)]
    [InlineData(ToolRegistry.InvoiceIssue, ActionRiskClass.Financial)]
    [InlineData(ToolRegistry.DeployTrigger, ActionRiskClass.Irreversible)]
    public void The_business_critical_tools_carry_the_risk_class_the_specification_requires(
        string toolKey,
        ActionRiskClass expected)
        => Assert.Equal(expected, ToolRegistry.RiskOf(toolKey));

    [Fact]
    public void Publishing_outreach_and_spend_are_all_gated()
    {
        foreach (string tool in new[] { ToolRegistry.ContentPublish, ToolRegistry.LinkedInMessage, ToolRegistry.AdsSpend })
        {
            Assert.True(ToolRegistry.RiskOf(tool).RequiresHumanApproval(), $"{tool} must be gated.");
        }
    }
}
