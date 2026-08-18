# 08 — System & Deployment Architecture

**Phase 2 · Status: awaiting approval.** No implementation, schema migrations or deployment artefacts are produced in this phase.

## 8.0 Assumptions this architecture is built on

Phase 1 named five blocking prerequisites. None has been answered, so each is carried here as an **explicit assumption**, with the design impact of a different answer stated. Nothing below has to be re-derived when the answers arrive.

| Prereq | Assumption taken | What changes if the answer differs |
|---|---|---|
| **P2-01** Team language competence | **A-01:** Platform core and workers in **.NET 9**; agent runtime in **Python 3.12 / FastAPI** | **Nothing structural.** Module boundaries, contracts, data model, security zones and all six figures are language-neutral. Variant B (Python end to end) swaps EF Core → SQLAlchemy + Alembic and the .NET Temporal SDK → the Python SDK. The decision is a stack substitution inside fixed boundaries — see §8.6 |
| **P2-02** Approval of the 14 decisions | **A-02:** All Phase 1 recommendations taken as approved | Each decision is isolated behind a named seam (§8.7). D-03 (Temporal) is the only one whose reversal touches code shape, and it is confined to the `IWorkflowHost` port |
| **P2-03** Website / CMS platform | **A-03:** A CMS with a REST API and canonical-URL control; **WordPress REST** as the reference implementation | The `ICmsPublisher` port is unchanged. Only the adapter and the canonical/indexation probe change. If the CMS has **no** write API, the content lane degrades to the human-assisted connector already built for outreach — the architecture already supports it, the MVP claim weakens |
| **P2-04** Named owner | **A-04:** A single `Owner` principal exists; approvals route to `Owner`, with `Manager` as delegate | Configuration only. No architectural impact |
| **P2-05** Cloud region / data residency | **A-05:** Single region, **EU/UK**, all personal data resident in-region; no cross-region replication | Multi-region residency (e.g. a KSA data boundary) would require per-org storage routing and a residency attribute on the org — a **data-architecture change**, flagged in §10.9 as the one deferred design |

Two further assumptions are inherited from Phase 1 and matter here: the PCI platform remains the revenue ledger (`DAT-020`), and no LinkedIn write automation exists at any layer (`OOS-01`).

---

## 8.1 Architectural philosophy, restated as a rule

> Deterministic software owns identity, permissions, schedules, state, budgets, approvals, integrations and audit. AI owns reasoning, research, synthesis, drafting, classification, prioritisation and bounded recommendation.

This is enforced by one structural decision, and everything else follows from it:

**The agent runtime has no path to any external system and never holds a credential.** An agent emits a *structured tool request*; the platform core validates, authorises, policy-checks, approval-checks, rate-limits, deduplicates, executes, and audits. The model proposes; the platform disposes.

If that rule is ever softened, the compliance regime in Phase 1 becomes unenforceable — because a model that can call an external API directly can bypass every control in this document.

## 8.2 Containers

Four deployable services plus managed infrastructure.

| Container | Responsibility | Language (A-01) | Egress permitted |
|---|---|---|---|
| **web** | Next.js App Router UI. Server components render from the API; no direct database access | TypeScript | `api` only |
| **api** — *Platform Core* | Modular monolith: 13 modules (§8.3). Owns all business state, the policy engine, the approval engine, the execution gate, the audit log | .NET 9 | `postgres`, `redis`, `object store`, `vault`, `temporal`, `agent-runtime` |
| **worker** | Temporal workflow and activity workers. Hosts **connector execution** — the only process that talks to third-party APIs | .NET 9 | `postgres`, `redis`, `temporal`, **allow-listed external hosts** |
| **agent-runtime** | Agent execution loop, model gateway, retrieval, evaluation harness | Python 3.12 / FastAPI | **Model providers only**, plus `api` (internal) |
| `postgres` + pgvector | Primary store, vector index, outbox, audit chain | — | — |
| `redis` | Cache, rate-limit token buckets, distributed locks, short-lived idempotency guards | — | — |
| `temporal` | Durable workflow engine: timers, human-wait, retries, versioning | — | — |
| object store | Documents, exports, report PDFs, evaluation artefacts | — | — |
| vault (cloud KMS) | Connector credentials, provider keys | — | — |

**Why `worker` — not `agent-runtime` — executes connectors.** It puts the network boundary in the right place. The agent runtime processes untrusted text by definition (researched web pages, uploaded documents, pasted replies). Giving that process outbound access to PCI's CRM, CMS and mail systems would make prompt injection an execution path rather than a content problem. With connector execution in `worker`, an injected instruction can at most produce a *tool request*, which then meets the gate.

**Why not one process.** The AI half has a different dependency profile, failure mode, scaling curve and blast radius. Splitting once along that seam buys isolation without distributed-systems tax. That is D-01, and §8.7 records where the next seam would be.

## 8.3 Service boundaries — the 13 modules of the platform core

