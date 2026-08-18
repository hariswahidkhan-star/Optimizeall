using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Orchestration;

public enum WorkTaskStatus
{
    /// <summary>Waiting on at least one predecessor.</summary>
    Pending = 1,

    /// <summary>All predecessors satisfied; eligible for dispatch.</summary>
    Ready = 2,

    Running = 3,
    AwaitingApproval = 4,
    Succeeded = 5,
    Failed = 6,
    Skipped = 7,

    /// <summary>Halted by a control the platform will not let the workflow proceed past.</summary>
    Blocked = 8,
}

public enum TaskDependencyType
{
    Completion = 1,
    Approval = 2,
    Data = 3,
}

/// <summary>
/// One node in a workflow's task graph, assigned to exactly one agent.
/// <para>
/// Tasks are entities inside the <see cref="WorkflowRun"/> aggregate, not aggregate roots of their
/// own: readiness depends on the state of sibling tasks, and that invariant is only enforceable if
/// the graph is loaded and saved as one unit.
/// </para>
/// </summary>
public sealed class WorkTask : Entity<WorkTaskId>
{
    private readonly List<TaskDependency> _dependencies = [];

    private WorkTask(
        WorkTaskId id,
        WorkflowRunId workflowRunId,
        int sequence,
        string title,
        string? assignedAgentKey,
        string inputJson,
        int maxAttempts)
        : base(id)
    {
        WorkflowRunId = workflowRunId;
        Sequence = sequence;
        Title = title;
        AssignedAgentKey = assignedAgentKey;
        InputJson = inputJson;
        MaxAttempts = maxAttempts;
        Status = WorkTaskStatus.Pending;
    }

    private WorkTask()
    {
    }

    public WorkflowRunId WorkflowRunId { get; private set; }

    public int Sequence { get; private set; }

    public string Title { get; private set; } = null!;

    /// <summary>Null when the orchestrator found no capable agent and escalated to a human.</summary>
    public string? AssignedAgentKey { get; private set; }

    public WorkTaskStatus Status { get; private set; }

    public string InputJson { get; private set; } = null!;

    public string? OutputJson { get; private set; }

    public AgentRunId? AgentRunId { get; private set; }

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; }

    /// <summary>Refreshed by the executing worker. A stale heartbeat is how a stall is detected.</summary>
    public DateTimeOffset? HeartbeatAt { get; private set; }

    public string? BlockedReason { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyList<TaskDependency> Dependencies => _dependencies.AsReadOnly();

    internal static WorkTask Create(
        WorkflowRunId workflowRunId,
        int sequence,
        string title,
        string? assignedAgentKey,
        string inputJson,
        int maxAttempts = 3)
    {
        Ensure.NotNullOrWhiteSpace(title);
        Ensure.Positive(maxAttempts);

        return new WorkTask(
            WorkTaskId.New(),
            workflowRunId,
            sequence,
            title.Trim(),
            assignedAgentKey?.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson,
            maxAttempts);
    }

    internal void AddDependency(WorkTaskId dependsOn, TaskDependencyType type)
    {
        if (dependsOn == Id)
        {
            throw new InvalidOperationException("A task cannot depend on itself.");
        }

        if (_dependencies.Any(d => d.DependsOnTaskId == dependsOn))
        {
            return;
        }

        _dependencies.Add(new TaskDependency(Id, dependsOn, type));
    }

    /// <summary>
    /// Promotes the task to <see cref="WorkTaskStatus.Ready"/> when every predecessor has succeeded.
    /// A predecessor that failed or was skipped does not satisfy a dependency — proceeding on
    /// partial input is how a workflow produces confidently wrong output.
    /// </summary>
    internal bool TryMarkReady(IReadOnlyDictionary<WorkTaskId, WorkTaskStatus> statuses)
    {
        if (Status != WorkTaskStatus.Pending)
        {
            return false;
        }

        foreach (TaskDependency dependency in _dependencies)
        {
            if (!statuses.TryGetValue(dependency.DependsOnTaskId, out WorkTaskStatus predecessor)
                || predecessor != WorkTaskStatus.Succeeded)
            {
                return false;
            }
        }

        Status = WorkTaskStatus.Ready;
        return true;
    }

    internal Result Start(AgentRunId runId, DateTimeOffset now)
    {
        if (Status != WorkTaskStatus.Ready)
        {
            return Result.Failure(Error.Conflict(
                "task.not_ready",
                $"Only a ready task can start; this one is {Status}."));
        }

        Status = WorkTaskStatus.Running;
        AgentRunId = runId;
        AttemptCount++;
        StartedAt ??= now;
        HeartbeatAt = now;
        return Result.Success();
    }

    internal void Heartbeat(DateTimeOffset now) => HeartbeatAt = now;

    internal void SuspendForApproval() => Status = WorkTaskStatus.AwaitingApproval;

    internal void ResumeFromApproval() => Status = WorkTaskStatus.Running;

    internal void Succeed(string outputJson, DateTimeOffset now)
    {
        Status = WorkTaskStatus.Succeeded;
        OutputJson = outputJson;
        CompletedAt = now;
    }

    /// <summary>
    /// Records a failed attempt. Returns to <see cref="WorkTaskStatus.Ready"/> while retries remain,
    /// so the orchestrator can redispatch without special-casing retry state.
    /// </summary>
    internal bool FailAttempt(string reason, DateTimeOffset now)
    {
        if (AttemptCount < MaxAttempts)
        {
            Status = WorkTaskStatus.Ready;
            BlockedReason = reason;
            return true;
        }

        Status = WorkTaskStatus.Failed;
        BlockedReason = reason;
        CompletedAt = now;
        return false;
    }

    internal void Block(string reason, DateTimeOffset now)
    {
        Status = WorkTaskStatus.Blocked;
        BlockedReason = Ensure.NotNullOrWhiteSpace(reason);
        CompletedAt = now;
    }

    internal void Skip(string reason, DateTimeOffset now)
    {
        Status = WorkTaskStatus.Skipped;
        BlockedReason = reason;
        CompletedAt = now;
    }

    /// <summary>A running task whose heartbeat has lapsed is presumed abandoned by its worker.</summary>
    public bool IsStalled(DateTimeOffset now, TimeSpan threshold)
        => Status == WorkTaskStatus.Running
            && HeartbeatAt is not null
            && now - HeartbeatAt.Value > threshold;

    public bool IsTerminal => Status is WorkTaskStatus.Succeeded
        or WorkTaskStatus.Failed
        or WorkTaskStatus.Skipped
        or WorkTaskStatus.Blocked;
}

/// <summary>An edge in the task graph.</summary>
public sealed record TaskDependency(WorkTaskId TaskId, WorkTaskId DependsOnTaskId, TaskDependencyType Type);
