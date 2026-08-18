using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Execution;

public enum AgentRunStatus
{
    Queued = 1,
    Running = 2,

    /// <summary>Suspended on a human decision. Holds no worker and no connection while it waits.</summary>
    AwaitingApproval = 3,

    Succeeded = 4,
    Failed = 5,
    Cancelled = 6,
    TimedOut = 7,
    BudgetExceeded = 8,
}

public enum RunTriggerType
{
    Manual = 1,
    Schedule = 2,
    Workflow = 3,
    Event = 4,
}

/// <summary>
/// One bounded execution of one agent definition version.
/// <para>
/// The lifecycle is deliberately explicit about suspension. An agent that proposes a gated action
/// releases its worker and becomes a row in the database until a human decides; hour-long approval
/// waits then cost nothing but a row, which is what makes mandatory gating affordable at scale.
/// </para>
/// </summary>
public sealed class AgentRun : AggregateRoot<AgentRunId>, IAuditable, ITenantOwned
{
    private readonly List<AgentRunStep> _steps = [];
    private readonly List<ToolInvocation> _toolInvocations = [];

    private AgentRun(
        AgentRunId id,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        AgentDefinitionId agentDefinitionId,
        string agentKey,
        RunTriggerType triggerType,
        Guid? triggeredBy,
        string inputJson,
        Guid correlationId,
        bool isDryRun,
        string currency)
        : base(id)
    {
        TenantIdentifier = tenantId;
        WorkspaceId = workspaceId;
        Environment = environment;
        AgentDefinitionId = agentDefinitionId;
        AgentKey = agentKey;
        TriggerType = triggerType;
        TriggeredBy = triggeredBy;
        InputJson = inputJson;
        CorrelationId = correlationId;
        IsDryRun = isDryRun;
        Budget = RunBudgetLedger.Empty(currency);
        Status = AgentRunStatus.Queued;
    }

    private AgentRun()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public WorkspaceId WorkspaceId { get; private set; }

    public EnvironmentTier Environment { get; private set; }

    public AgentDefinitionId AgentDefinitionId { get; private set; }

    public string AgentKey { get; private set; } = null!;

    public WorkTaskId? TaskId { get; private set; }

    public AgentRunStatus Status { get; private set; }

    public RunTriggerType TriggerType { get; private set; }

    public Guid? TriggeredBy { get; private set; }

    public string InputJson { get; private set; } = null!;

    public string? OutputJson { get; private set; }

    /// <summary>Structured failure detail. Never a bare message string — errors must be queryable.</summary>
    public string? ErrorJson { get; private set; }

    /// <summary>The provider that actually served the run, which may differ from the policy's primary after failover.</summary>
    public AiProvider? Provider { get; private set; }

    public string? Model { get; private set; }

    public RunBudgetLedger Budget { get; private set; } = null!;

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Identity of the worker currently holding this run, or null when unleased.</summary>
    public string? LeaseHolder { get; private set; }

    /// <summary>
    /// When the lease lapses and the run may be reclaimed. A worker that dies without releasing its
    /// lease is recovered by expiry rather than by an operator noticing.
    /// </summary>
    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public ApprovalRequestId? BlockingApprovalId { get; private set; }

    public Guid CorrelationId { get; private set; }

    /// <summary>When true, external-effect tools are stubbed: recorded, never applied.</summary>
    public bool IsDryRun { get; private set; }

    public IReadOnlyList<AgentRunStep> Steps => _steps.AsReadOnly();

    public IReadOnlyList<ToolInvocation> ToolInvocations => _toolInvocations.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Result<AgentRun> Queue(
        AgentDefinition definition,
        EnvironmentTier environment,
        RunTriggerType triggerType,
        Guid? triggeredBy,
        string inputJson,
        Guid correlationId,
        DateTimeOffset now,
        bool isDryRun = false,
        WorkTaskId? taskId = null,
        string currency = "USD")
    {
        Ensure.NotNull(definition);

        if (!definition.IsExecutable)
        {
            return Result.Failure<AgentRun>(Error.Conflict(
                "run.definition_not_executable",
                $"Agent '{definition.AgentKey}' version {definition.DefinitionVersion} is {definition.Status} and cannot be run."));
        }

        if (!Governance.CanonicalJson.IsValidJson(inputJson))
        {
            return Result.Failure<AgentRun>(Error.Validation(
                "run.invalid_input",
                "Run input must be a valid JSON document."));
        }

        AgentRun run = new(
            AgentRunId.New(),
            definition.TenantIdentifier,
            definition.WorkspaceId,
            environment,
            definition.Id,
            definition.AgentKey,
            triggerType,
            triggeredBy,
            inputJson,
            correlationId,
            isDryRun,
            currency)
        {
            TaskId = taskId,
        };

        run.Raise(new AgentRunQueued(
            run.Id, run.TenantIdentifier, run.WorkspaceId, environment, definition.AgentKey, correlationId, now));

        return Result.Success(run);
    }

