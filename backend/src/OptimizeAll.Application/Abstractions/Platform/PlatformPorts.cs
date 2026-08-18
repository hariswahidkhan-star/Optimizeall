using OptimizeAll.Domain.Audit;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Notifications;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Abstractions.Platform;

/// <summary>
/// Appends to the tamper-evident audit chain. Sequence allocation and hash linkage are serialised
/// per tenant by the implementation, because two concurrent writers computing "previous hash" from
/// the same predecessor would fork the chain.
/// </summary>
public interface IAuditTrail
{
    Task AppendAsync(
        string action,
        string resourceType,
        Guid? resourceId,
        AuditOutcome outcome,
        string? beforeStateJson = null,
        string? afterStateJson = null,
        string metadataJson = "{}",
        CancellationToken cancellationToken = default);

    /// <summary>Walks a tenant's chain and reports the first discontinuity, if any.</summary>
    Task<AuditChainVerification> VerifyChainAsync(TenantId tenantId, CancellationToken cancellationToken);
}

public sealed record AuditChainVerification(bool IsIntact, long EntriesChecked, long? FirstBrokenSequence);

/// <summary>
/// Resolves a secret for the current scope. The scope is part of the lookup key, which is why a
/// Development-scoped agent cannot obtain a Production credential: the reference does not resolve,
/// rather than resolving and then being rejected.
/// </summary>
public interface ISecretResolver
{
    Task<Result<string>> ResolveAsync(
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string secretName,
        CancellationToken cancellationToken);
}

/// <summary>The work queue agent runs are dispatched through.</summary>
public interface IRunQueue
{
    Task EnqueueAsync(AgentRunId runId, WorkspaceId workspaceId, CancellationToken cancellationToken);

    Task<AgentRunId?> DequeueAsync(TimeSpan waitTime, CancellationToken cancellationToken);

    Task AcknowledgeAsync(AgentRunId runId, CancellationToken cancellationToken);

    /// <summary>Current depth, used for autoscaling and for the queue-latency SLO.</summary>
    Task<long> DepthAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A mutual-exclusion lease. Held for a bounded time and released explicitly, so a crashed holder
/// blocks others only until expiry rather than indefinitely.
/// </summary>
public interface IDistributedLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan duration, CancellationToken cancellationToken);
}

/// <summary>Evaluates cron expressions with correct IANA timezone and DST handling.</summary>
public interface ICronEvaluator
{
    Result<DateTimeOffset?> NextOccurrence(string cronExpression, string timeZoneId, DateTimeOffset after);

    Result ValidateExpression(string cronExpression, string timeZoneId);

    /// <summary>Occurrences missed while the scheduler was unavailable, ordered oldest first.</summary>
    Result<IReadOnlyList<DateTimeOffset>> OccurrencesBetween(
        string cronExpression,
        string timeZoneId,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit);
}

public interface INotificationDispatcher
{
    Task<Result> SendAsync(Notification notification, NotificationChannel channel, CancellationToken cancellationToken);
}

/// <summary>
/// Removes personal data before prompts, completions and tool arguments are persisted.
/// Applied by default: an agent's trace is a durable copy of whatever passed through it, and an
/// unredacted trace is an unmanaged personal-data store outside the erasure path.
/// </summary>
public interface IDataRedactor
{
    string Redact(string content);

    bool ContainsPersonalData(string content);
}
