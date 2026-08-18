using System.Globalization;
using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Domain.Common;
using StackExchange.Redis;

namespace OptimizeAll.Infrastructure.Caching;

/// <summary>
/// The agent run queue, on a Redis stream with a consumer group.
/// <para>
/// A stream rather than a list because a consumer group gives explicit acknowledgement: a worker
/// that dies mid-run leaves its message pending rather than losing it, and it can be reclaimed.
/// Delivery is at-least-once, which is safe here because the run's own lease and terminal-state
/// checks make a duplicate delivery a no-op.
/// </para>
/// </summary>
public sealed class RedisRunQueue(
    IConnectionMultiplexer redis,
    ILogger<RedisRunQueue> logger)
    : IRunQueue
{
    private const string StreamKey = "optimizeall:runs";
    private const string ConsumerGroup = "agent-workers";
    private const string RunIdField = "runId";
    private const string WorkspaceField = "workspaceId";

    private readonly string _consumerName = $"{Environment.MachineName}:{Environment.ProcessId}";
    private bool _groupEnsured;

    public async Task EnqueueAsync(AgentRunId runId, WorkspaceId workspaceId, CancellationToken cancellationToken)
    {
        IDatabase database = redis.GetDatabase();
        await EnsureConsumerGroupAsync(database).ConfigureAwait(false);

        await database.StreamAddAsync(
            StreamKey,
            [
                new NameValueEntry(RunIdField, runId.Value.ToString()),
                new NameValueEntry(WorkspaceField, workspaceId.Value.ToString()),
            ]).ConfigureAwait(false);
    }

    public async Task<AgentRunId?> DequeueAsync(TimeSpan waitTime, CancellationToken cancellationToken)
    {
        IDatabase database = redis.GetDatabase();
        await EnsureConsumerGroupAsync(database).ConfigureAwait(false);

        StreamEntry[] entries = await database.StreamReadGroupAsync(
            StreamKey,
            ConsumerGroup,
            _consumerName,
            StreamPosition.NewMessages,
            count: 1).ConfigureAwait(false);

        if (entries.Length == 0)
        {
            // StackExchange.Redis has no blocking read on the async surface, so the caller's wait is
            // honoured here. A short poll is acceptable because enqueue latency is dominated by the
            // run itself, not by pickup.
            await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
            return null;
        }

        StreamEntry entry = entries[0];
        string? rawRunId = entry[RunIdField];

        if (!Guid.TryParse(rawRunId, out Guid runId))
        {
            // A malformed message would otherwise be redelivered forever. It is acknowledged and
            // logged so the queue keeps moving.
            logger.LogError("Discarding malformed queue entry {EntryId}: run id '{RunId}'.", entry.Id, rawRunId);
            await database.StreamAcknowledgeAsync(StreamKey, ConsumerGroup, entry.Id).ConfigureAwait(false);
            return null;
        }

        return AgentRunId.From(runId);
    }

    public async Task AcknowledgeAsync(AgentRunId runId, CancellationToken cancellationToken)
    {
        IDatabase database = redis.GetDatabase();

        // Acknowledgement is by stream entry id, so the pending list is scanned for this run. The
        // pending list is small by construction — it holds only in-flight work.
        StreamPendingMessageInfo[] pending = await database
            .StreamPendingMessagesAsync(StreamKey, ConsumerGroup, count: 100, consumerName: _consumerName)
            .ConfigureAwait(false);

        foreach (StreamPendingMessageInfo message in pending)
        {
            StreamEntry[] entries = await database
                .StreamRangeAsync(StreamKey, message.MessageId, message.MessageId, 1).ConfigureAwait(false);

            if (entries.Length > 0 && entries[0][RunIdField] == runId.Value.ToString())
            {
                await database.StreamAcknowledgeAsync(StreamKey, ConsumerGroup, message.MessageId)
                    .ConfigureAwait(false);
                return;
            }
        }
    }

    public async Task<long> DepthAsync(CancellationToken cancellationToken)
    {
        IDatabase database = redis.GetDatabase();
        return await database.StreamLengthAsync(StreamKey).ConfigureAwait(false);
    }

    private async Task EnsureConsumerGroupAsync(IDatabase database)
    {
        if (_groupEnsured)
        {
            return;
        }

        try
        {
            await database.StreamCreateConsumerGroupAsync(
                StreamKey, ConsumerGroup, StreamPosition.Beginning, createStream: true).ConfigureAwait(false);
        }
        catch (RedisServerException exception) when (exception.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
        {
            // Another replica created it first. This is the normal path after the first start.
        }

        _groupEnsured = true;
    }
}

/// <summary>
/// A time-boxed mutual-exclusion lease.
/// <para>
/// Release is done with a Lua script that compares the token before deleting, so a holder whose
/// lease already expired cannot delete a lock that a different holder has since acquired — the
/// classic way naive distributed locks silently stop providing mutual exclusion.
/// </para>
/// </summary>
public sealed class RedisDistributedLock(IConnectionMultiplexer redis, ILogger<RedisDistributedLock> logger)
    : IDistributedLock
{
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string key,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        IDatabase database = redis.GetDatabase();
        string redisKey = $"optimizeall:lock:{key}";
        string token = Guid.CreateVersion7().ToString();

        bool acquired = await database
            .StringSetAsync(redisKey, token, duration, When.NotExists).ConfigureAwait(false);

        return acquired ? new Lease(database, redisKey, token, logger) : null;
    }

    private sealed class Lease(IDatabase database, string key, string token, ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await database.ScriptEvaluateAsync(
                    ReleaseScript,
                    [key],
                    [token]).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Release failure is survivable: the lease expires on its own. Losing the process
                // over it would be worse than waiting out the TTL.
                logger.LogWarning(exception, "Failed to release lock {Key}; it will expire naturally.", key);
            }
        }
    }
}
