using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.SharedKernel.Results;
using Xunit;

namespace OptimizeAll.Domain.Tests.Execution;

public sealed class AgentRunTests
{
    private static AgentRun Queue(AgentDefinition? definition = null)
        => AgentRun.Queue(
            definition ?? TestData.PublishedAgent(),
            EnvironmentTier.Production,
            RunTriggerType.Manual,
            TestData.ApproverAlice,
            """{"brief":"write something"}""",
            Guid.NewGuid(),
            TestData.Now).Value;

    [Fact]
    public void A_draft_definition_cannot_be_run()
    {
        AgentDefinition draft = AgentDefinition.CreateDraft(
            TestData.Tenant, TestData.Workspace, "seo-agent", 1, "SEO", "Mission", "Prompt",
            ModelPolicy.Create(AiProvider.OpenAi, "gpt-5"),
            MemoryPolicy.Create(),
            BudgetPolicy.Default(),
            ActionRiskClass.Read).Value;

        Result<AgentRun> queued = AgentRun.Queue(
            draft, EnvironmentTier.Production, RunTriggerType.Manual, null, "{}", Guid.NewGuid(), TestData.Now);

        Assert.True(queued.IsFailure);
        Assert.Equal("run.definition_not_executable", queued.Error.Code);
    }

    [Fact]
    public void Acquiring_a_lease_starts_the_run()
    {
        AgentRun run = Queue();

        Result leased = run.AcquireLease("worker-1", TimeSpan.FromMinutes(5), TestData.Now);

        Assert.True(leased.IsSuccess);
        Assert.Equal(AgentRunStatus.Running, run.Status);
        Assert.Equal("worker-1", run.LeaseHolder);
    }

    [Fact]
    public void A_live_lease_cannot_be_stolen_by_another_worker()
    {
        AgentRun run = Queue();
        run.AcquireLease("worker-1", TimeSpan.FromMinutes(5), TestData.Now);

        Result stolen = run.AcquireLease("worker-2", TimeSpan.FromMinutes(5), TestData.Now.AddMinutes(1));

        Assert.True(stolen.IsFailure);
        Assert.Equal("run.lease_held", stolen.Error.Code);
        Assert.Equal("worker-1", run.LeaseHolder);
    }

    [Fact]
    public void An_expired_lease_is_reclaimable_so_a_dead_worker_does_not_strand_the_run()
    {
        AgentRun run = Queue();
        run.AcquireLease("worker-1", TimeSpan.FromMinutes(5), TestData.Now);

        Result reclaimed = run.AcquireLease("worker-2", TimeSpan.FromMinutes(5), TestData.Now.AddMinutes(6));

        Assert.True(reclaimed.IsSuccess);
        Assert.Equal("worker-2", run.LeaseHolder);
    }

    [Fact]
    public void Suspending_for_approval_releases_the_worker()
    {
        AgentRun run = Queue();
        run.AcquireLease("worker-1", TimeSpan.FromMinutes(5), TestData.Now);

        run.SuspendForApproval(ApprovalRequestId.New(), TestData.Now);

        Assert.Equal(AgentRunStatus.AwaitingApproval, run.Status);
        Assert.Null(run.LeaseHolder);
        Assert.Null(run.LeaseExpiresAt);
    }

    [Fact]
    public void A_suspended_run_returns_to_the_queue_when_resumed()
    {
        AgentRun run = Queue();
        run.AcquireLease("worker-1", TimeSpan.FromMinutes(5), TestData.Now);
        run.SuspendForApproval(ApprovalRequestId.New(), TestData.Now);

        run.Resume(TestData.Now.AddHours(2));

        Assert.Equal(AgentRunStatus.Queued, run.Status);
        Assert.Null(run.BlockingApprovalId);
    }

    [Fact]
    public void A_terminal_run_cannot_transition_again()
    {
        AgentRun run = Queue();
        run.AcquireLease("worker-1", TimeSpan.FromMinutes(5), TestData.Now);
        run.Succeed("""{"result":"ok"}""", TestData.Now);

        Result failed = run.Fail("""{"code":"x"}""", TestData.Now);

        Assert.True(failed.IsFailure);
        Assert.Equal(AgentRunStatus.Succeeded, run.Status);
    }

