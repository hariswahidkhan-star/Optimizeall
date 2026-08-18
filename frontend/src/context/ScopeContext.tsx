import { createContext, use, useCallback, useMemo, useState, type ReactNode } from 'react';
import type { EnvironmentTier } from '@/lib/types';

export interface Scope {
  readonly tenantName: string;
  readonly workspaceId: string;
  readonly workspaceName: string;
  readonly environment: EnvironmentTier;
}

interface ScopeContextValue {
  readonly scope: Scope;
  readonly setEnvironment: (environment: EnvironmentTier) => void;
  readonly setWorkspace: (workspaceId: string, workspaceName: string) => void;
}

const ScopeContext = createContext<ScopeContextValue | null>(null);

const STORAGE_KEY = 'optimizeall.scope';

function readStoredScope(): Scope | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return raw === null ? null : (JSON.parse(raw) as Scope);
  } catch {
    return null;
  }
}

export function ScopeProvider({
  children,
  initialScope,
}: {
  readonly children: ReactNode;
  readonly initialScope: Scope;
}) {
  // A stored scope is restored on load so an operator does not re-pick their workspace every
  // session. The environment is deliberately not restored beyond what was stored, and the UI marks
  // Production distinctly wherever it appears.
  const [scope, setScope] = useState<Scope>(() => readStoredScope() ?? initialScope);

  const persist = useCallback((next: Scope) => {
    setScope(next);

    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      // Storage can be unavailable in a locked-down browser profile. Losing the preference is
      // acceptable; failing the interaction is not.
    }
  }, []);

  const value = useMemo<ScopeContextValue>(
    () => ({
      scope,
      setEnvironment: (environment) => persist({ ...scope, environment }),
      setWorkspace: (workspaceId, workspaceName) => persist({ ...scope, workspaceId, workspaceName }),
    }),
    [scope, persist],
  );

  return <ScopeContext value={value}>{children}</ScopeContext>;
}

export function useScope(): ScopeContextValue {
  const value = use(ScopeContext);

  if (value === null) {
    throw new Error('useScope must be used inside a ScopeProvider.');
  }

  return value;
}
