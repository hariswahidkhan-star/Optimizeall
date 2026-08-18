import type {
  ApprovalDecisionKind,
  ApprovalDetail,
  ApprovalSummary,
  EnvironmentTier,
  ExecutiveDashboard,
  PagedResult,
  ProblemDetails,
  RunDetail,
  RunSummary,
} from './types';

/**
 * A failed API call, carrying the server's problem document.
 *
 * Preserving `code` matters: the UI branches on stable error codes rather than on message text,
 * so wording can change without breaking behaviour.
 */
export class ApiError extends Error {
  public readonly status: number;
  public readonly code: string | undefined;
  public readonly traceId: string | undefined;
  public readonly fieldErrors: Readonly<Record<string, readonly string[]>> | undefined;

  public constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Request failed with status ${status}.`);
    this.name = 'ApiError';
    this.status = status;
    this.code = problem.code;
    this.traceId = problem.traceId;
    this.fieldErrors = problem.errors;
  }

  /** True when retrying could plausibly succeed. Authorisation and validation failures cannot. */
  public get isRetryable(): boolean {
    return this.status >= 500 || this.status === 408 || this.status === 429;
  }
}

let accessTokenProvider: () => Promise<string | null> = async () => null;

/**
 * Supplies the bearer token.
 *
 * Injected rather than read from storage here so that the token never has to live somewhere this
 * module can reach — the auth library keeps it in memory and hands it over per request.
 */
export function configureAuth(provider: () => Promise<string | null>): void {
  accessTokenProvider = provider;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = await accessTokenProvider();

  const headers = new Headers(init?.headers);
  headers.set('Accept', 'application/json');

  if (init?.body !== undefined) {
    headers.set('Content-Type', 'application/json');
  }

  if (token !== null) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(path, { ...init, headers });

  if (response.status === 204) {
    return undefined as T;
  }

  if (!response.ok) {
    let problem: ProblemDetails = {};

    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // A non-JSON error body (a gateway timeout page, for instance) still has to surface as a
      // typed failure rather than as a parse exception the caller cannot interpret.
      problem = { title: response.statusText, status: response.status };
    }

    throw new ApiError(response.status, problem);
  }

  return (await response.json()) as T;
}

function buildQuery(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams();

  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) {
      search.set(key, String(value));
    }
  }

  const query = search.toString();
  return query.length > 0 ? `?${query}` : '';
}

export interface ScopeParams {
  readonly workspaceId: string;
  readonly environment: EnvironmentTier;
}

export const api = {
  approvals: {
    listPending: (scope: ScopeParams, page = 1, pageSize = 25): Promise<PagedResult<ApprovalSummary>> =>
      request(`/api/v1/approvals${buildQuery({ ...scope, page, pageSize })}`),

    get: (approvalId: string): Promise<ApprovalDetail> =>
      request(`/api/v1/approvals/${approvalId}`),

    decide: (
      approvalId: string,
      decision: ApprovalDecisionKind,
      rationale: string | null,
    ): Promise<{ readonly approvalRequestId: string; readonly status: string }> =>
      request(`/api/v1/approvals/${approvalId}/decision`, {
        method: 'POST',
        body: JSON.stringify({ decision, rationale }),
      }),
  },

  runs: {
    list: (
      scope: ScopeParams,
      filters: { readonly agentKey?: string; readonly status?: string } = {},
      page = 1,
      pageSize = 25,
    ): Promise<PagedResult<RunSummary>> =>
      request(`/api/v1/runs${buildQuery({ ...scope, ...filters, page, pageSize })}`),

    get: (runId: string): Promise<RunDetail> => request(`/api/v1/runs/${runId}`),
  },

  workspaces: {
    dashboard: (scope: ScopeParams, from: string, to: string): Promise<ExecutiveDashboard> =>
      request(
        `/api/v1/workspaces/${scope.workspaceId}/dashboard${buildQuery({
          environment: scope.environment,
          from,
          to,
        })}`,
      ),

    engageKillSwitch: (workspaceId: string, environment: EnvironmentTier, reason: string): Promise<unknown> =>
      request(`/api/v1/workspaces/${workspaceId}/kill-switch`, {
        method: 'POST',
        body: JSON.stringify({ environment, reason }),
      }),
  },
};
