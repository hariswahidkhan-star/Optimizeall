using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Behaviours;

/// <summary>
/// Wraps commands in a transaction. Queries are excluded by type, so a read can never be silently
/// promoted into a write path.
/// <para>
/// A handler returning a failed <c>Result</c> rolls back. That matters: an expected failure such as
/// "approval already resolved" must not leave behind the partial writes the handler made before it
/// discovered the conflict.
/// </para>
/// </summary>
public sealed class TransactionBehaviour<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehaviour<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICommand<TResponse>)
        {
            return await next().ConfigureAwait(false);
        }

        await using IAsyncDisposable transaction = await unitOfWork
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Result<TResponse> result = await next().ConfigureAwait(false);

            if (result.IsFailure)
            {
                await unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
