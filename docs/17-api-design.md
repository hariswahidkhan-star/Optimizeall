# 17 — API Architecture & Endpoint Contracts

## 17.1 Architecture

| Concern | Decision |
|---|---|
| Style | REST over HTTPS, JSON, resource-oriented. Not GraphQL: the client is one first-party UI with known screens, and GraphQL would trade a solved problem (typed endpoints per screen) for an unsolved one (per-field authorisation across seven layers) |
| Base | `/api/v1` — major version in the path |
| Evolution | Additive within a major version: new optional fields and endpoints only. Breaking changes create `v2` with a published deprecation window. Clients must tolerate unknown fields |
| Contract | OpenAPI 3.1, generated from the server types and checked into the repository; the TypeScript client is generated from it, so a contract drift breaks the build rather than production |
| Media type | `application/json`; errors as `application/problem+json` (RFC 9457) |
| Time | All timestamps RFC 3339 UTC. The organisation's time zone is returned separately and applied in the UI — never inferred from the payload |
| Pagination | Cursor-based: `?limit=50&cursor=<opaque>`. Offsets are not offered; they drift under concurrent writes and get slow at depth |
| Filtering | Explicit named parameters per endpoint. No query language — an expressive filter grammar becomes an unauthorised query surface |
| Expansion | `?expand=` against an allow-list per endpoint |
| Concurrency | `ETag` on every resource (from `row_version`); mutations require `If-Match`. A missing `If-Match` on a write is a `428 Precondition Required` |
| Idempotency | `Idempotency-Key` required on every `POST` that creates a record or causes an external effect (§17.4) |
| Rate limits | Per principal and per organisation; `RateLimit-Limit`, `RateLimit-Remaining`, `RateLimit-Reset` headers |
| Correlation | `X-Correlation-Id` accepted and echoed; generated when absent; flows into audit, workflow, agent and model records |

## 17.2 Error model

```json
{
  "type": "https://pciai.org/problems/policy-violation",
  "title": "Policy denied this action",
  "status": 422,
  "detail": "Outreach text contains award language prohibited by Golden Rule 1.",
  "instance": "/api/v1/outreach-items/018f…/submit",
  "correlation_id": "9f2c…",
  "rule": "policy.no_award_language",
  "errors": [
    { "field": "composed_text", "code": "banned_phrase", "message": "'you have been selected'" }
  ]
}
```

| Status | Used for | Never used for |
|---|---|---|
| `400` | Malformed syntax | Business-rule failure |
| `401` | No or invalid credential | Insufficient permission |
| `403` | Authenticated but not permitted — states which permission is missing | Hiding existence of a record |
| `404` | Not found **or** not visible to this organisation — deliberately indistinguishable | — |
| `409` | State conflict (e.g. approving an already-decided item) | Optimistic-concurrency failure |
| `412` | `If-Match` mismatch — someone else changed the row | — |
| `422` | **Business rule or policy denial**, with `rule` naming the exact rule | Validation of syntax |
| `423` | Locked by emergency stop | — |
| `428` | Mutation attempted without `If-Match` | — |
| `429` | Rate limited, with `Retry-After` | — |
| `503` | Integration or provider unavailable, with `Retry-After` when known | Permanent failure |

The distinction between `400` and `422` carries weight here: a client can retry a `400` after fixing the payload, but a `422` means the platform's rules rejected the *intent*, and the correct response is usually to change the plan, not the JSON.

## 17.3 Authentication at the API edge

| Caller | Mechanism |
|---|---|
| Human via browser | OIDC authorisation-code with PKCE against the identity provider; short-lived access token in an `HttpOnly`, `Secure`, `SameSite=Lax` cookie; refresh rotation with reuse detection |
| `worker` → `api` | mTLS plus a service token bound to the workload identity |
| `agent-runtime` → `api` | mTLS; the token carries the **agent principal** and the owning `task_id`, and is scoped to that task |
| Inbound webhooks | Signature verification per connector; no session |

The agent token is the mechanism behind scope binding: because it is issued per task and carries the task id, the gate can check that every referenced entity lies inside that task's scope graph without trusting anything the model said.

## 17.4 Idempotency

Required on every `POST` that creates a record or causes an external effect. The server stores `(org_id, idempotency_key, endpoint, request_hash) → response` for 24 hours.

- Same key, same request body → the stored response is replayed with `Idempotency-Replayed: true`.
- Same key, **different** body → `422` with `type: idempotency-key-reuse`. Silently accepting it is how duplicate posts happen.
- Endpoints that cause external effects additionally reserve an `integration.external_action` row before the connector runs; the API-level key protects against duplicate *requests*, the ledger protects against duplicate *effects*, and they are not the same problem.

`PUT` and `DELETE` are idempotent by definition. `PATCH` requires `If-Match`, which makes replay safe.

