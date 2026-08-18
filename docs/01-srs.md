# Software Requirements Specification — OptimizeAll Enterprise AI Operating System

- **Document ID:** OA-SRS-001
- **Version:** 1.0
- **Status:** Baselined
- **Owners:** Principal Architect, Product Manager, Security Architect
- **Audience:** Engineering, QA, Security, Compliance, Operations, Executive stakeholders

---

## 1. Introduction

### 1.1 Purpose

This SRS defines the complete functional and non-functional requirements for **OptimizeAll**, a
multi-tenant enterprise AI operating system. OptimizeAll is not a chatbot. It is a governed
execution platform in which a workforce of specialised AI agents performs real business work —
marketing, SEO, content, sales, finance, HR, engineering, compliance — under a central
orchestrator, with human approval gates on every sensitive action and an immutable audit trail
covering everything the system does.

### 1.2 Scope

OptimizeAll provides:

1. A **multi-tenant SaaS control plane** with organisation, workspace and environment isolation.
2. An **agent runtime** that executes agent definitions against a modular LLM provider layer
   (OpenAI, Anthropic, Google Gemini — interchangeable at runtime).
3. A **central orchestrator** that decomposes objectives into tasks, assigns them to agents,
   enforces policy, and drives workflows to completion.
4. A **human-in-the-loop approval system** gating publishing, outreach, spend, data export,
   and any action classified as sensitive.
5. A **scheduling engine and background worker fleet** for recurring and deferred work.
6. **RBAC, audit logging, notifications, KPI dashboards** and executive reporting.
7. **Knowledge and memory** subsystems so agents improve from prior runs.

Out of scope for v1.0: on-premises deployment, fine-tuning of foundation models, a public
marketplace for third-party agents, and mobile native applications.

### 1.3 Definitions

| Term | Definition |
|---|---|
| **Tenant** | A customer organisation. Top-level isolation boundary. |
| **Workspace** | A subdivision of a tenant (e.g. a brand, business unit, or region). |
| **Environment** | `Development`, `Staging`, or `Production` scope within a workspace. Data, credentials, and agent behaviour are isolated per environment. |
| **Agent Definition** | Versioned, declarative specification of an agent: mission, model policy, tool grants, memory policy, approval rules. |
| **Agent Run** | A single bounded execution of an agent against a task. |
| **Task** | A unit of work with inputs, outputs, an owning agent, and a lifecycle. |
| **Workflow** | A directed graph of tasks with dependencies and gates. |
| **Approval Gate** | A blocking checkpoint requiring a human decision before an action executes. |
| **Tool** | A capability an agent may invoke (HTTP call, DB query, publish, send email, etc.). |
| **Action Risk Class** | `Read`, `Write`, `External`, `Financial`, `Irreversible`. Determines the approval policy. |

### 1.4 References

- `docs/02-architecture.md` — system architecture and C4 views
- `docs/03-data-model.md` — logical and physical data model
- `docs/04-agent-catalog.md` — the agent workforce specification
- `docs/06-security.md` — threat model and security controls
- `docs/08-deployment-azure.md` — Azure deployment topology

### 1.5 Source of truth note

The requirements below were derived from the stakeholder brief. The brief referenced an
accompanying workbook of worksheets (schedules, KPIs, agent responsibilities). **That workbook
was not present in the repository at the time of writing.** Every requirement that would have
been sourced from it is marked `[ASSUMED]` and carries a documented default. When the workbook is
supplied, those defaults are to be reconciled via `docs/adr/` amendments rather than silent edits.

---

## 2. Overall Description

### 2.1 Product perspective

OptimizeAll is a greenfield SaaS platform composed of:

- **React 19 + TypeScript** single-page application (premium enterprise UI).
- **.NET 9** backend organised by Domain-Driven Design and Clean Architecture.
- **PostgreSQL 16** as the system of record, with row-level tenant scoping.
- **Redis 7** for caching, distributed locks, rate limiting, and the run queue.
- **Modular AI provider layer** — no provider-specific type escapes the infrastructure boundary.
- **Background worker fleet** for agent execution, scheduling, and outbox delivery.
- **Docker** images for every service; Azure Container Apps as the primary runtime target.

