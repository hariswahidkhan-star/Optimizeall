import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api, type ApiError } from '@/lib/api';
import { useScope } from '@/context/ScopeContext';
import { Card, EmptyState, ErrorState, LoadingState, Pill, RunStatusPill } from '@/components/primitives';
import { formatDateTime, formatMoney, formatNumber } from '@/lib/format';

export function RunList() {
  const { scope } = useScope();

  const query = useQuery({
    queryKey: ['runs', scope.workspaceId, scope.environment],
    queryFn: () => api.runs.list({ workspaceId: scope.workspaceId, environment: scope.environment }),
    refetchInterval: 10_000,
  });

  if (query.isPending) {
    return <LoadingState label="Loading agent runs…" />;
  }

  if (query.isError) {
    const error = query.error as ApiError;
    return <ErrorState message={error.message} traceId={error.traceId} />;
  }

  const runs = query.data.items;

  return (
    <>
      <header style={{ marginBottom: 'var(--space-5)' }}>
        <h1 style={{ fontSize: '1.375rem' }}>Agent runs</h1>
        <p style={{ color: 'var(--text-secondary)', margin: 'var(--space-1) 0 0' }}>
          Every execution, with its full reasoning trace and cost.
        </p>
      </header>

      <Card>
        {runs.length === 0 ? (
          <EmptyState
            title="No runs yet"
            description="Queue an agent run or start a workflow, and executions will appear here."
          />
        ) : (
          <table>
            <caption className="visually-hidden">Agent runs, newest first</caption>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '1px solid var(--border-subtle)' }}>
                <th scope="col" style={headerCell}>Agent</th>
                <th scope="col" style={headerCell}>Status</th>
                <th scope="col" style={headerCell}>Tokens</th>
                <th scope="col" style={headerCell}>Cost</th>
                <th scope="col" style={headerCell}>Started</th>
              </tr>
            </thead>
            <tbody>
              {runs.map((run) => (
                <tr key={run.id} style={{ borderBottom: '1px solid var(--border-subtle)' }}>
                  <td style={bodyCell}>
                    <Link
                      to={`/runs/${run.id}`}
                      style={{ color: 'var(--accent)', fontWeight: 500, textDecoration: 'none' }}
                    >
                      {run.agentKey}
                    </Link>
                    <span style={{ color: 'var(--text-muted)', fontSize: '0.75rem', marginLeft: 'var(--space-2)' }}>
                      v{run.definitionVersion}
                    </span>
                    {run.isDryRun && (
                      <span style={{ marginLeft: 'var(--space-2)' }}>
                        <Pill tone="accent">Dry run</Pill>
                      </span>
                    )}
                  </td>
                  <td style={bodyCell}>
                    <RunStatusPill status={run.status} />
                  </td>
                  <td style={bodyCell}>{formatNumber(run.totalTokens)}</td>
                  <td style={bodyCell}>{formatMoney(run.costAmount, run.costCurrency)}</td>
                  <td style={bodyCell}>
                    {run.startedAt === null ? (
                      <span style={{ color: 'var(--text-muted)' }}>Queued</span>
                    ) : (
                      <time dateTime={run.startedAt}>{formatDateTime(run.startedAt)}</time>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </>
  );
}

const headerCell = {
  padding: 'var(--space-2) var(--space-3)',
  fontSize: '0.75rem',
  fontWeight: 600,
  textTransform: 'uppercase' as const,
  letterSpacing: '0.04em',
  color: 'var(--text-muted)',
};

const bodyCell = { padding: 'var(--space-3)', verticalAlign: 'middle' as const };