---

## 17.5 Endpoint catalogue

Every endpoint specifies method, route, purpose, authentication, permission, request, response, validation, errors and idempotency. Detailed contracts follow for the endpoints where the platform's guarantees actually live; the remainder are tabulated.

### Approvals — the spine

#### `GET /api/v1/approvals`
| | |
|---|---|
| **Purpose** | The approval queue, grouped and ordered by cost of delay |
| **Auth** | Session (human) |
| **Permission** | `approval.read` |
| **Request** | `?state=pending_human&type=&risk=&agent=&campaign=&brand=&expiring_within=2h&requires_me=true&limit=&cursor=` |
| **Response** | `200` `{ items: ApprovalSummary[], groups: {expiring, high_risk, standard}, next_cursor, throughput: {median_decision_seconds, decided_today} }` |
| **Validation** | `expiring_within` ISO 8601 duration; `limit` ≤ 100 |
| **Errors** | `401`, `403` |
| **Idempotency** | n/a |

#### `GET /api/v1/approvals/{id}`
Returns the full item anatomy — the twelve mandatory elements from Phase 3, including `target`, `preview`, `evidence[]`, `compliance_checks[]`, `why{rules_applied, kpis_consulted, knowledge_used[], assumptions, missing_information}`, `four_eyes{generating_actor, reviewing_actor}`, and `expires_at`. Permission `approval.read`; `404` when outside the organisation.

#### `POST /api/v1/approvals/{id}/decision`
| | |
|---|---|
| **Purpose** | Approve, reject, or request revision |
| **Auth** | Session (human only) |
| **Permission** | `approval.grant` for approve; `approval.reject` for reject and revision |
| **Request** | `{ "decision": "approve" \| "reject" \| "request_revision", "feedback_code": "unsupported_claim", "note": "…" }` · headers `If-Match`, `Idempotency-Key` |
| **Response** | `200 ApprovalItem` with new `state`, `decided_by`, `decided_at` |
| **Validation** | `feedback_code` **required** for reject and revision, drawn from the fixed set (wrong tone, incorrect information, poor prospect fit, duplicate idea, too generic, unsupported claim, wrong strategic priority). Approval requires `state = pending_human` |
| **Errors** | `403` if the principal is an agent, or is the generating or reviewing actor — `type: four-eyes-violation`; `409` already decided; `412` stale; `422` expired — `type: approval-expired`; `423` emergency stop engaged |
| **Idempotency** | Required. Replay returns the original decision rather than re-deciding |

This one endpoint carries `APR-018`, `SEC-036`, `APR-014`, `APR-016` and the kill switch. It is the highest-value contract in the system and the one most worth reviewing carefully.

#### `POST /api/v1/approvals/{id}/edit`
Body `{ "edited_preview": {…} }`. Stores both the agent's version and the human's, returns a diff, and moves the item to `edited`. Approving an edited item records both. Permission `approval.edit`.

### Outreach — where the platform stops

#### `POST /api/v1/outreach-items`
| | |
|---|---|
| **Purpose** | Compose an outreach item from an approved template |
| **Auth** | Session or agent token (`OUT` principal) |
| **Permission** | `outreach.compose` |
| **Request** | `{ lead_id, template_code, channel, personal_line, personal_line_evidence_id }` |
| **Response** | `201 OutreachItem` including `char_count`, `char_limit`, `state` |
| **Validation** | Template must be `approved`; `char_count ≤ char_limit`; `personal_line` non-empty and its evidence must belong to this lead; lead must not be suppressed; frequency cap not exceeded |
| **Errors** | `422` with `rule` = `policy.template_not_approved` · `policy.char_limit` · `policy.personal_line_required` · `policy.suppressed_contact` · `policy.frequency_cap` |
| **Idempotency** | Required — key derived from `(lead_id, template_code, content_hash)` |

#### `POST /api/v1/outreach-items/{id}/submit`
Submits for approval. Runs the full compliance set and attaches results; `422` with the failing rule if any check fails. Permission `outreach.submit`.

#### `POST /api/v1/send-queue/{work_item_id}/confirm`
| | |
|---|---|
| **Purpose** | An operator confirms a message was actually sent |
| **Auth** | Session (human only) |
| **Permission** | `outreach.confirm_send` |
| **Request** | `{ "sent_text": "…", "sent_at": "2026-08-18T10:14:00Z" }` · `Idempotency-Key` |
| **Response** | `200 { outreach_item, divergence: true, divergence_effects: ["char_limit_breach"] }` |
| **Validation** | Work item must be `queued` and its approval still valid; `sent_at` not in the future; `sent_text` re-checked against character limit and banned phrases |
| **Errors** | `403` if the confirming principal is an agent — no agent may confirm a human send; `409` already confirmed; `422` approval expired before send |
| **Idempotency** | Required |