    /// <summary>
    /// Claims the run for a worker. Succeeds only from <see cref="AgentRunStatus.Queued"/> or from
    /// an expired lease, which is what makes reclamation of a dead worker's run safe: a live lease
    /// can never be stolen, and an expired one is by definition abandoned.
    /// </summary>
    public Result AcquireLease(string workerId, TimeSpan leaseDuration, DateTimeOffset now)
    {
        Ensure.NotNullOrWhiteSpace(workerId);

        bool leaseIsFree = LeaseExpiresAt is null || now >= LeaseExpiresAt.Value;

        if (!leaseIsFree)
        {
            return Result.Failure(Error.Conflict(
                "run.lease_held",
                $"The run is leased to '{LeaseHolder}' until {LeaseExpiresAt:O}."));
        }

        if (Status is not (AgentRunStatus.Queued or AgentRunStatus.Running or AgentRunStatus.AwaitingApproval))
        {
            return Result.Failure(Error.Conflict(
                "run.terminal",
                $"A run in state {Status} cannot be leased."));
        }

        LeaseHolder = workerId;
        LeaseExpiresAt = now.Add(leaseDuration);

        if (Status == AgentRunStatus.Queued)
        {
            Status = AgentRunStatus.Running;
            StartedAt = now;
            Raise(new AgentRunStarted(Id, TenantIdentifier, WorkspaceId, AgentKey, workerId, now));
        }

        return Result.Success();
    }

    public Result RenewLease(string workerId, TimeSpan leaseDuration, DateTimeOffset now)
    {
        if (!string.Equals(LeaseHolder, workerId, StringComparison.Ordinal))
        {
            return Result.Failure(Error.Conflict(
                "run.lease_not_held",
                "Only the current lease holder can renew the lease."));
        }

        LeaseExpiresAt = now.Add(leaseDuration);
        return Result.Success();
    }

    public void ReleaseLease()
    {
        LeaseHolder = null;
        LeaseExpiresAt = null;
    }

    public void RecordProviderRouting(AiProvider provider, string model)
    {
        Provider = provider;
        Model = Ensure.NotNullOrWhiteSpace(model);
    }

    public void AppendStep(AgentRunStep step)
    {
        Ensure.NotNull(step);
        _steps.Add(step);
    }

    public void RecordCompletionUsage(long promptTokens, long completionTokens, Money cost)
        => Budget = Budget.RecordCompletion(promptTokens, completionTokens, cost);

    /// <summary>
    /// Attaches a proposed tool call to the run's record. Deliberately does not consume budget:
    /// the call has not been authorised yet, and a denied call must not spend the agent's
    /// allowance — otherwise an agent could exhaust its own budget on calls it was never permitted
    /// to make.
    /// </summary>
    public ToolInvocation AttachToolInvocation(ToolInvocation invocation)
    {
        Ensure.NotNull(invocation);
        _toolInvocations.Add(invocation);
        return invocation;
    }

    /// <summary>Consumes one unit of the tool-call budget. Called only once a call is authorised to proceed.</summary>
    public void CountToolCall() => Budget = Budget.RecordToolCall();

    /// <summary>
    /// Suspends the run pending a human decision and releases the worker. The lease is dropped
    /// deliberately: holding it would keep a worker slot occupied for the whole approval wait.
    /// </summary>
    public Result SuspendForApproval(ApprovalRequestId approvalRequestId, DateTimeOffset now)
    {
        if (Status != AgentRunStatus.Running)
        {
            return Result.Failure(Error.Conflict(
                "run.not_running",
                $"Only a running run can be suspended; this one is {Status}."));
        }

        Status = AgentRunStatus.AwaitingApproval;
        BlockingApprovalId = approvalRequestId;
        ReleaseLease();
        Raise(new AgentRunSuspended(Id, TenantIdentifier, WorkspaceId, approvalRequestId, now));
        return Result.Success();
    }

