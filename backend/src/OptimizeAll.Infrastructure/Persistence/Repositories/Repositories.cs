using Microsoft.EntityFrameworkCore;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.Domain.Governance;
using OptimizeAll.Domain.Knowledge;
using OptimizeAll.Domain.Notifications;
using OptimizeAll.Domain.Orchestration;
using OptimizeAll.Domain.Scheduling;
using OptimizeAll.Domain.Tenancy;

namespace OptimizeAll.Infrastructure.Persistence.Repositories;

/// <summary>
/// The tenant aggregate sits above the isolation boundary — it has no <c>tenant_id</c> of its own —
/// so it is reached through <see cref="Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters{TEntity}"/>-free
/// queries and is only ever loaded by platform-scoped operations.
/// </summary>
public sealed class TenantRepository(OptimizeAllDbContext context) : ITenantRepository
{
    public Task<Tenant?> FindAsync(TenantId id, CancellationToken cancellationToken)
        => context.Tenants.FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt == null, cancellationToken);

    public Task<Tenant?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
        => context.Tenants.FirstOrDefaultAsync(
            t => t.Slug.Value == slug && t.DeletedAt == null, cancellationToken);

    public void Add(Tenant tenant) => context.Tenants.Add(tenant);
}

public sealed class WorkspaceRepository(OptimizeAllDbContext context) : IWorkspaceRepository
{
    public Task<Workspace?> FindAsync(WorkspaceId id, CancellationToken cancellationToken)
        => context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Workspace>> ListForTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
        => await context.Workspaces
            .Where(w => w.TenantIdentifier == tenantId)
            .OrderBy(w => w.DisplayName)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Projects a single boolean rather than loading the aggregate. This runs immediately before
    /// every external action, so it must stay a one-column index lookup: anything heavier would
    /// invite someone to cache it, and a cached emergency stop is not an emergency stop.
    /// </summary>
    public Task<bool> IsKillSwitchEngagedAsync(WorkspaceId id, CancellationToken cancellationToken)
        => context.Workspaces
            .Where(w => w.Id == id)
            .Select(w => w.KillSwitchEngaged)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(Workspace workspace) => context.Workspaces.Add(workspace);
}

public sealed class UserRepository(OptimizeAllDbContext context) : IUserRepository
{
    public Task<User?> FindAsync(UserId id, CancellationToken cancellationToken)
        => context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> FindByExternalSubjectAsync(
        TenantId tenantId,
        string externalSubject,
        CancellationToken cancellationToken)
        => context.Users.FirstOrDefaultAsync(
            u => u.TenantIdentifier == tenantId && u.ExternalSubject == externalSubject, cancellationToken);

    public Task<User?> FindByEmailAsync(TenantId tenantId, string email, CancellationToken cancellationToken)
        => context.Users.FirstOrDefaultAsync(
            u => u.TenantIdentifier == tenantId && u.Email.Value == email, cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}

public sealed class RoleRepository(OptimizeAllDbContext context) : IRoleRepository
{
    public Task<Role?> FindAsync(RoleId id, CancellationToken cancellationToken)
        => context.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Role?> FindByKeyAsync(TenantId tenantId, string key, CancellationToken cancellationToken)
        => context.Roles.FirstOrDefaultAsync(
            r => r.TenantIdentifier == tenantId && r.Key == key, cancellationToken);

    public async Task<IReadOnlyList<Role>> ListForTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
        => await context.Roles
            .Where(r => r.TenantIdentifier == tenantId)
            .OrderBy(r => r.Key)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<RoleAssignment>> ListAssignmentsAsync(
        PrincipalRef principal,
        TenantId tenantId,
        CancellationToken cancellationToken)
        => await context.RoleAssignments
            .Where(a => a.TenantIdentifier == tenantId
                && a.PrincipalId == principal.Id
                && a.PrincipalType == principal.Type)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(Role role) => context.Roles.Add(role);

    public void Add(RoleAssignment assignment) => context.RoleAssignments.Add(assignment);

