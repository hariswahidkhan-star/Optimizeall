using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Orchestration;

public enum WorkflowStatus
{
    Planning = 1,
    Running = 2,
    AwaitingApproval = 3,
    Succeeded = 4,
    Failed = 5,
    Cancelled = 6,
}

/// <summary>
/// A business objective decomposed into a governed task graph, and the aggregate that drives it to
/// completion.
/// <para>
/// The graph must stay acyclic. A cycle would deadlock the orchestrator permanently — every task in
/// the cycle waiting on another that is waiting on it — with no timeout that could distinguish it
/// from slow work. <see cref="AddDependency"/> therefore refuses an edge that would close a loop.
/// </para>
/// </summary>
public sealed class WorkflowRun : AggregateRoot<WorkflowRunId>, IAuditable, ITenantOwned
{
    private readonly List<WorkTask> _tasks = [];

    private WorkflowRun(
        WorkflowRunId id,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string objectiveTitle,
        string objectivePayloadJson,
        Guid correlationId,
        Money actualCost,
        DateTimeOffset? deadline)
        : base(id)
    {
        TenantIdentifier = tenantId;
        WorkspaceId = workspaceId;
        Environment = environment;
        ObjectiveTitle = objectiveTitle;
        ObjectivePayloadJson = objectivePayloadJson;
        CorrelationId = correlationId;
        ActualCost = actualCost;
        Deadline = deadline;
        Status = WorkflowStatus.Planning;
    }

    private WorkflowRun()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public WorkspaceId WorkspaceId { get; private set; }

    public EnvironmentTier Environment { get; private set; }

    public string ObjectiveTitle { get; private set; } = null!;

    public string ObjectivePayloadJson { get; private set; } = null!;

    public WorkflowStatus Status { get; private set; }

    public Money? EstimatedCost { get; private set; }

    public Money ActualCost { get; private set; } = null!;

    public DateTimeOffset? Deadline { get; private set; }

    public Guid CorrelationId { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public IReadOnlyList<WorkTask> Tasks => _tasks.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Result<WorkflowRun> Start(
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string objectiveTitle,
        string objectivePayloadJson,
        Guid correlationId,
        DateTimeOffset now,
        DateTimeOffset? deadline = null,
        string currency = "USD")
    {
        Ensure.NotNullOrWhiteSpace(objectiveTitle);

        if (!Governance.CanonicalJson.IsValidJson(objectivePayloadJson))
        {
            return Result.Failure<WorkflowRun>(Error.Validation(
                "workflow.invalid_objective",
                "The objective payload must be a valid JSON document."));
        }

        if (deadline is not null && deadline.Value <= now)
        {
            return Result.Failure<WorkflowRun>(Error.Validation(
                "workflow.deadline_in_past",
                "A workflow deadline must be in the future."));
        }

        WorkflowRun run = new(
            WorkflowRunId.New(),
            tenantId,
            workspaceId,
            environment,
            objectiveTitle.Trim(),
            objectivePayloadJson,
            correlationId,
            Money.Zero(currency),
            deadline);

        run.Raise(new WorkflowStarted(run.Id, tenantId, workspaceId, environment, objectiveTitle.Trim(), correlationId, now));
        return Result.Success(run);
    }

    public Result<WorkTask> AddTask(string title, string? assignedAgentKey, string inputJson, int maxAttempts = 3)
    {
        if (Status != WorkflowStatus.Planning)
        {
            return Result.Failure<WorkTask>(Error.Conflict(
                "workflow.not_planning",
                "Tasks can only be added while the workflow is still being planned."));
        }

        WorkTask task = WorkTask.Create(Id, _tasks.Count, title, assignedAgentKey, inputJson, maxAttempts);
        _tasks.Add(task);
        return Result.Success(task);
    }

    /// <summary>Declares that <paramref name="taskId"/> cannot begin until <paramref name="dependsOnId"/> succeeds.</summary>
    public Result AddDependency(WorkTaskId taskId, WorkTaskId dependsOnId, TaskDependencyType type = TaskDependencyType.Completion)
    {
        if (Status != WorkflowStatus.Planning)
        {
            return Result.Failure(Error.Conflict(
                "workflow.not_planning",
                "Dependencies can only be added while the workflow is still being planned."));
        }

        WorkTask? task = _tasks.FirstOrDefault(t => t.Id == taskId);
        WorkTask? dependency = _tasks.FirstOrDefault(t => t.Id == dependsOnId);

        if (task is null || dependency is null)
        {
            return Result.Failure(Error.NotFound(
                "workflow.task_not_found",
                "Both tasks must belong to this workflow."));
        }

        if (taskId == dependsOnId)
        {
            return Result.Failure(Error.Invariant(
                "workflow.self_dependency",
                "A task cannot depend on itself."));
        }

        if (WouldCreateCycle(taskId, dependsOnId))
        {
            return Result.Failure(Error.Invariant(
                "workflow.cyclic_dependency",
                "The dependency would create a cycle, which would deadlock the workflow permanently."));
        }

        task.AddDependency(dependsOnId, type);
        return Result.Success();
    }

    /// <summary>Freezes the plan and promotes every task with no unmet dependency to Ready.</summary>
    public Result Activate(Money estimatedCost, DateTimeOffset now)
    {
        if (Status != WorkflowStatus.Planning)
        {
            return Result.Failure(Error.Conflict(
                "workflow.not_planning",
                $"Only a workflow in planning can be activated; this one is {Status}."));
        }

        if (_tasks.Count == 0)
        {
            return Result.Failure(Error.Invariant(
                "workflow.empty_plan",
                "A workflow must contain at least one task."));
        }

        EstimatedCost = estimatedCost;
        Status = WorkflowStatus.Running;
        AdvanceReadiness();
        Raise(new WorkflowActivated(Id, TenantIdentifier, WorkspaceId, _tasks.Count, estimatedCost, now));
        return Result.Success();
    }

    public Result StartTask(WorkTaskId taskId, AgentRunId runId, DateTimeOffset now)
    {
        WorkTask? task = FindTask(taskId);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("workflow.task_not_found", "No such task in this workflow."));
        }