### 2.2 User classes

| Class | Description | Representative permissions |
|---|---|---|
| **Platform Operator** | Runs the SaaS itself. Cross-tenant. | Tenant provisioning, global config, incident tooling |
| **Tenant Owner** | Accountable executive for one tenant. | Everything within tenant, billing, approvals |
| **Administrator** | Configures the workspace. | RBAC, agent config, integrations, policies |
| **Approver** | Named human sign-off authority. | Approve/reject gated actions within their scope |
| **Operator** | Day-to-day user; launches and supervises work. | Create objectives, run workflows, view outputs |
| **Analyst** | Consumes reporting. | Read dashboards, export reports |
| **Auditor** | Independent oversight. | Read-only across audit log, approvals, agent runs |
| **Agent (non-human principal)** | A service identity representing an agent instance. | Only its granted tools, only within its environment |

### 2.3 Operating assumptions

- `[ASSUMED]` Default tenant scale target: 5,000 tenants, 50,000 named users, 2M agent runs/month.
- `[ASSUMED]` Default data residency: single region per tenant, selectable at provisioning.
- LLM provider credentials are tenant-scoped and may be BYO-key or platform-pooled.
- All outbound actions with third-party effect are reversible-by-design where technically
  possible; where not, they are classified `Irreversible` and always require approval.

---

## 3. Functional Requirements

Requirement IDs are stable. `MUST` = mandatory for v1.0 GA; `SHOULD` = target for v1.0, may slip
to v1.1 with written waiver; `MAY` = optional.

### 3.1 Tenancy and isolation (FR-TEN)

| ID | Requirement | Priority |
|---|---|---|
| FR-TEN-001 | The system MUST support multiple tenants sharing one deployment, with all tenant-owned rows carrying a non-null `tenant_id`. | MUST |
| FR-TEN-002 | The system MUST enforce tenant isolation in the database via PostgreSQL Row-Level Security, not solely in application code. | MUST |
| FR-TEN-003 | Every tenant MUST support one or more workspaces, and every workspace MUST support the three environments `Development`, `Staging`, `Production`. | MUST |
| FR-TEN-004 | Secrets, integration credentials, and provider keys MUST be scoped to a single (tenant, workspace, environment) triple and MUST NOT be readable across that boundary. | MUST |
| FR-TEN-005 | A cross-tenant query issued by application code without an explicit, audited platform-operator escalation MUST fail closed. | MUST |
| FR-TEN-006 | Tenant deletion MUST perform a soft delete with a configurable retention window, then a verifiable hard purge. | MUST |
| FR-TEN-007 | The system SHOULD support per-tenant region pinning for data residency. | SHOULD |

### 3.2 Identity, authentication, authorisation (FR-IAM)

| ID | Requirement | Priority |
|---|---|---|
| FR-IAM-001 | The system MUST authenticate humans via OpenID Connect against Microsoft Entra ID, with local credentials available only for development. | MUST |
| FR-IAM-002 | The system MUST issue short-lived access tokens (≤15 min) and rotating refresh tokens bound to a session. | MUST |
| FR-IAM-003 | The system MUST implement role-based access control with roles composed of fine-grained permissions of the form `resource:action`. | MUST |
| FR-IAM-004 | Role assignments MUST be scoped to a (tenant, workspace, environment) triple; a Production grant MUST NOT imply a Development grant or vice versa. | MUST |
| FR-IAM-005 | The system MUST ship immutable built-in roles and MUST allow tenant-defined custom roles composed only of permissions the assigning principal already holds. | MUST |
| FR-IAM-006 | Agents MUST authenticate as first-class non-human principals with their own permission sets; an agent MUST NOT inherit the permissions of the user who triggered it. | MUST |
| FR-IAM-007 | Privileged operations (role change, policy change, key rotation, approval-policy edit) MUST require step-up authentication. | MUST |
| FR-IAM-008 | The system MUST support SCIM 2.0 user and group provisioning. | SHOULD |
| FR-IAM-009 | API access for machine clients MUST use OAuth 2.0 client credentials with per-client scopes and independently revocable credentials. | MUST |

### 3.3 Agent framework (FR-AGT)