    [Fact]
    public void Non_json_output_is_rejected_so_the_run_record_stays_queryable()
    {
        AgentRun run = Queue();
        run.AcquireLease("worker-1", TimeSpan.FromMinutes(5), TestData.Now);

        Result succeeded = run.Succeed("just a string", TestData.Now);

        Assert.True(succeeded.IsFailure);
        Assert.Equal("run.invalid_output", succeeded.Error.Code);
    }

    [Fact]
    public void An_unstructured_failure_detail_is_replaced_rather_than_stored_raw()
    {
        AgentRun run = Queue();
        run.AcquireLease("worker-1", TimeSpan.FromMinutes(5), TestData.Now);

        run.Fail("boom", TestData.Now);

        Assert.NotNull(run.ErrorJson);
        Assert.Contains("run.unstructured_error", run.ErrorJson, StringComparison.Ordinal);
    }
}

public sealed class RunBudgetLedgerTests
{
    private static readonly BudgetPolicy Policy = BudgetPolicy.Create(
        maxTotalTokens: 1_000,
        maxCost: Money.Of(1.00m, "USD"),
        maxToolCalls: 3,
        maxIterations: 2,
        maxWallClock: TimeSpan.FromMinutes(5));

    [Fact]
    public void A_fresh_ledger_permits_the_first_iteration()
        => Assert.True(RunBudgetLedger.Empty("USD")
            .CanStartIteration(Policy, Money.Of(0.10m, "USD"), TimeSpan.Zero).IsSuccess);

    [Fact]
    public void The_iteration_limit_stops_a_non_converging_agent()
    {
        RunBudgetLedger ledger = RunBudgetLedger.Empty("USD")
            .RecordCompletion(10, 10, Money.Of(0.01m, "USD"))
            .RecordCompletion(10, 10, Money.Of(0.01m, "USD"));

        Result check = ledger.CanStartIteration(Policy, Money.Of(0.01m, "USD"), TimeSpan.Zero);

        Assert.True(check.IsFailure);
        Assert.Equal("run.iteration_limit", check.Error.Code);
    }

    [Fact]
    public void A_call_that_would_breach_the_cost_ceiling_is_refused_before_it_is_made()
    {
        RunBudgetLedger ledger = RunBudgetLedger.Empty("USD")
            .RecordCompletion(10, 10, Money.Of(0.95m, "USD"));

        Result check = ledger.CanStartIteration(Policy, Money.Of(0.20m, "USD"), TimeSpan.Zero);

        Assert.True(check.IsFailure);
        Assert.Equal("run.cost_limit", check.Error.Code);
    }

    [Fact]
    public void The_wall_clock_ceiling_is_enforced()
    {
        Result check = RunBudgetLedger.Empty("USD")
            .CanStartIteration(Policy, Money.Of(0.01m, "USD"), TimeSpan.FromMinutes(6));

        Assert.True(check.IsFailure);
        Assert.Equal("run.wall_clock_limit", check.Error.Code);
    }

    [Fact]
    public void The_tool_call_ceiling_is_enforced()
    {
        RunBudgetLedger ledger = RunBudgetLedger.Empty("USD")
            .RecordToolCall().RecordToolCall().RecordToolCall();

        Result check = ledger.CanCallTool(Policy);

        Assert.True(check.IsFailure);
        Assert.Equal("run.tool_call_limit", check.Error.Code);
    }

    [Fact]
    public void Token_consumption_accumulates_across_completions()
    {
        RunBudgetLedger ledger = RunBudgetLedger.Empty("USD")
            .RecordCompletion(100, 50, Money.Of(0.01m, "USD"))
            .RecordCompletion(200, 75, Money.Of(0.02m, "USD"));

        Assert.Equal(425, ledger.TotalTokens);
        Assert.Equal(0.03m, ledger.CostIncurred.Amount);
    }
}
