using OptimizeAll.Api.Middleware;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Agents.Queries;
using OptimizeAll.Application.Orchestration.Commands;
using OptimizeAll.Application.Tenancy.Commands;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Api.Endpoints;

public static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app.MapGroup("/api/v1/workspaces")
            .WithTags("Workspaces")
            .RequireAuthorization();

        group.MapPost("/{workspaceId:guid}/kill-switch", async (
            Guid workspaceId,
            EngageKillSwitchRequest body,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<KillSwitchResult> result = await dispatcher.SendAsync(
                new EngageKillSwitchCommand
                {
                    WorkspaceId = workspaceId,
                    Environment = body.Environment,
                    Reason = body.Reason,
                },
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("EngageKillSwitch")
        .WithSummary("Halts every external action by every agent in this workspace.")
        .WithDescription("Takes effect within seconds and requires a recorded reason.")
        .Produces<KillSwitchResult>();

        group.MapDelete("/{workspaceId:guid}/kill-switch", async (
            Guid workspaceId,
            ReleaseKillSwitchRequest body,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<KillSwitchResult> result = await dispatcher.SendAsync(
                new ReleaseKillSwitchCommand
                {
                    WorkspaceId = workspaceId,
                    Environment = body.Environment,
                    Acknowledgement = body.Acknowledgement,
                },
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("ReleaseKillSwitch")
        .WithSummary("Lifts the emergency stop.")
        .WithDescription("Requires step-up authentication — deliberately harder than engaging it.")
        .Produces<KillSwitchResult>()
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{workspaceId:guid}/dashboard", async (
            Guid workspaceId,
            EnvironmentTier environment,
            DateTimeOffset from,
            DateTimeOffset to,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<ExecutiveDashboard> result = await dispatcher.SendAsync(
                new GetExecutiveDashboardQuery
                {
                    WorkspaceId = workspaceId,
                    Environment = environment,
                    From = from,
                    To = to,
                },
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("GetExecutiveDashboard")
        .WithSummary("Executive view: work delivered, approval throughput, agent quality and spend.")
        .Produces<ExecutiveDashboard>();
    }
}

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app.MapGroup("/api/v1/workflows")
            .WithTags("Workflows")
            .RequireAuthorization();

        group.MapPost("/", async (
            StartWorkflowRequest body,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<StartWorkflowResult> result = await dispatcher.SendAsync(
                new StartWorkflowCommand
                {
                    WorkspaceId = body.WorkspaceId,
                    Environment = body.Environment,
                    ObjectiveTitle = body.ObjectiveTitle,
                    ObjectivePayloadJson = body.Objective.GetRawText(),
                    Deadline = body.Deadline,
                },
                cancellationToken);

            return result.ToHttpResult(value =>
                TypedResults.Accepted($"/api/v1/workflows/{value.WorkflowRunId}", value));
        })
        .WithName("StartWorkflow")
        .WithSummary("Accepts a business objective, plans it into a governed task graph, and starts it.")
        .WithDescription(
            "The returned unstaffedSteps list names any step the planner could not assign to a " +
            "capable agent. Those steps need a human, and are surfaced rather than silently dropped.")
        .Produces<StartWorkflowResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

public sealed record EngageKillSwitchRequest(EnvironmentTier Environment, string Reason);

public sealed record ReleaseKillSwitchRequest(EnvironmentTier Environment, string Acknowledgement);

public sealed record StartWorkflowRequest(
    Guid WorkspaceId,
    EnvironmentTier Environment,
    string ObjectiveTitle,
    System.Text.Json.JsonElement Objective,
    DateTimeOffset? Deadline);
