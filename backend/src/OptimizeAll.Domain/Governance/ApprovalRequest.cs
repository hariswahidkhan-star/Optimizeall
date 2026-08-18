using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Governance;

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4,
    Cancelled = 5,
}

public enum ApprovalDecisionKind
{
    Approve = 1,
    Reject = 2,
}

/// <summary>
/// A blocking request for human sign-off on a specific, immutable payload.
/// <para>
/// Four invariants live here, and all four are enforced by this aggregate rather than by the UI or
/// the API layer, because those are the parts most likely to grow a convenient shortcut:
/// </para>
/// <list type="number">
///   <item>An agent can never decide. Approval authority is human-only.</item>
///   <item>The requester can never decide their own request.</item>
///   <item>One approver contributes at most one decision.</item>
///   <item>Execution is refused unless the payload still hashes to the approved fingerprint.</item>
/// </list>
/// </summary>
public sealed class ApprovalRequest : AggregateRoot<ApprovalRequestId>, IAuditable, ITenantOwned
{
    private readonly List<ApprovalDecision> _decisions = [];

    private ApprovalRequest(
        ApprovalRequestId id,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        ActionRiskClass riskClass,
        string title,
        string payloadJson,
        PayloadFingerprint fingerprint,
        PrincipalRef requestedBy,
        int requiredApproverCount,
        string requiredRoleKey,
        DateTimeOffset expiresAt,
        Money? estimatedCost)
        : base(id)
    {
        TenantIdentifier = tenantId;
        WorkspaceId = workspaceId;
        Environment = environment;
        RiskClass = riskClass;
        Title = title;
        PayloadJson = payloadJson;
        PayloadFingerprint = fingerprint;
        RequestedByType = requestedBy.Type;
        RequestedById = requestedBy.Id;
        RequiredApproverCount = requiredApproverCount;
        RequiredRoleKey = requiredRoleKey;
        ExpiresAt = expiresAt;
        EstimatedCost = estimatedCost;
        Status = ApprovalStatus.Pending;
    }

    private ApprovalRequest()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public WorkspaceId WorkspaceId { get; private set; }

    public EnvironmentTier Environment { get; private set; }

    public AgentRunId? AgentRunId { get; private set; }

    public ToolInvocationId? ToolInvocationId { get; private set; }

    public ActionRiskClass RiskClass { get; private set; }

    public string Title { get; private set; } = null!;

    /// <summary>Exactly what will execute. Shown to the approver verbatim.</summary>
    public string PayloadJson { get; private set; } = null!;

    public PayloadFingerprint PayloadFingerprint { get; private set; } = null!;

    public Money? EstimatedCost { get; private set; }

    public PrincipalType RequestedByType { get; private set; }

    public Guid RequestedById { get; private set; }

    public int RequiredApproverCount { get; private set; }

    public string RequiredRoleKey { get; private set; } = null!;

