import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';
import { api, type ApiError } from '@/lib/api';
import { ApprovalStatusPill, Button, Card, ErrorState, LoadingState, RiskPill } from '@/components/primitives';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { formatDateTime, formatMoney, formatRelative, prettyJson, shortFingerprint } from '@/lib/format';
import type { ApprovalDecisionKind } from '@/lib/types';

export function ApprovalDetail() {
  const { approvalId = '' } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [pendingDecision, setPendingDecision] = useState<ApprovalDecisionKind | null>(null);
  const [rationale, setRationale] = useState('');

  const query = useQuery({
    queryKey: ['approval', approvalId],
    queryFn: () => api.approvals.get(approvalId),
  });

  const decide = useMutation({
    mutationFn: (decision: ApprovalDecisionKind) =>
      api.approvals.decide(approvalId, decision, rationale.trim() === '' ? null : rationale.trim()),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['approvals'] });
      await queryClient.invalidateQueries({ queryKey: ['approval', approvalId] });
      navigate('/approvals');
    },
  });

  if (query.isPending) {
    return <LoadingState label="Loading approval…" />;
  }

  if (query.isError) {
    const error = query.error as ApiError;
    return <ErrorState message={error.message} traceId={error.traceId} />;
  }

  const approval = query.data;
  const isDecidable = approval.status === 'Pending';
  const requiresTypedConfirmation = approval.riskClass === 'Irreversible';

  return (
    <>
      <header style={{ marginBottom: 'var(--space-5)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)', flexWrap: 'wrap' }}>
          <h1 style={{ fontSize: '1.375rem' }}>{approval.title}</h1>
          <RiskPill riskClass={approval.riskClass} />
          <ApprovalStatusPill status={approval.status} />
        </div>
        <p style={{ color: 'var(--text-secondary)', margin: 'var(--space-2) 0 0' }}>
          Requested by {approval.agentKey ?? 'an agent'}
          {approval.objectiveTitle !== null && <> for the objective “{approval.objectiveTitle}”</>}. Expires{' '}
          <time dateTime={approval.expiresAt} title={formatDateTime(approval.expiresAt)}>
            {formatRelative(approval.expiresAt)}
          </time>
          .
        </p>
      </header>

      <div style={{ display: 'grid', gap: 'var(--space-5)', gridTemplateColumns: 'minmax(0, 2fr) minmax(16rem, 1fr)' }}>
        <div style={{ display: 'grid', gap: 'var(--space-5)' }}>
          {/*
            The exact bytes that will execute. Shown verbatim rather than summarised: the platform's
            guarantee is that what was approved is what runs, and a summary would mean the approver
            never actually saw the thing they authorised.
          */}
          <Card title="Payload to be executed">
            <pre
              style={{
                margin: 0,
                padding: 'var(--space-4)',
                background: 'var(--surface-sunken)',
                borderRadius: 'var(--radius-md)',
                overflowX: 'auto',
                fontFamily: 'var(--font-mono)',
                fontSize: '0.8125rem',
                lineHeight: 1.6,
              }}
            >
              <code>{prettyJson(approval.payloadJson)}</code>
            </pre>
            <p style={{ color: 'var(--text-muted)', fontSize: '0.75rem', marginTop: 'var(--space-3)' }}>
              Fingerprint <code style={{ fontFamily: 'var(--font-mono)' }}>{shortFingerprint(approval.payloadFingerprint)}</code>.
              Execution is refused if the payload differs from this by a single byte.
            </p>
          </Card>

          {approval.decisions.length > 0 && (
            <Card title="Decisions so far">
              <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'grid', gap: 'var(--space-3)' }}>
                {approval.decisions.map((decision) => (
                  <li key={decision.approverUserId} style={{ display: 'grid', gap: 'var(--space-1)' }}>
                    <span>
                      <strong>{decision.approverDisplayName}</strong> {decision.decision === 'Approve' ? 'approved' : 'rejected'}{' '}
                      <time dateTime={decision.decidedAt}>{formatDateTime(decision.decidedAt)}</time>
                    </span>
                    {decision.rationale !== null && (
                      <span style={{ color: 'var(--text-secondary)' }}>“{decision.rationale}”</span>
                    )}
                  </li>
                ))}
              </ul>
            </Card>
          )}
        </div>

        <div style={{ display: 'grid', gap: 'var(--space-5)', alignContent: 'start' }}>
          <Card title="Impact">
            <dl style={{ margin: 0, display: 'grid', gap: 'var(--space-3)' }}>
              <div>
                <dt style={definitionTerm}>Estimated cost</dt>
                <dd style={definitionDetail}>
                  {approval.estimatedCostAmount !== null && approval.estimatedCostCurrency !== null
                    ? formatMoney(approval.estimatedCostAmount, approval.estimatedCostCurrency)
                    : 'No direct cost'}
                </dd>
              </div>
              <div>
                <dt style={definitionTerm}>Approvals</dt>
                <dd style={definitionDetail}>
                  {approval.approvalsReceived} of {approval.approvalsRequired} required
                </dd>
              </div>
              <div>
                <dt style={definitionTerm}>Reversibility</dt>
                <dd style={definitionDetail}>
                  {approval.riskClass === 'Irreversible'
                    ? 'This action cannot be undone by the platform.'
                    : 'Reversible where the target system permits it.'}
                </dd>
              </div>
            </dl>
          </Card>

          {isDecidable && (
            <Card title="Your decision">
              <label style={{ display: 'block' }}>
                <span style={{ fontSize: '0.8125rem', color: 'var(--text-secondary)' }}>
                  Rationale <span style={{ color: 'var(--text-muted)' }}>(required to reject)</span>
                </span>
                <textarea
                  value={rationale}
                  onChange={(event) => setRationale(event.target.value)}
                  rows={4}
                  style={{
                    marginTop: 'var(--space-2)',
                    width: '100%',
                    padding: 'var(--space-2)',
                    borderRadius: 'var(--radius-md)',
                    border: '1px solid var(--border-strong)',
                    background: 'var(--surface-canvas)',
                    color: 'var(--text-primary)',
                    font: 'inherit',
                    resize: 'vertical',
                  }}
                />
              </label>

              <div style={{ display: 'flex', gap: 'var(--space-2)', marginTop: 'var(--space-4)' }}>
                <Button variant="primary" onClick={() => setPendingDecision('Approve')}>
                  Approve
                </Button>
                <Button
                  variant="danger"
                  disabled={rationale.trim() === ''}
                  onClick={() => setPendingDecision('Reject')}
                >
                  Reject
                </Button>
              </div>

              {decide.isError && (
                <div style={{ marginTop: 'var(--space-4)' }}>
                  <ErrorState
                    message={(decide.error as ApiError).message}
                    traceId={(decide.error as ApiError).traceId}
                  />
                </div>
              )}
            </Card>
          )}
        </div>
      </div>

      <ConfirmDialog
        open={pendingDecision !== null}
        title={pendingDecision === 'Approve' ? 'Approve this action?' : 'Reject this action?'}
        description={
          pendingDecision === 'Approve'
            ? approval.riskClass === 'Irreversible'
              ? 'This action cannot be undone once it executes. Confirm that you have read the payload above.'
              : 'The agent will execute exactly the payload shown, and nothing else.'
            : 'The originating run will be cancelled and your rationale recorded against this request.'
        }
        confirmLabel={pendingDecision === 'Approve' ? 'Approve' : 'Reject'}
        variant={pendingDecision === 'Approve' ? 'primary' : 'danger'}
        {...(pendingDecision === 'Approve' && requiresTypedConfirmation
          ? { confirmPhrase: approval.agentKey ?? 'approve' }
          : {})}
        onCancel={() => setPendingDecision(null)}
        onConfirm={() => {
          if (pendingDecision !== null) {
            decide.mutate(pendingDecision);
            setPendingDecision(null);
          }
        }}
      />
    </>
  );
}

const definitionTerm = {
  fontSize: '0.75rem',
  textTransform: 'uppercase' as const,
  letterSpacing: '0.04em',
  color: 'var(--text-muted)',
};

const definitionDetail = {
  margin: 'var(--space-1) 0 0',
};
