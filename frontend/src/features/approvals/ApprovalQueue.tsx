import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api } from '@/lib/api';
import type { ApiError } from '@/lib/api';
import { useScope } from '@/context/ScopeContext';
import { Card, EmptyState, ErrorState, LoadingState, Pill, RiskPill } from '@/components/primitives';
import { formatMoney, formatRelative } from '@/lib/format';

export function ApprovalQueue() {
  const { scope } = useScope();

  const query = useQuery({
    queryKey: ['approvals', scope.workspaceId, scope.environment],
    queryFn: () =>
      api.approvals.listPending({ workspaceId: scope.workspaceId, environment: scope.environment }),

    // Approvals block agent runs, so a stale queue costs real time. Refetching on an interval is
    // simpler and more robust here than a socket, and the payload is small.
    refetchInterval: 15_000,
    staleTime: 5_000,
  });

  if (query.isPending) {
    return <LoadingState label="Loading approvals…" />;
  }

  if (query.isError) {
    const error = query.error as ApiError;
    return <ErrorState message={error.message} traceId={error.traceId} />;
  }

  const approvals = query.data.items;

  return (
    <>
      <header style={{ marginBottom: 'var(--space-5)' }}>
        <h1 style={{ fontSize: '1.375rem' }}>Approvals</h1>
        <p style={{ color: 'var(--text-secondary)', margin: 'var(--space-1) 0 0' }}>
          Actions waiting on a human decision. Nothing here has taken effect yet.
        </p>
      </header>

      <Card>
        {approvals.length === 0 ? (
          <EmptyState
            title="Nothing is waiting"
            description="No agent has proposed an action that requires your approval in this environment."
          />
        ) : (
          <table>
            <caption className="visually-hidden">
              Approvals awaiting a decision, ordered by how soon they expire
            </caption>
            <thead>
              <tr style={{ textAlign: 'left', borderBottom: '1px solid var(--border-subtle)' }}>
                <th scope="col" style={headerCell}>Action</th>
                <th scope="col" style={headerCell}>Risk</th>
                <th scope="col" style={headerCell}>Requested by</th>
                <th scope="col" style={headerCell}>Impact</th>
                <th scope="col" style={headerCell}>Approvals</th>
                <th scope="col" style={headerCell}>Expires</th>
              </tr>
            </thead>
            <tbody>
              {approvals.map((approval) => (
                <tr key={approval.id} style={{ borderBottom: '1px solid var(--border-subtle)' }}>
                  <td style={bodyCell}>
                    <Link
                      to={`/approvals/${approval.id}`}
                      style={{ color: 'var(--accent)', fontWeight: 500, textDecoration: 'none' }}
                    >
                      {approval.title}
                    </Link>
                  </td>
                  <td style={bodyCell}>
                    <RiskPill riskClass={approval.riskClass} />
                  </td>
                  <td style={{ ...bodyCell, fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }}>
                    {approval.requestedByAgentKey}
                  </td>
                  <td style={bodyCell}>
                    {approval.estimatedCostAmount !== null && approval.estimatedCostCurrency !== null
                      ? formatMoney(approval.estimatedCostAmount, approval.estimatedCostCurrency)
                      : '—'}
                  </td>
                  <td style={bodyCell}>
                    <Pill tone={approval.approvalsReceived >= approval.approvalsRequired ? 'positive' : 'neutral'}>
                      {approval.approvalsReceived} of {approval.approvalsRequired}
                    </Pill>
                  </td>
                  <td style={bodyCell}>
                    <time dateTime={approval.expiresAt} title={approval.expiresAt}>
                      {formatRelative(approval.expiresAt)}
                    </time>
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

const bodyCell = {
  padding: 'var(--space-3)',
  verticalAlign: 'middle' as const,
};
