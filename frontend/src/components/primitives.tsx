import type { CSSProperties, ReactNode } from 'react';
import type { ActionRiskClass, AgentRunStatus, ApprovalStatus, EnvironmentTier } from '@/lib/types';

type Tone = 'neutral' | 'positive' | 'caution' | 'critical' | 'accent';

const toneStyles: Record<Tone, CSSProperties> = {
  neutral: { color: 'var(--neutral)', background: 'var(--neutral-subtle)' },
  positive: { color: 'var(--positive)', background: 'var(--positive-subtle)' },
  caution: { color: 'var(--caution)', background: 'var(--caution-subtle)' },
  critical: { color: 'var(--critical)', background: 'var(--critical-subtle)' },
  accent: { color: 'var(--accent)', background: 'var(--accent-subtle)' },
};

/**
 * A status label.
 *
 * Tone is never the only signal — the text always states the status. Colour alone fails for the
 * roughly one in twelve men with a colour vision deficiency, and this is a governance product where
 * misreading "Rejected" as "Approved" has consequences.
 */
export function Pill({ tone, children }: { readonly tone: Tone; readonly children: ReactNode }) {
  return (
    <span
      style={{
        ...toneStyles[tone],
        display: 'inline-flex',
        alignItems: 'center',
        gap: 'var(--space-1)',
        padding: '0.125rem var(--space-2)',
        borderRadius: 'var(--radius-sm)',
        fontSize: '0.75rem',
        fontWeight: 600,
        letterSpacing: '0.02em',
        whiteSpace: 'nowrap',
      }}
    >
      {children}
    </span>
  );
}

export function RiskPill({ riskClass }: { readonly riskClass: ActionRiskClass }) {
  const tone: Tone =
    riskClass === 'Irreversible' || riskClass === 'Financial'
      ? 'critical'
      : riskClass === 'External'
        ? 'caution'
        : 'neutral';

  return <Pill tone={tone}>{riskClass}</Pill>;
}

export function ApprovalStatusPill({ status }: { readonly status: ApprovalStatus }) {
  const tone: Tone =
    status === 'Approved'
      ? 'positive'
      : status === 'Rejected' || status === 'Expired'
        ? 'critical'
        : status === 'Pending'
          ? 'caution'
          : 'neutral';

  return <Pill tone={tone}>{status}</Pill>;
}

export function RunStatusPill({ status }: { readonly status: AgentRunStatus }) {
  const tone: Tone =
    status === 'Succeeded'
      ? 'positive'
      : status === 'Failed' || status === 'TimedOut' || status === 'BudgetExceeded'
        ? 'critical'
        : status === 'AwaitingApproval'
          ? 'caution'
          : status === 'Running'
            ? 'accent'
            : 'neutral';

  return <Pill tone={tone}>{status}</Pill>;
}

/**
 * Marks the active environment.
 *
 * Production is rendered in the critical tone wherever it appears. Acting on production while
 * believing you are in staging is the mistake this product must make hard to commit, and a
 * persistent, unmissable marker is the cheapest defence against it.
 */
export function EnvironmentBadge({ environment }: { readonly environment: EnvironmentTier }) {
  const tone: Tone =
    environment === 'Production' ? 'critical' : environment === 'Staging' ? 'caution' : 'neutral';

  return (
    <Pill tone={tone}>
      <span aria-hidden="true">{environment === 'Production' ? '●' : '○'}</span>
      {environment}
    </Pill>
  );
}

export function Card({
  title,
  action,
  children,
}: {
  readonly title?: string;
  readonly action?: ReactNode;
  readonly children: ReactNode;
}) {
  return (
    <section
      style={{
        background: 'var(--surface-raised)',
        border: '1px solid var(--border-subtle)',
        borderRadius: 'var(--radius-lg)',
        boxShadow: 'var(--shadow-sm)',
        overflow: 'hidden',
      }}
    >
      {(title !== undefined || action !== undefined) && (
        <header
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 'var(--space-3)',
            padding: 'var(--space-4) var(--space-5)',
            borderBottom: '1px solid var(--border-subtle)',
          }}
        >
          {title !== undefined && <h2 style={{ fontSize: '0.9375rem' }}>{title}</h2>}
          {action}
        </header>
      )}
      <div style={{ padding: 'var(--space-5)' }}>{children}</div>
    </section>
  );
}

export function Button({
  variant = 'secondary',
  type = 'button',
  disabled = false,
  onClick,
  children,
}: {
  readonly variant?: 'primary' | 'secondary' | 'danger';
  readonly type?: 'button' | 'submit';
  readonly disabled?: boolean;
  readonly onClick?: () => void;
  readonly children: ReactNode;
}) {
  const palette: Record<string, CSSProperties> = {
    primary: { background: 'var(--accent)', color: 'var(--text-inverse)', borderColor: 'var(--accent)' },
    secondary: {
      background: 'var(--surface-raised)',
      color: 'var(--text-primary)',
      borderColor: 'var(--border-strong)',
    },
    danger: { background: 'var(--critical)', color: 'var(--text-inverse)', borderColor: 'var(--critical)' },
  };

  return (
    <button
      type={type}
      disabled={disabled}
      onClick={onClick}
      style={{
        ...palette[variant],
        padding: 'var(--space-2) var(--space-4)',
        borderRadius: 'var(--radius-md)',
        borderWidth: '1px',
        borderStyle: 'solid',
        fontSize: '0.875rem',
        fontWeight: 500,
        opacity: disabled ? 0.55 : 1,
        cursor: disabled ? 'not-allowed' : 'pointer',
      }}
    >
      {children}
    </button>
  );
}

/**
 * Explains an empty result rather than showing a blank region.
 *
 * "No approvals are waiting" and "we failed to load your approvals" look identical when both render
 * as nothing, and in a governance queue that ambiguity is dangerous.
 */
export function EmptyState({ title, description }: { readonly title: string; readonly description: string }) {
  return (
    <div style={{ padding: 'var(--space-8) var(--space-5)', textAlign: 'center' }}>
      <p style={{ margin: 0, fontWeight: 600 }}>{title}</p>
      <p style={{ margin: 'var(--space-2) 0 0', color: 'var(--text-secondary)' }}>{description}</p>
    </div>
  );
}

export function ErrorState({
  message,
  traceId,
}: {
  readonly message: string;
  // Explicitly `| undefined` so a caller can pass a possibly-absent trace id directly under
  // exactOptionalPropertyTypes, rather than having to conditionally spread the prop.
  readonly traceId?: string | undefined;
}) {
  return (
    <div
      role="alert"
      style={{
        padding: 'var(--space-4)',
        border: '1px solid var(--critical)',
        background: 'var(--critical-subtle)',
        borderRadius: 'var(--radius-md)',
        color: 'var(--text-primary)',
      }}
    >
      <p style={{ margin: 0, fontWeight: 600 }}>Something went wrong</p>
      <p style={{ margin: 'var(--space-2) 0 0' }}>{message}</p>
      {traceId !== undefined && (
        <p style={{ margin: 'var(--space-2) 0 0', fontFamily: 'var(--font-mono)', fontSize: '0.75rem' }}>
          Trace: {traceId}
        </p>
      )}
    </div>
  );
}

/**
 * A loading placeholder announced to assistive technology.
 *
 * A spinner that screen readers cannot perceive leaves those users with silence, unable to tell a
 * slow request from a broken one.
 */
export function LoadingState({ label }: { readonly label: string }) {
  return (
    <div role="status" aria-live="polite" style={{ padding: 'var(--space-6)', color: 'var(--text-secondary)' }}>
      {label}
    </div>
  );
}
