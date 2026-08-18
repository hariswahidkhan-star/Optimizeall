using System.Security.Cryptography;
using System.Text;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Governance;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Audit;

public enum AuditOutcome
{
    Success = 1,
    Failure = 2,
    Denied = 3,
}

/// <summary>
/// An append-only record of one state-changing operation, linked into a per-tenant hash chain.
/// <para>
/// Each entry's hash covers the previous entry's hash, so altering or removing any historical
/// record breaks every hash after it. That does not make tampering impossible — an attacker with
/// full database control could recompute the chain — but it makes silent tampering impossible,
/// which is the property an auditor actually needs.
/// </para>
/// <para>
/// This type has no update or delete operation, and the application database role is granted only
/// <c>INSERT</c> and <c>SELECT</c> on its table. The immutability is enforced by permissions, not
/// by the absence of a setter.
/// </para>
/// </summary>
public sealed class AuditEvent : Entity<AuditEventId>
{
    /// <summary>The chain anchor. The first entry for a tenant links to this all-zero hash.</summary>
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private AuditEvent(
        AuditEventId id,
        TenantId tenantId,
        long sequence,
        DateTimeOffset occurredAt,
        PrincipalType actorType,
        Guid? actorId,
        string action,
        string resourceType,
        Guid? resourceId,
        WorkspaceId? workspaceId,
        EnvironmentTier? environment,
        AuditOutcome outcome,
        string? beforeStateJson,
        string? afterStateJson,
        string metadataJson,
        Guid correlationId,
        string? ipAddress,
        string previousHash)
        : base(id)
    {
        TenantIdentifier = tenantId;
        Sequence = sequence;
        OccurredAt = occurredAt;
        ActorType = actorType;
        ActorId = actorId;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        WorkspaceId = workspaceId;
        Environment = environment;
        Outcome = outcome;
        BeforeStateJson = beforeStateJson;
        AfterStateJson = afterStateJson;
        MetadataJson = metadataJson;
        CorrelationId = correlationId;
        IpAddress = ipAddress;
        PreviousHash = previousHash;
        EntryHash = ComputeHash();
    }

    private AuditEvent()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    /// <summary>Monotonic per tenant. A gap is itself evidence of a problem.</summary>
    public long Sequence { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public PrincipalType ActorType { get; private set; }

    public Guid? ActorId { get; private set; }

    /// <summary>Dotted action name, e.g. <c>approval.decided</c>.</summary>
    public string Action { get; private set; } = null!;

    public string ResourceType { get; private set; } = null!;

    public Guid? ResourceId { get; private set; }

    public WorkspaceId? WorkspaceId { get; private set; }

    public EnvironmentTier? Environment { get; private set; }

    public AuditOutcome Outcome { get; private set; }

    public string? BeforeStateJson { get; private set; }

    public string? AfterStateJson { get; private set; }

    public string MetadataJson { get; private set; } = "{}";

    public Guid CorrelationId { get; private set; }

    public string? IpAddress { get; private set; }

    public string PreviousHash { get; private set; } = GenesisHash;

    public string EntryHash { get; private set; } = null!;

    public static AuditEvent Append(
        TenantId tenantId,
        long sequence,
        DateTimeOffset occurredAt,
        PrincipalRef actor,
        string action,
        string resourceType,
        Guid? resourceId,
        AuditOutcome outcome,
        Guid correlationId,
        string previousHash,
        WorkspaceId? workspaceId = null,
        EnvironmentTier? environment = null,
        string? beforeStateJson = null,
        string? afterStateJson = null,
        string metadataJson = "{}",
        string? ipAddress = null)
    {
        Ensure.NotNullOrWhiteSpace(action);
        Ensure.NotNullOrWhiteSpace(resourceType);
        Ensure.NotNullOrWhiteSpace(previousHash);

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Audit sequence numbers start at 1.");
        }

        return new AuditEvent(
            AuditEventId.New(),
            tenantId,
            sequence,
            occurredAt,
            actor.Type,
            actor.Id == Guid.Empty ? null : actor.Id,
            action.Trim(),
            resourceType.Trim(),
            resourceId,
            workspaceId,
            environment,
            outcome,
            beforeStateJson,
            afterStateJson,
            string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson,
            correlationId,
            ipAddress,
            previousHash.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Recomputes this entry's hash from its own fields and compares it with the stored value.
    /// A mismatch means the row was modified after it was written.
    /// </summary>
    public bool VerifySelf() => string.Equals(EntryHash, ComputeHash(), StringComparison.Ordinal);

    /// <summary>
    /// Verifies that this entry follows <paramref name="predecessor"/> in the chain: sequence
    /// contiguity, hash linkage, and the predecessor's own integrity.
    /// </summary>
    public bool VerifyFollows(AuditEvent? predecessor)
    {
        if (!VerifySelf())
        {
            return false;
        }

        if (predecessor is null)
        {
            return Sequence == 1 && string.Equals(PreviousHash, GenesisHash, StringComparison.Ordinal);
        }

        return predecessor.TenantIdentifier == TenantIdentifier
            && predecessor.Sequence == Sequence - 1
            && string.Equals(PreviousHash, predecessor.EntryHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// The hashed representation. Built through the canonical JSON serialiser so that field order
    /// and formatting cannot vary between the write path and the verification path.
    /// </summary>
    private string ComputeHash()
    {
        string payload = CanonicalJson.Canonicalise(System.Text.Json.JsonSerializer.Serialize(new
        {
            tenantId = TenantIdentifier.Value,
            sequence = Sequence,
            occurredAt = OccurredAt.ToUniversalTime().ToString("O"),
            actorType = ActorType.ToString(),
            actorId = ActorId,
            action = Action,
            resourceType = ResourceType,
            resourceId = ResourceId,
            workspaceId = WorkspaceId?.Value,
            environment = Environment?.ToString(),
            outcome = Outcome.ToString(),
            beforeState = BeforeStateJson,
            afterState = AfterStateJson,
            metadata = MetadataJson,
            correlationId = CorrelationId,
            ipAddress = IpAddress,
            previousHash = PreviousHash,
        }));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
