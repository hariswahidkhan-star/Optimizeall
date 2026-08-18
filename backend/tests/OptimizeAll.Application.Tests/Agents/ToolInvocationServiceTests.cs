using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Application.Abstractions.Tools;
using OptimizeAll.Application.Agents.Runtime;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.Domain.Governance;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;
using Xunit;

namespace OptimizeAll.Application.Tests.Agents;

/// <summary>
/// The tool gate is the single point where a model's intent becomes, or fails to become, a real
/// action. Each test here corresponds to a way that boundary could silently open.
/// </summary>
public sealed class ToolInvocationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly IToolCatalog _catalog = Substitute.For<IToolCatalog>();
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IApprovalRepository _approvals = Substitute.For<IApprovalRepository>();
    private readonly IDataRedactor _redactor = Substitute.For<IDataRedactor>();
    private readonly FixedClock _clock = new(Now);

    public ToolInvocationServiceTests()
    {
        _redactor.Redact(Arg.Any<string>()).Returns(call => call.Arg<string>());
        _workspaces.IsKillSwitchEngagedAsync(Arg.Any<WorkspaceId>(), Arg.Any<CancellationToken>()).Returns(false);
    }

    private ToolInvocationService CreateService() => new(
        _catalog, _workspaces, _approvals, _redactor, _clock, NullLogger<ToolInvocationService>.Instance);

    private static AgentDefinition Agent(ActionRiskClass ceiling, params string[] tools)
    {
        AgentDefinition definition = AgentDefinition.CreateDraft(
            TenantId.New(),
            WorkspaceId.New(),
            "test-agent",
            1,
            "Test Agent",
            "Test mission.",
            "System prompt.",
            ModelPolicy.Create(AiProvider.Anthropic, "claude-sonnet-5"),
            MemoryPolicy.Create(),
            BudgetPolicy.Default(),
            ceiling).Value;

        foreach (string tool in tools)
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

    private static AgentRun Run(AgentDefinition definition, bool dryRun = false)
    {
        AgentRun run = AgentRun.Queue(
            definition, EnvironmentTier.Production, RunTriggerType.Manual, null, "{}", Guid.NewGuid(), Now, dryRun).Value;

        run.AcquireLease("worker-1", TimeSpan.FromMinutes(5), Now);
        return run;
    }

    private static ToolCallRequest Call(string toolKey, string args = "{}") => new()
    {
        ToolKey = toolKey,
        ArgumentsJson = args,
        IdempotencyKey = "idem-1",
    };

    private void RegisterExecutor(string toolKey, string resultJson = """{"ok":true}""")
    {
        IToolExecutor executor = Substitute.For<IToolExecutor>();
        executor.ToolKey.Returns(toolKey);
        executor.Description.Returns("Test executor.");
        executor.ParametersJsonSchema.Returns("""{"type":"object"}""");
        executor.ExecuteAsync(Arg.Any<string>(), Arg.Any<ToolExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ToolOutcome(resultJson, 42)));

        _catalog.Find(toolKey).Returns(executor);
    }

    [Fact]
    public async Task An_ungranted_tool_is_denied()
    {
        AgentDefinition definition = Agent(ActionRiskClass.Read, ToolRegistry.WebSearch);
        RegisterExecutor(ToolRegistry.KnowledgeSearch);

        ToolDisposition disposition = await CreateService()
            .InvokeAsync(Run(definition), definition, Call(ToolRegistry.KnowledgeSearch), CancellationToken.None);

        Assert.Equal(ToolDispositionKind.Denied, disposition.Kind);
        Assert.Contains("no grant", disposition.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_unregistered_tool_is_denied()
    {
        AgentDefinition definition = Agent(ActionRiskClass.Read, ToolRegistry.WebSearch);

        ToolDisposition disposition = await CreateService()
            .InvokeAsync(Run(definition), definition, Call("totally.invented"), CancellationToken.None);

        Assert.Equal(ToolDispositionKind.Denied, disposition.Kind);
    }

    [Fact]
    public async Task A_granted_read_tool_executes_without_a_gate()
    {
        AgentDefinition definition = Agent(ActionRiskClass.Read, ToolRegistry.WebSearch);
        RegisterExecutor(ToolRegistry.WebSearch);

        ToolDisposition disposition = await CreateService()
            .InvokeAsync(Run(definition), definition, Call(ToolRegistry.WebSearch), CancellationToken.None);

        Assert.Equal(ToolDispositionKind.Executed, disposition.Kind);
        Assert.Equal(ToolInvocationStatus.Executed, disposition.Invocation.Status);
    }

    [Fact]
    public async Task An_external_tool_raises_an_approval_gate_rather_than_executing()
    {
        AgentDefinition definition = Agent(ActionRiskClass.External, ToolRegistry.ContentPublish);
        RegisterExecutor(ToolRegistry.ContentPublish);
        _approvals.FindPolicyAsync(Arg.Any<ApprovalPolicyId>(), Arg.Any<CancellationToken>())
            .Returns((ApprovalPolicy?)null);

        ToolDisposition disposition = await CreateService().InvokeAsync(
            Run(definition), definition, Call(ToolRegistry.ContentPublish, """{"post":"hello"}"""), CancellationToken.None);

        Assert.Equal(ToolDispositionKind.GatedOnApproval, disposition.Kind);
        Assert.NotNull(disposition.ApprovalRequestId);
        _approvals.Received(1).Add(Arg.Any<ApprovalRequest>());
    }

    [Fact]
    public async Task A_missing_approval_policy_still_gates_rather_than_failing_open()
    {
        // The most dangerous failure mode: configuration absent, so the gate is skipped. It is not.
        AgentDefinition definition = Agent(ActionRiskClass.Financial, ToolRegistry.AdsSpend);
        RegisterExecutor(ToolRegistry.AdsSpend);
        _approvals.FindPolicyAsync(Arg.Any<ApprovalPolicyId>(), Arg.Any<CancellationToken>())
            .Returns((ApprovalPolicy?)null);

        ToolDisposition disposition = await CreateService()
            .InvokeAsync(Run(definition), definition, Call(ToolRegistry.AdsSpend), CancellationToken.None);

        Assert.Equal(ToolDispositionKind.GatedOnApproval, disposition.Kind);
    }

    [Fact]
    public async Task An_engaged_kill_switch_blocks_an_external_action()
    {
        AgentDefinition definition = Agent(ActionRiskClass.External, ToolRegistry.EmailSend);
        RegisterExecutor(ToolRegistry.EmailSend);
        _workspaces.IsKillSwitchEngagedAsync(Arg.Any<WorkspaceId>(), Arg.Any<CancellationToken>()).Returns(true);

        ToolDisposition disposition = await CreateService()
            .InvokeAsync(Run(definition), definition, Call(ToolRegistry.EmailSend), CancellationToken.None);

        Assert.Equal(ToolDispositionKind.Denied, disposition.Kind);
        Assert.Contains("emergency stop", disposition.Reason!, StringComparison.OrdinalIgnoreCase);
        _approvals.DidNotReceive().Add(Arg.Any<ApprovalRequest>());
    }

    [Fact]
    public async Task An_approved_request_authorises_execution_of_the_exact_payload()
    {
        const string Payload = """{"channel":"blog","body":"approved text"}""";

        AgentDefinition definition = Agent(ActionRiskClass.External, ToolRegistry.ContentPublish);
        RegisterExecutor(ToolRegistry.ContentPublish);
        AgentRun run = Run(definition);

        ApprovalRequest approval = ApprovalRequest.Raise(
            run.TenantIdentifier, run.WorkspaceId, EnvironmentTier.Production, ActionRiskClass.External,
            "Publish", Payload, PrincipalRef.ForAgent(definition.Id),
            ApprovalRequirement.Required(1, Domain.Access.BuiltInRoles.Approver, TimeSpan.FromHours(24)), Now).Value;

        approval.Decide(new PrincipalRef(PrincipalType.User, Guid.NewGuid()), ApprovalDecisionKind.Approve, null, true, Now);
        _approvals.FindAsync(approval.Id, Arg.Any<CancellationToken>()).Returns(approval);

        ToolDisposition disposition = await CreateService().InvokeAsync(
            run,
            definition,
            new ToolCallRequest
            {
                ToolKey = ToolRegistry.ContentPublish,
                ArgumentsJson = Payload,
                IdempotencyKey = "idem-1",
                ExistingApprovalId = approval.Id,
            },
            CancellationToken.None);

        Assert.Equal(ToolDispositionKind.Executed, disposition.Kind);
    }

    [Fact]
    public async Task A_payload_substituted_after_approval_is_refused()
    {
        const string Approved = """{"channel":"blog","body":"approved text"}""";
        const string Substituted = """{"channel":"blog","body":"something else entirely"}""";

        AgentDefinition definition = Agent(ActionRiskClass.External, ToolRegistry.ContentPublish);
        RegisterExecutor(ToolRegistry.ContentPublish);
        AgentRun run = Run(definition);

        ApprovalRequest approval = ApprovalRequest.Raise(
            run.TenantIdentifier, run.WorkspaceId, EnvironmentTier.Production, ActionRiskClass.External,
            "Publish", Approved, PrincipalRef.ForAgent(definition.Id),
            ApprovalRequirement.Required(1, Domain.Access.BuiltInRoles.Approver, TimeSpan.FromHours(24)), Now).Value;

        approval.Decide(new PrincipalRef(PrincipalType.User, Guid.NewGuid()), ApprovalDecisionKind.Approve, null, true, Now);
        _approvals.FindAsync(approval.Id, Arg.Any<CancellationToken>()).Returns(approval);

        ToolDisposition disposition = await CreateService().InvokeAsync(
            run,
            definition,
            new ToolCallRequest
            {
                ToolKey = ToolRegistry.ContentPublish,
                ArgumentsJson = Substituted,
                IdempotencyKey = "idem-1",
                ExistingApprovalId = approval.Id,
            },
            CancellationToken.None);

        Assert.Equal(ToolDispositionKind.Denied, disposition.Kind);
        Assert.Contains("differs from the payload that was approved", disposition.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dry_run_records_an_external_action_without_applying_it()
    {
        AgentDefinition definition = Agent(ActionRiskClass.Write, ToolRegistry.ContentDraft);
        RegisterExecutor(ToolRegistry.ContentDraft);

        ToolDisposition disposition = await CreateService().InvokeAsync(
            Run(definition, dryRun: true), definition, Call(ToolRegistry.ContentDraft), CancellationToken.None);

        Assert.Equal(ToolDispositionKind.SkippedDryRun, disposition.Kind);
        Assert.Contains("\"applied\":false", disposition.Invocation.ResultJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_tool_call_budget_is_enforced_before_the_call()
    {
        AgentDefinition definition = AgentDefinition.CreateDraft(
            TenantId.New(), WorkspaceId.New(), "test-agent", 1, "Test", "Mission", "Prompt",
            ModelPolicy.Create(AiProvider.Anthropic, "claude-sonnet-5"),
            MemoryPolicy.Create(),
            BudgetPolicy.Create(100_000, Money.Of(5m, "USD"), maxToolCalls: 1, maxIterations: 10, TimeSpan.FromMinutes(5)),
            ActionRiskClass.Read).Value;

        definition.GrantTool(ToolRegistry.WebSearch);
        definition.Publish(Now);
        RegisterExecutor(ToolRegistry.WebSearch);

        AgentRun run = Run(definition);
        ToolInvocationService service = CreateService();

        ToolDisposition first = await service.InvokeAsync(run, definition, Call(ToolRegistry.WebSearch), CancellationToken.None);
        ToolDisposition second = await service.InvokeAsync(run, definition, Call(ToolRegistry.WebSearch), CancellationToken.None);

        Assert.Equal(ToolDispositionKind.Executed, first.Kind);
        Assert.Equal(ToolDispositionKind.Denied, second.Kind);
        Assert.Contains("tool-call limit", second.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_tool_that_throws_fails_the_call_without_aborting_the_run()
    {
        AgentDefinition definition = Agent(ActionRiskClass.Read, ToolRegistry.WebSearch);

        IToolExecutor executor = Substitute.For<IToolExecutor>();
        executor.ToolKey.Returns(ToolRegistry.WebSearch);
        executor.ExecuteAsync(Arg.Any<string>(), Arg.Any<ToolExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<ToolOutcome>>>(_ => throw new InvalidOperationException("boom"));
        _catalog.Find(ToolRegistry.WebSearch).Returns(executor);

        AgentRun run = Run(definition);
        ToolDisposition disposition = await CreateService()
            .InvokeAsync(run, definition, Call(ToolRegistry.WebSearch), CancellationToken.None);

        Assert.Equal(ToolDispositionKind.Failed, disposition.Kind);
        Assert.Equal(AgentRunStatus.Running, run.Status);
    }

    [Fact]
    public async Task Every_invocation_is_recorded_on_the_run_including_denials()
    {
        AgentDefinition definition = Agent(ActionRiskClass.Read, ToolRegistry.WebSearch);
        AgentRun run = Run(definition);

        await CreateService().InvokeAsync(run, definition, Call(ToolRegistry.KnowledgeWrite), CancellationToken.None);

        Assert.Single(run.ToolInvocations);
        Assert.Equal(ToolInvocationStatus.Denied, run.ToolInvocations[0].Status);
    }

    [Fact]
    public async Task A_denied_call_does_not_consume_the_tool_budget()
    {
        // Otherwise an agent could exhaust its own allowance on calls it was never permitted to
        // make, turning a permission error into a denial of service against itself.
        AgentDefinition definition = Agent(ActionRiskClass.Read, ToolRegistry.WebSearch);
        RegisterExecutor(ToolRegistry.WebSearch);

        AgentRun run = Run(definition);
        ToolInvocationService service = CreateService();

        await service.InvokeAsync(run, definition, Call(ToolRegistry.KnowledgeWrite), CancellationToken.None);
        await service.InvokeAsync(run, definition, Call("not.a.real.tool"), CancellationToken.None);

        Assert.Equal(0, run.Budget.ToolCallCount);

        ToolDisposition allowed = await service
            .InvokeAsync(run, definition, Call(ToolRegistry.WebSearch), CancellationToken.None);

        Assert.Equal(ToolDispositionKind.Executed, allowed.Kind);
        Assert.Equal(1, run.Budget.ToolCallCount);
    }

    [Fact]
    public async Task A_gated_call_does_not_consume_the_tool_budget_before_it_is_approved()
    {
        AgentDefinition definition = Agent(ActionRiskClass.External, ToolRegistry.ContentPublish);
        RegisterExecutor(ToolRegistry.ContentPublish);
        _approvals.FindPolicyAsync(Arg.Any<ApprovalPolicyId>(), Arg.Any<CancellationToken>())
            .Returns((ApprovalPolicy?)null);

        AgentRun run = Run(definition);

        await CreateService().InvokeAsync(run, definition, Call(ToolRegistry.ContentPublish), CancellationToken.None);

        Assert.Equal(0, run.Budget.ToolCallCount);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
