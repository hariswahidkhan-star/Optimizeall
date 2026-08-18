import { NavLink, Outlet } from 'react-router-dom';
import { useScope } from '@/context/ScopeContext';
import { EnvironmentBadge } from '@/components/primitives';
import type { EnvironmentTier } from '@/lib/types';

const environments: readonly EnvironmentTier[] = ['Development', 'Staging', 'Production'];

const navigation = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/approvals', label: 'Approvals' },
  { to: '/runs', label: 'Agent runs' },
] as const;

export function AppShell() {
  const { scope, setEnvironment } = useScope();
  const isProduction = scope.environment === 'Production';

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <a className="skip-link" href="#main">
        Skip to main content
      </a>

      {/*
        A full-width band in the critical tone whenever the active environment is Production.
        Redundant with the badge by design: the cost of one extra visual cue is nothing next to the
        cost of an operator publishing to a live audience while believing they are in staging.
      */}
      {isProduction && (
        <div
          role="note"
          style={{
            background: 'var(--critical)',
            color: 'var(--text-inverse)',
            padding: 'var(--space-1) var(--space-5)',
            fontSize: '0.8125rem',
            fontWeight: 600,
            textAlign: 'center',
          }}
        >
          You are acting in Production. Actions here affect real customers, spend and reputation.
        </div>
      )}

      <header
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 'var(--space-5)',
          padding: 'var(--space-3) var(--space-5)',
          borderBottom: '1px solid var(--border-subtle)',
          background: 'var(--surface-raised)',
        }}
      >
        <span style={{ fontWeight: 700, letterSpacing: '-0.02em' }}>OptimizeAll</span>

        <nav aria-label="Primary" style={{ display: 'flex', gap: 'var(--space-1)' }}>
          {navigation.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={'end' in item ? item.end : false}
              style={({ isActive }) => ({
                padding: 'var(--space-2) var(--space-3)',
                borderRadius: 'var(--radius-md)',
                textDecoration: 'none',
                fontSize: '0.875rem',
                fontWeight: isActive ? 600 : 500,
                color: isActive ? 'var(--accent)' : 'var(--text-secondary)',
                background: isActive ? 'var(--accent-subtle)' : 'transparent',
              })}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div
          style={{
            marginLeft: 'auto',
            display: 'flex',
            alignItems: 'center',
            gap: 'var(--space-3)',
          }}
        >
          <span style={{ color: 'var(--text-secondary)', fontSize: '0.8125rem' }}>
            {scope.tenantName} / {scope.workspaceName}
          </span>

          <label style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
            <span className="visually-hidden">Active environment</span>
            <select
              value={scope.environment}
              onChange={(event) => setEnvironment(event.target.value as EnvironmentTier)}
              style={{
                padding: 'var(--space-1) var(--space-2)',
                borderRadius: 'var(--radius-md)',
                border: `1px solid ${isProduction ? 'var(--critical)' : 'var(--border-strong)'}`,
                background: 'var(--surface-raised)',
                color: 'var(--text-primary)',
                font: 'inherit',
                fontSize: '0.8125rem',
              }}
            >
              {environments.map((environment) => (
                <option key={environment} value={environment}>
                  {environment}
                </option>
              ))}
            </select>
          </label>

          <EnvironmentBadge environment={scope.environment} />
        </div>
      </header>

      <main id="main" style={{ flex: 1, padding: 'var(--space-6) var(--space-5)', maxWidth: '84rem', width: '100%', margin: '0 auto' }}>
        <Outlet />
      </main>
    </div>
  );
}