| ID | Requirement | Priority |
|---|---|---|
| FR-AGT-001 | An agent MUST be defined declaratively with: identity, mission, inputs, outputs, model policy, tool grants, memory policy, schedule, approval rules, and KPIs. | MUST |
| FR-AGT-002 | Agent definitions MUST be versioned and immutable once published; changes create a new version. | MUST |
| FR-AGT-003 | An agent run MUST record: definition version, model and provider actually used, all tool invocations with arguments and results, token counts, cost, latency, and terminal status. | MUST |
| FR-AGT-004 | An agent MUST only invoke tools explicitly granted to its definition; an ungranted invocation MUST fail and raise a security event. | MUST |
| FR-AGT-005 | Agent runs MUST be bounded by configurable limits on wall-clock time, token spend, monetary cost, tool-call count, and iteration depth. Breaching a limit MUST terminate the run deterministically. | MUST |
| FR-AGT-006 | The system MUST support three memory tiers: short-term (run-scoped), episodic (prior runs of the same agent), and semantic (tenant knowledge base, vector-indexed). | MUST |
| FR-AGT-007 | Agents MUST be able to read prior run outcomes and reviewer feedback so that behaviour improves over time. | MUST |
| FR-AGT-008 | Every agent run MUST be replayable: given the recorded inputs and definition version, an operator MUST be able to re-execute it in a sandbox. | SHOULD |
| FR-AGT-009 | Agent-to-agent delegation MUST be mediated by the orchestrator; direct agent-to-agent invocation MUST NOT be possible. | MUST |
| FR-AGT-010 | The system MUST support a dry-run mode where all external-effect tools are stubbed and results are recorded but not applied. | MUST |

### 3.4 AI provider abstraction (FR-AIP)

| ID | Requirement | Priority |
|---|---|---|
| FR-AIP-001 | The system MUST expose a single internal chat-completion abstraction; no provider SDK type may cross the Infrastructure boundary. | MUST |
| FR-AIP-002 | The system MUST support OpenAI, Anthropic, and Google Gemini as interchangeable providers selectable per (tenant, agent, environment). | MUST |
| FR-AIP-003 | Adding a new provider MUST require implementing one interface and registering it — no changes to Domain or Application layers. | MUST |
| FR-AIP-004 | The system MUST support automatic failover to a configured secondary provider on 5xx, timeout, or rate-limit responses, and MUST record that a failover occurred. | MUST |
| FR-AIP-005 | The system MUST meter tokens and cost per run, per agent, per workspace, and per tenant, and enforce configurable budget caps. | MUST |
| FR-AIP-006 | Provider credentials MUST be stored in a managed secret store, never in the application database, and MUST be rotatable without redeployment. | MUST |
| FR-AIP-007 | Prompts and completions MUST be persisted for audit, with configurable redaction of detected PII before storage. | MUST |
| FR-AIP-008 | The system MUST implement per-provider circuit breakers, retries with jittered exponential backoff, and request-level idempotency keys. | MUST |

### 3.5 Orchestration (FR-ORC)

| ID | Requirement | Priority |
|---|---|---|
| FR-ORC-001 | The orchestrator MUST accept a business objective and decompose it into a directed acyclic graph of tasks. | MUST |
| FR-ORC-002 | The orchestrator MUST assign each task to the agent whose capability profile matches, respecting the agent's environment and permissions. | MUST |
| FR-ORC-003 | The orchestrator MUST enforce task dependencies, and MUST NOT start a task whose predecessors have not reached a terminal successful state. | MUST |
| FR-ORC-004 | The orchestrator MUST insert approval gates automatically wherever a task's action risk class requires one by policy. | MUST |
| FR-ORC-005 | Workflow state MUST be durable; an orchestrator process restart MUST NOT lose or duplicate work. | MUST |
| FR-ORC-006 | The orchestrator MUST detect stalled tasks via heartbeat timeout and apply a configured retry or escalation policy. | MUST |
| FR-ORC-007 | Every orchestrator decision (assignment, gate insertion, retry, escalation, abandonment) MUST be logged with its rationale. | MUST |
| FR-ORC-008 | The orchestrator MUST support workflow cancellation with compensating actions for completed external-effect steps where defined. | SHOULD |
| FR-ORC-009 | Concurrency per workspace MUST be bounded and configurable to protect downstream systems and budgets. | MUST |

