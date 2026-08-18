# 12 — Integration Architecture

## 12.1 The connector contract

Every connector — API-backed or human-assisted — implements one interface and publishes one manifest. The manifest is data, versioned in the database, and the platform refuses to bind a connector whose manifest is incomplete.

```
ConnectorManifest
  key, display name, area, value rank
  tier                    T1 read+write · T2 read-only · T3 human-assisted · T4 manual · T5 prohibited
  auth                    oauth2_authcode | oauth2_client | api_key | none | n/a
  scopes[]                least privilege, listed explicitly
  read_operations[]       operation → normalised entity
  write_operations[]      operation → idempotency strategy → reconciliation query
  restricted_operations[] operations this connector must never expose as tools
  rate_limits[]           requests per window, per credential, per endpoint
  webhooks                available? signature scheme?
  health_probe            how liveness and auth validity are checked
  data_classifications[]  what it may carry
  verified_on, verify_interval   default 6 months
```

Two fields are unusual and both are load-bearing.

**`restricted_operations`** names what the connector *could* do but must not expose. A CRM connector might technically support bulk delete; declaring it restricted means no tool for it exists, so no agent can request it and no policy exception can enable it without a manifest change and its approval.

**`verified_on` / `verify_interval`** mirror the workbook's own discipline — every platform fact it records is stamped "re-verify 6-monthly". A connector past its verification date raises a task and, for write operations, degrades to `Degraded` health: prepared work continues, execution requires confirmation. Acting on an expired platform rule is how a compliant integration silently becomes a terms breach (`R-020`, `SCH-020`).

## 12.2 Health as a state machine

```
Connected ──auth error──> AuthenticationExpired ──reauth──> Connected
    │                                  │
    ├──429 / quota──> RateLimited ─────┘
    ├──error rate > threshold──> Degraded ──recovery probe──> Connected
    ├──probe fail──> Unavailable
    └──admin──> Disabled
```

Agents check health **before** attempting work (`INT-011`); the orchestrator treats a degraded high-value connector as a planning input, not as a runtime surprise. `Unavailable` and `AuthenticationExpired` block dependent jobs and notify; `RateLimited` queues with backoff rather than failing.

## 12.3 Human-assisted connectors are first-class

This is the architectural answer to Phase 1's central constraint, and it is the piece most likely to be under-designed elsewhere.

`ISendChannel` has two implementations. They are interchangeable to every layer above:

| | **API channel** | **Human-assisted channel** |
|---|---|---|
| Queue | Reserve `ExternalAction`, call the connector | Reserve `ExternalAction`, create a `HumanWorkItem` |
| Execute | Provider API | A person acts on the platform they are already logged into |
| Confirm | Provider response | One-click confirmation capturing actual time, exact text sent, sender |
| Idempotency | Provider reference | Work-item id + content hash; re-queueing the same item is refused |
| Audit | Identical | Identical |
| Measurement | Identical | Identical |

Everything upstream — planning, drafting, QA, compliance, approval, sequencing, follow-up timers, suppression, frequency caps, measurement, cost, audit — is the same code on both paths. Only the last hop differs.

Consequences worth stating plainly:

- The outreach lane is **not a degraded feature**. It gets the full governed spine; the human performs the one step the platform is forbidden to perform.
- Follow-up timers anchor to the **confirmed send time**, not to record creation — which is exactly the defect the workbook found and fixed in its own change log (`WF-021`).
- If a platform later opens a compliant API, swapping the channel implementation changes nothing above it.
- Capacity planning becomes honest: human-executed steps are budgeted against human capacity, so the daily plan cannot promise 100 sends when one person is available.

## 12.4 Rate limiting and pacing

Centralised, never per-agent (`INT-012`). Token buckets in Redis keyed by `(org, integration, credential, endpoint, window)`, with a pacing scheduler that spreads work rather than bursting it — the workbook is explicit that bursts are what trigger platform warnings, and its own targets sit deliberately at the safe end of published limits.

Queue classes keep one expensive job from blocking the system (`SCH-021`): `critical`, `standard`, `research`, `external-write`, `approval-dependent`, `retry`. External writes get their own class because they are the ones with real-world consequences and the tightest rate constraints.

## 12.5 Retry taxonomy

Not everything that fails should be retried, and treating them alike is how duplicates and bans happen.

| Class | Example | Action |
|---|---|---|
| Transient | Timeout, 502, connection reset | Exponential backoff, jitter, capped attempts. If the operation is a write, reconcile before re-sending |
| Rate-limited | 429, quota | Requeue with the window's reset time; never retry immediately |
| Auth | 401, expired token | Do not retry. Set `AuthenticationExpired`, block dependents, notify |
| Permanent | 400, validation, policy rejection | Do not retry. Fail the task with the reason; escalate if it recurs |
| Unknown | Timeout after the request was accepted | **Reconcile first**, then decide (§10.5) |

Repeated failure moves work to a dead-letter state with reason, inputs, attempts and last error — never infinite retry (`FR-114`).

## 12.6 Webhooks

`Verify signature → persist raw body to object storage → deduplicate by provider event id → normalise → enqueue → acknowledge.` Acknowledgement is immediate; no AI work happens synchronously in a webhook handler (`INT-014`). The raw body is kept because a normalisation bug found later can be replayed against the original.

## 12.7 MVP connector set

Deliberately small, chosen for the ratio of business value to build risk:

| Connector | Tier | Why in the MVP |
|---|---|---|
| **CMS / website** | T1 | The only place the platform can complete a full publish loop (A-03) |
| **Google Search Console** | T2 | Primary SEO truth; read-only; zero risk |
| **Google Analytics 4** | T2 | Outcome measurement; freshness-stamped |
| **IndexNow / Bing Webmaster** | T1 | Cheap, high leverage, low surface |
| **UTM minting** | internal | Deterministic, unit-tested, fixes a defect the workbook already hit |
| **Human send channel** | T3 | The entire outreach lane |

Everything else in the 133-platform estate is specified and sequenced, not built. Paid media is excluded by decision, not by omission.