    public void Remove(RoleAssignment assignment) => context.RoleAssignments.Remove(assignment);
}

public sealed class AgentDefinitionRepository(OptimizeAllDbContext context) : IAgentDefinitionRepository
{
    public Task<AgentDefinition?> FindAsync(AgentDefinitionId id, CancellationToken cancellationToken)
        => context.AgentDefinitions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <summary>
    /// Resolves the highest published version. Ordering by version rather than by publication time
    /// keeps the answer stable if two versions are ever published out of chronological order.
    /// </summary>
    public Task<AgentDefinition?> FindPublishedAsync(
        WorkspaceId workspaceId,
        string agentKey,
        CancellationToken cancellationToken)
        => context.AgentDefinitions
            .Where(d => d.WorkspaceId == workspaceId
                && d.AgentKey == agentKey
                && d.Status == AgentDefinitionStatus.Published)
            .OrderByDescending(d => d.DefinitionVersion)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> GetNextVersionAsync(
        WorkspaceId workspaceId,
        string agentKey,
        CancellationToken cancellationToken)
    {
        int highest = await context.AgentDefinitions
            .Where(d => d.WorkspaceId == workspaceId && d.AgentKey == agentKey)
            .Select(d => (int?)d.DefinitionVersion)
            .MaxAsync(cancellationToken).ConfigureAwait(false) ?? 0;

        return highest + 1;
    }

    public async Task<IReadOnlyList<AgentDefinition>> ListForWorkspaceAsync(
        WorkspaceId workspaceId,
        CancellationToken cancellationToken)
    {
        // Only the newest published version of each agent: the planner should never be offered two
        // versions of the same agent as if they were two distinct capabilities.
        List<AgentDefinition> published = await context.AgentDefinitions
            .Where(d => d.WorkspaceId == workspaceId && d.Status == AgentDefinitionStatus.Published)
            .OrderBy(d => d.AgentKey)
            .ThenByDescending(d => d.DefinitionVersion)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return [.. published
            .GroupBy(d => d.AgentKey, StringComparer.Ordinal)
            .Select(group => group.First())];
    }

