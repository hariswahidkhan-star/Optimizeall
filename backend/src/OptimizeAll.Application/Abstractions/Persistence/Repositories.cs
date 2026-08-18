using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.Domain.Governance;
using OptimizeAll.Domain.Knowledge;
using OptimizeAll.Domain.Notifications;
using OptimizeAll.Domain.Orchestration;
using OptimizeAll.Domain.Scheduling;
using OptimizeAll.Domain.Tenancy;

namespace OptimizeAll.Application.Abstractions.Persistence;

/// <summary>
/// Repositories expose aggregate-shaped access only. There is deliberately no generic
/// <c>IRepository&lt;T&gt;</c> with an <c>IQueryable</c> escape hatch: that would let a caller
/// compose a query the persistence layer never sanctioned, and with it a query that forgets the
/// tenant filter.
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> FindAsync(TenantId id, CancellationToken cancellationToken);

    Task<Tenant?> FindBySlugAsync(string slug, CancellationToken cancellationToken);

    void Add(Tenant tenant);
}

public interface IWorkspaceRepository
{
    Task<Workspace?> FindAsync(WorkspaceId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Workspace>> ListForTenantAsync(TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Cheap read of the emergency stop, called immediately before every external effect.
    /// Separate from <see cref="FindAsync"/> so the check never tempts anyone to cache an aggregate.
    /// </summary>
    Task<bool> IsKillSwitchEngagedAsync(WorkspaceId id, CancellationToken cancellationToken);

    void Add(Workspace workspace);
}

public interface IUserRepository
{
    Task<User?> FindAsync(UserId id, CancellationToken cancellationToken);

    Task<User?> FindByExternalSubjectAsync(TenantId tenantId, string externalSubject, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(TenantId tenantId, string email, CancellationToken cancellationToken);

    void Add(User user);
}

public interface IRoleRepository
{
    Task<Role?> FindAsync(RoleId id, CancellationToken cancellationToken);

    Task<Role?> FindByKeyAsync(TenantId tenantId, string key, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> ListForTenantAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleAssignment>> ListAssignmentsAsync(PrincipalRef principal, TenantId tenantId, CancellationToken cancellationToken);

    void Add(Role role);

    void Add(RoleAssignment assignment);

    void Remove(RoleAssignment assignment);
}

public interface IAgentDefinitionRepository
{
    Task<AgentDefinition?> FindAsync(AgentDefinitionId id, CancellationToken cancellationToken);

    /// <summary>The current published version of an agent, or null when none is published.</summary>
    Task<AgentDefinition?> FindPublishedAsync(WorkspaceId workspaceId, string agentKey, CancellationToken cancellationToken);

    Task<int> GetNextVersionAsync(WorkspaceId workspaceId, string agentKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentDefinition>> ListForWorkspaceAsync(WorkspaceId workspaceId, CancellationToken cancellationToken);

    void Add(AgentDefinition definition);
}

public interface IAgentRunRepository
{
    Task<AgentRun?> FindAsync(AgentRunId id, CancellationToken cancellationToken);

    Task<AgentRun?> FindWithTraceAsync(AgentRunId id, CancellationToken cancellationToken);

    /// <summary>Runs whose lease has lapsed and which are therefore safe to reclaim.</summary>
    Task<IReadOnlyList<AgentRun>> ListReclaimableAsync(int limit, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentRun>> ListQueuedAsync(WorkspaceId workspaceId, int limit, CancellationToken cancellationToken);

    /// <summary>Total provider spend for a workspace in the current billing month, used for budget enforcement.</summary>
    Task<decimal> GetMonthToDateCostAsync(WorkspaceId workspaceId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(AgentRun run);
}

public interface IApprovalRepository
{
    Task<ApprovalRequest?> FindAsync(ApprovalRequestId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalRequest>> ListExpiredAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    Task<ApprovalPolicy?> FindPolicyAsync(ApprovalPolicyId id, CancellationToken cancellationToken);

    Task<ApprovalPolicy?> FindPolicyByKeyAsync(WorkspaceId workspaceId, string key, CancellationToken cancellationToken);

    void Add(ApprovalRequest request);

    void Add(ApprovalPolicy policy);
}

public interface IWorkflowRepository
{
    Task<WorkflowRun?> FindAsync(WorkflowRunId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowRun>> ListActiveAsync(WorkspaceId workspaceId, int limit, CancellationToken cancellationToken);

    void Add(WorkflowRun run);
}

public interface IKnowledgeRepository
{
    Task<KnowledgeDocument?> FindAsync(KnowledgeDocumentId id, CancellationToken cancellationToken);

    Task<KnowledgeDocument?> FindByContentHashAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string contentHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Semantic retrieval. The scope parameters are required, not optional: filtering happens before
    /// the nearest-neighbour scan so an approximate index cannot return another tenant's chunks.
    /// </summary>
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken);

    void Add(KnowledgeDocument document);
}

/// <summary>A knowledge chunk returned by semantic search, with its similarity score for citation.</summary>
public sealed record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentTitle,
    string Content,
    double Similarity);

public interface IMemoryRepository
{
    Task<IReadOnlyList<AgentMemoryEntry>> RecallAsync(
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string agentKey,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    void Add(AgentMemoryEntry entry);
}

public interface IScheduleRepository
{
    Task<ScheduleDefinition?> FindAsync(ScheduleDefinitionId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduleDefinition>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts an occurrence, returning false when one already exists for this logical instant.
    /// The uniqueness of <c>(schedule_id, occurrence_utc)</c> is what makes firing exactly-once
    /// across any number of scheduler replicas.
    /// </summary>
    Task<bool> TryClaimOccurrenceAsync(ScheduleOccurrence occurrence, CancellationToken cancellationToken);

    void Add(ScheduleDefinition schedule);
}

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxMessage>> ListReadyAsync(int limit, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(OutboxMessage message);
}

public interface INotificationRepository
{
    Task<Notification?> FindAsync(NotificationId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Notification>> ListForUserAsync(UserId userId, bool unreadOnly, int limit, CancellationToken cancellationToken);

    void Add(Notification notification);
}
