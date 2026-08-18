import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { AppShell } from '@/components/AppShell';
import { ScopeProvider } from '@/context/ScopeContext';
import { ApprovalQueue } from '@/features/approvals/ApprovalQueue';
import { ApprovalDetail } from '@/features/approvals/ApprovalDetail';
import { RunList } from '@/features/runs/RunList';
import { RunDetail } from '@/features/runs/RunDetail';
import { Dashboard } from '@/features/dashboard/Dashboard';
import { ApiError } from '@/lib/api';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Retrying a 403 or a 404 wastes the user's time and can look like a hung UI. Only failures
      // that could plausibly resolve on their own are retried.
      retry: (failureCount, error) =>
        error instanceof ApiError ? error.isRetryable && failureCount < 3 : failureCount < 3,
      refetchOnWindowFocus: true,
      staleTime: 30_000,
    },
    mutations: {
      // Never retried automatically. A mutation here approves spend or publishes content, and a
      // silent retry after an ambiguous failure could do it twice.
      retry: false,
    },
  },
});

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ScopeProvider
        initialScope={{
          tenantName: 'Acme',
          workspaceId: '00000000-0000-0000-0000-000000000000',
          workspaceName: 'Default workspace',
          environment: 'Development',
        }}
      >
        <BrowserRouter>
          <Routes>
            <Route element={<AppShell />}>
              <Route index element={<Dashboard />} />
              <Route path="approvals" element={<ApprovalQueue />} />
              <Route path="approvals/:approvalId" element={<ApprovalDetail />} />
              <Route path="runs" element={<RunList />} />
              <Route path="runs/:runId" element={<RunDetail />} />
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </ScopeProvider>
    </QueryClientProvider>
  );
}

function NotFound() {
  return (
    <div style={{ padding: 'var(--space-8)', textAlign: 'center' }}>
      <h1 style={{ fontSize: '1.25rem' }}>Page not found</h1>
      <p style={{ color: 'var(--text-secondary)' }}>The address you followed does not match a screen.</p>
    </div>
  );
}
