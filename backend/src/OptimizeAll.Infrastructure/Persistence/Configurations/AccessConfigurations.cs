using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.Common;

namespace OptimizeAll.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("app_user");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.TenantIdentifier).HasColumnName("tenant_id").IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasColumnType("citext")
            .HasMaxLength(320)
            .HasConversion(email => email.Value, value => EmailAddress.Create(value))
            .IsRequired();

        builder.HasIndex(u => new { u.TenantIdentifier, u.Email }).IsUnique();

        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(u => u.ExternalSubject).HasColumnName("external_subject").HasMaxLength(255);

        // Filtered unique: many invited users legitimately have no subject yet, and a plain unique
        // index would let only one of them exist.
        builder.HasIndex(u => new { u.TenantIdentifier, u.ExternalSubject })
            .IsUnique()
            .HasFilter("external_subject IS NOT NULL");

        builder.Property(u => u.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");
        builder.Property(u => u.DeletedAt).HasColumnName("deleted_at");
        builder.Property(u => u.Version).HasColumnName("version").IsConcurrencyToken();

        builder.Ignore(u => u.DomainEvents);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("role");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
        builder.Property(r => r.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();
        builder.Property(r => r.IsBuiltIn).HasColumnName("is_builtin").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(r => new { r.TenantIdentifier, r.Key }).IsUnique();

        // Mapped as a PostgreSQL text[] against the backing field. An array column keeps a role's
        // grants atomic with the role row itself, so a permission set can never be half-applied by a
        // partially failed write, and Npgsql translates containment checks into an indexable
        // operator when "which roles grant X" is asked.
        builder.PrimitiveCollection<HashSet<string>>("_permissions")
            .HasColumnName("permissions")
            .HasColumnType("text[]")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(r => r.Permissions);
        builder.Ignore(r => r.DomainEvents);
    }
}

public sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("role_assignment");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantIdentifier).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.PrincipalType).HasColumnName("principal_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.PrincipalId).HasColumnName("principal_id").IsRequired();
        builder.Property(a => a.RoleId).HasColumnName("role_id").IsRequired();
        builder.Property(a => a.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(a => a.Environment).HasColumnName("environment").HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.GrantedBy).HasColumnName("granted_by").IsRequired();
        builder.Property(a => a.ExpiresAt).HasColumnName("expires_at");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(a => new { a.PrincipalId, a.RoleId, a.WorkspaceId, a.Environment }).IsUnique();

        // The authorisation hot path: resolve every grant for one principal in one index seek.
        builder.HasIndex(a => new { a.TenantIdentifier, a.PrincipalId });

        builder.Ignore(a => a.DomainEvents);
    }
}