Two behaviours are specified here rather than left to the client: the confirmation records **the text actually sent**, and any divergence from the approved text is re-checked and, if it breaks a rule, recorded as a compliance breach — exactly as the workbook's own scoring does.

#### `POST /api/v1/send-queue/{work_item_id}/report-warning`
Records a platform warning, **pauses that channel for the organisation for the rest of the day**, and notifies the manager. Permission `outreach.report_warning`. No body beyond an optional note. This is the workbook's safety rule as an endpoint.

### Content

| Method | Route | Purpose | Permission | Idempotency |
|---|---|---|---|---|
| `GET` | `/content-items` | Calendar, board and list views | `content.read` | n/a |
| `POST` | `/content-items` | Create from a brief | `content.create` | required |
| `GET` | `/content-items/{id}` | Full record with versions, claims, QA, metrics | `content.read` | n/a |
| `POST` | `/content-items/{id}/versions` | New draft version | `content.draft` | required |
| `POST` | `/content-items/{id}/qa` | Run brand and editorial QA | `content.qa` | required |
| `POST` | `/content-items/{id}/compliance` | Claim classification and verification | `content.qa` | required |
| `POST` | `/content-items/{id}/schedule` | Move to scheduled | `content.schedule` | required |
| `POST` | `/content-items/{id}/publish` | Execute publication | `content.publish` | **required** |
| `POST` | `/content-items/{id}/repurpose` | Create a derivative on another channel | `content.create` | required |
| `GET` | `/article-briefs` | The 5,683-row brief bank, filtered and cursor-paged | `content.read` | n/a |
| `GET` | `/content-schedules` | Schedules with computed coverage | `content.read` | n/a |

#### `POST /api/v1/content-items/{id}/publish` — detailed
| | |
|---|---|
| **Auth** | Session or agent token (`PUB`) |
| **Permission** | `content.publish` **and** an approved `approval_id` on the item |
| **Request** | `{ "channel_id": "…", "scheduled_at": null }` · `Idempotency-Key` required |
| **Response** | `202 { external_action_id, state: "reserved" }` — publication is asynchronous; the client polls or subscribes |
| **Validation** | Item is `scheduled` or `pending_approval` with an approval granted; **canonical rule satisfied for the target channel**; original indexed if this is a syndication; derivative cap not exceeded; connector healthy |
| **Errors** | `422 rule: policy.canonical_unsupported` — a copy of a rankable page cannot go to a channel with no canonical support; `422 rule: policy.original_not_indexed`; `422 rule: policy.derivative_cap`; `423` emergency stop; `503` connector unavailable |
| **Idempotency** | Required at the API, **and** an `external_action` reservation keyed on `(org, publish, content_id, content_hash)` before the connector runs. A timeout after a successful write resolves through reconciliation, never a second publish |

### Leads

| Method | Route | Purpose | Permission | Notes |
|---|---|---|---|---|
| `GET` | `/leads` | Pipeline and table views | `lead.read` | Filters: score, band, stage, geography, industry, size, role, seniority, status, campaign, brand, owner, created, has_evidence, duplicate |
| `POST` | `/leads` | Create a qualified lead | `lead.create` | Idempotent on contact identity; `409 type: duplicate-lead` returns the existing record and its outcome rather than creating |
| `GET` | `/leads/{id}` | Full record with evidence and the score arithmetic | `lead.read` | `score`, `band`, `funnel_stage_code` are read-only — attempting to write them is `422 rule: derived_field` |
| `PATCH` | `/leads/{id}/qualification` | Change `icp_fit` / `intent` | `lead.qualify` | Requires at least one evidence item per changed rating; recomputes score by generation |
| `POST` | `/leads/{id}/decline` | Mark declined | `lead.decline` | **Writes a permanent suppression entry.** Confirmation required; `Idempotency-Key` required |
| `POST` | `/leads/{id}/handoff` | Hand to a closer | `lead.handoff` | — |
| `POST` | `/leads/{id}/erasure` | Subject erasure request | `data.erase` (Admin) | Tombstones personal data, preserves the audit chain by hashing payloads |

### Agents, workflows, scheduler