        return task.Start(runId, now);
    }

    public Result CompleteTask(WorkTaskId taskId, string outputJson, Money incurredCost, DateTimeOffset now)
    {
        WorkTask? task = FindTask(taskId);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("workflow.task_not_found", "No such task in this workflow."));
        }

        task.Succeed(outputJson, now);
        ActualCost = ActualCost.Add(incurredCost);
        AdvanceReadiness();
        EvaluateCompletion(now);
        return Result.Success();
    }

    public Result FailTask(WorkTaskId taskId, string reason, Money incurredCost, DateTimeOffset now)
    {
        WorkTask? task = FindTask(taskId);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("workflow.task_not_found", "No such task in this workflow."));
        }

        ActualCost = ActualCost.Add(incurredCost);

        if (task.FailAttempt(reason, now))
        {
            // Retries remain; the task returns to Ready and will be redispatched.
            return Result.Success();
        }

        // A terminally failed task strands everything downstream of it. Marking those Skipped
        // immediately is what stops the workflow sitting in Running forever waiting on work that
        // can never become ready.
        SkipDependents(task.Id, $"Upstream task '{task.Title}' failed.", now);
        EvaluateCompletion(now);
        return Result.Success();
    }

    /// <summary>Halts a task on a control decision, e.g. a compliance hard-fail.</summary>
    public Result BlockTask(WorkTaskId taskId, string reason, DateTimeOffset now)
    {
        WorkTask? task = FindTask(taskId);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("workflow.task_not_found", "No such task in this workflow."));
        }

        task.Block(reason, now);
        SkipDependents(task.Id, $"Upstream task '{task.Title}' was blocked.", now);
        Raise(new WorkflowTaskBlocked(Id, TenantIdentifier, taskId, reason, now));
        EvaluateCompletion(now);
        return Result.Success();
    }

    public Result SuspendTaskForApproval(WorkTaskId taskId)
    {
        WorkTask? task = FindTask(taskId);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("workflow.task_not_found", "No such task in this workflow."));
        }

        task.SuspendForApproval();
        Status = WorkflowStatus.AwaitingApproval;
        return Result.Success();
    }

    public Result ResumeTaskFromApproval(WorkTaskId taskId)
    {
        WorkTask? task = FindTask(taskId);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("workflow.task_not_found", "No such task in this workflow."));
        }

        task.ResumeFromApproval();

        if (_tasks.All(t => t.Status != WorkTaskStatus.AwaitingApproval))
        {
            Status = WorkflowStatus.Running;
        }

        return Result.Success();
    }

    public Result Cancel(string reason, DateTimeOffset now)
    {
        if (Status is WorkflowStatus.Succeeded or WorkflowStatus.Failed or WorkflowStatus.Cancelled)
        {
            return Result.Failure(Error.Conflict("workflow.terminal", $"The workflow is already {Status}."));
        }

        foreach (WorkTask task in _tasks.Where(t => !t.IsTerminal))
        {
            task.Skip("Workflow cancelled.", now);
        }

        Status = WorkflowStatus.Cancelled;
        FailureReason = reason;
        CompletedAt = now;
        Raise(new WorkflowCompleted(Id, TenantIdentifier, WorkspaceId, WorkflowStatus.Cancelled, ActualCost, now));
        return Result.Success();
    }

    public void RecordTaskHeartbeat(WorkTaskId taskId, DateTimeOffset now) => FindTask(taskId)?.Heartbeat(now);

    public IReadOnlyList<WorkTask> ReadyTasks => [.. _tasks.Where(t => t.Status == WorkTaskStatus.Ready)];

    public IReadOnlyList<WorkTask> StalledTasks(DateTimeOffset now, TimeSpan threshold)
        => [.. _tasks.Where(t => t.IsStalled(now, threshold))];

    public WorkTask? FindTask(WorkTaskId taskId) => _tasks.FirstOrDefault(t => t.Id == taskId);

    public void StampCreated(DateTimeOffset at, Guid? by)
    {
        CreatedAt = at;
        CreatedBy = by;
    }

    public void StampUpdated(DateTimeOffset at, Guid? by)
    {
        UpdatedAt = at;
        UpdatedBy = by;
    }

    private void AdvanceReadiness()
    {
        Dictionary<WorkTaskId, WorkTaskStatus> statuses = _tasks.ToDictionary(t => t.Id, t => t.Status);

        foreach (WorkTask task in _tasks)
        {
            task.TryMarkReady(statuses);
        }
    }

    private void SkipDependents(WorkTaskId failedTaskId, string reason, DateTimeOffset now)
    {
        // Breadth-first over the dependency edges, so transitive dependents are reached too.
        Queue<WorkTaskId> frontier = new();
        frontier.Enqueue(failedTaskId);
        HashSet<WorkTaskId> visited = [failedTaskId];

        while (frontier.Count > 0)
        {
            WorkTaskId current = frontier.Dequeue();

            foreach (WorkTask dependent in _tasks.Where(t => t.Dependencies.Any(d => d.DependsOnTaskId == current)))
            {
                if (!visited.Add(dependent.Id) || dependent.IsTerminal)
                {
                    continue;
                }

                dependent.Skip(reason, now);
                frontier.Enqueue(dependent.Id);
            }
        }
    }

    private void EvaluateCompletion(DateTimeOffset now)
    {
        if (Status is WorkflowStatus.Succeeded or WorkflowStatus.Failed or WorkflowStatus.Cancelled)
        {
            return;
        }

        if (_tasks.Any(t => !t.IsTerminal))
        {
            return;
        }

        bool anyUnsuccessful = _tasks.Any(t => t.Status is WorkTaskStatus.Failed or WorkTaskStatus.Blocked);

        Status = anyUnsuccessful ? WorkflowStatus.Failed : WorkflowStatus.Succeeded;
        CompletedAt = now;

        if (anyUnsuccessful)
        {
            FailureReason = _tasks.FirstOrDefault(t => t.Status is WorkTaskStatus.Failed or WorkTaskStatus.Blocked)?.BlockedReason;
        }

        Raise(new WorkflowCompleted(Id, TenantIdentifier, WorkspaceId, Status, ActualCost, now));
    }

    /// <summary>
    /// Depth-first reachability check: adding "task depends on dependsOn" closes a cycle exactly
    /// when <paramref name="taskId"/> is already reachable from <paramref name="dependsOnId"/>.
    /// </summary>
    private bool WouldCreateCycle(WorkTaskId taskId, WorkTaskId dependsOnId)
    {
        Stack<WorkTaskId> stack = new();
        stack.Push(dependsOnId);
        HashSet<WorkTaskId> visited = [];

        while (stack.Count > 0)
        {
            WorkTaskId current = stack.Pop();

            if (current == taskId)
            {
                return true;
            }

            if (!visited.Add(current))
            {
                continue;
            }

            WorkTask? node = _tasks.FirstOrDefault(t => t.Id == current);

            if (node is null)
            {
                continue;
            }

            foreach (TaskDependency edge in node.Dependencies)
            {
                stack.Push(edge.DependsOnTaskId);
            }
        }

        return false;
    }
}
