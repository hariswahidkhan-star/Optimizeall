using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Governance;

namespace OptimizeAll.Infrastructure.Persistence.Configurations;

public sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("approval_request");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(a => a.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.AgentRunId).HasColumnName("agent_run_id");
        builder.Property(a => a.ToolInvocationId).HasColumnName("tool_invocation_id");
        builder.Property(a => a.RiskClass).HasColumnName("risk_class").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(a => a.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();

        builder.Property(a => a.PayloadFingerprint)
            .HasColumnName("payload_hash")
            .HasMaxLength(64)
            .IsFixedLength()
            .HasConversion(
                fingerprint => fingerprint.Value,
                value => PayloadFingerprint.FromHex(value))
            .IsRequired();

        builder.OwnsOne(a => a.EstimatedCost, money =>
        {
            money.Property(m => m.Amount).HasColumnName("estimated_cost_amount").HasPrecision(19, 4);
            money.Property(m => m.Currency).HasColumnName("estimated_cost_currency").HasMaxLength(3).IsFixedLength();
        });

        builder.Property(a => a.RequestedByType).HasColumnName("requested_by_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.RequestedById).HasColumnName("requested_by_principal").IsRequired();
        builder.Property(a => a.RequiredApproverCount).HasColumnName("required_approver_count").IsRequired();
        builder.Property(a => a.RequiredRoleKey).HasColumnName("required_role_key").HasMaxLength(100).IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(a => a.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.Version).HasColumnName("version").IsConcurrencyToken();

        // The approver queue, and the expiry sweep.
        builder.HasIndex(a => new { a.WorkspaceId, a.Environment, a.Status, a.ExpiresAt });
        builder.HasIndex(a => a.AgentRunId);

        builder.OwnsMany(a => a.Decisions, decision =>
        {
            decision.ToTable("approval_decision");
            decision.WithOwner().HasForeignKey(nameof(ApprovalDecision.ApprovalRequestId));
            decision.HasKey(d => d.Id);

            decision.Property(d => d.Id).HasColumnName("id");
            decision.Property(d => d.ApprovalRequestId).HasColumnName("approval_request_id");
            decision.Property(d => d.ApproverUserId).HasColumnName("approver_user_id").IsRequired();
            decision.Property(d => d.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(20).IsRequired();
            decision.Property(d => d.Rationale).HasColumnName("rationale").HasMaxLength(4000);
            decision.Property(d => d.StepUpVerified).HasColumnName("step_up_verified").IsRequired();
            decision.Property(d => d.DecidedAt).HasColumnName("decided_at").IsRequired();

            // One decision per approver, enforced by the database rather than only by the aggregate.
            // A unique index holds even against a concurrent double-submit that both pass the
            // in-memory check before either commits.
            decision.HasIndex(d => new { d.ApprovalRequestId, d.ApproverUserId }).IsUnique();
        });

        builder.Ignore(a => a.RequiresStepUp);
        builder.Ignore(a => a.ApprovalsReceived);
        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class ApprovalPolicyConfiguration : IEntityTypeConfiguration<ApprovalPolicy>
{
    public void Configure(EntityTypeBuilder<ApprovalPolicy> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("approval_policy");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(p => p.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
        builder.Property(p => p.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        builder.Property(p => p.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(p => new { p.WorkspaceId, p.Key }).IsUnique();

        builder.OwnsMany(p => p.Rules, rule =>
        {
            rule.ToTable("approval_rule");
            rule.WithOwner().HasForeignKey(nameof(ApprovalRule.PolicyId));
            rule.HasKey(r => r.Id);

            rule.Property(r => r.Id).HasColumnName("id");
            rule.Property(r => r.PolicyId).HasColumnName("policy_id");
            rule.Property(r => r.RiskClass).HasColumnName("risk_class").HasConversion<string>().HasMaxLength(30).IsRequired();
            rule.Property(r => r.ToolKey).HasColumnName("tool_key").HasMaxLength(100);
            rule.Property(r => r.RequiredRoleKey).HasColumnName("required_role_key").HasMaxLength(100).IsRequired();
            rule.Property(r => r.RequiredApproverCount).HasColumnName("required_approver_count").IsRequired();
            rule.Property(r => r.Expiry).HasColumnName("expiry").IsRequired();

            rule.OwnsOne(r => r.ThresholdAmount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("threshold_amount").HasPrecision(19, 4);
                money.Property(m => m.Currency).HasColumnName("threshold_currency").HasMaxLength(3).IsFixedLength();
            });
        });

        builder.Ignore(p => p.DomainEvents);
    }
}

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<Domain.Audit.AuditEvent>
{
    public void Configure(EntityTypeBuilder<Domain.Audit.AuditEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("audit_event");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Sequence).HasColumnName("sequence").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.ActorType).HasColumnName("actor_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id");
        builder.Property(e => e.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ResourceType).HasColumnName("resource_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ResourceId).HasColumnName("resource_id");
        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(e => e.Environment).HasColumnName("environment").HasConversion<string?>().HasMaxLength(20);
        builder.Property(e => e.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.BeforeStateJson).HasColumnName("before_state").HasColumnType("jsonb");
        builder.Property(e => e.AfterStateJson).HasColumnName("after_state").HasColumnType("jsonb");
        builder.Property(e => e.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(e => e.PreviousHash).HasColumnName("previous_hash").HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(e => e.EntryHash).HasColumnName("entry_hash").HasMaxLength(64).IsFixedLength().IsRequired();

        // Gapless per tenant: the unique index is what makes a missing entry detectable rather than
        // merely improbable.
        builder.HasIndex(e => new { e.TenantIdentifier, e.Sequence }).IsUnique();
        builder.HasIndex(e => new { e.TenantIdentifier, e.OccurredAt });
        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => new { e.TenantIdentifier, e.Action, e.OccurredAt });

        // No created_at, updated_at, deleted_at or version: this table is append-only, and the
        // application database role holds INSERT and SELECT on it only.
    }
}