### 3.6 Approvals and human-in-the-loop (FR-APR)

| ID | Requirement | Priority |
|---|---|---|
| FR-APR-001 | Any action classified `External`, `Financial`, or `Irreversible` MUST require explicit human approval before execution. | MUST |
| FR-APR-002 | Publishing content, sending outreach, and committing spend MUST always be gated, with no tenant-level override to disable the gate entirely. | MUST |
| FR-APR-003 | Approval policies MUST support threshold rules (e.g. spend above a limit requires a second approver) and role-based routing. | MUST |
| FR-APR-004 | An approval request MUST present the exact payload to be executed, the originating agent and run, the diff versus any prior state, and the estimated cost or reach. | MUST |
| FR-APR-005 | The requester MUST NOT be able to approve their own request; agent-originated requests MUST be approved by a human, never another agent. | MUST |
| FR-APR-006 | Approval decisions MUST be recorded with approver identity, timestamp, decision, and mandatory rationale on rejection. | MUST |
| FR-APR-007 | Approvals MUST expire after a configurable window and fail closed (treated as rejected) on expiry. | MUST |
| FR-APR-008 | An approved payload MUST be cryptographically bound to what executes; if the payload changes after approval, execution MUST be refused. | MUST |
| FR-APR-009 | The system MUST support emergency stop ("kill switch") halting all agent execution for a tenant, workspace, or environment within 30 seconds. | MUST |

### 3.7 Scheduling and background execution (FR-SCH)

| ID | Requirement | Priority |
|---|---|---|
| FR-SCH-001 | The system MUST support cron-expression schedules with IANA timezone awareness, including correct DST handling. | MUST |
| FR-SCH-002 | Schedule firing MUST be exactly-once per logical occurrence across a horizontally scaled worker fleet. | MUST |
| FR-SCH-003 | A missed occurrence (worker outage) MUST be handled by a configurable policy: skip, run-once-on-recovery, or backfill-all. | MUST |
| FR-SCH-004 | Background jobs MUST be idempotent and safely retryable, with a dead-letter queue after exhausting retries. | MUST |
| FR-SCH-005 | Outbound integration calls MUST use the transactional outbox pattern so that a database commit and an external side effect cannot diverge. | MUST |
| FR-SCH-006 | Long-running work MUST report heartbeats and be observable mid-flight. | MUST |

### 3.8 Audit and compliance (FR-AUD)

| ID | Requirement | Priority |
|---|---|---|
| FR-AUD-001 | Every state-changing operation MUST produce an audit event capturing actor, action, resource, tenant, environment, correlation ID, before/after state, and outcome. | MUST |
| FR-AUD-002 | The audit log MUST be append-only. No API path may update or delete an audit event. | MUST |
| FR-AUD-003 | Audit events MUST be hash-chained per tenant so that tampering is detectable, with a scheduled verification job. | MUST |
| FR-AUD-004 | Audit records MUST be retained for a configurable period with a minimum of 7 years for `Financial` class events. | MUST |
| FR-AUD-005 | Auditors MUST be able to reconstruct the full lineage of any published artefact: objective → workflow → task → agent run → prompts → approval → publication. | MUST |
| FR-AUD-006 | The system MUST support GDPR data subject access and erasure requests, including erasure from agent memory and vector indexes. | MUST |
| FR-AUD-007 | PII MUST be detected and redacted in logs and telemetry by default. | MUST |

### 3.9 Knowledge and memory (FR-KNW)

| ID | Requirement | Priority |
|---|---|---|
| FR-KNW-001 | The system MUST provide a tenant knowledge base supporting document ingestion, chunking, embedding, and semantic retrieval. | MUST |
| FR-KNW-002 | Retrieval MUST be tenant- and environment-scoped; a query MUST NOT be able to return another tenant's chunks. | MUST |
| FR-KNW-003 | Every retrieved chunk used in a completion MUST be cited in the run record. | MUST |
| FR-KNW-004 | The system MUST capture reviewer feedback on agent outputs as structured learning signals linked to the agent definition. | MUST |
| FR-KNW-005 | The system SHOULD surface a per-agent quality trend derived from approval/rejection rates and reviewer scores. | SHOULD |

