import { useQuery } from '@tanstack/react-query';
import { api, type ApiError } from '@/lib/api';
import { useScope } from '@/context/ScopeContext';
import { Card, ErrorState, LoadingState, Pill } from '@/components/primitives';
import { formatDuration, formatMoney, formatNumber, formatPercent } from '@/lib/format';

export function Dashboard() {
  const { scope } = useScope();

  const to = new Date();
  const from = new Date(to.getTime() - 30 * 24 * 60 * 60 * 1000);

  const query = useQuery({
    queryKey: ['dashboard', scope.workspaceId, scope.environment, from.toISOString().slice(0, 10)],
    queryFn: () =>
      api.workspaces.dashboard(
        { workspaceId: scope.workspaceId, environment: scope.environment },
        from.toISOString(),
        to.toISOString(),
      ),
    staleTime: 60_000,
  });

  if (query.isPending) {
    return <LoadingState label="Loading dashboard…" />;
  }

  if (query.isError) {
    const error = query.error as ApiError;
    return <ErrorState message={error.message} traceId={error.traceId} />;
  }

  const data = query.data;
  const totalRuns = data.runsCompleted + data.runsFailed;
  const successRate = totalRuns === 0 ? 0 : data.runsCompleted / totalRuns;
  const budgetUsed = data.budgetCapAmount === 0 ? 0 : data.spendAmount / data.budgetCapAmount;

  return (
    <>
      <header style={{ marginBottom: 'var(--space-5)' }}>
        <h1 style={{ fontSize: '1.375rem' }}>Executive view</h1>
        <p style={{ color: 'var(--text-secondary)', margin: 'var(--space-1) 0 0' }}>
          Last 30 days in {scope.workspaceName} · {scope.environment}
        </p>
      </header>

      <div
        style={{
          display: 'grid',
          gap: 'var(--space-4)',
          gridTemplateColumns: 'repeat(auto-fit, minmax(13rem, 1fr))',
          marginBottom: 'var(--space-5)',
        }}
      >
        <Metric label="Runs completed" value={formatNumber(data.runsCompleted)} />
        <Metric
          label="Success rate"
          value={totalRuns === 0 ? '—' : formatPercent(successRate)}
          tone={totalRuns === 0 ? 'neutral' : successRate >= 0.95 ? 'positive' : 'caution'}
        />
        <Metric label="Approvals pending" value={formatNumber(data.approvalsPending)} tone={data.approvalsPending > 0 ? 'caution' : 'neutral'} />
        <Metric
          label="Median decision time"
          value={data.approvalMedianMinutes === 0 ? '—' : formatDuration(data.approvalMedianMinutes * 60)}
        />
        <Metric
          label="Spend"
          value={formatMoney(data.spendAmount, data.spendCurrency)}
          detail={
            data.budgetCapAmount > 0
              ? `${formatPercent(budgetUsed)} of ${formatMoney(data.budgetCapAmount, data.spendCurrency)}`
              : 'No cap set'
          }
          // Warns at 80% consumed rather than at breach: an executive needs to act before the cap
          // stops work, not to be told after it already has.
          tone={budgetUsed >= 1 ? 'critical' : budgetUsed >= 0.8 ? 'caution' : 'neutral'}
        />
      </div>

      <Card title="Agent performance">
        {data.agentPerformance.length === 0 ? (
          <p style={{ color: 'var(--text-secondary)', margin: 0 }}>No agent has run in this period.</p>
        ) : (
          <table>
            <caption className="visually-hidden">Per-agent performance over the last 30 days</caption>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '1px solid var(--border-subtle)' }}>
                <th scope="col" style={headerCell}>Agent</th>
                <th scope="col" style={headerCell}>Runs</th>
                <th scope="col" style={headerCell}>Success</th>
                <th scope="col" style={headerCell}>Approval rate</th>
                <th scope="col" style={headerCell}>Avg cost</th>
                <th scope="col" style={headerCell}>Avg duration</th>
              </tr>
            </thead>
            <tbody>
              {data.agentPerformance.map((agent) => (
                <tr key={agent.agentKey} style={{ borderBottom: '1px solid var(--border-subtle)' }}>
                  <td style={bodyCell}>
                    <strong>{agent.displayName}</strong>
                    <div style={{ color: 'var(--text-muted)', fontSize: '0.75rem', fontFamily: 'var(--font-mono)' }}>
                      {agent.agentKey}
                    </div>
                  </td>
                  <td style={bodyCell}>{formatNumber(agent.runCount)}</td>
                  <td style={bodyCell}>{formatPercent(agent.successRate)}</td>
                  <td style={bodyCell}>{formatPercent(agent.approvalRate)}</td>
                  <td style={bodyCell}>{formatMoney(agent.averageCost, data.spendCurrency)}</td>
                  <td style={bodyCell}>{formatDuration(agent.averageDurationSeconds)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </>
  );
}

function Metric({
  label,
  value,
  detail,
  tone = 'neutral',
}: {
  readonly label: string;
  readonly value: string;
  readonly detail?: string;
  readonly tone?: 'neutral' | 'positive' | 'caution' | 'critical';
}) {
  return (
    <div
      style={{
        background: 'var(--surface-raised)',
        border: '1px solid var(--border-subtle)',
        borderRadius: 'var(--radius-lg)',
        padding: 'var(--space-4)',
        boxShadow: 'var(--shadow-sm)',
      }}
    >
      <div style={{ fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--text-muted)' }}>
        {label}
      </div>
      <div style={{ fontSize: '1.75rem', fontWeight: 600, marginTop: 'var(--space-2)', letterSpacing: '-0.02em' }}>
        {value}
      </div>
      {detail !== undefined && (
        <div style={{ marginTop: 'var(--space-2)' }}>
          <Pill tone={tone}>{detail}</Pill>
        </div>
      )}
    </div>
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
