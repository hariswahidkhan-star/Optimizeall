using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Infrastructure.Persistence;

/// <summary>
/// Dispatches domain events to every registered handler for their concrete type.
/// <para>
/// A handler that throws aborts the transaction, and deliberately so: a domain event handler runs
/// inside the same transaction as the change that raised it, so partial success would leave the
/// system in a state the domain never sanctioned.
/// </para>
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider services, ILogger<DomainEventDispatcher> logger)
    : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (IDomainEvent domainEvent in events)
        {
            Type handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());

            foreach (object? handler in services.GetServices(handlerType))
            {
                if (handler is null)
                {
                    continue;
                }

                System.Reflection.MethodInfo method = handlerType.GetMethod("HandleAsync")!;

                Result result = await (Task<Result>)method.Invoke(handler, [domainEvent, cancellationToken])!;

                if (result.IsFailure)
                {
                    logger.LogError(
                        "Handler {Handler} rejected event {EventType}: {ErrorCode}. Aborting the transaction.",
                        handler.GetType().Name,
                        domainEvent.EventType,
                        result.Error.Code);

                    throw new InvalidOperationException(
                        $"Domain event handler {handler.GetType().Name} failed for '{domainEvent.EventType}': " +
                        $"{result.Error}");
                }
            }
        }
    }
}