    public void Add(AgentDefinition definition) => context.AgentDefinitions.Add(definition);
}

public sealed class AgentRunRepository(OptimizeAllDbContext context) : IAgentRunRepository
{
    public Task<AgentRun?> FindAsync(AgentRunId id, CancellationToken cancellationToken)
        => context.AgentRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<AgentRun?> FindWithTraceAsync(AgentRunId id, CancellationToken cancellationToken)
        => context.AgentRuns
            .Include(r => r.Steps)
            .Include(r => r.ToolInvocations)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <summary>
    /// Runs whose worker died. An expired lease is the only safe signal: a live lease means the
    /// worker is still heartbeating, and stealing it would run the same agent twice.
    /// </summary>
    public async Task<IReadOnlyList<AgentRun>> ListReclaimableAsync(
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => await context.AgentRuns
            .Where(r => (r.Status == AgentRunStatus.Running || r.Status == AgentRunStatus.Queued)
                && r.LeaseExpiresAt != null
                && r.LeaseExpiresAt < now)
            .OrderBy(r => r.LeaseExpiresAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<AgentRun>> ListQueuedAsync(
        WorkspaceId workspaceId,
        int limit,
        CancellationToken cancellationToken)
        => await context.AgentRuns
            .Where(r => r.WorkspaceId == workspaceId && r.Status == AgentRunStatus.Queued)
            .OrderBy(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Month-to-date provider spend. Summed in the database over the flattened cost column, so
    /// enforcing a workspace budget never requires materialising a month of runs.
    /// </summary>
    public async Task<decimal> GetMonthToDateCostAsync(
        WorkspaceId workspaceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DateTimeOffset monthStart = new(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        return await context.AgentRuns
            .Where(r => r.WorkspaceId == workspaceId && r.CreatedAt >= monthStart)
            .SumAsync(r => r.Budget.CostAmount, cancellationToken).ConfigureAwait(false);
    }

    public void Add(AgentRun run) => context.AgentRuns.Add(run);
}

public sealed class ApprovalRepository(OptimizeAllDbContext context) : IApprovalRepository
{
    public Task<ApprovalRequest?> FindAsync(ApprovalRequestId id, CancellationToken cancellationToken)
        => context.ApprovalRequests
            .Include(a => a.Decisions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        int limit,
        CancellationToken cancellationToken)
        => await context.ApprovalRequests
            .Include(a => a.Decisions)
            .Where(a => a.WorkspaceId == workspaceId
                && a.Environment == environment
                && a.Status == ApprovalStatus.Pending)
            .OrderBy(a => a.ExpiresAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Lapsed requests awaiting the expiry sweep. The sweep is a tidy-up: the aggregate also
    /// evaluates expiry on access, so a decision can never land on a lapsed request even if the
    /// sweeper is behind or stopped.
    /// </summary>
    public async Task<IReadOnlyList<ApprovalRequest>> ListExpiredAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
        => await context.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending && a.ExpiresAt <= now)
            .OrderBy(a => a.ExpiresAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<ApprovalPolicy?> FindPolicyAsync(ApprovalPolicyId id, CancellationToken cancellationToken)
        => context.ApprovalPolicies
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<ApprovalPolicy?> FindPolicyByKeyAsync(
        WorkspaceId workspaceId,
        string key,
        CancellationToken cancellationToken)
        => context.ApprovalPolicies
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.Key == key, cancellationToken);

    public void Add(ApprovalRequest request) => context.ApprovalRequests.Add(request);

    public void Add(ApprovalPolicy policy) => context.ApprovalPolicies.Add(policy);
}

public sealed class WorkflowRepository(OptimizeAllDbContext context) : IWorkflowRepository
{
    /// <summary>
    /// Loads the whole task graph. The readiness and cycle invariants are properties of the graph,
    /// not of individual tasks, so a partially loaded workflow could not enforce them.
    /// </summary>
    public Task<WorkflowRun?> FindAsync(WorkflowRunId id, CancellationToken cancellationToken)
        => context.WorkflowRuns
            .Include(w => w.Tasks)
            .ThenInclude(t => t.Dependencies)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WorkflowRun>> ListActiveAsync(
        WorkspaceId workspaceId,
        int limit,
        CancellationToken cancellationToken)
        => await context.WorkflowRuns
            .Include(w => w.Tasks)
            .ThenInclude(t => t.Dependencies)
            .Where(w => w.WorkspaceId == workspaceId
                && (w.Status == WorkflowStatus.Running || w.Status == WorkflowStatus.AwaitingApproval))
            .OrderBy(w => w.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(WorkflowRun run) => context.WorkflowRuns.Add(run);
}

public sealed class MemoryRepository(OptimizeAllDbContext context) : IMemoryRepository
{
    /// <summary>
    /// Recalls the most instructive prior outcomes. Ordered by importance first — rejections and
    /// failures are weighted highest — then by recency, so an agent that only ever succeeds still
    /// gets its recent context.
    /// </summary>
    public async Task<IReadOnlyList<AgentMemoryEntry>> RecallAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string agentKey,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => await context.AgentMemories
            .Where(m => m.WorkspaceId == workspaceId
                && m.Environment == environment
                && m.AgentKey == agentKey
                && (m.ExpiresAt == null || m.ExpiresAt > now))
            .OrderByDescending(m => m.Importance)
            .ThenByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(AgentMemoryEntry entry) => context.AgentMemories.Add(entry);
}

public sealed class ScheduleRepository(OptimizeAllDbContext context) : IScheduleRepository
{
    public Task<ScheduleDefinition?> FindAsync(ScheduleDefinitionId id, CancellationToken cancellationToken)
        => context.Schedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ScheduleDefinition>> ListDueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
        => await context.Schedules
            .Where(s => s.IsEnabled && s.NextOccurrenceUtc != null && s.NextOccurrenceUtc <= now)
            .OrderBy(s => s.NextOccurrenceUtc)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Attempts to claim one logical occurrence.
    /// <para>
    /// Every scheduler replica races to insert; the unique index on
    /// <c>(schedule_id, occurrence_utc)</c> lets exactly one win. Catching the uniqueness violation
    /// is the mechanism, not an error path — it is cheaper and more reliable than a distributed
    /// lock, and it has no window in which two replicas both believe they hold leadership.
    /// </para>
    /// </summary>
    public async Task<bool> TryClaimOccurrenceAsync(
        ScheduleOccurrence occurrence,
        CancellationToken cancellationToken)
    {
        context.ScheduleOccurrences.Add(occurrence);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.Entry(occurrence).State = EntityState.Detached;
            return false;
        }
    }

    public void Add(ScheduleDefinition schedule) => context.Schedules.Add(schedule);

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}

public sealed class OutboxRepository(OptimizeAllDbContext context) : IOutboxRepository
{
    public async Task<IReadOnlyList<OutboxMessage>> ListReadyAsync(
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => await context.OutboxMessages
            .Where(m => (m.Status == OutboxStatus.Pending || m.Status == OutboxStatus.Failed)
                && m.NextAttemptAt <= now)
            .OrderBy(m => m.NextAttemptAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(OutboxMessage message) => context.OutboxMessages.Add(message);
}

public sealed class NotificationRepository(OptimizeAllDbContext context) : INotificationRepository
{
    public Task<Notification?> FindAsync(NotificationId id, CancellationToken cancellationToken)
        => context.Notifications
            .Include(n => n.Deliveries)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListForUserAsync(
        UserId userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken)
        => await context.Notifications
            .Where(n => n.RecipientId == userId && (!unreadOnly || n.ReadAt == null))
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(Notification notification) => context.Notifications.Add(notification);
}

public sealed class KnowledgeRepository(OptimizeAllDbContext context) : IKnowledgeRepository
{
    public Task<KnowledgeDocument?> FindAsync(KnowledgeDocumentId id, CancellationToken cancellationToken)
        => context.KnowledgeDocuments
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<KnowledgeDocument?> FindByContentHashAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string contentHash,
        CancellationToken cancellationToken)
        => context.KnowledgeDocuments.FirstOrDefaultAsync(
            d => d.WorkspaceId == workspaceId
                && d.Environment == environment
                && d.ContentHash == contentHash,
            cancellationToken);

    /// <summary>
    /// Semantic search, scoped before it is ranked.
    /// <para>
    /// The workspace and environment predicates are applied inside the same query as the vector
    /// ordering, so PostgreSQL filters before the approximate-nearest-neighbour scan produces its
    /// candidate set. Ranking first and filtering afterwards would let another tenant's chunks
    /// occupy the candidate list, silently degrading recall — and would depend on the filter being
    /// remembered at every call site.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        if (queryEmbedding.Length == 0)
        {
            return [];
        }

        // Written as SQL rather than LINQ for two reasons. The embedding column carries a value
        // converter, so the `<=>` operator does not translate from an expression tree; and the
        // scope predicates must provably sit in the same WHERE clause that feeds the
        // nearest-neighbour scan, which is the property that stops an approximate index from ever
        // surfacing another tenant's content. Row-level security independently enforces the tenant
        // boundary beneath this.
        Pgvector.Vector query = new(queryEmbedding);
        string environmentName = environment.ToString();

        List<ChunkSearchRow> rows = await context.Database
            .SqlQuery<ChunkSearchRow>($"""
                SELECT  c.id            AS "ChunkId",
                        c.document_id   AS "DocumentId",
                        d.title         AS "DocumentTitle",
                        c.content       AS "Content",
                        (c.embedding <=> {query}) AS "Distance"
                FROM    knowledge_chunk c
                JOIN    knowledge_document d ON d.id = c.document_id
                WHERE   c.workspace_id = {workspaceId.Value}
                  AND   c.environment  = {environmentName}
                  AND   c.embedding IS NOT NULL
                  AND   d.status = 'Active'
                  AND   d.deleted_at IS NULL
                ORDER BY c.embedding <=> {query}
                LIMIT   {limit}
                """)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Cosine distance is in [0,2]; similarity is reported instead so a citation reads the way a
        // reviewer expects, where a higher number means more relevant.
        return [.. rows.Select(r => new RetrievedChunk(
            r.ChunkId, r.DocumentId, r.DocumentTitle, r.Content, 1.0 - r.Distance))];
    }

    /// <summary>Projection shape for the vector search. Exists only to give the SQL a target type.</summary>
    private sealed record ChunkSearchRow(
        Guid ChunkId,
        Guid DocumentId,
        string DocumentTitle,
        string Content,
        double Distance);

    public void Add(KnowledgeDocument document) => context.KnowledgeDocuments.Add(document);
}
