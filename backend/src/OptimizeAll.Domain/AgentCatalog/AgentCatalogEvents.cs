using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.AgentCatalog;

public sealed record AgentDefinitionPublished(
    AgentDefinitionId AgentDefinitionId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    string AgentKey,
    int Version,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "agent.definition_published";
}

public sealed record AgentDefinitionDeprecated(
    AgentDefinitionId AgentDefinitionId,
    TenantId TenantId,
    string AgentKey,
    int Version,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "agent.definition_deprecated";
}
