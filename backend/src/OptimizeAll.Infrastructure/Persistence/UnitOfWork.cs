using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Infrastructure.Persistence;

/// <summary>
/// The transaction boundary, and the place the database is told which tenant it is serving.
/// </summary>
public sealed class UnitOfWork(
    OptimizeAllDbContext context,
    ITenantContext tenantContext,
    IDomainEventDispatcher domainEvents,
    ILogger<UnitOfWork> logger)
    : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            // Nested calls join the existing transaction. A no-op disposable is returned so the
            // caller's `await using` does not close a transaction it did not open.
            return new NoOpAsyncDisposable();
        }

        _transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await ApplyTenantScopeAsync(cancellationToken).ConfigureAwait(false);

        return _transaction;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collected before saving, because saving clears the tracked entities' event lists and the
        // events must survive to be dispatched after the commit.
        IReadOnlyList<IDomainEvent> events = CollectDomainEvents();

        int affected = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (events.Count > 0)
        {
            // Dispatched inside the transaction so a handler's writes commit atomically with the
            // change that raised the event. Handlers that must reach the outside world enqueue an
            // outbox message rather than calling out directly.
            await domainEvents.DispatchAsync(events, cancellationToken).ConfigureAwait(false);
            affected += await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return affected;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // A rollback failure must not mask the original error that caused it.
            logger.LogError(exception, "Rollback failed; the connection will be discarded.");
        }
        finally
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }

    /// <summary>
    /// Binds the connection to the current tenant for the life of this transaction.
    /// <para>
    /// <c>SET LOCAL</c> rather than <c>SET</c>: the value is discarded at commit or rollback, so it
    /// cannot survive on a pooled connection and leak into whatever request borrows it next. That
    /// distinction is the difference between tenant isolation and a data breach.
    /// </para>
    /// </summary>
    private async Task ApplyTenantScopeAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.IsPlatformEscalation)
        {
            // Cross-tenant access runs under a different database role with BYPASSRLS, reached only
            // through the audited escalation path. No tenant is bound here on purpose.
            logger.LogWarning(
                "Transaction opened under platform escalation for correlation {CorrelationId}.",
                tenantContext.CorrelationId);

            return;
        }

        await context.Database.ExecuteSqlAsync(
            $"SELECT set_config('app.tenant_id', {tenantContext.TenantId.Value.ToString()}, true)",
            cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<IDomainEvent> CollectDomainEvents()
    {
        List<IAggregateRoot> roots =
        [
            .. context.ChangeTracker
                .Entries<IAggregateRoot>()
                .Select(entry => entry.Entity)
                .Where(root => root.DomainEvents.Count > 0),
        ];

        List<IDomainEvent> events = [.. roots.SelectMany(root => root.DomainEvents)];

        foreach (IAggregateRoot root in roots)
        {
            root.ClearDomainEvents();
        }

        return events;
    }

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>Routes domain events to their handlers after the state change that raised them.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken);
}

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task<Result> HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
