using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Ai;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Infrastructure.Ai;

/// <summary>
/// Selects a provider for each request and fails over when one is unhealthy.
/// <para>
/// This is where provider independence becomes operational rather than merely architectural: the
/// Application layer asks for a completion, and this decides which vendor serves it, whether a
/// failover happened, and whether a provider should be rested.
/// </para>
/// </summary>
public sealed class ChatCompletionRouter(
    IEnumerable<IChatCompletionService> providers,
    IClock clock,
    ILogger<ChatCompletionRouter> logger)
    : IChatCompletionRouter
{
    /// <summary>Consecutive failures before a provider is taken out of rotation.</summary>
    private const int FailureThreshold = 5;

    /// <summary>How long a tripped provider is rested before one probe is allowed through.</summary>
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);

    private readonly Dictionary<AiProvider, IChatCompletionService> _providers =
        providers.ToDictionary(p => p.Provider);

    private readonly ConcurrentDictionary<AiProvider, CircuitState> _circuits = new();

    public async Task<Result<ChatResponse>> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<(AiProvider Provider, string Model)> attempts =
        [
            (request.ModelPolicy.Provider, request.ModelPolicy.Model),
            .. request.ModelPolicy.Fallbacks.Select(f => (f.Provider, f.Model)),
        ];

        Error? lastError = null;
        bool primaryFailed = false;

        foreach ((AiProvider provider, string model) in attempts)
        {
            if (!_providers.TryGetValue(provider, out IChatCompletionService? service))
            {
                logger.LogWarning("No adapter is registered for provider {Provider}; skipping.", provider);
                lastError = Error.Unavailable(
                    "provider.not_registered", $"No adapter is registered for provider {provider}.");
                primaryFailed = true;
                continue;
            }

            if (!IsCircuitClosed(provider))
            {
                logger.LogDebug("Skipping {Provider}: circuit open.", provider);
                lastError = Error.Unavailable(
                    "provider.circuit_open", $"Provider {provider} is temporarily out of rotation.");
                primaryFailed = true;
                continue;
            }

            // The fallback may name a different model, so the policy is rewritten for the attempt
            // rather than assuming every provider serves the primary model's name.
            ChatRequest attempt = request with
            {
                ModelPolicy = ModelPolicy.Create(
                    provider,
                    model,
                    request.ModelPolicy.Temperature,
                    request.ModelPolicy.MaxOutputTokens),
            };

            Result<ChatResponse> result = await service
                .CompleteAsync(attempt, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                RecordSuccess(provider);

                // Reported so that a run's record shows a fallback served it. Without this, a
                // silent failover would make a quality regression impossible to explain later.
                return primaryFailed
                    ? Result.Success(result.Value with { FailedOver = true })
                    : result;
            }

            RecordFailure(provider);
            lastError = result.Error;
            primaryFailed = true;

            logger.LogWarning(
                "Provider {Provider} failed with {ErrorCode}; trying the next configured option.",
                provider,
                result.Error.Code);
        }

        // Every provider is exhausted. The platform stays up and the caller decides what to do —
        // for an agent run, that means a recorded failure rather than a crashed worker.
        return Result.Failure<ChatResponse>(lastError ?? Error.Unavailable(
            "provider.none_available",
            "No configured AI provider was able to serve the request."));
    }

    private bool IsCircuitClosed(AiProvider provider)
    {
        if (!_circuits.TryGetValue(provider, out CircuitState? state))
        {
            return true;
        }

        lock (state)
        {
            if (state.ConsecutiveFailures < FailureThreshold)
            {
                return true;
            }

            // Half-open: after the rest period one request is allowed through to probe recovery.
            // Letting all traffic back at once would re-trip the circuit instantly on a provider
            // that is still degraded.
            if (state.OpenedAt is { } openedAt && clock.UtcNow - openedAt >= BreakDuration)
            {
                state.ConsecutiveFailures = FailureThreshold - 1;
                state.OpenedAt = null;
                return true;
            }

            return false;
        }
    }

    private void RecordSuccess(AiProvider provider)
    {
        CircuitState state = _circuits.GetOrAdd(provider, static _ => new CircuitState());

        lock (state)
        {
            state.ConsecutiveFailures = 0;
            state.OpenedAt = null;
        }
    }

    private void RecordFailure(AiProvider provider)
    {
        CircuitState state = _circuits.GetOrAdd(provider, static _ => new CircuitState());

        lock (state)
        {
            state.ConsecutiveFailures++;

            if (state.ConsecutiveFailures >= FailureThreshold && state.OpenedAt is null)
            {
                state.OpenedAt = clock.UtcNow;

                logger.LogError(
                    "Provider {Provider} tripped its circuit after {Failures} consecutive failures; " +
                    "resting for {BreakSeconds}s.",
                    provider,
                    state.ConsecutiveFailures,
                    BreakDuration.TotalSeconds);
            }
        }
    }

    private sealed class CircuitState
    {
        public int ConsecutiveFailures { get; set; }

        public DateTimeOffset? OpenedAt { get; set; }
    }
}
