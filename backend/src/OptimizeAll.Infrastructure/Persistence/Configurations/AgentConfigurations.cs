using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;

namespace OptimizeAll.Infrastructure.Persistence.Configurations;

public sealed class AgentDefinitionConfiguration : IEntityTypeConfiguration<AgentDefinition>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<AgentDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("agent_definition");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(d => d.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(d => d.AgentKey).HasColumnName("agent_key").HasMaxLength(100).IsRequired();
        builder.Property(d => d.DefinitionVersion).HasColumnName("definition_version").IsRequired();
        builder.Property(d => d.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(d => d.Mission).HasColumnName("mission").HasMaxLength(2000).IsRequired();

        // System prompts are long-form and have no meaningful ceiling; capping them would eventually
        // truncate a legitimate agent specification.
        builder.Property(d => d.SystemPrompt).HasColumnName("system_prompt").HasMaxLength(int.MaxValue).IsRequired();

        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(d => d.MaxRiskClass).HasColumnName("max_risk_class").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(d => d.ApprovalPolicyId).HasColumnName("approval_policy_id");
        builder.Property(d => d.PublishedAt).HasColumnName("published_at");

        // Policies are stored as jsonb rather than flattened into columns: they are read as whole
        // objects, evolve independently of the schema, and are never queried field-by-field.
        builder.Property(d => d.ModelPolicy)
            .HasColumnName("model_policy")
            .HasColumnType("jsonb")
            .HasConversion(
                policy => JsonSerializer.Serialize(ModelPolicySnapshot.From(policy), Json),
                json => JsonSerializer.Deserialize<ModelPolicySnapshot>(json, Json)!.ToPolicy())
            .IsRequired();

        builder.Property(d => d.MemoryPolicy)
            .HasColumnName("memory_policy")
            .HasColumnType("jsonb")
            .HasConversion(
                policy => JsonSerializer.Serialize(MemoryPolicySnapshot.From(policy), Json),
                json => JsonSerializer.Deserialize<MemoryPolicySnapshot>(json, Json)!.ToPolicy())
            .IsRequired();

        builder.Property(d => d.BudgetPolicy)
            .HasColumnName("budget_policy")
            .HasColumnType("jsonb")
            .HasConversion(
                policy => JsonSerializer.Serialize(BudgetPolicySnapshot.From(policy), Json),
                json => JsonSerializer.Deserialize<BudgetPolicySnapshot>(json, Json)!.ToPolicy())
            .IsRequired();

        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
        builder.Property(d => d.DeletedAt).HasColumnName("deleted_at");
        builder.Property(d => d.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(d => new { d.WorkspaceId, d.AgentKey, d.DefinitionVersion }).IsUnique();
        builder.HasIndex(d => new { d.WorkspaceId, d.AgentKey, d.Status });

        builder.OwnsMany(d => d.ToolGrants, grant =>
        {
            grant.ToTable("agent_tool_grant");
            grant.WithOwner().HasForeignKey(nameof(ToolGrant.AgentDefinitionId));
            grant.HasKey(g => g.Id);

            grant.Property(g => g.Id).HasColumnName("id");
            grant.Property(g => g.AgentDefinitionId).HasColumnName("agent_definition_id");
            grant.Property(g => g.ToolKey).HasColumnName("tool_key").HasMaxLength(100).IsRequired();
            grant.Property(g => g.RiskClass).HasColumnName("risk_class").HasConversion<string>().HasMaxLength(30).IsRequired();
            grant.Property(g => g.ConstraintsJson).HasColumnName("constraints").HasColumnType("jsonb").IsRequired();

            grant.HasIndex(g => new { g.AgentDefinitionId, g.ToolKey }).IsUnique();
        });

        builder.OwnsMany(d => d.Kpis, kpi =>
        {
            kpi.ToTable("agent_kpi");
            kpi.WithOwner();

            kpi.Property(k => k.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
            kpi.Property(k => k.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            kpi.Property(k => k.Unit).HasColumnName("unit").HasMaxLength(50).IsRequired();
            kpi.Property(k => k.Direction).HasColumnName("direction").HasConversion<string>().HasMaxLength(20).IsRequired();
            kpi.Property(k => k.Target).HasColumnName("target").HasPrecision(19, 4);
        });

        builder.Ignore(d => d.DomainEvents);
    }

    /// <summary>
    /// Serialisation shapes for the policy value objects.
    /// <para>
    /// The value objects have private constructors and validated factories, which is what makes them
    /// trustworthy in the domain but unusable for direct deserialisation. Round-tripping through an
    /// explicit snapshot means a stored policy is re-validated on the way back in rather than being
    /// reconstructed by reflection past its own invariants.
    /// </para>
    /// </summary>
    private sealed record ModelPolicySnapshot(
        AiProvider Provider,
        string Model,
        decimal Temperature,
        int MaxOutputTokens,
        IReadOnlyList<ModelFallback> Fallbacks)
    {
        public static ModelPolicySnapshot From(ModelPolicy policy) => new(
            policy.Provider, policy.Model, policy.Temperature, policy.MaxOutputTokens, policy.Fallbacks);

        public ModelPolicy ToPolicy() => ModelPolicy.Create(Provider, Model, Temperature, MaxOutputTokens, Fallbacks);
    }

    private sealed record MemoryPolicySnapshot(
        bool EpisodicEnabled,
        bool SemanticEnabled,
        int EpisodicRecallLimit,
        int SemanticRecallLimit,
        TimeSpan? EpisodicRetention,
        bool RedactPersonalData)
    {
        public static MemoryPolicySnapshot From(MemoryPolicy policy) => new(
            policy.EpisodicEnabled,
            policy.SemanticEnabled,
            policy.EpisodicRecallLimit,
            policy.SemanticRecallLimit,
            policy.EpisodicRetention,
            policy.RedactPersonalData);

        public MemoryPolicy ToPolicy() => MemoryPolicy.Create(
            EpisodicEnabled, SemanticEnabled, EpisodicRecallLimit, SemanticRecallLimit, EpisodicRetention, RedactPersonalData);
    }

    private sealed record BudgetPolicySnapshot(
        long MaxTotalTokens,
        decimal MaxCostAmount,
        string MaxCostCurrency,
        int MaxToolCalls,
        int MaxIterations,
        TimeSpan MaxWallClock)
    {
        public static BudgetPolicySnapshot From(BudgetPolicy policy) => new(
            policy.MaxTotalTokens,
            policy.MaxCost.Amount,
            policy.MaxCost.Currency,
            policy.MaxToolCalls,
            policy.MaxIterations,
            policy.MaxWallClock);

        public BudgetPolicy ToPolicy() => BudgetPolicy.Create(
            MaxTotalTokens,
            Money.Of(MaxCostAmount, MaxCostCurrency),
            MaxToolCalls,
            MaxIterations,
            MaxWallClock);
    }
}
