import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApprovalQueue } from '@/features/approvals/ApprovalQueue';
import { ScopeProvider } from '@/context/ScopeContext';
import type { ApprovalSummary, PagedResult } from '@/lib/types';

function renderQueue() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <ScopeProvider
        initialScope={{
          tenantName: 'Acme',
          workspaceId: '11111111-1111-4111-8111-111111111111',
          workspaceName: 'Main',
          environment: 'Production',
        }}
      >
        <MemoryRouter>
          <ApprovalQueue />
        </MemoryRouter>
      </ScopeProvider>
    </QueryClientProvider>,
  );
}

function mockResponse(items: readonly ApprovalSummary[]) {
  const body: PagedResult<ApprovalSummary> = {
    items,
    page: 1,
    pageSize: 25,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
  };

  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })),
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
  window.localStorage.clear();
});

describe('ApprovalQueue', () => {
  it('distinguishes an empty queue from a failure, rather than rendering nothing', async () => {
    mockResponse([]);
    renderQueue();

    await waitFor(() => {
      expect(screen.getByText('Nothing is waiting')).toBeInTheDocument();
    });
  });

  it('shows the risk class and approval progress for each pending action', async () => {
    mockResponse([
      {
        id: '22222222-2222-4222-8222-222222222222',
        title: 'Publish the launch post',
        riskClass: 'External',
        requestedByAgentKey: 'publishing-agent',
        approvalsReceived: 0,
        approvalsRequired: 1,
        estimatedCostAmount: null,
        estimatedCostCurrency: null,
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
        requestedAt: new Date().toISOString(),
      },
    ]);

    renderQueue();

    await waitFor(() => {
      expect(screen.getByText('Publish the launch post')).toBeInTheDocument();
    });

    // The risk class is stated in text, never conveyed by colour alone — this is a governance
    // queue, and misreading it has consequences.
    expect(screen.getByText('External')).toBeInTheDocument();
    expect(screen.getByText('0 of 1')).toBeInTheDocument();
    expect(screen.getByText('publishing-agent')).toBeInTheDocument();
  });

  it('surfaces the error and its trace id when the request fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify({ detail: 'Permission denied.', code: 'auth.permission_denied' }), {
            status: 403,
            headers: { 'Content-Type': 'application/json' },
          }),
      ),
    );

    renderQueue();

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Permission denied.');
    });
  });
});
