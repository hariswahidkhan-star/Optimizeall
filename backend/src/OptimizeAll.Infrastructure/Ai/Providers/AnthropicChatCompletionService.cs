using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Ai;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;
using AppChatMessage = OptimizeAll.Application.Abstractions.Ai.ChatMessage;
using AppChatRole = OptimizeAll.Application.Abstractions.Ai.ChatRole;
using AppToolCall = OptimizeAll.Application.Abstractions.Ai.ToolCall;
using AppToolDefinition = OptimizeAll.Application.Abstractions.Ai.ToolDefinition;
using DomainProvider = OptimizeAll.Domain.AgentCatalog.AiProvider;

namespace OptimizeAll.Infrastructure.Ai.Providers;

/// <summary>
/// Anthropic adapter, built on the official Anthropic SDK.
/// <para>
/// Everything Anthropic-specific stops at this class. The Application layer sees only
/// <see cref="ChatRequest"/> and <see cref="ChatResponse"/>, which is what makes swapping providers
/// a configuration change instead of a rewrite.
/// </para>
/// </summary>
public sealed class AnthropicChatCompletionService(
    AnthropicClient client,
    IModelPricing pricing,
    ILogger<AnthropicChatCompletionService> logger)
    : IChatCompletionService
{
    public DomainProvider Provider => DomainProvider.Anthropic;

    /// <summary>
    /// Reported by the router. Availability is tracked by the resilience decorator around this
    /// service rather than here, so a provider adapter stays a pure translation layer.
    /// </summary>
    public bool IsAvailable => true;

    public async Task<Result<ChatResponse>> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            MessageCreateParams parameters = BuildParameters(request);

            Message response = await client.Messages
                .Create(parameters, cancellationToken: cancellationToken).ConfigureAwait(false);

            TimeSpan latency = Stopwatch.GetElapsedTime(startedAt);

            return Translate(request, response, latency);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Translated to a Result rather than propagated: a provider being unavailable is an
            // expected operational condition the router handles by failing over, not a defect.
            logger.LogWarning(
                exception,
                "Anthropic completion failed for model {Model}.",
                request.ModelPolicy.Model);

            return Result.Failure<ChatResponse>(Error.Unavailable(
                "provider.anthropic_failed",
                "The Anthropic provider did not return a completion."));
        }
    }

    private static MessageCreateParams BuildParameters(ChatRequest request)
    {
        (string? systemPrompt, List<MessageParam> messages) = SplitConversation(request.Messages);

        // Built in one initializer because the SDK's parameter properties are init-only.
        return new MessageCreateParams
        {
            Model = request.ModelPolicy.Model,
            MaxTokens = request.ModelPolicy.MaxOutputTokens,
            Messages = messages,
            System = systemPrompt is { } prompt ? (MessageCreateParamsSystem)prompt : null,
            Tools = request.Tools.Count > 0
                ? [.. request.Tools.Select(ToAnthropicTool)]
                : null,
        };
    }

    /// <summary>
    /// Anthropic takes the system prompt as a separate top-level field rather than as a message, so
    /// system turns are lifted out of the conversation and concatenated.
    /// </summary>
    private static (string? System, List<MessageParam> Messages) SplitConversation(
        IReadOnlyList<AppChatMessage> conversation)
    {
        List<string> systemParts = [];
        List<MessageParam> messages = [];

        foreach (AppChatMessage message in conversation)
        {
            switch (message.Role)
            {
                case AppChatRole.System:
                    systemParts.Add(message.Content);
                    break;

                case AppChatRole.User:
                    messages.Add(new MessageParam
                    {
                        Role = Role.User,
                        Content = message.Content,
                    });
                    break;

                case AppChatRole.Assistant:
                    messages.Add(new MessageParam
                    {
                        Role = Role.Assistant,
                        Content = message.Content,
                    });
                    break;

                case AppChatRole.Tool:
                    // A tool result is carried on a user turn, correlated by the id of the call it
                    // answers. The API rejects the request if any tool_use lacks a matching result.
                    messages.Add(new MessageParam
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new ToolResultBlockParam
                            {
                                ToolUseID = message.ToolCallId
                                    ?? throw new InvalidOperationException(
                                        "A tool result message must carry the id of the call it answers."),
                                Content = message.Content,
                            },
                        },
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported chat role '{message.Role}'.");
            }
        }

        return (systemParts.Count == 0 ? null : string.Join("\n\n", systemParts), messages);
    }

    private static ToolUnion ToAnthropicTool(AppToolDefinition definition)
    {
        using JsonDocument schema = JsonDocument.Parse(definition.ParametersJsonSchema);

        Dictionary<string, JsonElement> properties = [];
        List<string> required = [];

        if (schema.RootElement.TryGetProperty("properties", out JsonElement propertiesElement))
        {
            foreach (JsonProperty property in propertiesElement.EnumerateObject())
            {
                properties[property.Name] = property.Value.Clone();
            }
        }

        if (schema.RootElement.TryGetProperty("required", out JsonElement requiredElement)
            && requiredElement.ValueKind == JsonValueKind.Array)
        {
            required.AddRange(requiredElement.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null));
        }

        return new Tool
        {
            Name = definition.Name,
            Description = definition.Description,
            InputSchema = new()
            {
                Properties = properties,
                Required = required,
            },
        };
    }

    private Result<ChatResponse> Translate(ChatRequest request, Message response, TimeSpan latency)
    {
        List<string> textParts = [];
        List<AppToolCall> toolCalls = [];

        foreach (ContentBlock block in response.Content)
        {
            if (block.TryPickText(out TextBlock? text))
            {
                textParts.Add(text.Text);
            }
            else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
            {
                toolCalls.Add(new AppToolCall(
                    toolUse.ID,
                    toolUse.Name,
                    JsonSerializer.Serialize(toolUse.Input)));
            }
        }

        TokenUsage usage = new(response.Usage.InputTokens, response.Usage.OutputTokens);

        return Result.Success(new ChatResponse
        {
            Content = string.Join("\n", textParts),
            ToolCalls = toolCalls,
            FinishReason = MapFinishReason(response.StopReason?.ToString(), toolCalls.Count),
            Usage = usage,
            Provider = DomainProvider.Anthropic,
            Model = response.Model.ToString() ?? request.ModelPolicy.Model,
            Cost = pricing.Price(DomainProvider.Anthropic, request.ModelPolicy.Model, usage),
            Latency = latency,
        });
    }

    /// <summary>
    /// Maps Anthropic's stop reasons onto the platform's own.
    /// <para>
    /// A refusal is reported as <see cref="ChatFinishReason.ContentFilter"/> rather than as an error:
    /// the request completed, the model declined, and the agent runtime should record that outcome
    /// rather than retry against another provider as if the call had failed.
    /// </para>
    /// </summary>
    private static ChatFinishReason MapFinishReason(string? stopReason, int toolCallCount) => stopReason switch
    {
        "tool_use" => ChatFinishReason.ToolCalls,
        "max_tokens" => ChatFinishReason.MaxTokens,
        "refusal" => ChatFinishReason.ContentFilter,
        "end_turn" or "stop_sequence" => toolCallCount > 0 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
        _ => toolCallCount > 0 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
    };
}
