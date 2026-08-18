namespace OptimizeAll.Application.Abstractions.Persistence;

/// <summary>
/// The transaction boundary.
/// <para>
/// Saving does three things in one atomic step: it persists aggregate changes, appends the audit
/// entries, and writes queued outbox messages. They commit together or not at all, which is what
/// makes "every change is audited" and "no side effect without a committed cause" true rather than
/// merely intended.
/// </para>
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
