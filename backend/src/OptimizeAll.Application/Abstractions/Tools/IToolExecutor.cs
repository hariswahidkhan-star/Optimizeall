using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Abstractions.Tools;

/// <summary>Everything a tool needs in order to act, and to be prevented from acting.</summary>
public sealed record ToolExecutionContext
{
    public required TenantId TenantId { get; init; }

    public required WorkspaceId WorkspaceId { get; init; }

    public required EnvironmentTier Environment { get; init; }

    public required AgentRunId RunId { get; init; }

    public required string AgentKey { get; init; }

    /// <summary>Per-grant restrictions from the agent definition, e.g. a domain allow-list.</summary>
    public required string ConstraintsJson { get; init; }

    /// <summary>When true the tool records what it would have done and applies nothing.</summary>
    public required bool IsDryRun { get; init; }

    /// <summary>Deduplicates the external effect across retries.</summary>
    public required string IdempotencyKey { get; init; }

    public required Guid CorrelationId { get; init; }
}

public sealed record ToolOutcome(string ResultJson, int DurationMilliseconds);

/// <summary>
/// One capability's implementation.
/// <para>
/// Implementations never decide whether they are allowed to run. Authorisation, kill-switch checks
/// and approval gating all happen in <c>ToolInvocationService</c> before an executor is reached, so
/// a tool author cannot accidentally omit a control by forgetting to call it.
/// </para>
/// </summary>
public interface IToolExecutor
{
    string ToolKey { get; }

    /// <summary>JSON Schema of the arguments, advertised to the model.</summary>
    string ParametersJsonSchema { get; }

    string Description { get; }

    Task<Result<ToolOutcome>> ExecuteAsync(
        string argumentsJson,
        ToolExecutionContext context,
        CancellationToken cancellationToken);
}

/// <summary>Resolves a tool key to its executor.</summary>
public interface IToolCatalog
{
    IToolExecutor? Find(string toolKey);

    IReadOnlyList<IToolExecutor> All { get; }
}
