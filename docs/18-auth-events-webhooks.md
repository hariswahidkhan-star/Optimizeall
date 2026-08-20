# 18 — Authentication, Authorisation, Events & Webhooks

## 18.1 Authentication model

| Principal | Credential | Lifetime | Notes |
|---|---|---|---|
| **Human** | OIDC authorisation-code + PKCE against the organisation's identity provider | Access 15 min, refresh 8 h with rotation and reuse detection | MFA and conditional access inherited from the provider, which also gives joiner/leaver control |
| **Agent** | Internal token minted per agent execution, bound to `(agent_version_id, task_id)` | The activity's timeout, maximum 15 min | Never leaves the internal network; carries no external credential |
| **Service** | mTLS workload identity + service token | Rotated by the platform | One per connector |
| **Webhook sender** | Per-connector signature (HMAC or provider scheme) with timestamp and replay window | n/a | No session is created |

Session records live in `identity.session` with issue, expiry, revocation, IP and user agent, so revocation is immediate and enumerable — a requirement for the joiner/leaver control the workbook already cares about.

**The agent token is the mechanism behind scope binding.** Because it is minted per task and carries the task id, the gate can verify that every entity a tool request references lies inside that task's scope graph — without trusting anything the model asserted. An agent that has been prompt-injected into requesting an action against an unrelated entity fails at that check, before policy, because the token cannot address it.

## 18.2 Authorisation model — seven layers, concretely

| # | Layer | Where enforced | Failure |
|---|---|---|---|
| 1 | Organisation scope | PostgreSQL RLS, `app.org_id` from claims | `404` — indistinguishable from not-found |
| 2 | Role permission | `identity.role_permission` checked in the application service | `403` naming the missing permission |
| 3 | Field-level | Write-authorisation attributes on the DTO; derived columns are generated and cannot be written at all | `422 rule: derived_field` |
| 4 | Tool grant | `registry.agent_tool_grant` — absence of a row is absence of the capability | `403 denied_by: tool_grant` |
| 5 | Scope binding | Task scope graph vs the request's referenced entities | `403 denied_by: scope_binding` |
| 6 | Policy | `policy.policy` evaluated deterministically, versioned and testable | `422` with `rule` |
| 7 | Approval | `approval.approval_item` state | `202 parked` or `403` |

### The permission set

Permissions are verbs on modules, not screens — screens compose them.

```
content.read  content.create  content.draft  content.qa  content.schedule  content.publish
lead.read     lead.create     lead.qualify   lead.decline lead.handoff
outreach.compose  outreach.submit  outreach.confirm_send  outreach.report_warning
approval.read approval.grant  approval.reject approval.edit
agent.read    agent.edit      agent.evaluate agent.promote agent.control agent.run
workflow.read workflow.edit   workflow.promote workflow.run
schedule.read schedule.control
analytics.read report.read    report.generate cost.read budget.set
knowledge.read knowledge.write import.propose import.approve
integration.read integration.manage
audit.read    audit.verify    error.read     error.retry
system.emergency_stop  system.emergency_stop_release
admin.users   admin.policies  admin.templates  data.erase
hr.read                                            -- Employee Score, restricted
```

### Role matrix

| Permission group | Owner | Admin | Manager | Operator | Closer | Analyst | Compliance |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| Read operational data | ● | ● | ● | own | own | ● | ● |
| Content create / draft / QA | ● | ● | ● | ● | — | — | — |
| Content publish | ● | ● | ● | — | — | — | — |
| Lead qualify / decline | ● | ● | ● | ● | — | — | — |
| Outreach compose / submit | ● | ● | ● | ● | — | — | — |
| Outreach confirm send | ● | — | ● | ● | — | — | — |
| **Approval grant** | ● | — | ● | — | — | — | — |
| Approval reject | ● | — | ● | — | — | — | ● |
| Agent / workflow edit | ● | ● | — | — | — | — | — |
| Agent promote to production | ● | — | — | — | — | — | — |
| Budgets and policies | ● | ● | — | — | — | — | — |
| Emergency stop engage | ● | ● | ● | — | — | — | ● |
| **Emergency stop release** | ● | — | — | — | — | — | — |
| Audit read | ● | ● | own | own | own | own | ● |
| `hr.read` | ● | — | ● | — | — | — | — |

Three deliberate asymmetries: **Admin cannot approve** — administering the system and authorising its outward actions are different duties; **Compliance can reject but never approve** — a safety role that could also authorise is not a safety role; and **only the Owner can release an emergency stop**, though several roles can engage one, because stopping should be easy and restarting should not.

**No agent principal holds any permission in this matrix.** Agent capability comes solely from `registry.agent_tool_grant`, and every tool maps to a permission that the *platform* holds on the agent's behalf during execution. That is why an agent can publish content but can never approve it: there is no grant that maps to `approval.grant`, and a trigger additionally rejects any `decided_by` whose actor type is not human.

---

## 18.3 Event schemas

### Envelope

