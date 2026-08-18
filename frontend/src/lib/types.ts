/**
 * Contract types mirroring the API's OpenAPI document.
 *
 * Hand-written rather than generated so that the frontend states exactly what it consumes; a
 * generated surface tends to import the whole schema and hide which fields are actually used. The
 * CI contract test asserts these stay in step with the published document.
 */

export type EnvironmentTier = 'Development' | 'Staging' | 'Production';

export type ActionRiskClass = 'Read' | 'Write' | 'External' | 'Financial' | 'Irreversible';

export type ApprovalStatus = 'Pending' | 'Approved' | 'Rejected' | 'Expired' | 'Cancelled';

export type ApprovalDecisionKind = 'Approve' | 'Reject';

export type AgentRunStatus =
  | 'Queued'
  | 'Running'
  | 'AwaitingApproval'
  | 'Succeeded'
  | 'Failed'
  | 'Cancelled'
  | 'TimedOut'
  | 'BudgetExceeded';

export type RunStepType =
  | 'Prompt'
  | 'Completion'
  | 'ToolCall'
  | 'ToolResult'
  | 'Retrieval'
  | 'Decision'
  | 'Error'
  | 'ApprovalGate';

export type ToolInvocationStatus =
  | 'Authorised'
  | 'Denied'
  | 'AwaitingApproval'
  | 'Executed'
  | 'Failed'
  | 'Skipped';

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasNextPage: boolean;
}

export interface ApprovalSummary {
  readonly id: string;
  readonly title: string;
  readonly riskClass: ActionRiskClass;
  readonly requestedByAgentKey: string;
  readonly approvalsReceived: number;
  readonly approvalsRequired: number;
  readonly estimatedCostAmount: number | null;
  readonly estimatedCostCurrency: string | null;
  readonly expiresAt: string;
  readonly requestedAt: string;
}

export interface ApprovalDecisionView {
  readonly approverUserId: string;
  readonly approverDisplayName: string;
  readonly decision: ApprovalDecisionKind;
  readonly rationale: string | null;
  readonly decidedAt: string;
}

export interface ApprovalDetail {
  readonly id: string;
  readonly title: string;
  readonly riskClass: ActionRiskClass;
  readonly status: ApprovalStatus;
  readonly payloadJson: string;
  readonly payloadFingerprint: string;
  readonly estimatedCostAmount: number | null;
  readonly estimatedCostCurrency: string | null;
  readonly agentRunId: string | null;
  readonly agentKey: string | null;
  readonly workflowRunId: string | null;
  readonly objectiveTitle: string | null;
  readonly approvalsReceived: number;
  readonly approvalsRequired: number;
  readonly expiresAt: string;
  readonly decisions: readonly ApprovalDecisionView[];
}

export interface RunSummary {
  readonly id: string;
  readonly agentKey: string;
  readonly definitionVersion: number;
  readonly status: AgentRunStatus;
  readonly triggerType: string;
  readonly totalTokens: number;
  readonly costAmount: number;
  readonly costCurrency: string;
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly isDryRun: boolean;
}

export interface RunStepView {
  readonly sequence: number;
  readonly stepType: RunStepType;
  readonly contentJson: string;
  readonly tokens: number;
  readonly latencyMilliseconds: number;
  readonly occurredAt: string;
}

export interface ToolInvocationView {
  readonly id: string;
  readonly toolKey: string;
  readonly riskClass: ActionRiskClass;
  readonly status: ToolInvocationStatus;
  readonly argumentsJson: string;
  readonly resultJson: string | null;
  readonly denialReason: string | null;
  readonly approvalRequestId: string | null;
  readonly durationMilliseconds: number | null;
  readonly requestedAt: string;
}

export interface RunDetail {
  readonly id: string;
  readonly agentKey: string;
  readonly definitionVersion: number;
  readonly status: AgentRunStatus;
  readonly inputJson: string;
  readonly outputJson: string | null;
  readonly errorJson: string | null;
  readonly provider: string | null;
  readonly model: string | null;
  readonly promptTokens: number;
  readonly completionTokens: number;
  readonly costAmount: number;
  readonly costCurrency: string;
  readonly iterationCount: number;
  readonly correlationId: string;
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly steps: readonly RunStepView[];
  readonly toolInvocations: readonly ToolInvocationView[];
}

export interface AgentPerformance {
  readonly agentKey: string;
  readonly displayName: string;
  readonly runCount: number;
  readonly successRate: number;
  readonly approvalRate: number;
  readonly averageCost: number;
  readonly averageDurationSeconds: number;
}

export interface SpendPoint {
  readonly date: string;
  readonly amount: number;
}

export interface ExecutiveDashboard {
  readonly runsCompleted: number;
  readonly runsFailed: number;
  readonly approvalsPending: number;
  readonly approvalsApproved: number;
  readonly approvalsRejected: number;
  readonly approvalMedianMinutes: number;
  readonly spendAmount: number;
  readonly spendCurrency: string;
  readonly budgetCapAmount: number;
  readonly agentPerformance: readonly AgentPerformance[];
  readonly spendTrend: readonly SpendPoint[];
}

/** RFC 9457 problem document, as returned by the API for every failure. */
export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly code?: string;
  readonly traceId?: string;
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}
