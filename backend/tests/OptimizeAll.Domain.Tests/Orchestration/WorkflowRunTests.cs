using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Orchestration;
using OptimizeAll.SharedKernel.Results;
using Xunit;

namespace OptimizeAll.Domain.Tests.Orchestration;

public sealed class WorkflowRunTests
{
    private static WorkflowRun NewWorkflow()
        => WorkflowRun.Start(
            TestData.Tenant,
            TestData.Workspace,
            EnvironmentTier.Production,
            "Launch the spring campaign",
            """{"goal":"launch"}""",
            Guid.NewGuid(),
            TestData.Now).Value;

    [Fact]
    public void A_direct_cycle_is_refused()
    {
        WorkflowRun workflow = NewWorkflow();
        WorkTask a = workflow.AddTask("Research", "research-agent", "{}").Value;
        WorkTask b = workflow.AddTask("Draft", "content-agent", "{}").Value;

        workflow.AddDependency(b.Id, a.Id);
        Result cycle = workflow.AddDependency(a.Id, b.Id);

        Assert.True(cycle.IsFailure);
        Assert.Equal("workflow.cyclic_dependency", cycle.Error.Code);
    }

    [Fact]
    public void A_transitive_cycle_is_refused()
    {
        WorkflowRun workflow = NewWorkflow();
        WorkTask a = workflow.AddTask("A", "research-agent", "{}").Value;
        WorkTask b = workflow.AddTask("B", "content-agent", "{}").Value;
        WorkTask c = workflow.AddTask("C", "content-qa-agent", "{}").Value;

        workflow.AddDependency(b.Id, a.Id);
        workflow.AddDependency(c.Id, b.Id);
        Result cycle = workflow.AddDependency(a.Id, c.Id);

        Assert.True(cycle.IsFailure);
        Assert.Equal("workflow.cyclic_dependency", cycle.Error.Code);
    }

    [Fact]
    public void A_self_dependency_is_refused()
    {
        WorkflowRun workflow = NewWorkflow();
        WorkTask a = workflow.AddTask("A", "research-agent", "{}").Value;

        Result selfEdge = workflow.AddDependency(a.Id, a.Id);

        Assert.True(selfEdge.IsFailure);
        Assert.Equal("workflow.self_dependency", selfEdge.Error.Code);
    }

    [Fact]
    public void Activation_marks_only_tasks_with_no_predecessors_as_ready()
    {
        WorkflowRun workflow = NewWorkflow();
        WorkTask a = workflow.AddTask("A", "research-agent", "{}").Value;
        WorkTask b = workflow.AddTask("B", "content-agent", "{}").Value;
        workflow.AddDependency(b.Id, a.Id);

        workflow.Activate(Money.Of(1m, "USD"), TestData.Now);

        Assert.Equal(WorkTaskStatus.Ready, a.Status);
        Assert.Equal(WorkTaskStatus.Pending, b.Status);
    }

    [Fact]
    public void An_empty_plan_cannot_be_activated()
    {
        Result activated = NewWorkflow().Activate(Money.Of(1m, "USD"), TestData.Now);

        Assert.True(activated.IsFailure);
        Assert.Equal("workflow.empty_plan", activated.Error.Code);
    }

    [Fact]
    public void Completing_a_predecessor_makes_its_successor_ready()
    {
        WorkflowRun workflow = NewWorkflow();
        WorkTask a = workflow.AddTask("A", "research-agent", "{}").Value;
        WorkTask b = workflow.AddTask("B", "content-agent", "{}").Value;
        workflow.AddDependency(b.Id, a.Id);
        workflow.Activate(Money.Of(1m, "USD"), TestData.Now);

        workflow.StartTask(a.Id, AgentRunId.New(), TestData.Now);
        workflow.CompleteTask(a.Id, """{"ok":true}""", Money.Of(0.5m, "USD"), TestData.Now);

        Assert.Equal(WorkTaskStatus.Ready, b.Status);
    }