    public Result Resume(DateTimeOffset now)
    {
        if (Status != AgentRunStatus.AwaitingApproval)
        {
            return Result.Failure(Error.Conflict(
                "run.not_suspended",
                $"Only a suspended run can be resumed; this one is {Status}."));
        }

        Status = AgentRunStatus.Queued;
        BlockingApprovalId = null;
        Raise(new AgentRunResumed(Id, TenantIdentifier, WorkspaceId, now));
        return Result.Success();
    }

    public Result Succeed(string outputJson, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Failure(Error.Conflict("run.terminal", $"The run is already {Status}."));
        }

        if (!Governance.CanonicalJson.IsValidJson(outputJson))
        {
            return Result.Failure(Error.Validation(
                "run.invalid_output",
                "Run output must be a valid JSON document."));
        }

        OutputJson = outputJson;
        Transition(AgentRunStatus.Succeeded, now);
        Raise(new AgentRunCompleted(
            Id, TenantIdentifier, WorkspaceId, AgentKey, AgentRunStatus.Succeeded, Budget.CostIncurred, now));
        return Result.Success();
    }

    public Result Fail(string errorJson, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Failure(Error.Conflict("run.terminal", $"The run is already {Status}."));
        }

        ErrorJson = Governance.CanonicalJson.IsValidJson(errorJson)
            ? errorJson
            : """{"code":"run.unstructured_error","message":"The failure detail was not valid JSON."}""";

        Transition(AgentRunStatus.Failed, now);
        Raise(new AgentRunCompleted(
            Id, TenantIdentifier, WorkspaceId, AgentKey, AgentRunStatus.Failed, Budget.CostIncurred, now));
        return Result.Success();
    }

    /// <summary>Terminates the run because a budget ceiling would otherwise be crossed.</summary>
    public Result AbortForBudget(Error budgetError, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Failure(Error.Conflict("run.terminal", $"The run is already {Status}."));
        }

        ErrorJson = $$"""{"code":"{{budgetError.Code}}","message":{{System.Text.Json.JsonSerializer.Serialize(budgetError.Message)}}}""";
        Transition(AgentRunStatus.BudgetExceeded, now);
        Raise(new AgentRunCompleted(
            Id, TenantIdentifier, WorkspaceId, AgentKey, AgentRunStatus.BudgetExceeded, Budget.CostIncurred, now));
        return Result.Success();
    }

    public Result Cancel(string reason, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Failure(Error.Conflict("run.terminal", $"The run is already {Status}."));
        }

        ErrorJson = $$"""{"code":"run.cancelled","message":{{System.Text.Json.JsonSerializer.Serialize(reason)}}}""";
        Transition(AgentRunStatus.Cancelled, now);
        Raise(new AgentRunCompleted(
            Id, TenantIdentifier, WorkspaceId, AgentKey, AgentRunStatus.Cancelled, Budget.CostIncurred, now));
        return Result.Success();
    }

    public Result TimeOut(DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Failure(Error.Conflict("run.terminal", $"The run is already {Status}."));
        }

        ErrorJson = """{"code":"run.timed_out","message":"The run exceeded its wall-clock budget."}""";
        Transition(AgentRunStatus.TimedOut, now);
        Raise(new AgentRunCompleted(
            Id, TenantIdentifier, WorkspaceId, AgentKey, AgentRunStatus.TimedOut, Budget.CostIncurred, now));
        return Result.Success();
    }

    public bool IsTerminal => Status is AgentRunStatus.Succeeded
        or AgentRunStatus.Failed
        or AgentRunStatus.Cancelled
        or AgentRunStatus.TimedOut
        or AgentRunStatus.BudgetExceeded;

    public TimeSpan Elapsed(DateTimeOffset now) => StartedAt is null
        ? TimeSpan.Zero
        : (CompletedAt ?? now) - StartedAt.Value;

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

    private void Transition(AgentRunStatus status, DateTimeOffset now)
    {
        Status = status;
        CompletedAt = now;
        ReleaseLease();
    }
}