```json
{
  "event_id":       "018f2c…",
  "event_type":     "ContentPublished",
  "event_version":  1,
  "org_id":         "018e…",
  "occurred_at":    "2026-08-18T11:22:04.117Z",
  "correlation_id": "9f2c…",
  "causation_id":   "018f2b…",
  "actor":          { "id": "018a…", "type": "agent", "agent_version_id": "018c…" },
  "payload":        { }
}
```

Rules: consumers tolerate unknown fields; `event_version` increments only for breaking payload changes, and both versions are published for one release; `causation_id` names the event or command that produced this one, which is what makes a causal chain reconstructable from the audit log.

### The twenty domain events

| Event | Payload essentials | Notable consumers |
|---|---|---|
| `DailyPlanningStarted` | `plan_date`, `capacity{human,agent}`, `kpi_snapshot_id` | audit, notifications |
| `TaskCreated` | `task_id`, `workflow_run_id`, `objective_id`, `brand_id`, `priority`, `assigned_actor_id` | scheduler, dashboards |
| `TaskAssigned` | `task_id`, `actor_id`, `queue_class` | queues |
| `TaskCompleted` | `task_id`, `output_ref`, `cost_amount`, `duration_ms`, `confidence` | insight, ledger |
| `TaskFailed` | `task_id`, `reason`, `ceiling_breached`, `retryable` | errors, notifications |
| `ResearchCompleted` | `subject_ref`, `evidence[]{claim,source,retrieved_at,published_at}` | pipeline, knowledge |
| `DraftReady` | `content_item_id`, `version_no`, `model_execution_id` | QA workflow |
| `QARejected` | `content_item_id`, `version_no`, `criteria_failed[]`, `feedback_code` | writer, evaluation |
| `ApprovalRequested` | `approval_id`, `action_type`, `risk_level`, `expires_at`, `target` | notifications, dashboards |
| `ApprovalGranted` | `approval_id`, `decided_by`, `subject_ref` | execution gate |
| `ApprovalRejected` | `approval_id`, `feedback_code`, `note` | originating workflow, evaluation |
| `ApprovalExpired` | `approval_id`, `subject_ref`, `disposition` (`cancel` \| `regenerate`) | originating workflow |
| `ContentApproved` | `content_item_id`, `approval_id`, `channel_id` | publishing |
| `ContentPublished` | `content_item_id`, `published_url`, `provider_ref`, `external_action_id` | analytics schedule at +1d, +7d, +30d |
| `LeadQualified` | `lead_id`, `icp_fit`, `intent`, `score`, `band`, `evidence_count` | outreach, dashboards |
| `OutreachPrepared` | `outreach_item_id`, `lead_id`, `template_code`, `char_count` | approval, send queue |
| `HumanActionConfirmed` | `work_item_id`, `external_action_id`, `sent_at`, `divergence` | **follow-up timers**, compliance |
| `PartnershipQualified` | `partnership_id`, `score`, `band`, `stage_code` | dashboards |
| `KpiThresholdBreached` | `kpi_code`, `value`, `threshold`, `severity`, `computation_id` | orchestrator, notifications |
| `IntegrationFailed` | `connector_key`, `health_state`, `blocked_operations[]` | scheduler, notifications |
| `BudgetThresholdReached` | `scope`, `period`, `spent`, `limit`, `action_taken` | gateway, notifications |
| `AgentFailed` | `agent_version_id`, `task_id`, `reason`, `ceiling_breached` | errors, agent scorecard |
| `PolicyViolationRaised` | `rule`, `subject_ref`, `actor_id`, `denied_action` | audit, security alerts |

`HumanActionConfirmed` deserves the emphasis: it is what starts the day-4 and day-10 follow-up timers. Anchoring them to this event rather than to record creation is the fix for the defect the workbook found in itself.

### Delivery guarantees

At-least-once, ordered per aggregate, idempotent at the consumer via `messaging.processed`. The relay publishes from the outbox in `occurred_at` order per organisation; a consumer that must see events in order for one aggregate reads them keyed by aggregate id. **No consumer may assume global ordering**, and none needs to.

---

## 18.4 Webhook architecture

### Inbound

```
receive → verify signature and timestamp window
        → persist raw body to object storage
        → INSERT integration.webhook_event (unique on org + connector + provider_event_id)
        → 200 within 500 ms
        → enqueue normalisation asynchronously
```

Five properties, each earning its place:

1. **Signature and timestamp verified before anything is persisted** — an unsigned or replayed event never enters the system.
2. **Raw body kept** — a normalisation bug found three weeks later can be replayed against the original.
3. **Deduplicated on the provider's event id** — providers retry, and at-least-once delivery is the norm.
4. **Acknowledged fast** — no AI work, no connector calls, no database writes beyond the record itself happen inside the handler.
5. **Processed asynchronously** — through the same queue classes as everything else, so webhook floods cannot starve interactive work.

### Outbound

Not in v1 scope (A-08: no external consumers). The architecture reserves `integration.webhook_subscription` and specifies the contract now — HMAC-SHA256 over the raw body with a timestamped signature header, exponential retry over 24 hours, per-subscription dead-lettering — so adding it later is additive rather than a redesign.
