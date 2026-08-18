using FluentValidation;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Governance.Queries;

/// <summary>The approver's work queue.</summary>
public sealed record ListPendingApprovalsQuery
    : IQuery<PagedResult<ApprovalSummary>>, IRequirePermission, IScopedRequest
{
    public required Guid WorkspaceId { get; init; }

    public required EnvironmentTier Environment { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string RequiredPermission => Permissions.Approval.Read;
}

public sealed class ListPendingApprovalsQueryValidator : AbstractValidator<ListPendingApprovalsQuery>
{
    public ListPendingApprovalsQueryValidator()
    {
        RuleFor(q => q.WorkspaceId).NotEmpty();
        RuleFor(q => q.Page).GreaterThan(0);
        RuleFor(q => q.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
    }
}

public sealed class ListPendingApprovalsQueryHandler(IApprovalReadModel readModel)
    : IRequestHandler<ListPendingApprovalsQuery, PagedResult<ApprovalSummary>>
{
    public async Task<Result<PagedResult<ApprovalSummary>>> HandleAsync(
        ListPendingApprovalsQuery query,
        CancellationToken cancellationToken)
        => Result.Success(await readModel.ListPendingAsync(
            WorkspaceId.From(query.WorkspaceId),
            query.Environment,
            new PageRequest(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
}

/// <summary>
/// Everything an approver needs to decide without leaving the screen: the exact payload, its
/// fingerprint, the estimated impact, and the lineage back to the objective that caused it.
/// </summary>
public sealed record GetApprovalDetailQuery : IQuery<ApprovalDetail>, IRequirePermission
{
    public required Guid ApprovalRequestId { get; init; }

    public string RequiredPermission => Permissions.Approval.Read;
}

public sealed class GetApprovalDetailQueryHandler(IApprovalReadModel readModel)
    : IRequestHandler<GetApprovalDetailQuery, ApprovalDetail>
{
    public async Task<Result<ApprovalDetail>> HandleAsync(
        GetApprovalDetailQuery query,
        CancellationToken cancellationToken)
    {
        ApprovalDetail? detail = await readModel
            .GetDetailAsync(ApprovalRequestId.From(query.ApprovalRequestId), cancellationToken).ConfigureAwait(false);

        return detail is null
            ? Result.Failure<ApprovalDetail>(Error.NotFound("approval.not_found", "No such approval request."))
            : Result.Success(detail);
    }
}
