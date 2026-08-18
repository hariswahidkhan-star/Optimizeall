using System.Reflection;
using Microsoft.EntityFrameworkCore;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Audit;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.Domain.Governance;
using OptimizeAll.Domain.Insights;
using OptimizeAll.Domain.Knowledge;
using OptimizeAll.Domain.Notifications;
using OptimizeAll.Domain.Orchestration;
using OptimizeAll.Domain.Scheduling;
using OptimizeAll.Domain.Tenancy;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Infrastructure.Persistence;

public sealed class OptimizeAllDbContext(
    DbContextOptions<OptimizeAllDbContext> options,
    ITenantContext tenantContext,
    ICurrentPrincipal currentPrincipal,
    IClock clock)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<AgentDefinition> AgentDefinitions => Set<AgentDefinition>();

    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    public DbSet<AgentMemoryEntry> AgentMemories => Set<AgentMemoryEntry>();

    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();

    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();

    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    public DbSet<ScheduleDefinition> Schedules => Set<ScheduleDefinition>();

    public DbSet<ScheduleOccurrence> ScheduleOccurrences => Set<ScheduleOccurrence>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<KpiSnapshot> KpiSnapshots => Set<KpiSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplyTenantQueryFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Registers a Guid conversion for every strongly-typed id declared in the Domain assembly.
        // Discovering them by scan means a newly introduced id type is mapped automatically rather
        // than failing at runtime the first time it is persisted.
        foreach (Type idType in DiscoverStronglyTypedIds())
        {
            configurationBuilder
                .Properties(idType)
                .HaveConversion(
                    typeof(StronglyTypedIdConverter<>).MakeGenericType(idType),
                    typeof(StronglyTypedIdComparer<>).MakeGenericType(idType));
        }

        configurationBuilder.Properties<decimal>().HavePrecision(19, 4);
        configurationBuilder.Properties<string>().AreUnicode().HaveMaxLength(4000);

        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>
    /// Stamps provenance and increments concurrency tokens.
    /// <para>
    /// Done centrally rather than in each handler because "who changed this and when" is exactly the
    /// kind of bookkeeping that gets omitted from one code path in ten, and the one that is omitted
    /// is always the one an investigation later needs.
    /// </para>
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = clock.UtcNow;
        Guid? actor = currentPrincipal.IsAuthenticated ? currentPrincipal.Principal.Id : null;

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditable auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.StampCreated(now, actor);
                        break;

                    case EntityState.Modified:
                        auditable.StampUpdated(now, actor);
                        break;
                }
            }

            if (entry is { State: EntityState.Modified, Entity: IAggregateRoot })
            {
                Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry? version =
                    entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Version");

                if (version is not null && version.CurrentValue is int current)
                {
                    version.CurrentValue = current + 1;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Adds <c>tenant_id = current</c> to every read of a tenant-owned entity.
    /// <para>
    /// This is the second of three isolation layers. It catches a forgotten <c>WHERE</c> clause in
    /// application code; row-level security in PostgreSQL catches what this misses, such as a raw
    /// SQL query or an ORM bypass. Neither is trusted alone.
    /// </para>
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workspace>().HasQueryFilter(w =>
            w.DeletedAt == null && w.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<User>().HasQueryFilter(u =>
            u.DeletedAt == null && u.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<Role>().HasQueryFilter(r => r.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<RoleAssignment>().HasQueryFilter(a => a.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<AgentDefinition>().HasQueryFilter(d =>
            d.DeletedAt == null && d.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<AgentRun>().HasQueryFilter(r => r.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<AgentMemoryEntry>().HasQueryFilter(m => m.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<WorkflowRun>().HasQueryFilter(w => w.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<ApprovalRequest>().HasQueryFilter(a => a.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<ApprovalPolicy>().HasQueryFilter(p => p.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<AuditEvent>().HasQueryFilter(e => e.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<KnowledgeDocument>().HasQueryFilter(d =>
            d.DeletedAt == null && d.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<KnowledgeChunk>().HasQueryFilter(c => c.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<ScheduleDefinition>().HasQueryFilter(s =>
            s.DeletedAt == null && s.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<ScheduleOccurrence>().HasQueryFilter(o => o.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<Notification>().HasQueryFilter(n => n.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(m => m.TenantIdentifier == tenantContext.TenantId);

        modelBuilder.Entity<KpiSnapshot>().HasQueryFilter(k => k.TenantIdentifier == tenantContext.TenantId);
    }

    private static IEnumerable<Type> DiscoverStronglyTypedIds()
        => typeof(TenantId).Assembly
            .GetTypes()
            .Where(type => type is { IsValueType: true, IsGenericTypeDefinition: false }
                && type.GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>)));
}
