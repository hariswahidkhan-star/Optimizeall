using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Agents.Runtime;
using OptimizeAll.Application.Behaviours;

namespace OptimizeAll.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer.
    /// <para>
    /// Behaviour registration order is the pipeline order, outermost first, and it is load-bearing:
    /// </para>
    /// <list type="number">
    ///   <item><b>Logging</b> outermost, so it observes everything including validation rejections.</item>
    ///   <item><b>Validation</b> next, so a malformed request costs nothing further.</item>
    ///   <item><b>Authorisation</b> before any transaction opens, so an unauthorised request never
    ///   touches data.</item>
    ///   <item><b>Transaction</b> next, so everything inside commits atomically.</item>
    ///   <item><b>Audit</b> innermost, so its entry commits in the same transaction as the change
    ///   it describes and records the true outcome.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Assembly assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<IDispatcher, Dispatcher>();

        services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(LoggingBehaviour<,>));
        services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(ValidationBehaviour<,>));
        services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(AuthorizationBehaviour<,>));
        services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(TransactionBehaviour<,>));
        services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(AuditBehaviour<,>));

        RegisterHandlers(services, assembly);
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: false);

        services.AddScoped<ToolInvocationService>();
        services.AddScoped<PromptComposer>();
        services.AddScoped<AgentExecutor>();

        return services;
    }

    /// <summary>
    /// Discovers handlers by scanning for closed implementations of <see cref="IRequestHandler{TRequest,TResponse}"/>.
    /// Scanning rather than listing them keeps registration from silently falling behind the code —
    /// a missing registration would otherwise surface as a runtime failure on one endpoint.
    /// </summary>
    private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        foreach (Type implementation in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (Type contract in implementation.GetInterfaces())
            {
                if (contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                {
                    services.AddScoped(contract, implementation);
                }
            }
        }
    }
}
