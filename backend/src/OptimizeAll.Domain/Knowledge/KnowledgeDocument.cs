using System.Security.Cryptography;
using System.Text;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Knowledge;

public enum KnowledgeDocumentStatus
{
    Ingesting = 1,
    Active = 2,
    Deprecated = 3,
    Failed = 4,
}

/// <summary>
/// A source document in a tenant's knowledge base, together with the chunks agents retrieve from it.
/// <para>
/// Documents are deprecated rather than deleted. An agent output that cited a document must remain
/// explicable later, and a citation pointing at a row that no longer exists explains nothing.
/// </para>
/// </summary>
public sealed class KnowledgeDocument : AggregateRoot<KnowledgeDocumentId>, IAuditable, ISoftDeletable, ITenantOwned
{
    private readonly List<KnowledgeChunk> _chunks = [];

    private KnowledgeDocument(
        KnowledgeDocumentId id,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string title,
        string sourceUri,
        string sourceType,
        string contentHash)
        : base(id)
    {
        TenantIdentifier = tenantId;
        WorkspaceId = workspaceId;
        Environment = environment;
        Title = title;
        SourceUri = sourceUri;
        SourceType = sourceType;
        ContentHash = contentHash;
        Status = KnowledgeDocumentStatus.Ingesting;
    }

    private KnowledgeDocument()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public WorkspaceId WorkspaceId { get; private set; }

    public EnvironmentTier Environment { get; private set; }

    public string Title { get; private set; } = null!;

    public string SourceUri { get; private set; } = null!;

    public string SourceType { get; private set; } = null!;

    /// <summary>SHA-256 of the source content. Unique per scope, which is what makes re-ingestion idempotent.</summary>
    public string ContentHash { get; private set; } = null!;

    public KnowledgeDocumentStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset? IngestedAt { get; private set; }

    /// <summary>Null means the document has no scheduled staleness review.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public IReadOnlyList<KnowledgeChunk> Chunks => _chunks.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static KnowledgeDocument BeginIngestion(
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string title,
        string sourceUri,
        string sourceType,
        string rawContent,
        DateTimeOffset? expiresAt = null)
    {
        Ensure.NotNullOrWhiteSpace(title);
        Ensure.NotNullOrWhiteSpace(sourceUri);
        Ensure.NotNullOrWhiteSpace(sourceType);
        Ensure.NotNullOrWhiteSpace(rawContent);

        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawContent)));

        return new KnowledgeDocument(
            KnowledgeDocumentId.New(),
            tenantId,
            workspaceId,
            environment,
            title.Trim(),
            sourceUri.Trim(),
            sourceType.Trim(),
            hash)
        {
            ExpiresAt = expiresAt,
        };
    }

    public Result AddChunk(int sequence, string content, int tokenCount)
    {
        if (Status != KnowledgeDocumentStatus.Ingesting)
        {
            return Result.Failure(Error.Conflict(
                "knowledge.not_ingesting",
                "Chunks can only be added while the document is being ingested."));
        }

        _chunks.Add(KnowledgeChunk.Create(Id, TenantIdentifier, WorkspaceId, Environment, sequence, content, tokenCount));
        return Result.Success();
    }

    public Result CompleteIngestion(DateTimeOffset now)
    {
        if (_chunks.Count == 0)
        {
            return Result.Failure(Error.Invariant(
                "knowledge.no_chunks",
                "A document with no retrievable chunks contributes nothing and is treated as a failed ingestion."));
        }

        Status = KnowledgeDocumentStatus.Active;
        IngestedAt = now;
        Raise(new KnowledgeDocumentIngested(Id, TenantIdentifier, WorkspaceId, Environment, _chunks.Count, now));
        return Result.Success();
    }

    public void FailIngestion(string reason)
    {
        Status = KnowledgeDocumentStatus.Failed;
        FailureReason = Ensure.NotNullOrWhiteSpace(reason);
    }

    /// <summary>
    /// Withdraws the document from retrieval while preserving it for citation lookup.
    /// Irreversible in effect for agents, so this is a gated action at the application layer.
    /// </summary>
    public Result Deprecate(string reason, DateTimeOffset now)
    {
        if (Status == KnowledgeDocumentStatus.Deprecated)
        {
            return Result.Success();
        }

        Status = KnowledgeDocumentStatus.Deprecated;
        FailureReason = reason;
        Raise(new KnowledgeDocumentDeprecated(Id, TenantIdentifier, WorkspaceId, reason, now));
        return Result.Success();
    }

    public bool IsRetrievable(DateTimeOffset now)
        => Status == KnowledgeDocumentStatus.Active
            && DeletedAt is null
            && (ExpiresAt is null || now < ExpiresAt.Value);

    public void StampCreated(DateTimeOffset at, Guid? by)
    {
        CreatedAt = at;
        CreatedBy = by;
    }

    public void StampUpdated(DateTimeOffset at, Guid? by)
    {
        UpdatedAt = at;
        UpdatedBy = by;
    }

    public void MarkDeleted(DateTimeOffset at) => DeletedAt = at;
}

public sealed record KnowledgeDocumentIngested(
    KnowledgeDocumentId DocumentId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    EnvironmentTier Environment,
    int ChunkCount,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "knowledge.ingested";
}

public sealed record KnowledgeDocumentDeprecated(
    KnowledgeDocumentId DocumentId,
    TenantId TenantId,
    WorkspaceId WorkspaceId,
    string Reason,
    DateTimeOffset OccurredAt) : DomainEvent(OccurredAt)
{
    public override string EventType => "knowledge.deprecated";
}