| Method | Route | Purpose | Permission |
|---|---|---|---|
| `GET` | `/agents` | Workforce grid with live status and scorecard | `agent.read` |
| `GET` | `/agents/{code}/versions` | Version history with lifecycle | `agent.read` |
| `POST` | `/agents/{code}/versions` | Create a draft version | `agent.edit` |
| `POST` | `/agent-versions/{id}/evaluate` | Run the evaluation set | `agent.evaluate` |
| `POST` | `/agent-versions/{id}/promote` | Promote a lifecycle stage | `agent.promote` — production requires Owner and a non-regressing evaluation; `422 rule: evaluation_regression` names the criterion and the delta |
| `POST` | `/agents/{code}/pause` · `/resume` | Operational control | `agent.control` |
| `POST` | `/agents/{code}/run` | Run manually with a sample input | `agent.run` |
| `GET` | `/workflows` | Catalogue with enabled state and success rate | `workflow.read` |
| `POST` | `/workflow-versions/{id}/validate` · `/dry-run` · `/promote` | The safe-change path | `workflow.edit` / `workflow.promote` |
| `POST` | `/workflows/{key}/run` | Manual run | `workflow.run` |
| `GET` | `/schedules` | Jobs with **both `intended_at` and `actual_at`** | `schedule.read` |
| `POST` | `/schedules/{id}/pause` · `/resume` · `/run-now` | Control | `schedule.control` |

### Analytics, reports, costs

| Method | Route | Purpose | Permission |
|---|---|---|---|
| `GET` | `/kpis` | Catalogue with value, target, thresholds, **freshness per source** | `analytics.read` |
| `GET` | `/kpis/{code}/observations` | Time series for a period | `analytics.read` |
| `GET` | `/kpis/{code}/lineage?period=` | **Source → observation → transformation → formula version → value**, with links to the contributing rows | `analytics.read` |
| `GET` | `/objective-performance` | Value rank against share of minutes | `analytics.read` |
| `GET` | `/data-health` | The ten checks with current counts | `analytics.read` |
| `GET` | `/reports?type=daily_brief` | Archive | `report.read` |
| `POST` | `/reports/{type}/generate` | Regenerate | `report.generate` — response is watermarked when data-health is non-zero |
| `GET` | `/costs?group_by=agent\|workflow\|campaign\|model\|day` | Spend | `cost.read` (Owner/Admin) |
| `PUT` | `/budgets/{scope}` | Set a budget | `budget.set` — increases require approval |

### Knowledge and integrations

| Method | Route | Purpose | Permission |
|---|---|---|---|
| `POST` | `/knowledge/documents` | Upload — treated as untrusted input | `knowledge.write` |
| `GET` | `/knowledge/search?q=&mode=semantic\|fulltext\|exact` | Retrieval **with citations** | `knowledge.read` |
| `POST` | `/knowledge/imports` | Upload a workbook, returns detected sheets | `import.propose` |
| `GET` | `/knowledge/imports/{id}/diff` | Change groups against current configuration | `import.propose` |
| `POST` | `/knowledge/imports/{id}/decision` | Approve or reject **per change group** | `import.approve` (Owner) — partial imports are never applied |
| `GET` | `/integrations` | Health, scopes, headroom, verification expiry | `integration.read` |
| `POST` | `/integrations/{key}/test` | Liveness and auth probe | `integration.manage` |
| `POST` | `/integrations/{key}/reauthorise` | OAuth re-consent | `integration.manage` |

Secrets never appear in any response on any of these routes.

### System control

| Method | Route | Purpose | Permission |
|---|---|---|---|
| `POST` | `/system/emergency-stop` | Engage — scope `all_external_writes` · `channel` · `workflow` · `agent` | `system.emergency_stop` — any Admin or Compliance |
| `DELETE` | `/system/emergency-stop` | Release | `system.emergency_stop_release` — **Owner only** |
| `GET` | `/audit/events` | Filterable audit stream | `audit.read` |
| `GET` | `/audit/correlations/{id}` | One business workflow across services, agents and model calls | `audit.read` |
| `POST` | `/audit/verify-chain` | Hash-chain integrity check | `audit.verify` |
| `GET` | `/errors` | Failures and dead letters | `error.read` |
| `POST` | `/errors/{id}/retry` | Retry — **absent** in the response's `_actions` when the retry taxonomy forbids it | `error.retry` |

### The internal tool gate

Not part of the public surface. `agent-runtime` calls a single internal endpoint:

#### `POST /internal/v1/tool-requests`
| | |
|---|---|
| **Auth** | mTLS plus an agent token scoped to one `task_id` |
| **Request** | `{ tool_key, arguments, task_id, agent_version_id, correlation_id }` |
| **Response** | `200 { result }` · `202 { parked: "awaiting_approval", approval_id }` · `403 { denied_by: "tool_grant" \| "scope_binding" \| "policy" }` |
| **Validation** | The ten gate stages in order: schema, tool grant, scope binding, policy, approval, kill switch, rate limit, idempotency, execution, audit |
| **Errors** | Every denial is terminal and non-retryable, and is written to `audit.audit_event` with the matched rule before the response is returned |
| **Idempotency** | Derived server-side; the agent does not supply a key, because a compromised or confused agent must not be able to choose one |

That last line is deliberate. Letting the caller pick the idempotency key for an external effect would hand a prompt-injected agent the ability to collide or evade the ledger.
