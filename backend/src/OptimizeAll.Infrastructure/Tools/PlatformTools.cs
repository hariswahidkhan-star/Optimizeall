using System.Diagnostics;
using System.Text.Json;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Application.Abstractions.Tools;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.Domain.Governance;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Infrastructure.Tools;

/// <summary>
/// Requests that the orchestrator schedule work for another agent.
/// <para>
/// This is the only inter-agent pathway in the platform, and it is a request, not an invocation:
/// the orchestrator decides whether the target agent exists, whether the requester's workspace
/// permits it, and what gates apply. Direct agent-to-agent calls would let one agent's compromise
/// become an arbitrary capability chain.
/// </para>
/// </summary>
public sealed class AgentDelegateTool(
    IAgentDefinitionRepository definitions,
    IAgentRunRepository runs,
    IRunQueue queue,
    IClock clock)
    : IToolExecutor
{
    public string ToolKey => ToolRegistry.AgentDelegate;

    public string Description =>
        "Ask the orchestrator to schedule work for another agent. The request is queued and governed " +
        "independently; you do not receive the other agent's output within this run.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "agentKey": { "type": "string", "description": "The key of the agent to schedule, e.g. 'seo-agent'." },
            "input": { "type": "object", "description": "The input payload for that agent." },
            "reason": { "type": "string", "description": "Why this delegation is necessary." }
          },
          "required": ["agentKey", "input", "reason"],
          "additionalProperties": false
        }
        """;

    public async Task<Result<ToolOutcome>> ExecuteAsync(
        string argumentsJson,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        long startedAt = Stopwatch.GetTimestamp();

        using JsonDocument arguments = JsonDocument.Parse(argumentsJson);
        JsonElement root = arguments.RootElement;

        if (!root.TryGetProperty("agentKey", out JsonElement keyElement)
            || keyElement.GetString() is not { Length: > 0 } agentKey)
        {
            return Result.Failure<ToolOutcome>(Error.Validation(
                "tool.delegate.agent_key_required", "The key of the agent to schedule is required."));
        }

        if (string.Equals(agentKey, context.AgentKey, StringComparison.OrdinalIgnoreCase))
        {
            // Self-delegation is refused. An agent that can schedule itself can loop indefinitely
            // while staying inside every per-run budget, because each iteration is a fresh run.
            return Result.Failure<ToolOutcome>(Error.Forbidden(
                "tool.delegate.self_delegation",
                "An agent cannot schedule itself. Complete the work in this run or escalate."));
        }

        string inputJson = root.TryGetProperty("input", out JsonElement input)
            ? input.GetRawText()
            : "{}";

        AgentDefinition? target = await definitions
            .FindPublishedAsync(context.WorkspaceId, agentKey, cancellationToken).ConfigureAwait(false);

        if (target is null)
        {
            return Result.Failure<ToolOutcome>(Error.NotFound(
                "tool.delegate.agent_not_found",
                $"No published agent '{agentKey}' exists in this workspace."));
        }

        Result<AgentRun> queued = AgentRun.Queue(
            target,
            context.Environment,
            RunTriggerType.Workflow,
            triggeredBy: null,
            inputJson,
            context.CorrelationId,
            clock.UtcNow,
            context.IsDryRun);

        if (queued.IsFailure)
        {
            return Result.Failure<ToolOutcome>(queued.Error);
        }

        runs.Add(queued.Value);
        await queue.EnqueueAsync(queued.Value.Id, context.WorkspaceId, cancellationToken).ConfigureAwait(false);

        return Result.Success(new ToolOutcome(
            JsonSerializer.Serialize(new
            {
                scheduled = true,
                runId = queued.Value.Id.Value,
                agentKey,
                note = "The delegated run executes independently under its own budget and approval rules.",
            }),
            (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
    }
}

/// <summary>
/// Produces a content draft as a durable artefact.
/// <para>
/// Drafting is a <see cref="ActionRiskClass.Write"/> action: nothing leaves the platform. Publishing
/// it is a separate, always-gated step, which is what allows content agents to work at full speed
/// while nothing reaches an audience without a human.
/// </para>
/// </summary>
public sealed class ContentDraftTool(IClock clock) : IToolExecutor
{
    public string ToolKey => ToolRegistry.ContentDraft;

    public string Description =>
        "Record a content draft as an artefact for review. This does not publish anything.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "maxLength": 300 },
            "body": { "type": "string" },
            "format": { "type": "string", "enum": ["markdown", "html", "plaintext"] },
            "metaDescription": { "type": "string", "maxLength": 320 },
            "citations": {
              "type": "array",
              "items": { "type": "string" },
              "description": "Citation ids for every factual claim in the body."
            }
          },
          "required": ["title", "body", "format", "citations"],
          "additionalProperties": false
        }
        """;

    public Task<Result<ToolOutcome>> ExecuteAsync(
        string argumentsJson,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        long startedAt = Stopwatch.GetTimestamp();

        using JsonDocument arguments = JsonDocument.Parse(argumentsJson);
        JsonElement root = arguments.RootElement;

        string? body = root.TryGetProperty("body", out JsonElement b) ? b.GetString() : null;

        if (string.IsNullOrWhiteSpace(body))
        {
            return Task.FromResult(Result.Failure<ToolOutcome>(Error.Validation(
                "tool.content_draft.body_required", "A draft must have a body.")));
        }

        int citationCount = root.TryGetProperty("citations", out JsonElement citations)
            && citations.ValueKind == JsonValueKind.Array
                ? citations.GetArrayLength()
                : 0;

        // Surfaced to the agent rather than silently accepted. An uncited draft is not rejected
        // outright — some content is genuinely opinion — but the gap is made visible so QA and the
        // human approver see it rather than discovering it after publication.
        string? warning = citationCount == 0
            ? "This draft carries no citations. Any factual claim in it is unverifiable and will be flagged in review."
            : null;

        return Task.FromResult(Result.Success(new ToolOutcome(
            JsonSerializer.Serialize(new
            {
                draftId = Guid.CreateVersion7(),
                recordedAt = clock.UtcNow,
                wordCount = body.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                citationCount,
                warning,
                published = false,
                note = "Publication is a separate action and requires human approval.",
            }),
            (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds)));
    }
}

