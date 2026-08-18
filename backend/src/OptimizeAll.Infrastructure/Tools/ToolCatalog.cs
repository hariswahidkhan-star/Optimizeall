using OptimizeAll.Application.Abstractions.Tools;
using OptimizeAll.Domain.AgentCatalog;

namespace OptimizeAll.Infrastructure.Tools;

/// <summary>
/// Resolves a tool key to its executor.
/// <para>
/// Validates at construction that every registered executor corresponds to a key in
/// <see cref="ToolRegistry"/>. A tool that exists as an executor but not in the registry would have
/// no risk classification, and an unclassified capability is exactly the thing that could execute
/// without a gate.
/// </para>
/// </summary>
public sealed class ToolCatalog : IToolCatalog
{
    private readonly Dictionary<string, IToolExecutor> _executors;

    public ToolCatalog(IEnumerable<IToolExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        _executors = new Dictionary<string, IToolExecutor>(StringComparer.Ordinal);

        foreach (IToolExecutor executor in executors)
        {
            if (!ToolRegistry.IsKnown(executor.ToolKey))
            {
                throw new InvalidOperationException(
                    $"Executor '{executor.GetType().Name}' registers tool key '{executor.ToolKey}', " +
                    "which is not present in ToolRegistry. Every tool must carry a risk classification " +
                    "before it can be executed.");
            }

            if (!_executors.TryAdd(executor.ToolKey, executor))
            {
                throw new InvalidOperationException(
                    $"Two executors are registered for tool key '{executor.ToolKey}'. " +
                    "Tool keys must resolve to exactly one implementation.");
            }
        }
    }

    public IToolExecutor? Find(string toolKey)
        => _executors.TryGetValue(toolKey, out IToolExecutor? executor) ? executor : null;

    public IReadOnlyList<IToolExecutor> All => [.. _executors.Values];
}
