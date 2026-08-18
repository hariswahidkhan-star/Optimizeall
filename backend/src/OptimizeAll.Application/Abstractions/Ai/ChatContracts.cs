using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;

namespace OptimizeAll.Application.Abstractions.Ai;

public enum ChatRole
{
    System = 1,
    User = 2,
    Assistant = 3,

    /// <summary>The result of a tool call being fed back to the model.</summary>
    Tool = 4,
}

/// <summary>
/// One turn in a conversation. Owned by OptimizeAll, not by any provider SDK: this type is what
/// keeps OpenAI, Anthropic and Gemini interchangeable, and what stops a provider's message shape
/// from leaking into the Application layer where swapping vendors would become a rewrite.
/// </summary>
public sealed record ChatMessage(ChatRole Role, string Content)
{
    /// <summary>Tool calls the assistant requested. Empty for every other role.</summary>
    public IReadOnlyList<ToolCall> ToolCalls { get; init; } = [];

    /// <summary>Correlates a <see cref="ChatRole.Tool"/> message with the call it answers.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// True when the content came from outside the platform — a fetched page, a retrieved document,
    /// a customer message. Providers render these inside untrusted-content delimiters, and any
    /// instruction found within is treated as data.
    /// </summary>
    public bool IsUntrustedContent { get; init; }

    public static ChatMessage System(string content) => new(ChatRole.System, content);

    public static ChatMessage User(string content) => new(ChatRole.User, content);

    public static ChatMessage Assistant(string content) => new(ChatRole.Assistant, content);

    public static ChatMessage Untrusted(string content) => new(ChatRole.User, content) { IsUntrustedContent = true };

    public static ChatMessage ToolResult(string toolCallId, string content)
        => new(ChatRole.Tool, content) { ToolCallId = toolCallId };
}

/// <summary>A tool the model may request. Advertising it is not the same as authorising it.</summary>
public sealed record ToolDefinition(string Name, string Description, string ParametersJsonSchema);

/// <summary>
/// A model's request to invoke a tool. The platform decides whether it may: this is an intent, never
/// a permission.
/// </summary>
public sealed record ToolCall(string Id, string ToolName, string ArgumentsJson);

public sealed record ChatRequest
{
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    public required ModelPolicy ModelPolicy { get; init; }

    public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];

    /// <summary>Deduplicates a retried request at the provider, so an ambiguous timeout is not billed twice.</summary>
    public string? IdempotencyKey { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);
}

public enum ChatFinishReason
{
    Stop = 1,
    ToolCalls = 2,
    MaxTokens = 3,
    ContentFilter = 4,
    Error = 5,
}

public sealed record ChatResponse
{
    public required string Content { get; init; }

    public IReadOnlyList<ToolCall> ToolCalls { get; init; } = [];

    public required ChatFinishReason FinishReason { get; init; }

    public required TokenUsage Usage { get; init; }

    /// <summary>The provider that actually served this call, which may differ from the primary after failover.</summary>
    public required AiProvider Provider { get; init; }

    public required string Model { get; init; }

    public required Money Cost { get; init; }

    public required TimeSpan Latency { get; init; }

    /// <summary>True when the primary provider failed and a fallback served the request.</summary>
    public bool FailedOver { get; init; }
}

public sealed record TokenUsage(long PromptTokens, long CompletionTokens)
{
    public long Total => PromptTokens + CompletionTokens;
}
