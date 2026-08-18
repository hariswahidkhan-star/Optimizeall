using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Abstractions.Messaging;

/// <summary>A unit of work the application can be asked to perform.</summary>
public interface IRequest<TResponse>;

/// <summary>
/// A request that changes state. Commands pass through the full pipeline including transaction and
/// audit behaviours.
/// </summary>
public interface ICommand<TResponse> : IRequest<TResponse>;

/// <summary>
/// A request that only reads. Queries skip the transaction and audit behaviours, which is why the
/// distinction is a type rather than a convention: a handler cannot accidentally mutate state
/// inside something the pipeline treats as a read.
/// </summary>
public interface IQuery<TResponse> : IRequest<TResponse>;

/// <summary>The permission a request demands, checked by the authorisation behaviour before any handler runs.</summary>
public interface IRequirePermission
{
    string RequiredPermission { get; }
}

/// <summary>
/// A request whose authorisation depends on a specific workspace and environment rather than on the
/// caller's ambient scope. Implementing this makes the target explicit so a caller cannot be
/// authorised against one scope and then act in another.
/// </summary>
public interface IScopedRequest
{
    Guid WorkspaceId { get; }

    Domain.Common.EnvironmentTier Environment { get; }
}

/// <summary>Marks a request whose effect must be recorded in the audit trail.</summary>
public interface IAuditableRequest
{
    string AuditAction { get; }

    string AuditResourceType { get; }

    Guid? AuditResourceId { get; }
}

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

public delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>();

/// <summary>
/// A cross-cutting concern wrapped around every handler. Ordering is fixed at registration and is
/// load-bearing — authorisation must run after tenant resolution and before any transaction opens.
/// </summary>
public interface IPipelineBehaviour<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

/// <summary>Entry point into the application layer. The API and workers speak only to this.</summary>
public interface IDispatcher
{
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

/// <summary>Nothing to return, but a <c>Result</c> still needs a type parameter.</summary>
public readonly record struct Unit
{
    public static Unit Value { get; }
}