    public ApprovalStatus Status { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public IReadOnlyList<ApprovalDecision> Decisions => _decisions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Result<ApprovalRequest> Raise(
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        ActionRiskClass riskClass,
        string title,
        string payloadJson,
        PrincipalRef requestedBy,
        ApprovalRequirement requirement,
        DateTimeOffset now,
        Money? estimatedCost = null,
        AgentRunId? agentRunId = null,
        ToolInvocationId? toolInvocationId = null)
    {
        Ensure.NotNullOrWhiteSpace(title);
        Ensure.NotNull(requirement);

        if (!requirement.IsRequired)
        {
            return Result.Failure<ApprovalRequest>(Error.Invariant(
                "approval.not_required",
                "An approval request must not be raised for an action that does not require approval."));
        }

        if (!CanonicalJson.IsValidJson(payloadJson))
        {
            return Result.Failure<ApprovalRequest>(Error.Validation(
                "approval.invalid_payload",
                "The payload must be a valid JSON document so that it can be canonically fingerprinted."));
        }

        PayloadFingerprint fingerprint = PayloadFingerprint.Compute(payloadJson);

        ApprovalRequest request = new(
            ApprovalRequestId.New(),
            tenantId,
            workspaceId,
            environment,
            riskClass,
            title.Trim(),
            payloadJson,
            fingerprint,
            requestedBy,
            requirement.ApproverCount,
            requirement.RequiredRoleKey!,
            now.Add(requirement.Expiry),
            estimatedCost)
        {
            AgentRunId = agentRunId,
            ToolInvocationId = toolInvocationId,
        };

        request.Raise(new ApprovalRequested(
            request.Id, tenantId, workspaceId, environment, riskClass, title.Trim(), requestedBy, request.ExpiresAt, now));

        return Result.Success(request);
    }

    /// <summary>
    /// Records one approver's decision.
    /// <para>
    /// <paramref name="approver"/> must be a human principal and must not be the requester. Both
    /// checks are here rather than in the calling service so that no future call path can omit them.
    /// </para>
    /// </summary>
    public Result Decide(
        PrincipalRef approver,
        ApprovalDecisionKind decision,
        string? rationale,
        bool stepUpVerified,
        DateTimeOffset now)
    {
        if (!approver.IsHuman)
        {
            return Result.Failure(Error.Forbidden(
                "approval.non_human_approver",
                "Only a human principal can decide an approval request."));
        }

        if (approver.Id == RequestedById)
        {
            return Result.Failure(Error.Forbidden(
                "approval.self_approval",
                "The principal that requested an action cannot decide it."));
        }

        if (Status == ApprovalStatus.Pending && now >= ExpiresAt)
        {
            // Expiry is evaluated on access rather than by a sweeper alone, so a decision can never
            // land on a request that has already lapsed, whatever the background job is doing.
            Expire(now);
            return Result.Failure(Error.Conflict(
                "approval.expired",
                "This approval request has expired and must be raised again."));
        }

        if (Status != ApprovalStatus.Pending)
        {
            return Result.Failure(Error.Conflict(
                "approval.already_resolved",
                $"This approval request is already {Status}."));
        }

        if (decision == ApprovalDecisionKind.Reject && string.IsNullOrWhiteSpace(rationale))
        {
            return Result.Failure(Error.Validation(
                "approval.rationale_required",
                "Rejecting an action requires a recorded rationale."));
        }

        if (RequiresStepUp && !stepUpVerified)
        {
            return Result.Failure(Error.Forbidden(
                "approval.step_up_required",
                $"Deciding a {RiskClass} action requires step-up authentication."));
        }

        if (_decisions.Any(d => d.ApproverUserId == approver.Id))
        {
            return Result.Failure(Error.Conflict(
                "approval.duplicate_decision",
                "This approver has already decided this request."));
        }

        _decisions.Add(ApprovalDecision.Create(Id, UserId.From(approver.Id), decision, rationale, stepUpVerified, now));

        if (decision == ApprovalDecisionKind.Reject)
        {
            // One rejection is decisive. Requiring unanimity to reject would let a risky action
            // proceed because a second reviewer never got round to it.
            Status = ApprovalStatus.Rejected;
            ResolvedAt = now;
            Raise(new ApprovalRejected(Id, TenantIdentifier, WorkspaceId, approver, rationale!, now));
            return Result.Success();
        }

        int approvals = _decisions.Count(d => d.Decision == ApprovalDecisionKind.Approve);

        if (approvals >= RequiredApproverCount)
        {
            Status = ApprovalStatus.Approved;
            ResolvedAt = now;
            Raise(new ApprovalGranted(Id, TenantIdentifier, WorkspaceId, PayloadFingerprint.Value, approvals, now));
        }

        return Result.Success();
    }

    public Result Cancel(string reason, DateTimeOffset now)
    {
        if (Status != ApprovalStatus.Pending)
        {
            return Result.Failure(Error.Conflict(
                "approval.already_resolved",
                $"This approval request is already {Status}."));
        }

        Status = ApprovalStatus.Cancelled;
        ResolvedAt = now;
        Raise(new ApprovalCancelled(Id, TenantIdentifier, reason, now));
        return Result.Success();
    }

    /// <summary>Fails closed: an unresolved request that has run out of time counts as refused.</summary>
    public void Expire(DateTimeOffset now)
    {
        if (Status != ApprovalStatus.Pending)
        {
            return;
        }

        Status = ApprovalStatus.Expired;
        ResolvedAt = now;
        Raise(new ApprovalExpired(Id, TenantIdentifier, WorkspaceId, now));
    }

    /// <summary>
    /// The gate consulted immediately before execution. Every condition must hold: approved,
    /// unexpired, and hashing to the fingerprint the approvers actually saw.
    /// </summary>
    public Result AuthoriseExecution(string payloadAboutToExecute, DateTimeOffset now)
    {
        if (Status != ApprovalStatus.Approved)
        {
            return Result.Failure(Error.Forbidden(
                "approval.not_approved",
                $"Execution requires an approved request; this one is {Status}."));
        }

        if (now >= ExpiresAt)
        {
            return Result.Failure(Error.Forbidden(
                "approval.expired",
                "The approval window has closed. The action must be approved again."));
        }

        if (!CanonicalJson.IsValidJson(payloadAboutToExecute))
        {
            return Result.Failure(Error.Validation(
                "approval.invalid_payload",
                "The payload presented for execution is not valid JSON."));
        }

        PayloadFingerprint presented = PayloadFingerprint.Compute(payloadAboutToExecute);

        if (!PayloadFingerprint.Matches(presented))
        {
            return Result.Failure(Error.Forbidden(
                "approval.payload_mismatch",
                "The payload presented for execution differs from the payload that was approved."));
        }

        return Result.Success();
    }

    /// <summary>Higher-risk decisions require the approver to re-authenticate.</summary>
    public bool RequiresStepUp => RiskClass is ActionRiskClass.Financial or ActionRiskClass.Irreversible;

    public int ApprovalsReceived => _decisions.Count(d => d.Decision == ApprovalDecisionKind.Approve);

    public bool IsPending(DateTimeOffset now) => Status == ApprovalStatus.Pending && now < ExpiresAt;

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
}
