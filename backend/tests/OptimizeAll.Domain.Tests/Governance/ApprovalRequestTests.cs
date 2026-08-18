using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Governance;
using OptimizeAll.SharedKernel.Results;
using Xunit;

namespace OptimizeAll.Domain.Tests.Governance;

/// <summary>
/// The approval invariants are the platform's central safety property. Each of these tests
/// corresponds to a way a real system quietly loses that property.
/// </summary>
public sealed class ApprovalRequestTests
{
    private const string Payload = """{"channel":"blog","title":"Launch post","body":"Hello world"}""";

    private static Result<ApprovalRequest> Raise(
        ApprovalRequirement? requirement = null,
        ActionRiskClass risk = ActionRiskClass.External,
        string payload = Payload)
        => ApprovalRequest.Raise(
            TestData.Tenant,
            TestData.Workspace,
            EnvironmentTier.Production,
            risk,
            "Publish launch post",
            payload,
            TestData.Agent,
            requirement ?? TestData.SingleApprover(),
            TestData.Now);

    [Fact]
    public void Raise_rejects_a_request_for_an_action_that_needs_no_approval()
    {
        Result<ApprovalRequest> result = ApprovalRequest.Raise(
            TestData.Tenant,
            TestData.Workspace,
            EnvironmentTier.Production,
            ActionRiskClass.Read,
            "Read something",
            Payload,
            TestData.Agent,
            ApprovalRequirement.None,
            TestData.Now);

        Assert.True(result.IsFailure);
        Assert.Equal("approval.not_required", result.Error.Code);
    }

    [Fact]
    public void Raise_rejects_a_payload_that_is_not_valid_json()
    {
        Result<ApprovalRequest> result = Raise(payload: "not json at all");

        Assert.True(result.IsFailure);
        Assert.Equal("approval.invalid_payload", result.Error.Code);
    }

    [Fact]
    public void An_agent_can_never_approve()
    {
        ApprovalRequest request = Raise().Value;

        Result decision = request.Decide(
            new PrincipalRef(PrincipalType.Agent, Guid.NewGuid()),
            ApprovalDecisionKind.Approve,
            rationale: null,
            stepUpVerified: true,
            TestData.Now);

        Assert.True(decision.IsFailure);
        Assert.Equal("approval.non_human_approver", decision.Error.Code);
        Assert.Equal(ApprovalStatus.Pending, request.Status);
    }

    [Fact]
    public void A_requester_can_never_approve_their_own_request()
    {
        Result<ApprovalRequest> raised = ApprovalRequest.Raise(
            TestData.Tenant,
            TestData.Workspace,
            EnvironmentTier.Production,
            ActionRiskClass.External,
            "Publish launch post",
            Payload,
            TestData.Alice,
            TestData.SingleApprover(),
            TestData.Now);

        Result decision = raised.Value.Decide(
            TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now);

        Assert.True(decision.IsFailure);
        Assert.Equal("approval.self_approval", decision.Error.Code);
    }

