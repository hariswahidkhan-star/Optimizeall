using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Ai;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Tools;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Application.Agents.Runtime;

/// <summary>Why an executor stopped. The caller persists a different terminal state for each.</summary>
public enum RunConclusion
{
    Completed = 1,

    /// <summary>Suspended on an approval. The run holds no worker while it waits.</summary>
    SuspendedForApproval = 2,

    BudgetExhausted = 3,
    Failed = 4,
}

public sealed record RunOutcome(RunConclusion Conclusion, string? OutputJson, Error? Error);

/// <summary>
/// Drives one agent run: compose the prompt, call the model, mediate every tool request, and stop
/// at the first hard boundary.
/// <para>
/// The loop is deliberately bounded on five independent axes — iterations, tokens, cost, tool calls
/// and wall clock — and every one is checked <em>before</em> the step that would exceed it. An agent
/// that has already spent the money cannot be stopped, only apologised for.
/// </para>
/// </summary>
public sealed class AgentExecutor(
    IChatCompletionRouter router,
    IToolCatalog toolCatalog,
    ToolInvocationService toolInvocation,
    PromptComposer promptComposer,
    IKnowledgeRepository knowledge,
    IEmbeddingService embeddings,
    IModelPricing pricing,
    IClock clock,
    ILogger<AgentExecutor> logger)
{
    public async Task<RunOutcome> ExecuteAsync(
        AgentRun run,
        AgentDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(definition);

        DateTimeOffset startedAt = run.StartedAt ?? clock.UtcNow;
        int sequence = run.Steps.Count;

        IReadOnlyList<RetrievedChunk> retrieved = await RetrieveKnowledgeAsync(
            run, definition, cancellationToken).ConfigureAwait(false);

        List<ChatMessage> conversation =
        [
            .. await promptComposer.ComposeAsync(definition, run, retrieved, clock.UtcNow, cancellationToken)
                .ConfigureAwait(false),
        ];

        IReadOnlyList<ToolDefinition> advertisedTools = BuildToolDefinitions(definition);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Estimated conservatively: a low estimate here would let the very call this check
            // exists to prevent go through.
            Money projectedCost = pricing.EstimateUpperBound(
                definition.ModelPolicy.Provider,
                definition.ModelPolicy.Model,
                EstimatePromptTokens(conversation),
                definition.ModelPolicy.MaxOutputTokens);

            Result budgetCheck = run.Budget.CanStartIteration(
                definition.BudgetPolicy, projectedCost, clock.UtcNow - startedAt);

            if (budgetCheck.IsFailure)
            {
                logger.LogInformation(
                    "Run {RunId} stopped on budget: {Reason}", run.Id, budgetCheck.Error.Code);

                return new RunOutcome(RunConclusion.BudgetExhausted, null, budgetCheck.Error);
            }

            ChatRequest chatRequest = new()
            {
                Messages = conversation,
                ModelPolicy = definition.ModelPolicy,
                Tools = advertisedTools,
                IdempotencyKey = $"{run.Id}:{sequence}",
            };

            Result<ChatResponse> completion = await router
                .CompleteAsync(chatRequest, cancellationToken).ConfigureAwait(false);

            if (completion.IsFailure)
            {
                return new RunOutcome(RunConclusion.Failed, null, completion.Error);
            }

            ChatResponse response = completion.Value;

            run.RecordProviderRouting(response.Provider, response.Model);
            run.RecordCompletionUsage(response.Usage.PromptTokens, response.Usage.CompletionTokens, response.Cost);

            run.AppendStep(AgentRunStep.Create(
                run.Id,
                sequence++,
                RunStepType.Completion,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    content = response.Content,
                    finishReason = response.FinishReason.ToString(),
                    provider = response.Provider.ToString(),
                    model = response.Model,
                    failedOver = response.FailedOver,
                    toolCalls = response.ToolCalls.Select(c => c.ToolName),
                }),
                clock.UtcNow,
                response.Usage.Total,
                (int)response.Latency.TotalMilliseconds));

            if (response.FinishReason != ChatFinishReason.ToolCalls || response.ToolCalls.Count == 0)
            {
                return new RunOutcome(RunConclusion.Completed, BuildOutput(response, retrieved), null);
            }

            conversation.Add(new ChatMessage(ChatRole.Assistant, response.Content)
            {
                ToolCalls = response.ToolCalls,
            });

            foreach (ToolCall call in response.ToolCalls)
            {
                ToolDisposition disposition = await toolInvocation.InvokeAsync(
                    run,
                    definition,
                    new ToolCallRequest
                    {
                        ToolKey = call.ToolName,
                        ArgumentsJson = string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson,
                        IdempotencyKey = $"{run.Id}:{call.Id}",
                    },
                    cancellationToken).ConfigureAwait(false);

                run.AppendStep(AgentRunStep.Create(
                    run.Id,
                    sequence++,
                    disposition.Kind == ToolDispositionKind.GatedOnApproval ? RunStepType.ApprovalGate : RunStepType.ToolCall,
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        tool = call.ToolName,
                        disposition = disposition.Kind.ToString(),
                        reason = disposition.Reason,
                        approvalRequestId = disposition.ApprovalRequestId?.Value,
                    }),
                    clock.UtcNow));

                if (disposition.Kind == ToolDispositionKind.GatedOnApproval)
                {
                    // The run stops here and releases its worker. It resumes as a fresh execution
                    // once a human decides, which is what makes an hours-long wait cost nothing.
                    return new RunOutcome(RunConclusion.SuspendedForApproval, null, null);
                }

                // A denial is fed back to the model rather than aborting the run: the agent may well
                // have a legitimate alternative, and it learns which capabilities it does not have.
                conversation.Add(ChatMessage.ToolResult(
                    call.Id,
                    disposition.ResultJson ?? System.Text.Json.JsonSerializer.Serialize(new
                    {
                        denied = disposition.Kind == ToolDispositionKind.Denied,
                        reason = disposition.Reason,
                    })));
            }
        }
    }

    private async Task<IReadOnlyList<RetrievedChunk>> RetrieveKnowledgeAsync(
        AgentRun run,
        AgentDefinition definition,
        CancellationToken cancellationToken)
    {
        if (!definition.MemoryPolicy.SemanticEnabled || definition.MemoryPolicy.SemanticRecallLimit == 0)
        {
            return [];
        }

        Result<IReadOnlyList<float[]>> embedded = await embeddings
            .EmbedAsync([run.InputJson], cancellationToken).ConfigureAwait(false);

        if (embedded.IsFailure || embedded.Value.Count == 0)
        {
            // Retrieval is an enhancement, not a precondition. A failed embedding degrades the run's
            // grounding; it should not fail a run that may not have needed knowledge at all.
            logger.LogWarning("Knowledge retrieval skipped for run {RunId}: embedding unavailable.", run.Id);
            return [];
        }

        return await knowledge.SearchAsync(
            run.WorkspaceId,
            run.Environment,
            embedded.Value[0],
            definition.MemoryPolicy.SemanticRecallLimit,
            cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<ToolDefinition> BuildToolDefinitions(AgentDefinition definition)
    {
        List<ToolDefinition> definitions = [];

        foreach (ToolGrant grant in definition.ToolGrants)
        {
            IToolExecutor? executor = toolCatalog.Find(grant.ToolKey);

            if (executor is null)
            {
                // Advertising a tool with no executor would invite the model to call something that
                // can only fail. Skipping it keeps the advertised surface honest.
                logger.LogWarning(
                    "Agent {AgentKey} is granted {ToolKey} but no executor is registered; it will not be advertised.",
                    definition.AgentKey,
                    grant.ToolKey);

                continue;
            }

            definitions.Add(new ToolDefinition(executor.ToolKey, executor.Description, executor.ParametersJsonSchema));
        }

        return definitions;
    }

    private static string BuildOutput(ChatResponse response, IReadOnlyList<RetrievedChunk> retrieved)
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            content = response.Content,
            finishReason = response.FinishReason.ToString(),
            citations = retrieved.Select(c => new
            {
                chunkId = c.ChunkId,
                documentId = c.DocumentId,
                title = c.DocumentTitle,
                similarity = c.Similarity,
            }),
        });

    /// <summary>
    /// A deliberately crude four-characters-per-token approximation, used only to over-estimate the
    /// cost of the next call. Calling a tokeniser here would add a dependency and a per-iteration
    /// cost to a number that exists purely to be conservative.
    /// </summary>
    private static long EstimatePromptTokens(IReadOnlyList<ChatMessage> messages)
        => messages.Sum(m => (long)m.Content.Length) / 4;
}
