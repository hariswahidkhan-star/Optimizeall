using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Governance;

/// <summary>
/// One approver's recorded judgement. Immutable once written: an approval history that can be
/// edited is not evidence of anything.
/// </summary>
public sealed class ApprovalDecision : Entity<Guid>
{
    private ApprovalDecision(
        Guid id,
        ApprovalRequestId requestId,
        UserId approverUserId,
        ApprovalDecisionKind decision,
        string? rationale,
        bool stepUpVerified,
        DateTimeOffset decidedAt)
        : base(id)
    {
        ApprovalRequestId = requestId;
        ApproverUserId = approverUserId.Value;
        Decision = decision;
        Rationale = rationale;
        StepUpVerified = stepUpVerified;
        DecidedAt = decidedAt;
    }

    private ApprovalDecision()
    {
    }

    public ApprovalRequestId ApprovalRequestId { get; private set; }

    public Guid ApproverUserId { get; private set; }

    public ApprovalDecisionKind Decision { get; private set; }

    /// <summary>Mandatory on rejection, optional on approval.</summary>
    public string? Rationale { get; private set; }

    public bool StepUpVerified { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    internal static ApprovalDecision Create(
        ApprovalRequestId requestId,
        UserId approverUserId,
        ApprovalDecisionKind decision,
        string? rationale,
        bool stepUpVerified,
        DateTimeOffset decidedAt)
        => new(
            Guid.CreateVersion7(),
            requestId,
            approverUserId,
            decision,
            string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim(),
            stepUpVerified,
            decidedAt);
}
