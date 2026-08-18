using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Abstractions.Ai;

/// <summary>
/// One LLM vendor. Adding a provider means implementing this interface and registering it — the
/// Domain and Application layers never change, which is the whole point of the abstraction.
/// </summary>
public interface IChatCompletionService
{
    AiProvider Provider { get; }

    Task<Result<ChatResponse>> CompleteAsync(ChatRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Whether this provider is currently usable. Consulted by the router so a provider with an open
    /// circuit breaker is skipped rather than tried and failed.
    /// </summary>
    bool IsAvailable { get; }
}

/// <summary>
/// Chooses a provider for a request and fails over when one is unavailable.
/// The Application layer depends on this, never on a concrete provider.
/// </summary>
public interface IChatCompletionRouter
{
    Task<Result<ChatResponse>> CompleteAsync(ChatRequest request, CancellationToken cancellationToken);
}

public interface IEmbeddingService
{
    AiProvider Provider { get; }

    int Dimensions { get; }

    Task<Result<IReadOnlyList<float[]>>> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken);
}

/// <summary>
/// Converts token counts into money. Prices live in configuration rather than in code so that a
/// vendor price change is a deployment of configuration, not a release.
/// </summary>
public interface IModelPricing
{
    Domain.Common.Money Price(AiProvider provider, string model, TokenUsage usage);

    /// <summary>
    /// Worst-case cost of a call before it is made. Used by the budget pre-check, so it must
    /// over-estimate rather than under-estimate — an optimistic estimate would let a run cross its
    /// ceiling on the very call the check exists to prevent.
    /// </summary>
    Domain.Common.Money EstimateUpperBound(AiProvider provider, string model, long promptTokens, int maxOutputTokens);
}
