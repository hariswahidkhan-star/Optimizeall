using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using OptimizeAll.Application.Abstractions.Ai;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;

namespace OptimizeAll.Infrastructure.Ai;

/// <summary>
/// Per-million-token prices for one model, in a single currency.
/// </summary>
public sealed class ModelPrice
{
    public required string Model { get; init; }

    public required decimal InputPerMillionTokens { get; init; }

    public required decimal OutputPerMillionTokens { get; init; }

    public string Currency { get; init; } = "USD";
}

public sealed class ModelPricingOptions
{
    public const string SectionName = "Ai:Pricing";

    /// <summary>Prices keyed by provider, then by model id.</summary>
    public Dictionary<string, List<ModelPrice>> Providers { get; init; } = [];
}

/// <summary>
/// Converts token usage into money.
/// <para>
/// Prices live in configuration, not in code, because a vendor price change should be a
/// configuration deployment rather than a release. An unknown model is a hard failure rather than a
/// zero price: silently costing an unpriced model at nothing would make every budget ceiling,
/// spend alert and cost report quietly wrong, and the failure would surface as a surprise invoice
/// rather than as an error.
/// </para>
/// </summary>
public sealed class ModelPricing : IModelPricing
{
    private const decimal TokensPerMillion = 1_000_000m;

    private readonly ConcurrentDictionary<(AiProvider Provider, string Model), ModelPrice> _prices;

    public ModelPricing(IOptions<ModelPricingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _prices = new ConcurrentDictionary<(AiProvider, string), ModelPrice>();

        foreach ((string providerName, List<ModelPrice> prices) in options.Value.Providers)
        {
            if (!Enum.TryParse(providerName, ignoreCase: true, out AiProvider provider))
            {
                throw new InvalidOperationException(
                    $"Pricing configuration names an unknown provider '{providerName}'. " +
                    $"Valid providers: {string.Join(", ", Enum.GetNames<AiProvider>())}.");
            }

            foreach (ModelPrice price in prices)
            {
                _prices[(provider, price.Model)] = price;
            }
        }
    }

    public Money Price(AiProvider provider, string model, TokenUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        ModelPrice price = Resolve(provider, model);

        decimal amount =
            ((usage.PromptTokens * price.InputPerMillionTokens)
             + (usage.CompletionTokens * price.OutputPerMillionTokens))
            / TokensPerMillion;

        return Money.Of(amount, price.Currency);
    }

    /// <summary>
    /// Upper bound on what a call could cost before it is made.
    /// <para>
    /// Assumes the model emits its full <paramref name="maxOutputTokens"/> allowance. That is
    /// deliberately pessimistic: this figure gates the budget pre-check, and an optimistic estimate
    /// would let through exactly the call the check exists to prevent.
    /// </para>
    /// </summary>
    public Money EstimateUpperBound(AiProvider provider, string model, long promptTokens, int maxOutputTokens)
    {
        ModelPrice price = Resolve(provider, model);

        decimal amount =
            ((promptTokens * price.InputPerMillionTokens)
             + (maxOutputTokens * price.OutputPerMillionTokens))
            / TokensPerMillion;

        return Money.Of(amount, price.Currency);
    }

    private ModelPrice Resolve(AiProvider provider, string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (_prices.TryGetValue((provider, model), out ModelPrice? price))
        {
            return price;
        }

        throw new InvalidOperationException(
            $"No price is configured for {provider} model '{model}'. " +
            "Add it under the Ai:Pricing configuration section. Runs are refused rather than " +
            "costed at zero, because an uncosted run would silently bypass every budget ceiling.");
    }
}