    [Fact]
    public void One_approver_cannot_satisfy_a_two_approver_requirement_by_deciding_twice()
    {
        ApprovalRequest request = Raise(TestData.TwoApprovers(), ActionRiskClass.Irreversible).Value;

        Result first = request.Decide(TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now);
        Result second = request.Decide(TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal("approval.duplicate_decision", second.Error.Code);
        Assert.Equal(ApprovalStatus.Pending, request.Status);
    }

    [Fact]
    public void Two_distinct_approvers_satisfy_a_two_approver_requirement()
    {
        ApprovalRequest request = Raise(TestData.TwoApprovers(), ActionRiskClass.Irreversible).Value;

        request.Decide(TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now);
        request.Decide(TestData.Bob, ApprovalDecisionKind.Approve, null, true, TestData.Now);

        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.Equal(2, request.ApprovalsReceived);
    }

    [Fact]
    public void A_single_rejection_is_decisive_even_when_more_approvers_are_required()
    {
        ApprovalRequest request = Raise(TestData.TwoApprovers(), ActionRiskClass.Irreversible).Value;

        request.Decide(TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now);
        request.Decide(TestData.Bob, ApprovalDecisionKind.Reject, "Wrong audience.", true, TestData.Now);

        Assert.Equal(ApprovalStatus.Rejected, request.Status);
    }

    [Fact]
    public void Rejection_requires_a_rationale()
    {
        ApprovalRequest request = Raise().Value;

        Result decision = request.Decide(TestData.Alice, ApprovalDecisionKind.Reject, "   ", false, TestData.Now);

        Assert.True(decision.IsFailure);
        Assert.Equal("approval.rationale_required", decision.Error.Code);
    }

    [Fact]
    public void Financial_and_irreversible_decisions_require_step_up_authentication()
    {
        ApprovalRequest request = Raise(risk: ActionRiskClass.Financial).Value;

        Result decision = request.Decide(
            TestData.Alice, ApprovalDecisionKind.Approve, null, stepUpVerified: false, TestData.Now);

        Assert.True(decision.IsFailure);
        Assert.Equal("approval.step_up_required", decision.Error.Code);
    }

    [Fact]
    public void An_expired_request_fails_closed_rather_than_remaining_decidable()
    {
        ApprovalRequest request = Raise(TestData.SingleApprover(TimeSpan.FromHours(1))).Value;

        Result decision = request.Decide(
            TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now.AddHours(2));

        Assert.True(decision.IsFailure);
        Assert.Equal("approval.expired", decision.Error.Code);
        Assert.Equal(ApprovalStatus.Expired, request.Status);
    }

    [Fact]
    public void Execution_is_authorised_only_for_the_exact_payload_that_was_approved()
    {
        ApprovalRequest request = Raise().Value;
        request.Decide(TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now);

        Result authorised = request.AuthoriseExecution(Payload, TestData.Now);

        Assert.True(authorised.IsSuccess);
    }

    [Fact]
    public void Execution_is_refused_when_the_payload_changed_after_approval()
    {
        ApprovalRequest request = Raise().Value;
        request.Decide(TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now);

        const string Substituted = """{"channel":"blog","title":"Launch post","body":"Hello world!"}""";

        Result authorised = request.AuthoriseExecution(Substituted, TestData.Now);

        Assert.True(authorised.IsFailure);
        Assert.Equal("approval.payload_mismatch", authorised.Error.Code);
    }

    [Fact]
    public void Execution_is_authorised_when_the_payload_differs_only_in_key_order()
    {
        // Canonicalisation exists so that a semantically identical payload does not fail the check
        // merely because a serialiser reordered its properties.
        ApprovalRequest request = Raise().Value;
        request.Decide(TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now);

        const string Reordered = """{"body":"Hello world","title":"Launch post","channel":"blog"}""";

        Assert.True(request.AuthoriseExecution(Reordered, TestData.Now).IsSuccess);
    }

    [Fact]
    public void Execution_is_refused_once_the_approval_window_has_closed()
    {
        ApprovalRequest request = Raise(TestData.SingleApprover(TimeSpan.FromHours(2))).Value;
        request.Decide(TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now);

        Result authorised = request.AuthoriseExecution(Payload, TestData.Now.AddHours(3));

        Assert.True(authorised.IsFailure);
        Assert.Equal("approval.expired", authorised.Error.Code);
    }

    [Fact]
    public void Execution_is_refused_while_the_request_is_still_pending()
    {
        ApprovalRequest request = Raise().Value;

        Result authorised = request.AuthoriseExecution(Payload, TestData.Now);

        Assert.True(authorised.IsFailure);
        Assert.Equal("approval.not_approved", authorised.Error.Code);
    }

    [Fact]
    public void A_resolved_request_cannot_be_decided_again()
    {
        ApprovalRequest request = Raise().Value;
        request.Decide(TestData.Alice, ApprovalDecisionKind.Approve, null, true, TestData.Now);

        Result second = request.Decide(TestData.Bob, ApprovalDecisionKind.Reject, "Too late.", true, TestData.Now);

        Assert.True(second.IsFailure);
        Assert.Equal("approval.already_resolved", second.Error.Code);
        Assert.Equal(ApprovalStatus.Approved, request.Status);
    }
}