/// <summary>
/// Reports platform metrics to an agent.
/// <para>
/// Restricted to the platform's own operational data. It is not a general query interface: an agent
/// that could compose arbitrary SQL would be a data-exfiltration path with a natural-language
/// front end.
/// </para>
/// </summary>
public sealed class AnalyticsQueryTool(IAgentRunRepository runs, IClock clock) : IToolExecutor
{
    public string ToolKey => ToolRegistry.AnalyticsQuery;

    public string Description =>
        "Read platform operational metrics for this workspace: run volume, success rate, and spend.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "metric": {
              "type": "string",
              "enum": ["spend_month_to_date", "run_volume", "approval_throughput"]
            }
          },
          "required": ["metric"],
          "additionalProperties": false
        }
        """;

    public async Task<Result<ToolOutcome>> ExecuteAsync(
        string argumentsJson,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        long startedAt = Stopwatch.GetTimestamp();

        using JsonDocument arguments = JsonDocument.Parse(argumentsJson);

        string metric = arguments.RootElement.TryGetProperty("metric", out JsonElement m)
            ? m.GetString() ?? string.Empty
            : string.Empty;

        object payload = metric switch
        {
            "spend_month_to_date" => new
            {
                metric,
                amount = await runs.GetMonthToDateCostAsync(context.WorkspaceId, clock.UtcNow, cancellationToken)
                    .ConfigureAwait(false),
                asOf = clock.UtcNow,
            },

            // Enumerated rather than defaulted: an unrecognised metric returns an explicit refusal
            // so the agent learns the supported set instead of receiving a plausible empty result.
            _ => new
            {
                metric,
                error = "unsupported_metric",
                supported = new[] { "spend_month_to_date", "run_volume", "approval_throughput" },
            },
        };

        return Result.Success(new ToolOutcome(
            JsonSerializer.Serialize(payload),
            (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
    }
}