    [Fact]
    public void A_terminally_failed_task_skips_everything_downstream_instead_of_stalling_the_workflow()
    {
        WorkflowRun workflow = NewWorkflow();
        WorkTask a = workflow.AddTask("A", "research-agent", "{}", maxAttempts: 1).Value;
        WorkTask b = workflow.AddTask("B", "content-agent", "{}").Value;
        WorkTask c = workflow.AddTask("C", "publishing-agent", "{}").Value;
        workflow.AddDependency(b.Id, a.Id);
        workflow.AddDependency(c.Id, b.Id);
        workflow.Activate(Money.Of(1m, "USD"), TestData.Now);

        workflow.StartTask(a.Id, AgentRunId.New(), TestData.Now);
        workflow.FailTask(a.Id, "Upstream API unavailable.", Money.Of(0.1m, "USD"), TestData.Now);

        Assert.Equal(WorkTaskStatus.Failed, a.Status);
        Assert.Equal(WorkTaskStatus.Skipped, b.Status);
        Assert.Equal(WorkTaskStatus.Skipped, c.Status);
        Assert.Equal(WorkflowStatus.Failed, workflow.Status);
    }

    [Fact]
    public void A_failure_with_retries_remaining_returns_the_task_to_ready()
    {
        WorkflowRun workflow = NewWorkflow();
        WorkTask a = workflow.AddTask("A", "research-agent", "{}", maxAttempts: 3).Value;
        workflow.Activate(Money.Of(1m, "USD"), TestData.Now);

        workflow.StartTask(a.Id, AgentRunId.New(), TestData.Now);
        workflow.FailTask(a.Id, "Transient timeout.", Money.Of(0.1m, "USD"), TestData.Now);

        Assert.Equal(WorkTaskStatus.Ready, a.Status);
        Assert.Equal(WorkflowStatus.Running, workflow.Status);
    }

    [Fact]
    public void A_workflow_succeeds_only_when_every_task_succeeded()
    {
        WorkflowRun workflow = NewWorkflow();
        WorkTask a = workflow.AddTask("A", "research-agent", "{}").Value;
        WorkTask b = workflow.AddTask("B", "content-agent", "{}").Value;
        workflow.AddDependency(b.Id, a.Id);
        workflow.Activate(Money.Of(1m, "USD"), TestData.Now);

        workflow.StartTask(a.Id, AgentRunId.New(), TestData.Now);
        workflow.CompleteTask(a.Id, "{}", Money.Of(0.2m, "USD"), TestData.Now);
        workflow.StartTask(b.Id, AgentRunId.New(), TestData.Now);
        workflow.CompleteTask(b.Id, "{}", Money.Of(0.3m, "USD"), TestData.Now);

        Assert.Equal(WorkflowStatus.Succeeded, workflow.Status);
        Assert.Equal(0.5m, workflow.ActualCost.Amount);
    }

    [Fact]
    public void Blocking_a_task_fails_the_workflow_and_strands_nothing()
    {
        WorkflowRun workflow = NewWorkflow();
        WorkTask a = workflow.AddTask("A", "content-agent", "{}").Value;
        WorkTask b = workflow.AddTask("B", "publishing-agent", "{}").Value;
        workflow.AddDependency(b.Id, a.Id);
        workflow.Activate(Money.Of(1m, "USD"), TestData.Now);

        workflow.BlockTask(a.Id, "Compliance control failed.", TestData.Now);

        Assert.Equal(WorkTaskStatus.Blocked, a.Status);
        Assert.Equal(WorkTaskStatus.Skipped, b.Status);
        Assert.Equal(WorkflowStatus.Failed, workflow.Status);
    }

    [Fact]
    public void A_stalled_task_is_detected_by_a_lapsed_heartbeat()
    {
        WorkflowRun workflow = NewWorkflow();
        WorkTask a = workflow.AddTask("A", "research-agent", "{}").Value;
        workflow.Activate(Money.Of(1m, "USD"), TestData.Now);
        workflow.StartTask(a.Id, AgentRunId.New(), TestData.Now);

        IReadOnlyList<WorkTask> stalled = workflow.StalledTasks(
            TestData.Now.AddMinutes(10), TimeSpan.FromMinutes(5));

        Assert.Single(stalled);
        Assert.Equal(a.Id, stalled[0].Id);
    }

    [Fact]
    public void Tasks_cannot_be_added_after_activation()
    {
        WorkflowRun workflow = NewWorkflow();
        workflow.AddTask("A", "research-agent", "{}");
        workflow.Activate(Money.Of(1m, "USD"), TestData.Now);

        Result<WorkTask> added = workflow.AddTask("Late", "content-agent", "{}");

        Assert.True(added.IsFailure);
        Assert.Equal("workflow.not_planning", added.Error.Code);
    }
}
