using OptimizeAll.Api.Middleware;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Governance.Commands;
using OptimizeAll.Application.Governance.Queries;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Governance;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Api.Endpoints;

/// <summary>
/// The approval surface — the part of the API an approver actually uses.
/// </summary>
public static class ApprovalEndpoints
{
    public static void MapApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app.MapGroup("/api/v1/approvals")
            .WithTags("Approvals")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid workspaceId,
            EnvironmentTier environment,
            IDispatcher dispatcher,
            CancellationToken cancellationToken,
            int page = 1,
            int pageSize = 25) =>
        {
            Result<PagedResult<ApprovalSummary>> result = await dispatcher.SendAsync(
                new ListPendingApprovalsQuery
                {
                    WorkspaceId = workspaceId,
                    Environment = environment,
                    Page = page,
                    PageSize = pageSize,
                },
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("ListPendingApprovals")
        .WithSummary("Lists approvals awaiting a decision in a workspace and environment.")
        .Produces<PagedResult<ApprovalSummary>>()
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{approvalId:guid}", async (
            Guid approvalId,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<ApprovalDetail> result = await dispatcher.SendAsync(
                new GetApprovalDetailQuery { ApprovalRequestId = approvalId },
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("GetApprovalDetail")
        .WithSummary("Returns the full payload, fingerprint, estimated impact and lineage of one approval.")
        .Produces<ApprovalDetail>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{approvalId:guid}/decision", async (
            Guid approvalId,
            DecideApprovalRequest body,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<DecideApprovalResult> result = await dispatcher.SendAsync(
                new DecideApprovalCommand
                {
                    ApprovalRequestId = approvalId,
                    Decision = body.Decision,
                    Rationale = body.Rationale,
                },
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("DecideApproval")
        .WithSummary("Approves or rejects a gated action.")
        .WithDescription(
            "Financial and Irreversible decisions additionally require the corresponding elevated " +
            "permission and step-up authentication. The requester can never decide their own request, " +
            "and an agent can never decide at all.")
        .Produces<DecideApprovalResult>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

public sealed record DecideApprovalRequest(ApprovalDecisionKind Decision, string? Rationale);