### 3.10 Notifications (FR-NTF)

| ID | Requirement | Priority |
|---|---|---|
| FR-NTF-001 | The system MUST notify approvers of pending approvals through in-app, email, and webhook channels. | MUST |
| FR-NTF-002 | Notification delivery MUST be retried with backoff and recorded; permanent failures MUST be visible to administrators. | MUST |
| FR-NTF-003 | Users MUST be able to configure per-category notification preferences and digest frequency. | SHOULD |
| FR-NTF-004 | Approaching budget, quota, and SLA breaches MUST raise proactive alerts. | MUST |

### 3.11 Analytics, KPI and reporting (FR-RPT)

| ID | Requirement | Priority |
|---|---|---|
| FR-RPT-001 | The system MUST provide an executive dashboard covering work delivered, approval throughput, agent quality, spend, and SLA attainment. | MUST |
| FR-RPT-002 | Every agent MUST declare its KPIs, and the platform MUST compute and store them on a defined cadence. | MUST |
| FR-RPT-003 | Reports MUST be exportable to CSV and PDF, with export itself being an audited, permission-gated action. | MUST |
| FR-RPT-004 | Dashboard queries MUST read from pre-aggregated snapshots, not from live transactional scans. | MUST |
| FR-RPT-005 | Cost attribution MUST be available per agent, per workflow, and per business objective. | MUST |

### 3.12 Frontend (FR-UI)

| ID | Requirement | Priority |
|---|---|---|
| FR-UI-001 | The application MUST meet WCAG 2.2 Level AA. | MUST |
| FR-UI-002 | All destructive or irreversible UI actions MUST require typed confirmation naming the target. | MUST |
| FR-UI-003 | The UI MUST display the active tenant, workspace, and environment persistently, with Production visually distinguished. | MUST |
| FR-UI-004 | The approval queue MUST let an approver review the full payload, diff, cost, and lineage without leaving the screen. | MUST |
| FR-UI-005 | Agent run detail MUST render a step-by-step timeline including tool calls, retrievals, and token/cost accounting. | MUST |
| FR-UI-006 | The UI MUST support light and dark themes and MUST persist the user's choice. | SHOULD |
| FR-UI-007 | All data tables MUST support server-side pagination, filtering, and sorting; no unbounded client-side fetches. | MUST |

---

## 4. Non-Functional Requirements

### 4.1 Performance (NFR-PRF)

| ID | Requirement |
|---|---|
| NFR-PRF-001 | API read endpoints: p95 ≤ 300 ms, p99 ≤ 800 ms at 500 RPS per region. |
| NFR-PRF-002 | API write endpoints: p95 ≤ 600 ms excluding downstream LLM latency. |
| NFR-PRF-003 | Dashboard first contentful paint ≤ 1.5 s on a 10 Mbps connection; time to interactive ≤ 3.0 s. |
| NFR-PRF-004 | Agent run queue pickup latency p95 ≤ 5 s under nominal load. |
| NFR-PRF-005 | The platform MUST sustain 2,000 concurrent agent runs per region without queue growth. |

### 4.2 Availability and resilience (NFR-AVL)

| ID | Requirement |
|---|---|
| NFR-AVL-001 | Control-plane availability target 99.9% monthly, measured by synthetic probes. |
| NFR-AVL-002 | RPO ≤ 5 minutes; RTO ≤ 60 minutes. |
| NFR-AVL-003 | No single point of failure in the request path; all stateless services run ≥ 2 replicas across availability zones. |
| NFR-AVL-004 | Degradation MUST be graceful: if all LLM providers are unavailable, the platform stays up, queues work, and reports provider status. |
| NFR-AVL-005 | Backups MUST be tested by an automated monthly restore drill; an untested backup is treated as no backup. |

### 4.3 Security (NFR-SEC)

