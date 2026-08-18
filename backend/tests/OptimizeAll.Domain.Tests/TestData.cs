using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Governance;

namespace OptimizeAll.Domain.Tests;

/// <summary>
/// Shared fixtures. Deliberately explicit rather than randomised: a test that fails only on some
/// runs because a generator produced an unusual value costs more to diagnose than it ever saves.
/// </summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    public static readonly TenantId Tenant = TenantId.From(Guid.Parse("0195c0de-0000-7000-8000-000000000001"));

    public static readonly WorkspaceId Workspace = WorkspaceId.From(Guid.Parse("0195c0de-0000-7000-8000-000000000002"));

    public static readonly Guid RequesterAgent = Guid.Parse("0195c0de-0000-7000-8000-000000000003");

    public static readonly Guid ApproverAlice = Guid.Parse("0195c0de-0000-7000-8000-000000000004");

    public static readonly Guid ApproverBob = Guid.Parse("0195c0de-0000-7000-8000-000000000005");

    public static PrincipalRef Agent => new(PrincipalType.Agent, RequesterAgent);

    public static PrincipalRef Alice => new(PrincipalType.User, ApproverAlice);

    public static PrincipalRef Bob => new(PrincipalType.User, ApproverBob);

    public static ApprovalRequirement SingleApprover(TimeSpan? expiry = null)
        => ApprovalRequirement.Required(1, OptimizeAll.Domain.Access.BuiltInRoles.Approver, expiry ?? TimeSpan.FromHours(24));

    public static ApprovalRequirement TwoApprovers(TimeSpan? expiry = null)
        => ApprovalRequirement.Required(2, OptimizeAll.Domain.Access.BuiltInRoles.Approver, expiry ?? TimeSpan.FromHours(4));

    public static AgentDefinition PublishedAgent(
        string key = "content-agent",
        ActionRiskClass ceiling = ActionRiskClass.Write,
        params string[] tools)
    {
        AgentDefinition definition = AgentDefinition.CreateDraft(
            Tenant,
            Workspace,
            key,
            version: 1,
            displayName: key,
            mission: "Test mission.",
            systemPrompt: "You are a test agent.",
            ModelPolicy.Create(AiProvider.Anthropic, "claude-sonnet-5"),
            MemoryPolicy.Create(),
            BudgetPolicy.Default(),
            ceiling).Value;

        foreach (string tool in tools.Length > 0 ? tools : [ToolRegistry.ContentDraft])
        {
            definition.GrantTool(tool);
        }

        if (ceiling.RequiresHumanApproval())
        {
            definition.AttachApprovalPolicy(ApprovalPolicyId.New());
        }

        definition.Publish(Now);
        return definition;
    }
}
