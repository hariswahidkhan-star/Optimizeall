using System.Diagnostics;
using System.Text.Json;
using OptimizeAll.Application.Abstractions.Ai;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Tools;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Knowledge;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Infrastructure.Tools;

/// <summary>Semantic search over the tenant knowledge base.</summary>
public sealed class KnowledgeSearchTool(
    IKnowledgeRepository knowledge,
    IEmbeddingService embeddings)
    : IToolExecutor
{
    public string ToolKey => ToolRegistry.KnowledgeSearch;

    public string Description =>
        "Search the organisation's knowledge base for passages relevant to a question. " +
        "Returns passages with citation ids that must be quoted when the passage informs a claim.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "description": "The question or topic to search for." },
            "limit": { "type": "integer", "minimum": 1, "maximum": 20, "default": 8 }
          },
          "required": ["query"],
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

        if (!arguments.RootElement.TryGetProperty("query", out JsonElement queryElement)
            || queryElement.GetString() is not { Length: > 0 } query)
        {
            return Result.Failure<ToolOutcome>(Error.Validation(
                "tool.knowledge_search.query_required", "A search query is required."));
        }

        int limit = arguments.RootElement.TryGetProperty("limit", out JsonElement limitElement)
            ? Math.Clamp(limitElement.GetInt32(), 1, 20)
            : 8;

        Result<IReadOnlyList<float[]>> embedded = await embeddings
            .EmbedAsync([query], cancellationToken).ConfigureAwait(false);

        if (embedded.IsFailure || embedded.Value.Count == 0)
        {
            return Result.Failure<ToolOutcome>(Error.Unavailable(
                "tool.knowledge_search.embedding_failed",
                "The search could not be performed because the query could not be embedded."));
        }

        // Scope comes from the run's context, never from the model's arguments. Letting an agent
        // name its own workspace would make every isolation guarantee negotiable by prompt.
        IReadOnlyList<RetrievedChunk> results = await knowledge.SearchAsync(
            context.WorkspaceId,
            context.Environment,
            embedded.Value[0],
            limit,
            cancellationToken).ConfigureAwait(false);

        string payload = JsonSerializer.Serialize(new
        {
            resultCount = results.Count,
            results = results.Select(r => new
            {
                citationId = r.ChunkId,
                source = r.DocumentTitle,
                similarity = Math.Round(r.Similarity, 4),
                content = r.Content,
            }),
            note = results.Count == 0
                ? "No relevant passages were found. Say so rather than answering from assumption."
                : "Cite the citationId of any passage you rely on.",
        });

        return Result.Success(new ToolOutcome(payload, (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
    }
}

/// <summary>Adds a document to the tenant knowledge base.</summary>
public sealed class KnowledgeWriteTool(
    IKnowledgeRepository knowledge,
    IClock clock)
    : IToolExecutor
{
    /// <summary>
    /// Chunk size in characters. Small enough that a retrieved passage is specific, large enough
    /// that a paragraph's meaning survives being split out of its document.
    /// </summary>
    private const int ChunkSize = 1500;

    private const int ChunkOverlap = 200;

    public string ToolKey => ToolRegistry.KnowledgeWrite;

    public string Description =>
        "Record a document in the organisation's knowledge base so it can be retrieved later. " +
        "Use for durable findings, not for transient working notes.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "maxLength": 500 },
            "content": { "type": "string" },
            "sourceUri": { "type": "string", "maxLength": 2000 },
            "sourceType": { "type": "string", "enum": ["research", "policy", "playbook", "reference", "note"] }
          },
          "required": ["title", "content", "sourceType"],
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

        string? title = root.TryGetProperty("title", out JsonElement t) ? t.GetString() : null;
        string? content = root.TryGetProperty("content", out JsonElement c) ? c.GetString() : null;
        string sourceType = root.TryGetProperty("sourceType", out JsonElement st) ? st.GetString() ?? "note" : "note";
        string sourceUri = root.TryGetProperty("sourceUri", out JsonElement su)
            ? su.GetString() ?? $"agent://{context.AgentKey}/{context.RunId}"
            : $"agent://{context.AgentKey}/{context.RunId}";

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
        {
            return Result.Failure<ToolOutcome>(Error.Validation(
                "tool.knowledge_write.invalid", "Both a title and content are required."));
        }

        KnowledgeDocument? existing = await knowledge.FindByContentHashAsync(
            context.WorkspaceId,
            context.Environment,
            ComputeHash(content),
            cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            // Identical content is already indexed. Reported as success rather than as an error:
            // the desired end state holds, and an error would push the agent into a retry loop.
            return Result.Success(new ToolOutcome(
                JsonSerializer.Serialize(new
                {
                    documentId = existing.Id.Value,
                    created = false,
                    reason = "An identical document is already in the knowledge base.",
                }),
                (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
        }

        KnowledgeDocument document = KnowledgeDocument.BeginIngestion(
            context.TenantId,
            context.WorkspaceId,
            context.Environment,
            title,
            sourceUri,
            sourceType,
            content);

        int sequence = 0;

        foreach (string chunk in Chunk(content))
        {
            document.AddChunk(sequence++, chunk, EstimateTokens(chunk));
        }

        Result completed = document.CompleteIngestion(clock.UtcNow);

        if (completed.IsFailure)
        {
            return Result.Failure<ToolOutcome>(completed.Error);
        }

        knowledge.Add(document);

        return Result.Success(new ToolOutcome(
            JsonSerializer.Serialize(new
            {
                documentId = document.Id.Value,
                created = true,
                chunkCount = sequence,
                note = "Embeddings are generated asynchronously; the document becomes searchable shortly.",
            }),
            (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
    }

    /// <summary>
    /// Splits content into overlapping windows. The overlap exists so that a passage spanning a
    /// boundary is still retrievable in full from at least one chunk.
    /// </summary>
    private static IEnumerable<string> Chunk(string content)
    {
        if (content.Length <= ChunkSize)
        {
            yield return content;
            yield break;
        }

        int position = 0;

        while (position < content.Length)
        {
            int length = Math.Min(ChunkSize, content.Length - position);
            yield return content.Substring(position, length);

            if (position + length >= content.Length)
            {
                yield break;
            }

            position += ChunkSize - ChunkOverlap;
        }
    }

    private static int EstimateTokens(string text) => text.Length / 4;

    private static string ComputeHash(string content)
        => Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));
}
