using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Infrastructure.Seed;

/// <summary>
/// The default agent workforce seeded into a new workspace.
/// <para>
/// Held as data rather than as code so the catalog can be reviewed by the people accountable for it
/// — a marketing lead can read the SEO agent's mission and budget without reading C#. The loader
/// validates it against the domain's own rules, so a seed file that would produce an unpublishable
/// agent fails at load rather than at first run.
/// </para>
/// </summary>
public sealed class AgentWorkforceSeed
{
    private const string ResourceName = "OptimizeAll.Infrastructure.Seed.agents.default-workforce.json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    public required int Version { get; init; }

    public required IReadOnlyList<SeedAgent> Agents { get; init; }

    /// <summary>Reads the embedded catalog. Throws when it is missing or malformed — there is no usable fallback.</summary>
    public static AgentWorkforceSeed Load()
    {
        Assembly assembly = typeof(AgentWorkforceSeed).Assembly;

        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The agent workforce seed '{ResourceName}' is not embedded in {assembly.GetName().Name}.");

        return JsonSerializer.Deserialize<AgentWorkforceSeed>(stream, Json)
            ?? throw new InvalidOperationException("The agent workforce seed deserialised to null.");
    }

    /// <summary>
    /// Materialises one seed entry as a published <see cref="AgentDefinition"/>.
    /// <para>
    /// Every grant goes through <see cref="AgentDefinition.GrantTool"/>, so the risk-ceiling check
    /// applies to seeded agents exactly as it does to tenant-defined ones. Seeded data is not
    /// trusted more than user data; it is simply written by us.
    /// </para>
    /// </summary>
    public static Result<AgentDefinition> Materialise(
        SeedAgent seed,
        TenantId tenantId,
        WorkspaceId workspaceId,
        ApprovalPolicyId? approvalPolicyId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(seed);

        Result<AgentDefinition> created = AgentDefinition.CreateDraft(
            tenantId,
            workspaceId,
            seed.AgentKey,
            version: 1,
            seed.DisplayName,
            seed.Mission,
            seed.SystemPrompt,
            ModelPolicy.Create(
                seed.ModelPolicy.Provider,
                seed.ModelPolicy.Model,
                seed.ModelPolicy.Temperature,
                seed.ModelPolicy.MaxOutputTokens,
                [.. seed.ModelPolicy.Fallbacks.Select(f => new ModelFallback(f.Provider, f.Model))]),
            MemoryPolicy.Create(
                seed.MemoryPolicy.EpisodicEnabled,
                seed.MemoryPolicy.SemanticEnabled,
                seed.MemoryPolicy.EpisodicRecallLimit,
                seed.MemoryPolicy.SemanticRecallLimit,
                episodicRetention: null,
                seed.MemoryPolicy.RedactPersonalData),
            BudgetPolicy.Create(
                seed.BudgetPolicy.MaxTotalTokens,
                Money.Of(seed.BudgetPolicy.MaxCostAmount, seed.BudgetPolicy.MaxCostCurrency),
                seed.BudgetPolicy.MaxToolCalls,
                seed.BudgetPolicy.MaxIterations,
                TimeSpan.FromMinutes(seed.BudgetPolicy.MaxWallClockMinutes)),
            seed.MaxRiskClass);

        if (created.IsFailure)
        {
            return created;
        }

        AgentDefinition definition = created.Value;

        foreach (string toolKey in seed.Tools)
        {
            Result granted = definition.GrantTool(toolKey);

            if (granted.IsFailure)
            {
                return Result.Failure<AgentDefinition>(Error.Invariant(
                    "seed.invalid_tool_grant",
                    $"Agent '{seed.AgentKey}' cannot be granted '{toolKey}': {granted.Error.Message}"));
            }
        }

        foreach (SeedKpi kpi in seed.Kpis)
        {
            definition.AddKpi(KpiDefinition.Create(kpi.Key, kpi.DisplayName, kpi.Unit, kpi.Direction));
        }

        if (approvalPolicyId is { } policyId)
        {
            definition.AttachApprovalPolicy(policyId);
        }

        Result published = definition.Publish(now);

        return published.IsFailure
            ? Result.Failure<AgentDefinition>(published.Error)
            : Result.Success(definition);
    }
}

public sealed record SeedAgent
{
    public required string AgentKey { get; init; }

    public required string DisplayName { get; init; }

    public required string Mission { get; init; }

    public required ActionRiskClass MaxRiskClass { get; init; }

    public required string SystemPrompt { get; init; }

    public required SeedModelPolicy ModelPolicy { get; init; }

    public required SeedMemoryPolicy MemoryPolicy { get; init; }

    public required SeedBudgetPolicy BudgetPolicy { get; init; }

    public required IReadOnlyList<string> Tools { get; init; }

    /// <summary>Null when the agent is purely event-driven.</summary>
    public SeedSchedule? Schedule { get; init; }

    public IReadOnlyList<SeedKpi> Kpis { get; init; } = [];

    /// <summary>Whether this agent can propose an action that requires human sign-off.</summary>
    public bool RequiresApprovalPolicy => MaxRiskClass.RequiresHumanApproval();
}

public sealed record SeedModelPolicy
{
    public required AiProvider Provider { get; init; }

    public required string Model { get; init; }

    public decimal Temperature { get; init; } = 0.2m;

    public int MaxOutputTokens { get; init; } = 8192;

    public IReadOnlyList<SeedModelFallback> Fallbacks { get; init; } = [];
}

public sealed record SeedModelFallback(AiProvider Provider, string Model);

public sealed record SeedMemoryPolicy
{
    public bool EpisodicEnabled { get; init; } = true;

    public bool SemanticEnabled { get; init; } = true;

    public int EpisodicRecallLimit { get; init; } = 10;

    public int SemanticRecallLimit { get; init; } = 8;

    public bool RedactPersonalData { get; init; } = true;
}

public sealed record SeedBudgetPolicy
{
    public required long MaxTotalTokens { get; init; }

    public required decimal MaxCostAmount { get; init; }

    public string MaxCostCurrency { get; init; } = "USD";

    public required int MaxToolCalls { get; init; }

    public required int MaxIterations { get; init; }

    public required int MaxWallClockMinutes { get; init; }
}

public sealed record SeedSchedule
{
    public required string Cron { get; init; }

    public required string Timezone { get; init; }

    public string MissPolicy { get; init; } = "Skip";
}

public sealed record SeedKpi
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public required string Unit { get; init; }

    public required KpiDirection Direction { get; init; }
}
