using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Notifications;

public enum OutboxStatus
{
    Pending = 1,
    Dispatched = 2,
    Failed = 3,
    DeadLettered = 4,
}

/// <summary>
/// A side effect queued to happen after the transaction that caused it commits.
/// <para>
/// Writing to the database and calling an external system are two operations that cannot be made
/// atomic. Calling first risks an effect for a transaction that then rolls back; committing first
/// risks a commit whose effect never happens. The outbox removes the choice: the message is written
/// inside the same transaction, and a separate dispatcher delivers it afterwards. Delivery is
/// at-least-once, which is why every consumer carries an idempotency key.
/// </para>
/// </summary>
public sealed class OutboxMessage : AggregateRoot<OutboxMessageId>, ITenantOwned
{
    /// <summary>Attempts before the message is parked for human attention rather than retried forever.</summary>
    public const int MaxAttempts = 8;

    private OutboxMessage(
        OutboxMessageId id,
        TenantId tenantId,
        string messageType,
        string payloadJson,
        string idempotencyKey,
        Guid correlationId,
        DateTimeOffset occurredAt)
        : base(id)
    {
        TenantIdentifier = tenantId;
        MessageType = messageType;
        PayloadJson = payloadJson;
        IdempotencyKey = idempotencyKey;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
        NextAttemptAt = occurredAt;
        Status = OutboxStatus.Pending;
    }

    private OutboxMessage()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public string MessageType { get; private set; } = null!;

    public string PayloadJson { get; private set; } = null!;

    public string IdempotencyKey { get; private set; } = null!;

    public Guid CorrelationId { get; private set; }

    public OutboxStatus Status { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? DispatchedAt { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Enqueue(
        TenantId tenantId,
        string messageType,
        string payloadJson,
        string idempotencyKey,
        Guid correlationId,
        DateTimeOffset now)
    {
        Ensure.NotNullOrWhiteSpace(messageType);
        Ensure.NotNullOrWhiteSpace(payloadJson);
        Ensure.NotNullOrWhiteSpace(idempotencyKey);

        return new OutboxMessage(
            OutboxMessageId.New(),
            tenantId,
            messageType.Trim(),
            payloadJson,
            idempotencyKey.Trim(),
            correlationId,
            now);
    }

    public void MarkDispatched(DateTimeOffset now)
    {
        Status = OutboxStatus.Dispatched;
        DispatchedAt = now;
        AttemptCount++;
        LastError = null;
    }

    /// <summary>
    /// Records a failed delivery and schedules the retry with exponential backoff plus jitter.
    /// Jitter matters: without it, a downstream outage that fails a thousand messages at once
    /// produces a thousand synchronised retries that keep the downstream down.
    /// </summary>
    public void RecordFailure(string error, DateTimeOffset now, TimeSpan baseDelay, double jitterFactor)
    {
        AttemptCount++;
        LastError = error;

        if (AttemptCount >= MaxAttempts)
        {
            Status = OutboxStatus.DeadLettered;
            return;
        }

        Status = OutboxStatus.Failed;

        double exponent = Math.Min(AttemptCount, 10);
        double delaySeconds = baseDelay.TotalSeconds * Math.Pow(2, exponent - 1);
        double jittered = delaySeconds * (1 + (Math.Clamp(jitterFactor, -0.5, 0.5)));

        NextAttemptAt = now.AddSeconds(Math.Min(jittered, TimeSpan.FromHours(6).TotalSeconds));
    }

    public bool IsReadyToDispatch(DateTimeOffset now)
        => Status is OutboxStatus.Pending or OutboxStatus.Failed && now >= NextAttemptAt;
}
