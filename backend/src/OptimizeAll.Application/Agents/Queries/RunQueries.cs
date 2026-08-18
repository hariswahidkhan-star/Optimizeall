using FluentValidation;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Agents.Queries;

public sealed record ListAgentRunsQuery : IQuery<PagedResult<RunSummary>>, IRequirePermission, IScopedRequest
{
    public required Guid WorkspaceId { get; init; }

    public required EnvironmentTier Environment { get; init; }

    public string? AgentKey { get; init; }

    public AgentRunStatus? Status { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string RequiredPermission => Permissions.Run.Read;
}

public sealed class ListAgentRunsQueryValidator : AbstractValidator<ListAgentRunsQuery>
{
    public ListAgentRunsQueryValidator()
    {
        RuleFor(q => q.WorkspaceId).NotEmpty();
        RuleFor(q => q.Page).GreaterThan(0);
        RuleFor(q => q.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
        RuleFor(q => q.AgentKey).MaximumLength(100);
    }
}

public sealed class ListAgentRunsQueryHandler(IRunReadModel readModel)
    : IRequestHandler<ListAgentRunsQuery, PagedResult<RunSummary>>
{
    public async Task<Result<PagedResult<RunSummary>>> HandleAsync(
        ListAgentRunsQuery query,
        CancellationToken cancellationToken)
        => Result.Success(await readModel.ListAsync(
            WorkspaceId.From(query.WorkspaceId),
            query.Environment,
            query.AgentKey,
            query.Status,
            new PageRequest(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
}

/// <summary>The full reasoning trace of one run: every prompt, tool call, retrieval and cost.</summary>
public sealed record GetRunDetailQuery : IQuery<RunDetail>, IRequirePermission
{
    public required Guid RunId { get; init; }

    public string RequiredPermission => Permissions.Run.Read;
}

public sealed class GetRunDetailQueryHandler(IRunReadModel readModel)
    : IRequestHandler<GetRunDetailQuery, RunDetail>
{
    public async Task<Result<RunDetail>> HandleAsync(GetRunDetailQuery query, CancellationToken cancellationToken)
    {
        RunDetail? detail = await readModel
            .GetDetailAsync(AgentRunId.From(query.RunId), cancellationToken).ConfigureAwait(false);

        return detail is null
            ? Result.Failure<RunDetail>(Error.NotFound("run.not_found", "No such agent run."))
            : Result.Success(detail);
    }
}

public sealed record GetExecutiveDashboardQuery : IQuery<ExecutiveDashboard>, IRequirePermission, IScopedRequest
{
    public required Guid WorkspaceId { get; init; }

    public required EnvironmentTier Environment { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public string RequiredPermission => Permissions.Analytics.Read;
}

public sealed class GetExecutiveDashboardQueryValidator : AbstractValidator<GetExecutiveDashboardQuery>
{
    public GetExecutiveDashboardQueryValidator()
    {
        RuleFor(q => q.WorkspaceId).NotEmpty();
        RuleFor(q => q.To).GreaterThan(q => q.From);

        RuleFor(q => q)
            .Must(q => q.To - q.From <= TimeSpan.FromDays(400))
            .WithMessage("Dashboard windows are capped at 400 days; use exported reports for longer ranges.");
    }
}

public sealed class GetExecutiveDashboardQueryHandler(IDashboardReadModel readModel)
    : IRequestHandler<GetExecutiveDashboardQuery, ExecutiveDashboard>
{
    public async Task<Result<ExecutiveDashboard>> HandleAsync(
        GetExecutiveDashboardQuery query,
        CancellationToken cancellationToken)
        => Result.Success(await readModel.GetExecutiveDashboardAsync(
            WorkspaceId.From(query.WorkspaceId),
            query.Environment,
            query.From,
            query.To,
            cancellationToken).ConfigureAwait(false));
}
