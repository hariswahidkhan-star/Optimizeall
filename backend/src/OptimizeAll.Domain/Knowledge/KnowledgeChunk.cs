using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Knowledge;

/// <summary>
/// A retrievable fragment of a document, with the embedding used for semantic search.
/// <para>
/// The scope columns are duplicated from the parent document deliberately. Vector search filters on
/// them <em>before</em> the nearest-neighbour scan, so isolation does not depend on a join being
/// written correctly at every call site — and an approximate index can never surface another
/// tenant's content.
/// </para>
/// </summary>
public sealed class KnowledgeChunk : Entity<KnowledgeChunkId>, ITenantOwned
{
    private KnowledgeChunk(
        KnowledgeChunkId id,
        KnowledgeDocumentId documentId,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        int sequence,
        string content,
        int tokenCount)
        : base(id)
    {
        DocumentId = documentId;
        TenantIdentifier = tenantId;
        WorkspaceId = workspaceId;
        Environment = environment;
        Sequence = sequence;
        Content = content;
        TokenCount = tokenCount;
    }

    private KnowledgeChunk()
    {
    }

    public KnowledgeDocumentId DocumentId { get; private set; }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public WorkspaceId WorkspaceId { get; private set; }

    public EnvironmentTier Environment { get; private set; }

    public int Sequence { get; private set; }

    public string Content { get; private set; } = null!;

    public int TokenCount { get; private set; }

    /// <summary>
    /// Set by the ingestion worker after the embedding call. Null means the chunk is not yet
    /// searchable, which is a normal transient state rather than an error.
    /// </summary>
    public float[]? Embedding { get; private set; }

    internal static KnowledgeChunk Create(
        KnowledgeDocumentId documentId,
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        int sequence,
        string content,
        int tokenCount)
    {
        Ensure.NotNullOrWhiteSpace(content);

        return new KnowledgeChunk(
            KnowledgeChunkId.New(),
            documentId,
            tenantId,
            workspaceId,
            environment,
            sequence,
            content,
            tokenCount);
    }

    public void AttachEmbedding(float[] embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        if (embedding.Length == 0)
        {
            throw new ArgumentException("An embedding must not be empty.", nameof(embedding));
        }

        Embedding = embedding;
    }

    public bool IsSearchable => Embedding is { Length: > 0 };
}
