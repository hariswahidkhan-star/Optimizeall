using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Audit;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Infrastructure.Persistence;

/// <summary>
/// Appends to the per-tenant hash chain.
/// <para>
/// Two writers computing "the previous hash" from the same predecessor would fork the chain and
/// make it unverifiable, so sequence allocation is serialised per tenant by a PostgreSQL advisory
/// lock held for the duration of the transaction. The lock is per tenant, not global, so one
/// tenant's write volume cannot stall another's.
/// </para>
/// </summary>
public sealed class AuditTrail(
    OptimizeAllDbContext context,
    ITenantContext tenantContext,
    ICurrentPrincipal principal,
    IClock clock,
    ILogger<AuditTrail> logger)
    : IAuditTrail
{
    public async Task AppendAsync(
        string action,
        string resourceType,
        Guid? resourceId,
        AuditOutcome outcome,
        string? beforeStateJson = null,
        string? afterStateJson = null,
        string metadataJson = "{}",
        CancellationToken cancellationToken = default)
    {
        TenantId tenantId = tenantContext.TenantId;

        await AcquireTenantAdvisoryLockAsync(tenantId, cancellationToken).ConfigureAwait(false);

        (long sequence, string previousHash) = await ReadChainHeadAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        AuditEvent entry = AuditEvent.Append(
            tenantId,
            sequence,
            clock.UtcNow,
            principal.Principal,
            action,
            resourceType,
            resourceId,
            outcome,
            Guid.TryParse(tenantContext.CorrelationId, out Guid correlationId) ? correlationId : Guid.Empty,
            previousHash,
            tenantContext.WorkspaceId,
            tenantContext.Environment,
            beforeStateJson,
            afterStateJson,
            metadataJson,
            principal.IpAddress);

        context.AuditEvents.Add(entry);
    }

    public async Task<AuditChainVerification> VerifyChainAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        const int BatchSize = 1000;

        long checkedCount = 0;
        long offset = 0;
        AuditEvent? previous = null;

        while (true)
        {
            List<AuditEvent> batch = await context.AuditEvents
                .Where(e => e.TenantIdentifier == tenantId)
                .OrderBy(e => e.Sequence)
                .Skip((int)offset)
                .Take(BatchSize)
                .AsNoTracking()
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (batch.Count == 0)
            {
                return new AuditChainVerification(true, checkedCount, null);
            }

            foreach (AuditEvent entry in batch)
            {
                if (!entry.VerifyFollows(previous))
                {
                    logger.LogCritical(
                        "Audit chain broken for tenant {TenantId} at sequence {Sequence}.",
                        tenantId,
                        entry.Sequence);

                    return new AuditChainVerification(false, checkedCount, entry.Sequence);
                }

                previous = entry;
                checkedCount++;
            }

            offset += batch.Count;
        }
    }

    /// <summary>
    /// Serialises audit appends for one tenant. A transaction-scoped advisory lock is used rather
    /// than a table lock so that ordinary reads and writes to <c>audit_event</c> are unaffected, and
    /// the lock is released automatically on commit or rollback — including on a process crash.
    /// </summary>
    private Task AcquireTenantAdvisoryLockAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        // A 64-bit key derived from the tenant id. Collisions across tenants would only cause
        // unnecessary serialisation, never a correctness problem.
        long lockKey = BitConverter.ToInt64(tenantId.Value.ToByteArray(), 0);

        return context.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);
    }

    /// <summary>
    /// Reads the tail of the chain. Includes entries added in this transaction but not yet flushed,
    /// so several audit events written by one request link to each other correctly rather than all
    /// claiming the same predecessor.
    /// </summary>
    private async Task<(long Sequence, string PreviousHash)> ReadChainHeadAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        AuditEvent? pending = context.ChangeTracker
            .Entries<AuditEvent>()
            .Where(entry => entry.State == EntityState.Added
                && entry.Entity.TenantIdentifier == tenantId)
            .Select(entry => entry.Entity)
            .OrderByDescending(entry => entry.Sequence)
            .FirstOrDefault();

        if (pending is not null)
        {
            return (pending.Sequence + 1, pending.EntryHash);
        }

        var head = await context.AuditEvents
            .Where(e => e.TenantIdentifier == tenantId)
            .OrderByDescending(e => e.Sequence)
            .Select(e => new { e.Sequence, e.EntryHash })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return head is null
            ? (1, AuditEvent.GenesisHash)
            : (head.Sequence + 1, head.EntryHash);
    }
}