Rules that make this a modular monolith rather than a large namespace:

1. A module owns its own PostgreSQL schema. **No module reads another module's tables** — ever, including in reports.
2. Synchronous cross-module calls go through a published application-service interface, not a repository.
3. Asynchronous cross-module communication is a **domain event via the transactional outbox**. Consumers are idempotent.
4. A module's public surface is: its application-service interface, the events it publishes, and the tools it exposes to the gate. Everything else is internal.
5. Any module may be extracted into its own service by replacing (2) with a network call and (3) with a broker — no other change.

| # | Module | Owns | Publishes | Exposes as tools |
|---|---|---|---|---|
| 1 | `identity` | Organisations, users, roles, permissions, **agent principals**, service accounts, sessions | `PrincipalCreated`, `GrantChanged` | — |
| 2 | `config` | Settings, targets, brands, objectives + value ranks, enumerations, dedup rules, feature flags, **business calendar**, kill switch | `ConfigChanged`, `CalendarChanged`, `KillSwitchToggled` | `get_config`, `get_calendar` |
| 3 | `worksurface` | Goals, OKRs, campaigns + briefs, tasks, queues, priority rules | `TaskCreated`, `TaskAssigned`, `TaskCompleted`, `TaskFailed`, `DailyPlanningStarted` | `get_campaign_brief`, `create_task`, `update_task` |
| 4 | `policy` | Policy engine, the 15 compliance checks, attestations, **suppression list**, consent records, frequency caps | `PolicyViolationRaised`, `SuppressionAdded`, `AttestationSigned` | `check_suppression`, `evaluate_policy` |
| 5 | `approval` | Approval items, decisions, expiry timers, four-eyes state, structured feedback, **emergency stop** | `ApprovalRequested`, `ApprovalGranted`, `ApprovalRejected`, `ApprovalExpired` | `create_approval_request` |
| 6 | `content` | Content items + versions, article briefs, schedules, channel policies, similarity index | `DraftReady`, `QARejected`, `ContentApproved`, `ContentPublished` | `get_brief`, `create_content_draft`, `check_similarity` |
| 7 | `pipeline` | Accounts, contacts, leads, interactions, outreach items, partnerships, community actions, link prospects | `LeadQualified`, `OutreachPrepared`, `PartnershipQualified`, `InteractionRecorded` | `search_leads`, `create_lead`, `create_outreach_item` |
| 8 | `search` | SEO keywords, clusters, opportunities, answer-engine audits, verification expiry | `SeoOpportunityFound`, `VerificationExpired` | `get_keywords`, `get_cluster_health` |
| 9 | `insight` | KPI definitions + thresholds, observations, **lineage**, reports, experiments | `KpiThresholdBreached`, `ReportPublished`, `ExperimentConcluded` | `get_kpi_data`, `get_report` |
| 10 | `ledger` | Budgets, cost lines, model-execution rollups, ROI computations | `BudgetThresholdReached` | `get_budget_state` |
| 11 | `knowledge` | Documents, versions, chunk metadata + ACLs, workbook imports and diffs | `DocumentVersioned`, `ImportProposed` | `search_company_knowledge` |
| 12 | `integration` | Connector registry, credential references, health, rate limits, webhooks, **external action ledger**, human send queue | `IntegrationFailed`, `ExternalActionSucceeded`, `HumanActionConfirmed` | *(all write tools, via the gate)* |
| 13 | `audit` | Audit event chain, error events, notifications | `AgentFailed`, `SecurityAlertRaised` | — |

**Deliberately not modules:** "reporting" (a read model over `insight`), "dashboard" (UI over read models), "LinkedIn" (a connector, not a domain).

## 8.4 The execution gate

Every external effect in the system — publishing a post, sending an approved email, writing to the CRM, queueing an outreach item for a human — passes through one pipeline in `integration`. There is no second path.

```
ToolRequest
  → 1. Schema validation        reject → regenerate (bounded) → escalate
  → 2. Tool grant check         deny   → audit + security alert
  → 3. Scope binding check      deny   → audit + security alert
  → 4. Policy engine            deny   → audit + policy violation
  → 5. Approval check           pending→ park until ApprovalGranted
  → 6. Kill switch              stop   → cancel, notify
  → 7. Rate limit / health      wait   → queue with backoff
  → 8. Idempotency reservation  dup    → return prior result
  → 9. Connector execution              (credentials resolved here, never before)
  → 10. Audit + event + result
```

Two of these are worth naming because they are unusual and they are what makes prompt injection survivable:

**Scope binding (step 3).** A tool request may only reference entities inside the *task's scope graph* — the campaign, lead, content item or route the task was created against. An injected instruction that says "email everyone in the CRM" fails here, before policy, because the target entities are not in scope. This converts a whole class of injection from an authorisation question into a structural impossibility.

**Idempotency reservation (step 8).** Reserved *before* the connector runs, keyed on `hash(org, action_type, entity_id, content_hash)`, with the reservation row carrying the state machine in §10.5. This is what makes a publish timeout safe.

