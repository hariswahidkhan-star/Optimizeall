using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;

namespace OptimizeAll.Infrastructure.Persistence.Configurations;

public sealed class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("agent_run");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(r => r.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.AgentDefinitionId).HasColumnName("agent_definition_id").IsRequired();
        builder.Property(r => r.AgentKey).HasColumnName("agent_key").HasMaxLength(100).IsRequired();
        builder.Property(r => r.TaskId).HasColumnName("task_id");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(r => r.TriggerType).HasColumnName("trigger_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(r => r.TriggeredBy).HasColumnName("triggered_by");
        builder.Property(r => r.InputJson).HasColumnName("input").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.OutputJson).HasColumnName("output").HasColumnType("jsonb");
        builder.Property(r => r.ErrorJson).HasColumnName("error").HasColumnType("jsonb");
        builder.Property(r => r.Provider).HasColumnName("provider").HasConversion<string?>().HasMaxLength(30);
        builder.Property(r => r.Model).HasColumnName("model").HasMaxLength(100);
        builder.Property(r => r.StartedAt).HasColumnName("started_at");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.LeaseHolder).HasColumnName("lease_holder").HasMaxLength(200);
        builder.Property(r => r.LeaseExpiresAt).HasColumnName("lease_expires_at");
        builder.Property(r => r.BlockingApprovalId).HasColumnName("blocking_approval_id");
        builder.Property(r => r.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(r => r.IsDryRun).HasColumnName("is_dry_run").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.Version).HasColumnName("version").IsConcurrencyToken();

        builder.OwnsOne(r => r.Budget, ledger =>
        {
            ledger.Property(l => l.PromptTokens).HasColumnName("prompt_tokens").IsRequired();
            ledger.Property(l => l.CompletionTokens).HasColumnName("completion_tokens").IsRequired();
            ledger.Property(l => l.ToolCallCount).HasColumnName("tool_call_count").IsRequired();
            ledger.Property(l => l.IterationCount).HasColumnName("iteration_count").IsRequired();

            ledger.Property(l => l.CostAmount).HasColumnName("cost_amount").HasPrecision(19, 4).IsRequired();
            ledger.Property(l => l.CostCurrency).HasColumnName("cost_currency").HasMaxLength(3).IsFixedLength().IsRequired();

            ledger.Ignore(l => l.TotalTokens);
            ledger.Ignore(l => l.CostIncurred);
        });

        builder.Navigation(r => r.Budget).IsRequired();

        // Serves the lease-reclamation sweep, which runs every few seconds and must not scan.
        builder.HasIndex(r => new { r.WorkspaceId, r.Status, r.LeaseExpiresAt });
        builder.HasIndex(r => new { r.WorkspaceId, r.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(r => r.CorrelationId);

        builder.OwnsMany(r => r.Steps, step =>
        {
            step.ToTable("agent_run_step");
            step.WithOwner().HasForeignKey(nameof(AgentRunStep.AgentRunId));
            step.HasKey(s => s.Id);

            step.Property(s => s.Id).HasColumnName("id");
            step.Property(s => s.AgentRunId).HasColumnName("agent_run_id");
            step.Property(s => s.Sequence).HasColumnName("sequence").IsRequired();
            step.Property(s => s.StepType).HasColumnName("step_type").HasConversion<string>().HasMaxLength(30).IsRequired();
            step.Property(s => s.ContentJson).HasColumnName("content").HasColumnType("jsonb").IsRequired();
            step.Property(s => s.Tokens).HasColumnName("tokens").IsRequired();
            step.Property(s => s.LatencyMilliseconds).HasColumnName("latency_ms").IsRequired();
            step.Property(s => s.OccurredAt).HasColumnName("occurred_at").IsRequired();

            step.HasIndex(s => new { s.AgentRunId, s.Sequence }).IsUnique();
        });

        builder.OwnsMany(r => r.ToolInvocations, invocation =>
        {
            invocation.ToTable("tool_invocation");
            invocation.WithOwner().HasForeignKey(nameof(ToolInvocation.AgentRunId));
            invocation.HasKey(i => i.Id);

            invocation.Property(i => i.Id).HasColumnName("id");
            invocation.Property(i => i.AgentRunId).HasColumnName("agent_run_id");
            invocation.Property(i => i.ToolKey).HasColumnName("tool_key").HasMaxLength(100).IsRequired();
            invocation.Property(i => i.RiskClass).HasColumnName("risk_class").HasConversion<string>().HasMaxLength(30).IsRequired();
            invocation.Property(i => i.ArgumentsJson).HasColumnName("arguments").HasColumnType("jsonb").IsRequired();
            invocation.Property(i => i.ResultJson).HasColumnName("result").HasColumnType("jsonb");
            invocation.Property(i => i.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
            invocation.Property(i => i.DenialReason).HasColumnName("denial_reason").HasMaxLength(2000);
            invocation.Property(i => i.ApprovalRequestId).HasColumnName("approval_request_id");
            invocation.Property(i => i.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200);
            invocation.Property(i => i.RequestedAt).HasColumnName("requested_at").IsRequired();
            invocation.Property(i => i.CompletedAt).HasColumnName("completed_at");
            invocation.Property(i => i.DurationMilliseconds).HasColumnName("duration_ms");

            invocation.Ignore(i => i.RequiresApproval);
            invocation.HasIndex(i => i.AgentRunId);
        });

        builder.Ignore(r => r.IsTerminal);
        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class AgentMemoryEntryConfiguration : IEntityTypeConfiguration<AgentMemoryEntry>
{
    public void Configure(EntityTypeBuilder<AgentMemoryEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("agent_memory_entry");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(m => m.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.AgentKey).HasColumnName("agent_key").HasMaxLength(100).IsRequired();
        builder.Property(m => m.Tier).HasColumnName("tier").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.SourceRunId).HasColumnName("source_run_id");
        builder.Property(m => m.Content).HasColumnName("content").HasMaxLength(int.MaxValue).IsRequired();
        builder.Property(m => m.Importance).HasColumnName("importance").IsRequired();
        builder.Property(m => m.Outcome).HasColumnName("outcome_signal").HasConversion<string?>().HasMaxLength(30);
        builder.Property(m => m.ExpiresAt).HasColumnName("expires_at");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
        builder.Property(m => m.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(m => new { m.WorkspaceId, m.AgentKey, m.Environment, m.Tier });
        builder.Ignore(m => m.DomainEvents);
    }
}
