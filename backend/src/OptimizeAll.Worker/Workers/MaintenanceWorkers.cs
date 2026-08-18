using Microsoft.Extensions.DependencyInjection;
using OptimizeAll.Application.Abstractions.Persistence;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Domain.Execution;
using OptimizeAll.Domain.Governance;
using OptimizeAll.Domain.Scheduling;
using OptimizeAll.SharedKernel.Results;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Worker.Workers;

/// <summary>
/// Reclaims runs whose worker died.
/// <para>
/// An expired lease is the only evidence used. A live lease means the holder is still heartbeating,
/// and reclaiming it would execute the same agent twice — with the external effects that implies.
/// </para>
/// </summary>
public sealed class LeaseReclaimWorker(
    IServiceScopeFactory scopeFactory,
    IRunQueue queue,
    IClock clock,
    ILogger<LeaseReclaimWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(SweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Lease reclamation sweep failed; will retry next tick.");
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        WorkerTenantContext tenantContext = scope.ServiceProvider.GetRequiredService<WorkerTenantContext>();

        // A cross-tenant sweep, so it runs under the platform scope with a BYPASSRLS role. This is
        // one of the few places that is legitimate, and it reads only lease metadata.
        tenantContext.EnablePlatformScope(Guid.CreateVersion7());

        IAgentRunRepository runs = scope.ServiceProvider.GetRequiredService<IAgentRunRepository>();

        IReadOnlyList<AgentRun> reclaimable = await runs
            .ListReclaimableAsync(BatchSize, clock.UtcNow, cancellationToken).ConfigureAwait(false);

        if (reclaimable.Count == 0)
        {
            return;
        }

        logger.LogWarning("Reclaiming {Count} run(s) with expired leases.", reclaimable.Count);

        foreach (AgentRun run in reclaimable)
        {
            run.ReleaseLease();
            await queue.EnqueueAsync(run.Id, run.WorkspaceId, cancellationToken).ConfigureAwait(false);
        }

        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await using IAsyncDisposable transaction = await unitOfWork
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Expires approval requests whose window has closed.
/// <para>
/// A tidy-up, not the control. The aggregate evaluates expiry whenever a decision is attempted, so
/// a lapsed request cannot be approved even if this sweep is stopped or far behind. What this adds
/// is that the queue an approver sees reflects reality.
/// </para>
/// </summary>
public sealed class ApprovalExpiryWorker(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<ApprovalExpiryWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    private const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(SweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Approval expiry sweep failed; will retry next tick.");
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        WorkerTenantContext tenantContext = scope.ServiceProvider.GetRequiredService<WorkerTenantContext>();
        tenantContext.EnablePlatformScope(Guid.CreateVersion7());

        IApprovalRepository approvals = scope.ServiceProvider.GetRequiredService<IApprovalRepository>();
        IAgentRunRepository runs = scope.ServiceProvider.GetRequiredService<IAgentRunRepository>();

        IReadOnlyList<ApprovalRequest> expired = await approvals
            .ListExpiredAsync(clock.UtcNow, BatchSize, cancellationToken).ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return;
        }

        logger.LogInformation("Expiring {Count} approval request(s) past their window.", expired.Count);

        foreach (ApprovalRequest request in expired)
        {
            request.Expire(clock.UtcNow);

            if (request.AgentRunId is not { } runId)
            {
                continue;
            }

            AgentRun? run = await runs.FindAsync(runId, cancellationToken).ConfigureAwait(false);

            // A run blocked on an approval that expired has nothing left to do. Cancelling it is
            // the fail-closed outcome: it must not resume as if permission had been granted.
            if (run is { Status: AgentRunStatus.AwaitingApproval })
            {
                run.Cancel("The required approval expired without a decision.", clock.UtcNow);
            }
        }

        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await using IAsyncDisposable transaction = await unitOfWork
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Turns due schedules into occurrences.
/// <para>
/// Every replica runs this loop. Exactly-once firing comes from the unique constraint on
/// <c>(schedule_id, occurrence_utc)</c>: all replicas race to insert and exactly one wins. There is
/// no leader election, and therefore no window in which two replicas both believe they lead.
/// </para>
/// </summary>
public sealed class SchedulerWorker(
    IServiceScopeFactory scopeFactory,
    ICronEvaluator cron,
    IClock clock,
    ILogger<SchedulerWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);

    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduler tick failed; will retry.");
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        WorkerTenantContext tenantContext = scope.ServiceProvider.GetRequiredService<WorkerTenantContext>();
        tenantContext.EnablePlatformScope(Guid.CreateVersion7());

        IScheduleRepository schedules = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
        DateTimeOffset now = clock.UtcNow;

        IReadOnlyList<ScheduleDefinition> due = await schedules
            .ListDueAsync(now, BatchSize, cancellationToken).ConfigureAwait(false);

        foreach (ScheduleDefinition schedule in due)
        {
            DateTimeOffset occurrenceUtc = schedule.NextOccurrenceUtc ?? now;

            ScheduleOccurrence occurrence = ScheduleOccurrence.Claim(
                schedule.TenantIdentifier, schedule.Id, occurrenceUtc, now);

            bool claimed = await schedules
                .TryClaimOccurrenceAsync(occurrence, cancellationToken).ConfigureAwait(false);

            if (!claimed)
            {
                // Another replica won the race. Expected, and not worth logging above debug.
                logger.LogDebug(
                    "Occurrence {Occurrence:O} of schedule {ScheduleKey} was claimed elsewhere.",
                    occurrenceUtc,
                    schedule.Key);

                continue;
            }

            Result<DateTimeOffset?> next = cron.NextOccurrence(
                schedule.CronExpression, schedule.TimeZoneId, now);

            if (next.IsFailure)
            {
                // A schedule whose expression no longer parses is disabled rather than retried
                // forever. Left enabled it would be re-selected on every tick.
                logger.LogError(
                    "Schedule {ScheduleKey} has an invalid expression and has been disabled: {Reason}",
                    schedule.Key,
                    next.Error.Message);

                schedule.Disable();
                occurrence.MarkFailed(next.Error.Message, now);
                continue;
            }

            schedule.RecordOccurrenceDispatched(occurrenceUtc, next.Value);

            logger.LogInformation(
                "Schedule {ScheduleKey} fired for {Occurrence:O}; next at {Next:O}.",
                schedule.Key,
                occurrenceUtc,
                next.Value);
        }

        if (due.Count > 0)
        {
            IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await using IAsyncDisposable transaction = await unitOfWork
                .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
