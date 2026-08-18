using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptimizeAll.Domain.Insights;
using OptimizeAll.Domain.Knowledge;
using OptimizeAll.Domain.Notifications;
using OptimizeAll.Domain.Orchestration;
using OptimizeAll.Domain.Scheduling;
using Pgvector;

namespace OptimizeAll.Infrastructure.Persistence.Configurations;

public sealed class WorkflowRunConfiguration : IEntityTypeConfiguration<WorkflowRun>
{
    public void Configure(EntityTypeBuilder<WorkflowRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("workflow_run");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(w => w.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(w => w.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(w => w.ObjectiveTitle).HasColumnName("objective_title").HasMaxLength(300).IsRequired();
        builder.Property(w => w.ObjectivePayloadJson).HasColumnName("objective_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(w => w.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(w => w.Deadline).HasColumnName("deadline");
        builder.Property(w => w.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(w => w.CompletedAt).HasColumnName("completed_at");
        builder.Property(w => w.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
        builder.Property(w => w.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");
        builder.Property(w => w.Version).HasColumnName("version").IsConcurrencyToken();

        builder.OwnsOne(w => w.EstimatedCost, money =>
        {
            money.Property(m => m.Amount).HasColumnName("estimated_cost_amount").HasPrecision(19, 4);
            money.Property(m => m.Currency).HasColumnName("estimated_cost_currency").HasMaxLength(3).IsFixedLength();
        });

        builder.OwnsOne(w => w.ActualCost, money =>
        {
            money.Property(m => m.Amount).HasColumnName("actual_cost_amount").HasPrecision(19, 4).IsRequired();
            money.Property(m => m.Currency).HasColumnName("actual_cost_currency").HasMaxLength(3).IsFixedLength().IsRequired();
        });

        builder.Navigation(w => w.ActualCost).IsRequired();

        builder.HasIndex(w => new { w.WorkspaceId, w.Status });
        builder.HasIndex(w => w.CorrelationId);

        builder.OwnsMany(w => w.Tasks, task =>
        {
            task.ToTable("work_task");
            task.WithOwner().HasForeignKey(nameof(WorkTask.WorkflowRunId));
            task.HasKey(t => t.Id);

            task.Property(t => t.Id).HasColumnName("id");
            task.Property(t => t.WorkflowRunId).HasColumnName("workflow_run_id");
            task.Property(t => t.Sequence).HasColumnName("sequence").IsRequired();
            task.Property(t => t.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            task.Property(t => t.AssignedAgentKey).HasColumnName("assigned_agent_key").HasMaxLength(100);
            task.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
            task.Property(t => t.InputJson).HasColumnName("input").HasColumnType("jsonb").IsRequired();
            task.Property(t => t.OutputJson).HasColumnName("output").HasColumnType("jsonb");
            task.Property(t => t.AgentRunId).HasColumnName("agent_run_id");
            task.Property(t => t.AttemptCount).HasColumnName("attempt_count").IsRequired();
            task.Property(t => t.MaxAttempts).HasColumnName("max_attempts").IsRequired();
            task.Property(t => t.HeartbeatAt).HasColumnName("heartbeat_at");
            task.Property(t => t.BlockedReason).HasColumnName("blocked_reason").HasMaxLength(2000);
            task.Property(t => t.StartedAt).HasColumnName("started_at");
            task.Property(t => t.CompletedAt).HasColumnName("completed_at");

            task.Ignore(t => t.IsTerminal);

            task.OwnsMany(t => t.Dependencies, dependency =>
            {
                dependency.ToTable("task_dependency");
                dependency.WithOwner().HasForeignKey(nameof(TaskDependency.TaskId));

                dependency.Property(d => d.TaskId).HasColumnName("task_id");
                dependency.Property(d => d.DependsOnTaskId).HasColumnName("depends_on_task_id");
                dependency.Property(d => d.Type).HasColumnName("dependency_type").HasConversion<string>().HasMaxLength(30).IsRequired();

                dependency.HasIndex(d => new { d.TaskId, d.DependsOnTaskId }).IsUnique();
            });

            task.HasIndex(t => new { t.WorkflowRunId, t.Status });
        });

        builder.Ignore(w => w.ReadyTasks);
        builder.Ignore(w => w.DomainEvents);
    }
}

public sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("knowledge_document");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(d => d.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(d => d.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(d => d.SourceUri).HasColumnName("source_uri").HasMaxLength(2000).IsRequired();
        builder.Property(d => d.SourceType).HasColumnName("source_type").HasMaxLength(50).IsRequired();
        builder.Property(d => d.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(d => d.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
        builder.Property(d => d.IngestedAt).HasColumnName("ingested_at");
        builder.Property(d => d.ExpiresAt).HasColumnName("expires_at");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
        builder.Property(d => d.DeletedAt).HasColumnName("deleted_at");
        builder.Property(d => d.Version).HasColumnName("version").IsConcurrencyToken();

        // Re-ingesting identical content is a no-op rather than a duplicate.
        builder.HasIndex(d => new { d.WorkspaceId, d.Environment, d.ContentHash }).IsUnique();

        builder.HasMany(d => d.Chunks).WithOne().HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(d => d.DomainEvents);
    }
}

public sealed class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    /// <summary>Matches the platform's embedding model. Changing it is a migration, not a setting.</summary>
    public const int EmbeddingDimensions = 1536;

    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("knowledge_chunk");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(c => c.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(c => c.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Sequence).HasColumnName("sequence").IsRequired();
        builder.Property(c => c.Content).HasColumnName("content").HasMaxLength(int.MaxValue).IsRequired();
        builder.Property(c => c.TokenCount).HasColumnName("token_count").IsRequired();

        builder.Property(c => c.Embedding)
            .HasColumnName("embedding")
            .HasColumnType($"vector({EmbeddingDimensions})")
            .HasConversion(
                value => value == null ? null : new Vector(value),
                vector => vector == null ? null : vector.Memory.ToArray());

        // Scope first, vector second. Filtering before the nearest-neighbour scan is what makes it
        // impossible for an approximate index to surface another tenant's content.
        builder.HasIndex(c => new { c.WorkspaceId, c.Environment });
        builder.Ignore(c => c.IsSearchable);
    }
}

public sealed class ScheduleDefinitionConfiguration : IEntityTypeConfiguration<ScheduleDefinition>
{
    public void Configure(EntityTypeBuilder<ScheduleDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("schedule_definition");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(s => s.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
        builder.Property(s => s.CronExpression).HasColumnName("cron_expression").HasMaxLength(200).IsRequired();
        builder.Property(s => s.TimeZoneId).HasColumnName("timezone").HasMaxLength(100).IsRequired();
        builder.Property(s => s.TargetType).HasColumnName("target_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(s => s.TargetKey).HasColumnName("target_key").HasMaxLength(100).IsRequired();
        builder.Property(s => s.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.MissPolicy).HasColumnName("miss_policy").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(s => s.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(s => s.NextOccurrenceUtc).HasColumnName("next_occurrence_utc");
        builder.Property(s => s.LastOccurrenceUtc).HasColumnName("last_occurrence_utc");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(s => new { s.WorkspaceId, s.Environment, s.Key }).IsUnique();

        // The scheduler tick's only query: due, enabled schedules ordered by next occurrence.
        builder.HasIndex(s => new { s.IsEnabled, s.NextOccurrenceUtc });

        builder.Ignore(s => s.DomainEvents);
    }
}

public sealed class ScheduleOccurrenceConfiguration : IEntityTypeConfiguration<ScheduleOccurrence>
{
    public void Configure(EntityTypeBuilder<ScheduleOccurrence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("schedule_occurrence");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(o => o.ScheduleId).HasColumnName("schedule_id").IsRequired();
        builder.Property(o => o.OccurrenceUtc).HasColumnName("occurrence_utc").IsRequired();
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(o => o.ClaimedAt).HasColumnName("claimed_at").IsRequired();
        builder.Property(o => o.DispatchedAt).HasColumnName("dispatched_at");
        builder.Property(o => o.DispatchedRunId).HasColumnName("dispatched_run_id");
        builder.Property(o => o.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
        builder.Property(o => o.Version).HasColumnName("version").IsConcurrencyToken();

        // The entire exactly-once guarantee, in one constraint: every scheduler replica may attempt
        // the insert, and exactly one succeeds.
        builder.HasIndex(o => new { o.ScheduleId, o.OccurrenceUtc }).IsUnique();

        builder.Ignore(o => o.DomainEvents);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("notification");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(n => n.RecipientId).HasColumnName("recipient_user_id").IsRequired();
        builder.Property(n => n.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(n => n.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(n => n.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(n => n.Body).HasColumnName("body").HasMaxLength(4000).IsRequired();
        builder.Property(n => n.LinkUrl).HasColumnName("link_url").HasMaxLength(2000);
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(n => n.ReadAt).HasColumnName("read_at");
        builder.Property(n => n.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(n => new { n.RecipientId, n.ReadAt, n.CreatedAt });

        builder.OwnsMany(n => n.Deliveries, delivery =>
        {
            delivery.ToTable("notification_delivery");
            delivery.WithOwner();
            delivery.HasKey(d => d.Id);

            delivery.Property(d => d.Id).HasColumnName("id");
            delivery.Property(d => d.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(30).IsRequired();
            delivery.Property(d => d.IsDelivered).HasColumnName("is_delivered").IsRequired();
            delivery.Property(d => d.DeliveredAt).HasColumnName("delivered_at");
            delivery.Property(d => d.AttemptCount).HasColumnName("attempt_count").IsRequired();
            delivery.Property(d => d.LastError).HasColumnName("last_error").HasMaxLength(2000);
        });

        builder.Ignore(n => n.IsUnread);
        builder.Ignore(n => n.HasUndeliveredChannel);
        builder.Ignore(n => n.DomainEvents);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_message");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.MessageType).HasColumnName("message_type").HasMaxLength(200).IsRequired();
        builder.Property(m => m.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200).IsRequired();
        builder.Property(m => m.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(m => m.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(m => m.DispatchedAt).HasColumnName("dispatched_at");
        builder.Property(m => m.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(m => m.NextAttemptAt).HasColumnName("next_attempt_at").IsRequired();
        builder.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(4000);
        builder.Property(m => m.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(m => new { m.Status, m.NextAttemptAt });
        builder.HasIndex(m => new { m.TenantIdentifier, m.MessageType, m.IdempotencyKey }).IsUnique();

        builder.Ignore(m => m.DomainEvents);
    }
}

public sealed class KpiSnapshotConfiguration : IEntityTypeConfiguration<KpiSnapshot>
{
    public void Configure(EntityTypeBuilder<KpiSnapshot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("kpi_snapshot");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id).HasColumnName("id");
        builder.Property(k => k.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(k => k.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(k => k.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(k => k.KpiKey).HasColumnName("kpi_key").HasMaxLength(100).IsRequired();
        builder.Property(k => k.PeriodStart).HasColumnName("period_start").IsRequired();
        builder.Property(k => k.PeriodEnd).HasColumnName("period_end").IsRequired();
        builder.Property(k => k.Granularity).HasColumnName("granularity").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(k => k.Value).HasColumnName("value").HasPrecision(19, 4).IsRequired();
        builder.Property(k => k.Unit).HasColumnName("unit").HasMaxLength(50).IsRequired();
        builder.Property(k => k.DimensionsJson).HasColumnName("dimensions").HasColumnType("jsonb").IsRequired();
        builder.Property(k => k.ComputedAt).HasColumnName("computed_at").IsRequired();
        builder.Property(k => k.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(k => new { k.WorkspaceId, k.Environment, k.KpiKey, k.PeriodStart, k.Granularity });

        builder.Ignore(k => k.DomainEvents);
    }
}
