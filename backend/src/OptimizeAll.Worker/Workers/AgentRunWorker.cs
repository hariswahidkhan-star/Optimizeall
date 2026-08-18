using Microsoft.Extensions.DependencyInjection;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Application.Agents.Runtime;
using OptimizeAll.Domain.AgentCatalog;
using OptimizeAll.Domain.Common;
using OptimizeAll.Domain.Execution;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Worker.Workers;

/// <summary>
/// Executes queued agent runs.
/// <para>
/// One run at a time per worker instance, each in its own dependency-injection scope. Concurrency
/// comes from running more instances rather than more threads per instance, which keeps the lease,
/// the tenant scope and the database transaction one-to-one with the run and removes a whole class
/// of cross-contamination bug.
/// </para>
/// </summary>
public sealed class AgentRunWorker(
    IServiceScopeFactory scopeFactory,
    IRunQueue queue,
    IClock clock,
    ILogger<AgentRunWorker> logger)
    : BackgroundService
{
    /// <summary>
    /// How long a worker's claim on a run survives without a heartbeat. Long enough to ride out a
    /// slow provider call, short enough that a dead worker's run is reclaimed promptly.
    /// </summary>
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan QueuePollInterval = TimeSpan.FromSeconds(2);

    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Agent run worker {WorkerId} started.", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                AgentRunId? runId = await queue
                    .DequeueAsync(QueuePollInterval, stoppingToken).ConfigureAwait(false);

                if (runId is null)
                {
                    continue;
                }

                await ProcessRunAsync(runId.Value, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The loop must survive anything one run can do to it. A worker that exits on the
                // first unexpected failure takes its whole share of the queue's throughput with it.
                logger.LogError(exception, "Agent run worker loop error; continuing.");
                await Task.Delay(QueuePollInterval, stoppingToken).ConfigureAwait(false);
            }
        }

        logger.LogInformation("Agent run worker {WorkerId} stopping.", _workerId);
    }

    private async Task ProcessRunAsync(AgentRunId runId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        WorkerTenantContext tenantContext = scope.ServiceProvider.GetRequiredService<WorkerTenantContext>();
        IAgentRunRepository runs = scope.ServiceProvider.GetRequiredService<IAgentRunRepository>();
        IAgentDefinitionRepository definitions = scope.ServiceProvider.GetRequiredService<IAgentDefinitionRepository>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        AgentExecutor executor = scope.ServiceProvider.GetRequiredService<AgentExecutor>();

        // The scope is read from the run itself before any tenant-scoped query is issued, because
        // row-level security keys off it: querying first would return nothing.
        tenantContext.EnablePlatformScope(Guid.CreateVersion7());

        AgentRun? run = await runs.FindAsync(runId, cancellationToken).ConfigureAwait(false);

        if (run is null)
        {
            // Normal, not exceptional: the queue is at-least-once, so a duplicate delivery for a
            // run that was purged simply has nothing to do.
            logger.LogDebug("Run {RunId} no longer exists; acknowledging.", runId);
            await queue.AcknowledgeAsync(runId, cancellationToken).ConfigureAwait(false);
            return;
        }

        tenantContext.SetScope(run.TenantIdentifier, run.WorkspaceId, run.Environment, run.CorrelationId);

        if (run.IsTerminal)
        {
            await queue.AcknowledgeAsync(runId, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using IAsyncDisposable transaction = await unitOfWork
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        SharedKernel.Results.Result leased = run.AcquireLease(_workerId, LeaseDuration, clock.UtcNow);

        if (leased.IsFailure)
        {
            // Another worker holds a live lease. Acknowledged so the message is not redelivered in
            // a tight loop while that worker is legitimately working.
            logger.LogDebug("Run {RunId} is leased elsewhere; skipping.", runId);
            await unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
            await queue.AcknowledgeAsync(runId, cancellationToken).ConfigureAwait(false);
            return;
        }

        AgentDefinition? definition = await definitions
            .FindAsync(run.AgentDefinitionId, cancellationToken).ConfigureAwait(false);

        if (definition is null)
        {
            run.Fail(
                """{"code":"run.definition_missing","message":"The agent definition this run references no longer exists."}""",
                clock.UtcNow);

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            await queue.AcknowledgeAsync(runId, cancellationToken).ConfigureAwait(false);
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Executing run {RunId} of agent {AgentKey} v{Version} in {Environment}.",
            run.Id,
            run.AgentKey,
            definition.DefinitionVersion,
            run.Environment);

        RunOutcome outcome;

        try
        {
            outcome = await executor.ExecuteAsync(run, definition, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown mid-run. The lease is released so another worker picks it up rather than
            // waiting for the TTL, and the run is left in its current state.
            run.ReleaseLease();
            await PersistAsync(scope, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Run {RunId} threw during execution.", run.Id);

            run.Fail(
                """{"code":"run.unhandled_exception","message":"The run failed unexpectedly."}""",
                clock.UtcNow);

            await PersistAsync(scope, cancellationToken).ConfigureAwait(false);
            await queue.AcknowledgeAsync(runId, cancellationToken).ConfigureAwait(false);
            return;
        }

        ApplyOutcome(run, outcome);
        await PersistAsync(scope, cancellationToken).ConfigureAwait(false);
        await queue.AcknowledgeAsync(runId, cancellationToken).ConfigureAwait(false);
    }

    private void ApplyOutcome(AgentRun run, RunOutcome outcome)
    {
        switch (outcome.Conclusion)
        {
            case RunConclusion.Completed:
                run.Succeed(outcome.OutputJson ?? "{}", clock.UtcNow);
                break;

            case RunConclusion.SuspendedForApproval:
                // The run already released its lease when it suspended. It re-enters the queue when
                // a human decides, not on a timer.
                break;

            case RunConclusion.BudgetExhausted:
                run.AbortForBudget(
                    outcome.Error ?? SharedKernel.Results.Error.Exhausted(
                        "run.budget_exceeded", "The run exhausted its budget."),
                    clock.UtcNow);
                break;

            case RunConclusion.Failed:
                run.Fail(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        code = outcome.Error?.Code ?? "run.failed",
                        message = outcome.Error?.Message ?? "The run failed.",
                    }),
                    clock.UtcNow);
                break;
        }
    }

    private static async Task PersistAsync(AsyncServiceScope scope, CancellationToken cancellationToken)
    {
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await using IAsyncDisposable transaction = await unitOfWork
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
