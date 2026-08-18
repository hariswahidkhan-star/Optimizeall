using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Ai;
using OptimizeAll.SharedKernel.Results;
using AppChatMessage = OptimizeAll.Application.Abstractions.Ai.ChatMessage;
using AppChatRole = OptimizeAll.Application.Abstractions.Ai.ChatRole;
using AppToolCall = OptimizeAll.Application.Abstractions.Ai.ToolCall;
using DomainProvider = OptimizeAll.Domain.AgentCatalog.AiProvider;

namespace OptimizeAll.Infrastructure.Ai.Providers;

/// <summary>
/// Google Gemini adapter, over the REST generateContent endpoint.
/// <para>
/// Unlike the Anthropic and OpenAI adapters this one speaks raw HTTP, because Google does not ship a
/// first-party .NET SDK for the Gemini API. The wire shape is handled here and nowhere else, so the
/// rest of the platform is unaffected by the difference.
/// </para>
/// </summary>
public sealed class GeminiChatCompletionService(
    HttpClient httpClient,
    IModelPricing pricing,
    ILogger<GeminiChatCompletionService> logger)
    : IChatCompletionService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public DomainProvider Provider => DomainProvider.Google;

    public bool IsAvailable => true;

    public async Task<Result<ChatResponse>> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            GenerateContentRequest payload = BuildRequest(request);

            using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                $"v1beta/models/{request.ModelPolicy.Model}:generateContent",
                payload,
                Json,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Gemini returned {StatusCode} for model {Model}.",
                    (int)response.StatusCode,
                    request.ModelPolicy.Model);

                return Result.Failure<ChatResponse>(Error.Unavailable(
                    "provider.gemini_failed",
                    $"The Gemini provider returned status {(int)response.StatusCode}."));
            }

            GenerateContentResponse? body = await response.Content
                .ReadFromJsonAsync<GenerateContentResponse>(Json, cancellationToken).ConfigureAwait(false);

            return body is null
                ? Result.Failure<ChatResponse>(Error.Unavailable(
                    "provider.gemini_empty",
                    "The Gemini provider returned an empty body."))
                : Translate(request, body, Stopwatch.GetElapsedTime(startedAt));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Gemini completion failed for model {Model}.", request.ModelPolicy.Model);

            return Result.Failure<ChatResponse>(Error.Unavailable(
                "provider.gemini_failed",
                "The Gemini provider did not return a completion."));
        }
    }

    private static GenerateContentRequest BuildRequest(ChatRequest request)
    {
        List<Content> contents = [];
        List<Part> systemParts = [];

        foreach (AppChatMessage message in request.Messages)
        {
            switch (message.Role)
            {
                case AppChatRole.System:
                    systemParts.Add(new Part { Text = message.Content });
                    break;

                case AppChatRole.User:
                    contents.Add(new Content { Role = "user", Parts = [new Part { Text = message.Content }] });
                    break;

                case AppChatRole.Assistant:
                    // Gemini names the assistant turn "model".
                    contents.Add(new Content { Role = "model", Parts = [new Part { Text = message.Content }] });
                    break;

                case AppChatRole.Tool:
                    contents.Add(new Content
                    {
                        Role = "user",
                        Parts =
                        [
                            new Part
                            {
                                FunctionResponse = new FunctionResponse
                                {
                                    Name = message.ToolCallId
                                        ?? throw new InvalidOperationException(
                                            "A tool result message must carry the id of the call it answers."),
                                    Response = JsonSerializer.Deserialize<JsonElement>(
                                        WrapAsObject(message.Content)),
                                },
                            },
                        ],
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported chat role '{message.Role}'.");
            }
        }

        return new GenerateContentRequest
        {
            Contents = contents,
            SystemInstruction = systemParts.Count == 0 ? null : new Content { Parts = systemParts },
            GenerationConfig = new GenerationConfig
            {
                Temperature = (double)request.ModelPolicy.Temperature,
                MaxOutputTokens = request.ModelPolicy.MaxOutputTokens,
            },
            Tools = request.Tools.Count == 0
                ? null
                :
                [
                    new ToolDeclaration
                    {
                        FunctionDeclarations =
                        [
                            .. request.Tools.Select(tool => new FunctionDeclaration
                            {
                                Name = tool.Name,
                                Description = tool.Description,
                                Parameters = JsonSerializer.Deserialize<JsonElement>(tool.ParametersJsonSchema),
                            }),
                        ],
                    },
                ],
        };
    }

    /// <summary>
    /// Gemini requires a function response to be a JSON object. Tool results that are arrays or
    /// scalars are wrapped rather than rejected, so a perfectly valid tool cannot fail purely
    /// because of one provider's shape requirement.
    /// </summary>
    private static string WrapAsObject(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind == JsonValueKind.Object
                ? json
                : JsonSerializer.Serialize(new { result = document.RootElement.Clone() });
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { result = json });
        }
    }

    private Result<ChatResponse> Translate(ChatRequest request, GenerateContentResponse body, TimeSpan latency)
    {
        Candidate? candidate = body.Candidates?.FirstOrDefault();

        List<string> textParts = [];
        List<AppToolCall> toolCalls = [];

        foreach (Part part in candidate?.Content?.Parts ?? [])
        {
            if (!string.IsNullOrEmpty(part.Text))
            {
                textParts.Add(part.Text);
            }

            if (part.FunctionCall is { } call)
            {
                // Gemini does not return a per-call id, so one is synthesised from the function name.
                // The platform correlates results by this value, and it must round-trip unchanged.
                toolCalls.Add(new AppToolCall(
                    call.Name,
                    call.Name,
                    call.Args.HasValue ? call.Args.Value.GetRawText() : "{}"));
            }
        }

        TokenUsage usage = new(
            body.UsageMetadata?.PromptTokenCount ?? 0,
            body.UsageMetadata?.CandidatesTokenCount ?? 0);

        return Result.Success(new ChatResponse
        {
            Content = string.Join("\n", textParts),
            ToolCalls = toolCalls,
            FinishReason = MapFinishReason(candidate?.FinishReason, toolCalls.Count),
            Usage = usage,
            Provider = DomainProvider.Google,
            Model = request.ModelPolicy.Model,
            Cost = pricing.Price(DomainProvider.Google, request.ModelPolicy.Model, usage),
            Latency = latency,
        });
    }

    private static ChatFinishReason MapFinishReason(string? finishReason, int toolCallCount) => finishReason switch
    {
        "MAX_TOKENS" => ChatFinishReason.MaxTokens,
        "SAFETY" or "BLOCKLIST" or "PROHIBITED_CONTENT" => ChatFinishReason.ContentFilter,
        _ => toolCallCount > 0 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
    };

    // Wire contracts. Kept private so no Gemini-shaped type can escape this adapter.

    private sealed record GenerateContentRequest
    {
        [JsonPropertyName("contents")]
        public required List<Content> Contents { get; init; }

        [JsonPropertyName("systemInstruction")]
        public Content? SystemInstruction { get; init; }

        [JsonPropertyName("generationConfig")]
        public GenerationConfig? GenerationConfig { get; init; }

        [JsonPropertyName("tools")]
        public List<ToolDeclaration>? Tools { get; init; }
    }

    private sealed record Content
    {
        [JsonPropertyName("role")]
        public string? Role { get; init; }

        [JsonPropertyName("parts")]
        public required List<Part> Parts { get; init; }
    }

    private sealed record Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("functionCall")]
        public FunctionCall? FunctionCall { get; init; }

        [JsonPropertyName("functionResponse")]
        public FunctionResponse? FunctionResponse { get; init; }
    }

    private sealed record FunctionCall
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("args")]
        public JsonElement? Args { get; init; }
    }

    private sealed record FunctionResponse
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("response")]
        public required JsonElement Response { get; init; }
    }

    private sealed record GenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; init; }
    }

    private sealed record ToolDeclaration
    {
        [JsonPropertyName("functionDeclarations")]
        public required List<FunctionDeclaration> FunctionDeclarations { get; init; }
    }

    private sealed record FunctionDeclaration
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("description")]
        public required string Description { get; init; }

        [JsonPropertyName("parameters")]
        public JsonElement Parameters { get; init; }
    }

    private sealed record GenerateContentResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; init; }

        [JsonPropertyName("usageMetadata")]
        public UsageMetadata? UsageMetadata { get; init; }
    }

    private sealed record Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; init; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; init; }
    }

    private sealed record UsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public long PromptTokenCount { get; init; }

        [JsonPropertyName("candidatesTokenCount")]
        public long CandidatesTokenCount { get; init; }
    }
}
