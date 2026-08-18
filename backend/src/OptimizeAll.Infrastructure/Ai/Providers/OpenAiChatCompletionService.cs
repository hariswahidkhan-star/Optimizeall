using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using OptimizeAll.Application.Abstractions.Ai;
using OptimizeAll.SharedKernel.Results;
using AppChatMessage = OptimizeAll.Application.Abstractions.Ai.ChatMessage;
using AppChatRole = OptimizeAll.Application.Abstractions.Ai.ChatRole;
using AppToolCall = OptimizeAll.Application.Abstractions.Ai.ToolCall;
using AppToolDefinition = OptimizeAll.Application.Abstractions.Ai.ToolDefinition;
using AppFinishReason = OptimizeAll.Application.Abstractions.Ai.ChatFinishReason;
using DomainProvider = OptimizeAll.Domain.AgentCatalog.AiProvider;
using OpenAiChatMessage = OpenAI.Chat.ChatMessage;

namespace OptimizeAll.Infrastructure.Ai.Providers;

/// <summary>
/// OpenAI adapter, built on the official OpenAI SDK.
/// <para>
/// A separate client is resolved per model because the SDK binds the model at client construction.
/// The factory is injected rather than a client, so a single agent definition can name any model
/// without the adapter holding a fixed one.
/// </para>
/// </summary>
public sealed class OpenAiChatCompletionService(
    Func<string, ChatClient> clientFactory,
    IModelPricing pricing,
    ILogger<OpenAiChatCompletionService> logger)
    : IChatCompletionService
{
    public DomainProvider Provider => DomainProvider.OpenAi;

    public bool IsAvailable => true;

    public async Task<Result<ChatResponse>> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            ChatClient client = clientFactory(request.ModelPolicy.Model);

            ChatCompletionOptions options = new()
            {
                MaxOutputTokenCount = request.ModelPolicy.MaxOutputTokens,
                Temperature = (float)request.ModelPolicy.Temperature,
            };

            foreach (AppToolDefinition tool in request.Tools)
            {
                options.Tools.Add(ChatTool.CreateFunctionTool(
                    tool.Name,
                    tool.Description,
                    BinaryData.FromString(tool.ParametersJsonSchema)));
            }

            ClientResult<ChatCompletion> result = await client
                .CompleteChatAsync(Translate(request.Messages), options, cancellationToken)
                .ConfigureAwait(false);

            return Translate(request, result.Value, Stopwatch.GetElapsedTime(startedAt));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception, "OpenAI completion failed for model {Model}.", request.ModelPolicy.Model);

            return Result.Failure<ChatResponse>(Error.Unavailable(
                "provider.openai_failed",
                "The OpenAI provider did not return a completion."));
        }
    }

    private static List<OpenAiChatMessage> Translate(IReadOnlyList<AppChatMessage> conversation)
    {
        List<OpenAiChatMessage> messages = [];

        foreach (AppChatMessage message in conversation)
        {
            messages.Add(message.Role switch
            {
                AppChatRole.System => new SystemChatMessage(message.Content),
                AppChatRole.User => new UserChatMessage(message.Content),
                AppChatRole.Assistant => new AssistantChatMessage(message.Content),
                AppChatRole.Tool => new ToolChatMessage(
                    message.ToolCallId
                        ?? throw new InvalidOperationException(
                            "A tool result message must carry the id of the call it answers."),
                    message.Content),
                _ => throw new InvalidOperationException($"Unsupported chat role '{message.Role}'."),
            });
        }

        return messages;
    }

    private Result<ChatResponse> Translate(ChatRequest request, ChatCompletion completion, TimeSpan latency)
    {
        List<AppToolCall> toolCalls =
        [
            .. completion.ToolCalls.Select(call => new AppToolCall(
                call.Id,
                call.FunctionName,
                call.FunctionArguments.ToString())),
        ];

        string content = string.Join(
            "\n",
            completion.Content.Where(part => !string.IsNullOrEmpty(part.Text)).Select(part => part.Text));

        TokenUsage usage = new(
            completion.Usage?.InputTokenCount ?? 0,
            completion.Usage?.OutputTokenCount ?? 0);

        return Result.Success(new ChatResponse
        {
            Content = content,
            ToolCalls = toolCalls,
            FinishReason = MapFinishReason(completion.FinishReason, toolCalls.Count),
            Usage = usage,
            Provider = DomainProvider.OpenAi,
            Model = completion.Model ?? request.ModelPolicy.Model,
            Cost = pricing.Price(DomainProvider.OpenAi, request.ModelPolicy.Model, usage),
            Latency = latency,
        });
    }

    private static AppFinishReason MapFinishReason(
        OpenAI.Chat.ChatFinishReason finishReason,
        int toolCallCount)
        => finishReason switch
        {
            OpenAI.Chat.ChatFinishReason.ToolCalls or OpenAI.Chat.ChatFinishReason.FunctionCall
                => AppFinishReason.ToolCalls,
            OpenAI.Chat.ChatFinishReason.Length => AppFinishReason.MaxTokens,
            OpenAI.Chat.ChatFinishReason.ContentFilter => AppFinishReason.ContentFilter,
            _ => toolCallCount > 0 ? AppFinishReason.ToolCalls : AppFinishReason.Stop,
        };
}

/// <summary>
/// Embedding generation, used for knowledge ingestion and retrieval.
/// <para>
/// Deliberately pinned to one provider and one model across the platform: an index built with one
/// embedding model cannot be queried with another, so making this configurable per agent would
/// produce silently meaningless similarity scores rather than an error.
/// </para>
/// </summary>
public sealed class OpenAiEmbeddingService(
    OpenAI.Embeddings.EmbeddingClient client,
    ILogger<OpenAiEmbeddingService> logger)
    : IEmbeddingService
{
    public DomainProvider Provider => DomainProvider.OpenAi;

    /// <summary>Must match the <c>vector(n)</c> column width in the schema.</summary>
    public int Dimensions => 1536;

    public async Task<Result<IReadOnlyList<float[]>>> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count == 0)
        {
            return Result.Success<IReadOnlyList<float[]>>([]);
        }

        try
        {
            ClientResult<OpenAI.Embeddings.OpenAIEmbeddingCollection> result = await client
                .GenerateEmbeddingsAsync(inputs, cancellationToken: cancellationToken).ConfigureAwait(false);

            return Result.Success<IReadOnlyList<float[]>>(
                [.. result.Value.Select(embedding => embedding.ToFloats().ToArray())]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Embedding generation failed for {Count} inputs.", inputs.Count);

            return Result.Failure<IReadOnlyList<float[]>>(Error.Unavailable(
                "provider.embedding_failed",
                "Embeddings could not be generated."));
        }
    }
}
