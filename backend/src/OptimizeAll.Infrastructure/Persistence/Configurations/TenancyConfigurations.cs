using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Tenancy;

namespace OptimizeAll.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tenant");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .HasColumnType("citext")
            .HasMaxLength(63)
            .HasConversion(slug => slug.Value, value => Slug.Create(value))
            .IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Region).HasColumnName("region").HasMaxLength(50).IsRequired();
        builder.Property(t => t.PlanCode).HasColumnName("plan_code").HasMaxLength(50).IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.SuspensionReason).HasColumnName("suspension_reason").HasMaxLength(1000);
        builder.Property(t => t.PurgeAfter).HasColumnName("purge_after");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");
        builder.Property(t => t.Version).HasColumnName("version").IsConcurrencyToken();

        builder.Ignore(t => t.DomainEvents);
    }
}

public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("workspace");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.TenantIdentifier).HasColumnName("tenant_id").IsRequired();

        builder.Property(w => w.Slug)
            .HasColumnName("slug")
            .HasColumnType("citext")
            .HasMaxLength(63)
            .HasConversion(slug => slug.Value, value => Slug.Create(value))
            .IsRequired();

        builder.HasIndex(w => new { w.TenantIdentifier, w.Slug }).IsUnique();

        builder.Property(w => w.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();

        // Money is stored as two columns rather than a serialised value, so budget aggregation can
        // be done in SQL and a currency mismatch is visible in the schema rather than at runtime.
        builder.OwnsOne(w => w.MonthlyBudgetCap, money =>
        {
            money.Property(m => m.Amount).HasColumnName("budget_cap_amount").HasPrecision(19, 4).IsRequired();
            money.Property(m => m.Currency).HasColumnName("budget_cap_currency").HasMaxLength(3).IsFixedLength().IsRequired();
        });

        builder.Navigation(w => w.MonthlyBudgetCap).IsRequired();

        builder.Property(w => w.KillSwitchEngaged).HasColumnName("kill_switch_engaged").IsRequired();
        builder.Property(w => w.KillSwitchReason).HasColumnName("kill_switch_reason").HasMaxLength(2000);
        builder.Property(w => w.KillSwitchEngagedAt).HasColumnName("kill_switch_engaged_at");
        builder.Property(w => w.KillSwitchEngagedBy).HasColumnName("kill_switch_engaged_by");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");
        builder.Property(w => w.DeletedAt).HasColumnName("deleted_at");
        builder.Property(w => w.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(w => w.TenantIdentifier);
        builder.Ignore(w => w.DomainEvents);
    }
}
