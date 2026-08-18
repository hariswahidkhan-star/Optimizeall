using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.Infrastructure.Seed;
using OptimizeAll.SharedKernel.Results;
using Xunit;

namespace OptimizeAll.Infrastructure.Tests;

/// <summary>
/// The seeded workforce is the platform's default configuration, so a mistake in it ships to every
/// new workspace. These tests assert the properties the catalog document promises.
/// </summary>
public sealed class AgentWorkforceSeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly AgentWorkforceSeed Seed = AgentWorkforceSeed.Load();

    [Fact]
    public void The_seed_loads_and_contains_the_documented_workforce()
    {
        Assert.NotEmpty(Seed.Agents);
        Assert.Equal(29, Seed.Agents.Count);
    }

    [Fact]
    public void Agent_keys_are_unique()
    {
        List<string> duplicates =
        [
            .. Seed.Agents
                .GroupBy(a => a.AgentKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key),
        ];

        Assert.True(duplicates.Count == 0, $"Duplicate agent keys: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void Every_agent_materialises_into_a_publishable_definition()
    {
        // Exercises the same path a workspace provisioning run takes. A seed entry whose tools
        // exceed its declared risk ceiling, or which claims a gated ceiling without an approval
        // policy, fails here rather than on a customer's first day.
        List<string> failures = [];

        foreach (SeedAgent agent in Seed.Agents)
        {
            Result<AgentDefinition> materialised = AgentWorkforceSeed.Materialise(
                agent,
                TenantId.New(),
                WorkspaceId.New(),
                agent.RequiresApprovalPolicy ? ApprovalPolicyId.New() : null,
                Now);

            if (materialised.IsFailure)
            {
                failures.Add($"{agent.AgentKey}: {materialised.Error}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void No_agent_is_granted_a_tool_riskier_than_its_declared_ceiling()
    {
        List<string> violations = [];

        foreach (SeedAgent agent in Seed.Agents)
        {
            foreach (string toolKey in agent.Tools)
            {
                if (!ToolRegistry.TryGetRisk(toolKey, out ActionRiskClass risk))
                {
                    violations.Add($"{agent.AgentKey} grants unregistered tool '{toolKey}'.");
                    continue;
                }

                if (risk > agent.MaxRiskClass)
                {
                    violations.Add(
                        $"{agent.AgentKey} declares a {agent.MaxRiskClass} ceiling but is granted " +
                        $"'{toolKey}' which is {risk}.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void The_declared_ceiling_is_justified_by_at_least_one_granted_tool()
    {
        // A ceiling higher than anything the agent can actually do misrepresents its capability to
        // whoever reviews the catalog, which is the audience the ceiling exists for.
        List<string> overstated = [];

        foreach (SeedAgent agent in Seed.Agents.Where(a => a.MaxRiskClass > ActionRiskClass.Read))
        {
            ActionRiskClass highestGranted = agent.Tools
                .Select(ToolRegistry.RiskOf)
                .DefaultIfEmpty(ActionRiskClass.Read)
                .Max();

            if (highestGranted < agent.MaxRiskClass)
            {
                overstated.Add(
                    $"{agent.AgentKey} declares {agent.MaxRiskClass} but its riskiest tool is {highestGranted}.");
            }
        }

        Assert.True(overstated.Count == 0, string.Join("\n", overstated));
    }

    [Theory]
    [InlineData("publishing-agent")]
    [InlineData("linkedin-outreach-agent")]
    [InlineData("pr-agent")]
    public void Agents_that_reach_an_audience_can_only_do_so_through_a_gated_tool(string agentKey)
    {
        SeedAgent agent = Seed.Agents.Single(a => a.AgentKey == agentKey);

        Assert.True(
            agent.MaxRiskClass.RequiresHumanApproval(),
            $"{agentKey} reaches people outside the organisation and must be gated.");
    }

    [Fact]
    public void The_publishing_agent_is_stateless()
    {
        // Publishing must be deterministic: it applies exactly the approved bytes. An agent that
        // learned between runs would introduce variability into the one operation whose entire
        // value is that it does not vary.
        SeedAgent publishing = Seed.Agents.Single(a => a.AgentKey == "publishing-agent");

        Assert.False(publishing.MemoryPolicy.EpisodicEnabled);
        Assert.False(publishing.MemoryPolicy.SemanticEnabled);
    }

    [Fact]
    public void No_agent_is_granted_the_deployment_tool_without_an_irreversible_ceiling()
    {
        foreach (SeedAgent agent in Seed.Agents.Where(a => a.Tools.Contains(ToolRegistry.DeployTrigger)))
        {
            Assert.Equal(ActionRiskClass.Irreversible, agent.MaxRiskClass);
        }
    }

    [Fact]
    public void Agents_handling_personal_data_redact_it_before_writing_to_memory()
    {
        // Otherwise an agent's durable memory becomes an unmanaged copy of personal data sitting
        // outside the erasure path.
        foreach (string agentKey in new[] { "hr-agent", "support-agent", "crm-agent" })
        {
            SeedAgent agent = Seed.Agents.Single(a => a.AgentKey == agentKey);
            Assert.True(agent.MemoryPolicy.RedactPersonalData, $"{agentKey} must redact personal data.");
        }
    }

    [Fact]
    public void Every_agent_declares_at_least_one_kpi()
    {
        List<string> unmeasured = [.. Seed.Agents.Where(a => a.Kpis.Count == 0).Select(a => a.AgentKey)];

        Assert.True(
            unmeasured.Count == 0,
            $"An agent with no KPI cannot be evaluated: {string.Join(", ", unmeasured)}");
    }

    [Fact]
    public void Every_agent_has_a_bounded_budget()
    {
        foreach (SeedAgent agent in Seed.Agents)
        {
            Assert.True(agent.BudgetPolicy.MaxTotalTokens > 0, $"{agent.AgentKey} has no token budget.");
            Assert.True(agent.BudgetPolicy.MaxIterations > 0, $"{agent.AgentKey} has no iteration limit.");
            Assert.True(agent.BudgetPolicy.MaxCostAmount > 0, $"{agent.AgentKey} has no cost ceiling.");
            Assert.InRange(agent.BudgetPolicy.MaxWallClockMinutes, 1, 360);
        }
    }

    [Fact]
    public void Every_scheduled_agent_names_a_resolvable_timezone()
    {
        foreach (SeedAgent agent in Seed.Agents.Where(a => a.Schedule is not null))
        {
            SeedSchedule schedule = agent.Schedule!;

            Exception? thrown = Record.Exception(() => TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone));

            Assert.True(thrown is null, $"{agent.AgentKey} names an unresolvable timezone '{schedule.Timezone}'.");
        }
    }
}
