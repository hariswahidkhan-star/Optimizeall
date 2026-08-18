using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Common;

/// <summary>
/// Every aggregate and entity identifier in the platform. Declared together so that the set is
/// reviewable in one place and so the persistence layer can register conversions by assembly scan.
/// <para>
/// Values are UUIDv7: time-ordered, which keeps B-tree index inserts local instead of scattering
/// them across the whole index, while still revealing nothing about row counts.
/// </para>
/// </summary>
public readonly record struct TenantId(Guid Value) : IStronglyTypedId<TenantId>
{
    public static TenantId From(Guid value) => new(value);

    public static TenantId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct WorkspaceId(Guid Value) : IStronglyTypedId<WorkspaceId>
{
    public static WorkspaceId From(Guid value) => new(value);

    public static WorkspaceId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId>
{
    public static UserId From(Guid value) => new(value);

    public static UserId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct RoleId(Guid Value) : IStronglyTypedId<RoleId>
{
    public static RoleId From(Guid value) => new(value);

    public static RoleId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct RoleAssignmentId(Guid Value) : IStronglyTypedId<RoleAssignmentId>
{
    public static RoleAssignmentId From(Guid value) => new(value);

    public static RoleAssignmentId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct AgentDefinitionId(Guid Value) : IStronglyTypedId<AgentDefinitionId>
{
    public static AgentDefinitionId From(Guid value) => new(value);

    public static AgentDefinitionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct AgentRunId(Guid Value) : IStronglyTypedId<AgentRunId>
{
    public static AgentRunId From(Guid value) => new(value);

    public static AgentRunId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct ToolInvocationId(Guid Value) : IStronglyTypedId<ToolInvocationId>
{
    public static ToolInvocationId From(Guid value) => new(value);

    public static ToolInvocationId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct WorkflowRunId(Guid Value) : IStronglyTypedId<WorkflowRunId>
{
    public static WorkflowRunId From(Guid value) => new(value);

    public static WorkflowRunId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct WorkTaskId(Guid Value) : IStronglyTypedId<WorkTaskId>
{
    public static WorkTaskId From(Guid value) => new(value);

    public static WorkTaskId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct ApprovalRequestId(Guid Value) : IStronglyTypedId<ApprovalRequestId>
{
    public static ApprovalRequestId From(Guid value) => new(value);

    public static ApprovalRequestId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct ApprovalPolicyId(Guid Value) : IStronglyTypedId<ApprovalPolicyId>
{
    public static ApprovalPolicyId From(Guid value) => new(value);

    public static ApprovalPolicyId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct AuditEventId(Guid Value) : IStronglyTypedId<AuditEventId>
{
    public static AuditEventId From(Guid value) => new(value);

    public static AuditEventId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct KnowledgeDocumentId(Guid Value) : IStronglyTypedId<KnowledgeDocumentId>
{
    public static KnowledgeDocumentId From(Guid value) => new(value);

    public static KnowledgeDocumentId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct KnowledgeChunkId(Guid Value) : IStronglyTypedId<KnowledgeChunkId>
{
    public static KnowledgeChunkId From(Guid value) => new(value);

    public static KnowledgeChunkId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct MemoryEntryId(Guid Value) : IStronglyTypedId<MemoryEntryId>
{
    public static MemoryEntryId From(Guid value) => new(value);

    public static MemoryEntryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct ScheduleDefinitionId(Guid Value) : IStronglyTypedId<ScheduleDefinitionId>
{
    public static ScheduleDefinitionId From(Guid value) => new(value);

    public static ScheduleDefinitionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct ScheduleOccurrenceId(Guid Value) : IStronglyTypedId<ScheduleOccurrenceId>
{
    public static ScheduleOccurrenceId From(Guid value) => new(value);

    public static ScheduleOccurrenceId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct NotificationId(Guid Value) : IStronglyTypedId<NotificationId>
{
    public static NotificationId From(Guid value) => new(value);

    public static NotificationId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct KpiSnapshotId(Guid Value) : IStronglyTypedId<KpiSnapshotId>
{
    public static KpiSnapshotId From(Guid value) => new(value);

    public static KpiSnapshotId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

public readonly record struct OutboxMessageId(Guid Value) : IStronglyTypedId<OutboxMessageId>
{
    public static OutboxMessageId From(Guid value) => new(value);

    public static OutboxMessageId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