| ID | Requirement |
|---|---|
| NFR-SEC-001 | TLS 1.2+ in transit; AES-256 at rest for all stores. |
| NFR-SEC-002 | Secrets MUST live in Azure Key Vault, accessed via workload identity. No secret in source, image, or environment file. |
| NFR-SEC-003 | All input MUST be validated at the API boundary; all output MUST be encoded contextually. |
| NFR-SEC-004 | Prompt injection defences MUST be applied to all untrusted content entering a prompt, and tool authorisation MUST be enforced server-side regardless of model output. |
| NFR-SEC-005 | Dependency, container, secret, SAST and IaC scanning MUST run on every pull request and block on High/Critical findings. |
| NFR-SEC-006 | Least privilege MUST be applied to every identity, human and machine. |
| NFR-SEC-007 | Security events MUST stream to a SIEM within 5 minutes. |

### 4.4 Maintainability (NFR-MNT)

| ID | Requirement |
|---|---|
| NFR-MNT-001 | Layer dependencies MUST be enforced by automated architecture tests, not convention. |
| NFR-MNT-002 | Line coverage ≥ 80% overall; ≥ 95% for Domain and approval/authorisation logic. |
| NFR-MNT-003 | Every public API MUST be described by a generated OpenAPI 3.1 document validated in CI. |
| NFR-MNT-004 | Database schema changes MUST ship as reversible, versioned migrations. |
| NFR-MNT-005 | Cyclomatic complexity per method ≤ 15 unless waived with a recorded justification. |

### 4.5 Observability (NFR-OBS)

| ID | Requirement |
|---|---|
| NFR-OBS-001 | Structured JSON logs with correlation ID, tenant ID, and environment on every entry. |
| NFR-OBS-002 | OpenTelemetry traces spanning API → orchestrator → worker → provider call. |
| NFR-OBS-003 | RED metrics for every endpoint and USE metrics for every resource pool. |
| NFR-OBS-004 | Alerting on SLO burn rate, not on raw thresholds alone. |
| NFR-OBS-005 | Every alert MUST link to a runbook entry. |

---

## 5. Data Requirements

See `docs/03-data-model.md` for the full model. Summary constraints:

- Every tenant-owned table carries `tenant_id uuid not null` and is protected by RLS.
- All primary keys are UUIDv7 for time-ordered locality without exposing sequence counts.
- All timestamps are `timestamptz` stored in UTC.
- Monetary values use `numeric(19,4)` with an explicit ISO-4217 currency column. Never floats.
- Soft delete via `deleted_at`; all read paths filter it by default.
- Optimistic concurrency via a `xmin`-backed or explicit `version` column on aggregates.

---

## 6. External Interfaces

| Interface | Protocol | Notes |
|---|---|---|
| Web UI → API | HTTPS / REST + JSON | OAuth 2.0 bearer, OpenAPI 3.1 described |
| API → PostgreSQL | TCP/TLS | Connection pooled, RLS enforced |
| API/Worker → Redis | TCP/TLS | Cache, locks, rate limits, queues |
| Worker → LLM providers | HTTPS | Through the provider abstraction only |
| Platform → Entra ID | OIDC | Authentication and SCIM provisioning |
| Platform → Azure Key Vault | HTTPS | Workload identity, no static credentials |
| Platform → customer webhooks | HTTPS | Signed payloads, replay-protected |

---

## 7. Acceptance Criteria (GA gate)

v1.0 GA is blocked until all of the following hold:

1. All `MUST` requirements are implemented and covered by automated tests.
2. Tenant isolation is proven by an automated cross-tenant access test suite that must fail closed.
3. Approval bypass is proven impossible by a dedicated adversarial test suite.
4. Audit chain verification passes over a synthetic 1M-event dataset.
5. Load test sustains NFR-PRF targets for 60 minutes with no error-budget burn.
6. Restore drill meets RPO/RTO from a cold backup.
7. Independent security review returns no open High or Critical findings.
8. Runbooks exist for every alert, and an on-call rotation is staffed.

---

## 8. Traceability

Each requirement ID above is referenced in code by test names and in `docs/07-testing-strategy.md`
by the verification method (Test / Analysis / Inspection / Demonstration). CI publishes a
traceability report mapping requirement → test → result on every main-branch build.
