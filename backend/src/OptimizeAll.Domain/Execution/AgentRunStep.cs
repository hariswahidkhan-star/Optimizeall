using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Execution;

public enum RunStepType
{
    Prompt = 1,
    Completion = 2,
    ToolCall = 3,
    ToolResult = 4,
    Retrieval = 5,
    Decision = 6,
    Error = 7,
    ApprovalGate = 8,
}

/// <summary>
/// One entry in a run's reasoning trace. The trace is what makes an agent's behaviour reviewable
/// after the fact: without it, "why did the agent do that?" has no answer an auditor can accept.
/// </summary>
public sealed class AgentRunStep : Entity<Guid>
{
    private AgentRunStep(
        Guid id,
        AgentRunId runId,
        int sequence,
        RunStepType stepType,
        string contentJson,
        long tokens,
        int latencyMilliseconds,
        DateTimeOffset occurredAt)
        : base(id)
    {
        AgentRunId = runId;
        Sequence = sequence;
        StepType = stepType;
        ContentJson = contentJson;
        Tokens = tokens;
        LatencyMilliseconds = latencyMilliseconds;
        OccurredAt = occurredAt;
    }

    private AgentRunStep()
    {
    }

    public AgentRunId AgentRunId { get; private set; }

    /// <summary>Monotonic within the run. Ordering by timestamp alone is unreliable at sub-millisecond resolution.</summary>
    public int Sequence { get; private set; }

    public RunStepType StepType { get; private set; }

    public string ContentJson { get; private set; } = null!;

    public long Tokens { get; private set; }

    public int LatencyMilliseconds { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AgentRunStep Create(
        AgentRunId runId,
        int sequence,
        RunStepType stepType,
        string contentJson,
        DateTimeOffset occurredAt,
        long tokens = 0,
        int latencyMilliseconds = 0)
    {
        Ensure.NotNullOrWhiteSpace(contentJson);

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Step sequence cannot be negative.");
        }

        return new AgentRunStep(
            Guid.CreateVersion7(),
            runId,
            sequence,
            stepType,
            contentJson,
            tokens,
            latencyMilliseconds,
            occurredAt);
    }
}
