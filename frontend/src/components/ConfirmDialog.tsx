import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { Button } from '@/components/primitives';

/**
 * A modal confirmation for consequential actions.
 *
 * When `confirmPhrase` is supplied the operator must type the target's name before the action is
 * enabled. That deliberately breaks the muscle memory of clicking through a dialog, and it is
 * reserved for actions the platform cannot undo — releasing the emergency stop, approving an
 * irreversible action.
 */
export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel,
  confirmPhrase,
  variant = 'primary',
  onConfirm,
  onCancel,
  children,
}: {
  readonly open: boolean;
  readonly title: string;
  readonly description: string;
  readonly confirmLabel: string;
  readonly confirmPhrase?: string;
  readonly variant?: 'primary' | 'danger';
  readonly onConfirm: () => void;
  readonly onCancel: () => void;
  readonly children?: ReactNode;
}) {
  const [typed, setTyped] = useState('');
  const dialogRef = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    if (!open) {
      setTyped('');
      return;
    }

    // Focus moves into the dialog on open so a keyboard user is not left behind it, and Escape
    // always cancels — both are baseline expectations of a modal, and both are routinely missed.
    dialogRef.current?.focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onCancel();
      }
    };

    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [open, onCancel]);

  if (!open) {
    return null;
  }

  const confirmEnabled = confirmPhrase === undefined || typed.trim() === confirmPhrase;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'var(--surface-overlay)',
        display: 'grid',
        placeItems: 'center',
        padding: 'var(--space-4)',
        zIndex: 50,
      }}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
        tabIndex={-1}
        style={{
          background: 'var(--surface-raised)',
          border: '1px solid var(--border-subtle)',
          borderRadius: 'var(--radius-lg)',
          boxShadow: 'var(--shadow-lg)',
          maxWidth: '32rem',
          width: '100%',
          padding: 'var(--space-5)',
        }}
      >
        <h2 id={titleId} style={{ fontSize: '1rem' }}>
          {title}
        </h2>

        <p id={descriptionId} style={{ color: 'var(--text-secondary)', marginTop: 'var(--space-2)' }}>
          {description}
        </p>

        {children}

        {confirmPhrase !== undefined && (
          <label style={{ display: 'block', marginTop: 'var(--space-4)' }}>
            <span style={{ fontSize: '0.8125rem', color: 'var(--text-secondary)' }}>
              Type <strong style={{ fontFamily: 'var(--font-mono)' }}>{confirmPhrase}</strong> to confirm
            </span>
            <input
              value={typed}
              onChange={(event) => setTyped(event.target.value)}
              autoComplete="off"
              style={{
                marginTop: 'var(--space-2)',
                width: '100%',
                padding: 'var(--space-2)',
                borderRadius: 'var(--radius-md)',
                border: '1px solid var(--border-strong)',
                background: 'var(--surface-canvas)',
                color: 'var(--text-primary)',
                font: 'inherit',
                fontFamily: 'var(--font-mono)',
              }}
            />
          </label>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--space-2)', marginTop: 'var(--space-5)' }}>
          <Button onClick={onCancel}>Cancel</Button>
          <Button variant={variant} disabled={!confirmEnabled} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
