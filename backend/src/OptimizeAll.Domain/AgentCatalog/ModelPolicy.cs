using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.AgentCatalog;

/// <summary>The LLM vendors the platform can route to. Adding one is an infrastructure change only.</summary>
public enum AiProvider
{
    OpenAi = 1,
    Anthropic = 2,
    Google = 3,
}

/// <summary>
/// Which model an agent uses, and what to fall back to when the primary provider is unavailable.
/// Expressed in the domain because "this agent must not be routed to a weaker model" is a business
/// rule about output quality, not a deployment detail.
/// </summary>
public sealed class ModelPolicy : ValueObject
{
    private ModelPolicy(
        AiProvider provider,
        string model,
        decimal temperature,
        int maxOutputTokens,
        IReadOnlyList<ModelFallback> fallbacks)
    {
        Provider = provider;
        Model = model;
        Temperature = temperature;
        MaxOutputTokens = maxOutputTokens;
        Fallbacks = fallbacks;
    }

    public AiProvider Provider { get; }

    public string Model { get; }

    public decimal Temperature { get; }

    public int MaxOutputTokens { get; }

    /// <summary>Tried in order when the primary provider fails. Empty means fail rather than degrade.</summary>
    public IReadOnlyList<ModelFallback> Fallbacks { get; }

    public static ModelPolicy Create(
        AiProvider provider,
        string model,
        decimal temperature = 0.2m,
        int maxOutputTokens = 4096,
        IReadOnlyList<ModelFallback>? fallbacks = null)
    {
        Ensure.NotNullOrWhiteSpace(model);

        if (temperature is < 0m or > 2m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(temperature),
                temperature,
                "Temperature must be between 0 and 2.");
        }

        Ensure.Positive(maxOutputTokens);

        return new ModelPolicy(provider, model.Trim(), temperature, maxOutputTokens, fallbacks ?? []);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Provider;
        yield return Model;
        yield return Temperature;
        yield return MaxOutputTokens;

        foreach (ModelFallback fallback in Fallbacks)
        {
            yield return fallback;
        }
    }
}

public sealed record ModelFallback(AiProvider Provider, string Model);
