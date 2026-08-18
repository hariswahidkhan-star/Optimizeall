using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Abstractions.Messaging;

/// <summary>
/// Resolves the handler for a request and composes the registered pipeline behaviours around it.
/// <para>
/// Hand-rolled rather than taken from a mediator library. The pipeline is the platform's
/// authorisation and audit chokepoint, so owning it outright — roughly a hundred lines — is
/// preferable to depending on a package whose ordering semantics or licence could change under a
/// control this important.
/// </para>
/// </summary>
public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, Type> HandlerTypeCache = new();
    private static readonly ConcurrentDictionary<Type, Type> BehaviourTypeCache = new();

    private readonly IServiceProvider _services = services
        ?? throw new ArgumentNullException(nameof(services));

    public async Task<Result<TResponse>> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Type requestType = request.GetType();

        Type handlerType = HandlerTypeCache.GetOrAdd(
            requestType,
            static type => typeof(IRequestHandler<,>).MakeGenericType(type, typeof(TResponse)));

        object handler = _services.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No handler is registered for '{requestType.Name}'. " +
                "Handlers are discovered by assembly scan; check that the handler is public and non-abstract.");

        Type behaviourType = BehaviourTypeCache.GetOrAdd(
            requestType,
            static type => typeof(IPipelineBehaviour<,>).MakeGenericType(type, typeof(TResponse)));

        // Materialised so the ordering is stable and the reversal below is well defined.
        object[] behaviours = [.. ((IEnumerable<object>)_services.GetServices(behaviourType)).Where(b => b is not null)];

        RequestHandlerDelegate<TResponse> pipeline = () => InvokeHandlerAsync<TResponse>(
            handler, handlerType, request, cancellationToken);

        // Reversed so that the first registered behaviour ends up outermost, matching how the
        // registration order reads in DependencyInjection.cs.
        for (int index = behaviours.Length - 1; index >= 0; index--)
        {
            object behaviour = behaviours[index];
            RequestHandlerDelegate<TResponse> next = pipeline;

            pipeline = () => InvokeBehaviourAsync<TResponse>(
                behaviour, behaviourType, request, next, cancellationToken);
        }

        return await pipeline().ConfigureAwait(false);
    }

    private static Task<Result<TResponse>> InvokeHandlerAsync<TResponse>(
        object handler,
        Type handlerType,
        object request,
        CancellationToken cancellationToken)
    {
        System.Reflection.MethodInfo method = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync))
            ?? throw new InvalidOperationException($"'{handlerType.Name}' does not expose HandleAsync.");

        return (Task<Result<TResponse>>)method.Invoke(handler, [request, cancellationToken])!;
    }

    private static Task<Result<TResponse>> InvokeBehaviourAsync<TResponse>(
        object behaviour,
        Type behaviourType,
        object request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        System.Reflection.MethodInfo method = behaviourType.GetMethod(
            nameof(IPipelineBehaviour<IRequest<TResponse>, TResponse>.HandleAsync))
            ?? throw new InvalidOperationException($"'{behaviourType.Name}' does not expose HandleAsync.");

        return (Task<Result<TResponse>>)method.Invoke(behaviour, [request, next, cancellationToken])!;
    }
}
