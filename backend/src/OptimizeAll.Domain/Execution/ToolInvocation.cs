using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Execution;

public enum ToolInvocationStatus
{
    /// <summary>Authorised and ready to execute — no approval was required.</summary>
    Authorised = 1,

    /// <summary>Refused. The agent lacked the grant, or the kill switch was engaged.</summary>
    Denied = 2,

    /// <summary>Blocked on a human decision.</summary>
    AwaitingApproval = 3,

    Executed = 4,
    Failed = 5,

    /// <summary>Recorded but not applied, because the run was a dry run.</summary>
    Skipped = 6,
}

/// <summary>
/// One attempt by an agent to use a capability.
/// <para>
/// The model's output is a <em>request</em> to call a tool, never a decision that it may. This
/// record is created before anything executes, and its status carries the platform's decision — so
/// a denied call is as durably recorded as a successful one.
/// </para>
/// </summary>
public sealed class ToolInvocation : Entity<ToolInvocationId>
{
    private ToolInvocation(
        ToolInvocationId id,
        AgentRunId runId,
        string toolKey,
        ActionRiskClass riskClass,
        string argumentsJson,
        DateTimeOffset requestedAt)
        : base(id)
    {
        AgentRunId = runId;
        ToolKey = toolKey;
        RiskClass = riskClass;
        ArgumentsJson = argumentsJson;
        RequestedAt = requestedAt;
    }

    private ToolInvocation()
    {
    }

    public AgentRunId AgentRunId { get; private set; }

    public string ToolKey { get; private set; } = null!;

    public ActionRiskClass RiskClass { get; private set; }

    /// <summary>Arguments as proposed, after redaction policy has been applied.</summary>
    public string ArgumentsJson { get; private set; } = null!;

    public string? ResultJson { get; private set; }

    public ToolInvocationStatus Status { get; private set; }

    public string? DenialReason { get; private set; }

    public ApprovalRequestId? ApprovalRequestId { get; private set; }

    /// <summary>
    /// Stable key for de-duplicating an external effect across retries. Without it, a retry after an
    /// ambiguous timeout would send the same email twice.
    /// </summary>
    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public int? DurationMilliseconds { get; private set; }

    public static Result<ToolInvocation> Propose(
        AgentRunId runId,
        string toolKey,
        string argumentsJson,
        DateTimeOffset requestedAt)
    {
        Ensure.NotNullOrWhiteSpace(toolKey);

        if (!AgentCatalog.ToolRegistry.TryGetRisk(toolKey, out ActionRiskClass risk))
        {
            return Result.Failure<ToolInvocation>(Error.Validation(
                "tool.unknown",
                $"'{toolKey}' is not a registered tool."));
        }

        if (!Governance.CanonicalJson.IsValidJson(argumentsJson))
        {
            return Result.Failure<ToolInvocation>(Error.Validation(
                "tool.invalid_arguments",
                "Tool arguments must be a valid JSON document."));
        }

        return Result.Success(new ToolInvocation(
            ToolInvocationId.New(), runId, toolKey.Trim(), risk, argumentsJson, requestedAt));
    }

    public void Authorise() => Status = ToolInvocationStatus.Authorised;

    public void Deny(string reason, DateTimeOffset at)
    {
        Status = ToolInvocationStatus.Denied;
        DenialReason = Ensure.NotNullOrWhiteSpace(reason);
        CompletedAt = at;
    }

    public void GateOnApproval(ApprovalRequestId approvalRequestId)
    {
        Status = ToolInvocationStatus.AwaitingApproval;
        ApprovalRequestId = approvalRequestId;
    }

    public void AssignIdempotencyKey(string key) => IdempotencyKey = Ensure.NotNullOrWhiteSpace(key);

    public void Complete(string resultJson, DateTimeOffset at, int durationMilliseconds)
    {
        Status = ToolInvocationStatus.Executed;
        ResultJson = resultJson;
        CompletedAt = at;
        DurationMilliseconds = durationMilliseconds;
    }

    public void FailWith(string errorJson, DateTimeOffset at, int durationMilliseconds)
    {
        Status = ToolInvocationStatus.Failed;
        ResultJson = errorJson;
        CompletedAt = at;
        DurationMilliseconds = durationMilliseconds;
    }

    /// <summary>Marks a dry-run invocation: fully recorded, never applied.</summary>
    public void SkipAsDryRun(DateTimeOffset at)
    {
        Status = ToolInvocationStatus.Skipped;
        ResultJson = """{"dryRun":true,"applied":false}""";
        CompletedAt = at;
    }

    public bool RequiresApproval => RiskClass.RequiresHumanApproval();
}
