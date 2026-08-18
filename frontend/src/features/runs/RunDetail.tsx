import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { api, type ApiError } from '@/lib/api';
import { Card, ErrorState, LoadingState, Pill, RiskPill, RunStatusPill } from '@/components/primitives';
import { formatDateTime, formatMoney, formatNumber, prettyJson } from '@/lib/format';
import type { RunStepView, ToolInvocationView } from '@/lib/types';

/**
 * The full record of one run.
 *
 * This screen is the answer to "why did the agent do that?", so it shows the trace in order rather
 * than a summary: prompts, completions, retrievals, and every tool call including the ones that
 * were denied. A denied call is often the most informative entry in the list.
 */
export function RunDetail() {
  const { runId = '' } = useParams();

  const query = useQuery({
    queryKey: ['run', runId],
    queryFn: () => api.runs.get(runId),
    refetchInterval: (data) =>
      data.state.data?.status === 'Running' || data.state.data?.status === 'Queued' ? 5_000 : false,
  });

  if (query.isPending) {
    return <LoadingState label="Loading run…" />;
  }

  if (query.isError) {
    const error = query.error as ApiError;
    return <ErrorState message={error.message} traceId={error.traceId} />;
  }

  const run = query.data;

  return (
    <>
      <header style={{ marginBottom: 'var(--space-5)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)', flexWrap: 'wrap' }}>
          <h1 style={{ fontSize: '1.375rem' }}>{run.agentKey}</h1>
          <Pill tone="neutral">v{run.definitionVersion}</Pill>
          <RunStatusPill status={run.status} />
        </div>
        <p style={{ color: 'var(--text-secondary)', margin: 'var(--space-2) 0 0', fontFamily: 'var(--font-mono)', fontSize: '0.75rem' }}>
          correlation {run.correlationId}
        </p>
      </header>

      <div style={{ display: 'grid', gap: 'var(--space-5)', gridTemplateColumns: 'minmax(0, 2fr) minmax(16rem, 1fr)' }}>
        <div style={{ display: 'grid', gap: 'var(--space-5)' }}>
          <Card title="Execution trace">
            {run.steps.length === 0 ? (
              <p style={{ color: 'var(--text-secondary)', margin: 0 }}>
                This run has not produced any steps yet.
              </p>
            ) : (
              <ol style={{ listStyle: 'none', margin: 0, padding: 0, display: 'grid', gap: 'var(--space-3)' }}>
                {run.steps.map((step) => (
                  <TraceStep key={step.sequence} step={step} />
                ))}
              </ol>
            )}
          </Card>

          {run.toolInvocations.length > 0 && (
            <Card title="Tool calls">
              <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'grid', gap: 'var(--space-3)' }}>
                {run.toolInvocations.map((invocation) => (
                  <ToolCall key={invocation.id} invocation={invocation} />
                ))}
              </ul>
            </Card>
          )}

          {run.errorJson !== null && (
            <Card title="Failure detail">
              <pre style={preStyle}>
                <code>{prettyJson(run.errorJson)}</code>
              </pre>
            </Card>
          )}
        </div>

        <div style={{ display: 'grid', gap: 'var(--space-5)', alignContent: 'start' }}>
          <Card title="Accounting">
            <dl style={{ margin: 0, display: 'grid', gap: 'var(--space-3)' }}>
              <Definition label="Provider" value={run.provider ?? 'Not yet routed'} />
              <Definition label="Model" value={run.model ?? '—'} />
              <Definition label="Prompt tokens" value={formatNumber(run.promptTokens)} />
              <Definition label="Completion tokens" value={formatNumber(run.completionTokens)} />
              <Definition label="Cost" value={formatMoney(run.costAmount, run.costCurrency)} />
              <Definition label="Iterations" value={String(run.iterationCount)} />
              <Definition
                label="Started"
                value={run.startedAt === null ? 'Not started' : formatDateTime(run.startedAt)}
              />
              <Definition
                label="Completed"
                value={run.completedAt === null ? 'In progress' : formatDateTime(run.completedAt)}
              />
            </dl>
          </Card>
        </div>
      </div>
    </>
  );
}

function TraceStep({ step }: { readonly step: RunStepView }) {
  return (
    <li
      style={{
        border: '1px solid var(--border-subtle)',
        borderRadius: 'var(--radius-md)',
        padding: 'var(--space-3)',
        background: 'var(--surface-sunken)',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)', flexWrap: 'wrap' }}>
        <Pill tone={step.stepType === 'Error' ? 'critical' : step.stepType === 'ApprovalGate' ? 'caution' : 'neutral'}>
          {step.stepType}
        </Pill>
        <span style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>
          step {step.sequence} · {formatNumber(step.tokens)} tokens · {step.latencyMilliseconds}ms
        </span>
        <time
          dateTime={step.occurredAt}
          style={{ marginLeft: 'auto', color: 'var(--text-muted)', fontSize: '0.75rem' }}
        >
          {formatDateTime(step.occurredAt)}
        </time>
      </div>
      <pre style={{ ...preStyle, marginTop: 'var(--space-2)', background: 'var(--surface-raised)' }}>
        <code>{prettyJson(step.contentJson)}</code>
      </pre>
    </li>
  );
}

function ToolCall({ invocation }: { readonly invocation: ToolInvocationView }) {
  const tone =
    invocation.status === 'Executed'
      ? 'positive'
      : invocation.status === 'Denied' || invocation.status === 'Failed'
        ? 'critical'
        : invocation.status === 'AwaitingApproval'
          ? 'caution'
          : 'neutral';

  return (
    <li style={{ border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)', padding: 'var(--space-3)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)', flexWrap: 'wrap' }}>
        <code style={{ fontFamily: 'var(--font-mono)', fontWeight: 600 }}>{invocation.toolKey}</code>
        <RiskPill riskClass={invocation.riskClass} />
        <Pill tone={tone}>{invocation.status}</Pill>
        {invocation.durationMilliseconds !== null && (
          <span style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>
            {invocation.durationMilliseconds}ms
          </span>
        )}
      </div>

      {invocation.denialReason !== null && (
        <p style={{ margin: 'var(--space-2) 0 0', color: 'var(--critical)' }}>{invocation.denialReason}</p>
      )}

      <details style={{ marginTop: 'var(--space-2)' }}>
        <summary style={{ cursor: 'pointer', fontSize: '0.8125rem', color: 'var(--text-secondary)' }}>
          Arguments and result
        </summary>
        <pre style={{ ...preStyle, marginTop: 'var(--space-2)' }}>
          <code>{prettyJson(invocation.argumentsJson)}</code>
        </pre>
        {invocation.resultJson !== null && (
          <pre style={{ ...preStyle, marginTop: 'var(--space-2)' }}>
            <code>{prettyJson(invocation.resultJson)}</code>
          </pre>
        )}
      </details>
    </li>
  );
}

function Definition({ label, value }: { readonly label: string; readonly value: string }) {
  return (
    <div>
      <dt style={{ fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--text-muted)' }}>
        {label}
      </dt>
      <dd style={{ margin: 'var(--space-1) 0 0', fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }}>{value}</dd>
    </div>
  );
}

const preStyle = {
  margin: 0,
  padding: 'var(--space-3)',
  background: 'var(--surface-sunken)',
  borderRadius: 'var(--radius-sm)',
  overflowX: 'auto' as const,
  fontFamily: 'var(--font-mono)',
  fontSize: '0.75rem',
  lineHeight: 1.6,
};