## 8.5 Runtime topology of one day

The daily loop is a Temporal workflow, not a cron chain — a distinction that matters because step order carries a gate:

1. `07:00` **Data health sweep.** If any of the ten checks is non-zero, planning still runs but the executive report is blocked until acknowledged (`KPI-030`), and the orchestrator receives the failure list as planning input.
2. `07:15` Analytics collection — read-only connectors, freshness stamped.
3. `07:30` Market intelligence — research tools only.
4. `07:45` **Daily planning.** Orchestrator reads goals, KPI state, objective value ranks, platform attention flags, approval backlog, budget state and capacity; emits a `DailyPlan` and a task set.
5. `08:00` Daily brief published.
6. `09:00`–`17:00` Lane workflows execute against the plan, each dispatching to queues by class (§12.4).
7. `18:00` End-of-day report.

Capacity in step 4 counts **human and agent actors** (`FR-007`). A plan that assumes ten agents and one human, when the human is the only one who can send outreach, would be a plan that cannot execute — so human-executed steps are budgeted against human capacity explicitly.

## 8.6 Stack variant B — Python end to end

If P2-01 returns "Python only", the following substitutions apply and **nothing else in Phase 2 changes**:

| Concern | Variant A (assumed) | Variant B |
|---|---|---|
| Platform core | .NET 9 minimal API, EF Core | FastAPI, SQLAlchemy 2.x |
| Migrations | EF Core migrations | Alembic |
| Workflow SDK | Temporal .NET SDK | Temporal Python SDK |
| Validation | FluentValidation + source-generated DTOs | Pydantic v2 |
| Background hosting | Generic Host workers | Temporal worker processes |
| Contract sharing | OpenAPI + generated TS client | identical |

Module boundaries, event names, the execution gate, the data model, the security zones and all six figures are unchanged. **This is deliberate** — the language question should not have been able to block architecture, and now it cannot.

## 8.7 Named seams

Where the architecture is designed to be changed later without redesign:

| Seam | Port | What it isolates |
|---|---|---|
| `IWorkflowHost` | start / signal / query / timer | D-03. Swapping Temporal for a job runner touches this port and the worker host only |
| `IModelProvider` | complete / stream / embed | D-09. Providers are configuration keyed on task class |
| `ICmsPublisher` | publish / update / probe-index / set-canonical | A-03. The unknown CMS |
| `ICrmConnector` | upsert / query / link-external-id | D-13. The unchosen CRM |
| `ISendChannel` | queue / confirm / measure | Unifies API-sent and human-sent channels (§12.3) |
| `IKnowledgeIndex` | index / retrieve / invalidate | D-05. pgvector today, a dedicated store later |
| Module → service | application-service interface + events | Extraction of any module, `insight` first |

## 8.8 Deployment architecture

**Environments.** `local` (Docker Compose, one command), `test` (ephemeral, CI-provisioned), `staging` (production-shaped, sandbox connectors), `production`.

**Environment isolation is asserted, not assumed.** At startup every service asserts that the bound connector implementations match the environment class; a production connector type in a non-production environment fails the health check and the service refuses to serve. This is what makes `SEC-050` — "test agents cannot contact real audiences" — a mechanism rather than a hope.

**Release.**

| Step | Mechanism |
|---|---|
| Migration | Runs as a job before rollout; forward-compatible only (expand → migrate → contract across two releases) |
| `api` / `web` | Rolling with health gates |
| `worker` | Drain first: stop polling, let in-flight activities finish, then replace. Temporal replays anything interrupted |
| `agent-runtime` | Rolling; in-flight agent activities are retried by Temporal, not lost |
| Agent versions | Separate lifecycle from code: Draft → offline evaluation → Staging → approval → Production (`AI-010`, `A13`) |
| Rollback | Code rolls back by image; agent behaviour rolls back by version pointer, without a deployment |

**Backup and recovery**, mapping to `NFR-022` (RPO ≤ 15 min, RTO ≤ 4 h):

| Asset | Mechanism | Recovery |
|---|---|---|
| PostgreSQL | Continuous archiving, PITR | Restore to timestamp; ≤ 15 min data loss |
| Object storage | Versioning + lifecycle | Object-level restore |
| Temporal history | Its own persistence, backed up with the cluster | Workflows resume from last recorded event |
| Configuration, prompts, agent versions | Versioned rows, exported nightly to object storage | Point-in-time config restore without a code deploy |
| Secrets | Vault-native backup | Re-bind references |

Recovery is exercised quarterly against `staging` (`NFR-023`); an untested backup is not a backup.

**Observability.** OpenTelemetry throughout: one `correlation_id` per business workflow flows request → workflow → activity → agent execution → model call → tool request → connector call → audit event, so `AUD-005` is satisfiable by query rather than by inference. Three signal planes per `OBS-001…003`, alerting per `OBS-005`.
