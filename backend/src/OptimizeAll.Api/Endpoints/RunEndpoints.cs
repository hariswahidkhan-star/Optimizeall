using OptimizeAll.Api.Middleware;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Agents.Commands;
using OptimizeAll.Application.Agents.Queries;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Api.Endpoints;

public static class RunEndpoints
{
    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app.MapGroup("/api/v1/runs")
            .WithTags("Agent runs")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid workspaceId,
            EnvironmentTier environment,
            IDispatcher dispatcher,
            CancellationToken cancellationToken,
            string? agentKey = null,
            AgentRunStatus? status = null,
            int page = 1,
            int pageSize = 25) =>
        {
            Result<PagedResult<RunSummary>> result = await dispatcher.SendAsync(
                new ListAgentRunsQuery
                {
                    WorkspaceId = workspaceId,
                    Environment = environment,
                    AgentKey = agentKey,
                    Status = status,
                    Page = page,
                    PageSize = pageSize,
                },
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("ListAgentRuns")
        .WithSummary("Lists agent runs, filterable by agent and status.")
        .Produces<PagedResult<RunSummary>>();

        group.MapGet("/{runId:guid}", async (
            Guid runId,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<RunDetail> result = await dispatcher.SendAsync(
                new GetRunDetailQuery { RunId = runId },
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("GetRunDetail")
        .WithSummary("Returns one run's full reasoning trace, tool calls, retrievals and cost.")
        .Produces<RunDetail>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            QueueAgentRunRequest body,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<QueueAgentRunResult> result = await dispatcher.SendAsync(
                new QueueAgentRunCommand
                {
                    WorkspaceId = body.WorkspaceId,
                    Environment = body.Environment,
                    AgentKey = body.AgentKey,
                    InputJson = body.Input.GetRawText(),
                    IsDryRun = body.IsDryRun,
                },
                cancellationToken);

            // 202 rather than 201: the run is queued, not completed. A worker executes it, and the
            // caller polls or subscribes for the outcome.
            return result.ToHttpResult(value =>
                TypedResults.Accepted($"/api/v1/runs/{value.RunId}", value));
        })
        .WithName("QueueAgentRun")
        .WithSummary("Queues an agent run for execution by a worker.")
        .WithDescription(
            "Refused while the workspace emergency stop is engaged, and while the workspace has " +
            "consumed its monthly budget. Set isDryRun to record what the agent would do without " +
            "applying any external effect.")
        .Produces<QueueAgentRunResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }
}

public sealed record QueueAgentRunRequest(
    Guid WorkspaceId,
    EnvironmentTier Environment,
    string AgentKey,
    System.Text.Json.JsonElement Input,
    bool IsDryRun = false);
