# 24 — Workflow Catalogue

**Phase 6 · Status: awaiting approval.** Definitions, not implementations.

Every important workbook activity as an executable workflow. Each carries: ID, name, workbook source, business objective, trigger, schedule, participating agents, steps, conditions, dependencies, APIs, inputs, outputs, human approvals, failure path, retry policy, KPI, expected duration, expected AI cost and audit requirements.

## 24.0 Assumption carried

`P6-02` is unanswered. **A-13: only the MVP set ships enabled; the remaining workflows ship specified and disabled**, to be enabled deliberately once the first set is stable. Forty workflows live on day one gives one manager forty things that can fail at once.

| Ships **enabled** | Ships **disabled** |
|---|---|
| WF-001 Daily Growth Planning · WF-002 Data Health · WF-003 Daily Brief · WF-004 End-of-Day Report · WF-020 Content Production · WF-010/011/012/013/015/016/017 the outreach lane · WF-042 Analytics Collection · WF-005 Weekly Review | Everything else — partnerships, PR, community, events, link building, direct channels, answer-engine audit, geo pages, syndication beyond the primary channel |

`P6-03` is unanswered. **A-14: approval expiry defaults stand as specified in Phase 3** (48 h outreach, 72 h content, 24 h news-linked, 2 h journalist requests), to be revised after one month of real decision-time data. If the manager checks twice a week, these produce a queue of expired work — which is a signal worth having early rather than a defect to prevent.

---

## 24.1 Cost model

Expected AI cost per run is derived from the Phase 5 ceilings and the agents each workflow invokes. These are **budget estimates for capacity planning**, not measurements; the ledger replaces them with actuals from the first week.

| Workflow class | Agents invoked | Est. cost/run | Est. duration |
|---|---|---|---|
| Planning | 4–5, one deep-reasoning | £0.35–0.60 | 3–6 min |
| Content production | 4 (strategy, write, QA, compliance) | £0.45–0.70 | 8–15 min + approval wait |
| Outreach preparation (per item) | 2 (compose, compliance) | £0.02–0.04 | 20–40 s + approval wait |
| Research sweep | 1–2 research-class | £0.30–0.50 | 4–8 min |
| Reporting | 1–2 | £0.15–0.30 | 2–4 min |
| Deterministic (health, analytics, coverage) | 0–1 cheap | £0.00–0.02 | < 60 s |

At the MVP volumes — one planning run, ~3 content items a week, ~20 outreach items a day, two reports a day — the modelled steady state is **£6–11 per working day**, dominated by content drafting. Outreach, the highest-value lane, is the cheapest to run because the platform prepares rather than generates at length.

---

## 24.2 The daily spine — full definitions

### WF-002 · Data Health & Reconciliation

| | |
|---|---|
| **Workbook source** | `Dashboard` §9 DATA HEALTH (10 checks); `UPGRADE NOTES` #45, #46 |
| **Objective** | No number reaches a report unless it can be trusted |
| **Trigger** | Schedule 07:00 org time, working days; also after any bulk write |
| **Agents** | DQ |
| **Steps** | 1 · run the ten integrity checks as queries 2 · reconcile conversions against PCI order references 3 · assert every dashboard tile resolves to its intended KPI 4 · write results 5 · if any check ≠ 0, set the report-block flag |
| **Conditions** | Blocks nothing else; **gates report publication only** |
| **Dependencies** | None — deliberately first in the day |
| **APIs** | None external |
| **Inputs** | `{ org_id, as_of }` · **Outputs** `{ checks[], failing_records[], blocking_report }` |
| **Approvals** | None |
| **Failure path** | If the sweep itself fails, the report is blocked as a precaution and Admin is notified — a failed check is not the same as a passed one |
| **Retry** | 2, 30 s backoff |
| **KPI** | K-090…K-099, all target zero |
| **Duration / cost** | < 30 s · ~£0.00 |
| **Audit** | Result set retained 24 months; every non-zero check raises an `ErrorEvent` |

### WF-001 · Daily Growth Planning

| | |
|---|---|
| **Workbook source** | `Summary` daily rhythm; `Objective Performance` value ranks; `START HERE` §3 targets; `Platform Progress` attention |
| **Objective** | The workforce knows what to do before the working day starts |
| **Trigger** | Schedule 07:45; re-plan on `KpiThresholdBreached(critical)` |
| **Agents** | OPS → ANA → DQ (inputs) → MKT → ORCH |
| **Steps** | 1 · assemble KPI snapshot, goal progress, backlog, attention flags, approval backlog, budget, capacity 2 · market scan 3 · ORCH produces plan 4 · validate plan against capacity and enabled workflows 5 · create tasks 6 · emit `DailyPlanningStarted` |
| **Conditions** | Tasks for disabled workflows or degraded connectors are rejected at step 4 and returned as deferred with a reason |
| **Dependencies** | WF-002 and WF-042 must have completed |
| **Inputs** | see Phase 5 ORCH contract · **Outputs** `{ plan, tasks[], deferred[], risks[] }` |
| **Approvals** | None — the plan is visible; dispatched items carry their own |
| **Failure path** | If ORCH fails twice, yesterday's plan shape is reused with today's KPI values and the fallback is stated in the brief |
| **Retry** | 2 |
| **KPI** | K-066 attainment; K-068 rank vs share of minutes |
| **Duration / cost** | 3–6 min · £0.35–0.60 |
| **Audit** | Full input snapshot retained — a plan that cannot be reconstructed cannot be reviewed |

### WF-003 · Daily Brief · WF-004 · End-of-Day Report

| | |
|---|---|
| **Workbook source** | `Summary`; `Dashboard`; `Weekly Pulse` |
| **Objective** | The owner starts and ends the day informed without opening a log |
| **Trigger** | 08:00 and 18:00 org time |
| **Agents** | EXEC |
| **Steps** | 1 · gather period data with freshness 2 · compose sections 3 · attach citations 4 · **watermark if data health non-zero** 5 · publish and notify |
| **Conditions** | A report generated with failing checks is watermarked and names them — never silently published |
| **Approvals** | None; reports are read, not executed |
| **Failure path** | Publish a reduced report from raw KPI values with a note that narrative generation failed. **Never no report** — silence reads as "nothing happened" |
| **KPI** | RPT-001/002 punctuality |
| **Duration / cost** | 2–4 min · £0.15–0.30 |
| **Audit** | Report content, sources, freshness and health state at generation |

---

## 24.3 The content lane — full definition

### WF-020 · Content Production

| | |
|---|---|
| **Workbook source** | `Article Bank`; `Keyword Plan`; `SEO Clusters`; `Content Calendar`; `Publishing Plan`; Playbook 1, 2, 7 |
| **Objective** | Compounding owned traffic, without the thin-content failure the workbook warns about |
| **Trigger** | Scheduled 13:00 from the daily plan; or manual |
| **Agents** | CSTRAT → CWRITE → CQA → COMP → *(human)* → PUB → ANA |
| **Steps** | 1 select brief · 2 similarity check · 3 research · 4 draft · 5 brand QA · 6 revise if rejected · 7 claim verification · 8 approval request · 9 human decision · 10 publish · 11 record URL · 12 schedule metric capture at +1d/+7d/+30d |
| **Conditions** | Throughput cap not exceeded · similarity below threshold · canonical rule satisfied for the channel · original indexed before syndication · derivative cap not exceeded |
| **Dependencies** | CMS connector healthy; approved-claims store reachable |
| **APIs** | CMS publish; Search Console for indexation; GA4 for metrics |
| **Inputs** | `{ brief_id?, campaign_id?, channel }` · **Outputs** `{ content_item_id, published_url, external_action_id }` |
| **Approvals** | **A04 — every external publication.** 72 h expiry, 24 h if news-linked |
| **Failure path** | QA rejects → revise (max 2, then escalate: three rejections means the brief is wrong) · compliance blocks → escalate with the unsupported claim named · publish fails → external-action state machine, reconcile before retry · approval expires → return to drafting |
| **Retry** | Draft 2 · publish per retry taxonomy · **never a blind publish retry** |
| **KPI** | K-021 published · K-024 engagement rate · K-027 coverage · K-029 cluster position |
| **Duration / cost** | 8–15 min of machine time · £0.45–0.70 · plus approval wait |
| **Audit** | Every version, every QA finding, the claim table, the approval trail, the publish action and its provider reference |

---

## 24.4 The outreach lane — full definitions

The lane that carries the workbook's highest-value objective and cannot be automated end to end.

### WF-010 · Lead Discovery & Qualification
Source `LinkedIn Playbook` step 1, `LinkedIn Outreach` A–L. Trigger 09:00. Agents LEAD. Steps: candidate set → four qualification tests → duplicate check → suppression check → ICP and intent with evidence → platform computes score and band → create lead. Conditions: no lead created without at least one evidence row. Approvals: none to qualify. Failure: unqualified candidates are recorded with the failing test, not discarded. KPI K-001. ~£0.30/batch.

### WF-011 · Personalisation Line
Source Playbook step 2, Golden Rule 3. Agents LEAD. Output must cite the profile evidence used. A blocked personalisation is a blocked send — by design.

### WF-012 · Outreach Preparation
Source `Message Bank`, Playbook steps 3–4. Trigger 10:00 notes, 11:00 messages, and follow-up timers. Agents OUT → COMP → CQA. Steps: template select → substitute → **hard length check** → banned-phrase check → suppression, consent and frequency checks → four-eyes review → approval item. Approvals **A01, always, 48 h expiry**. Failure: any policy failure blocks composition and returns the rule. KPI K-002/K-005/K-011. ~£0.03/item.

### WF-013 · Outreach Execution
Source Golden Rule 5. **No automated send exists at any layer.** Steps: approved item → human work item → operator acts on the platform → confirmation captures actual time, exact text and sender → divergence re-checked → `HumanActionConfirmed`. Approvals already granted. Failure: expired approval withdraws the item with a one-click regeneration.

### WF-015 · Follow-up Sequence
Timers at **confirmed send + 4 days** (M4) and **+10 days** (M5). Maximum two, then `NoResponse`. Source Playbook step 6 and `UPGRADE NOTES` #8 — the workbook's own bug, fixed by anchoring to the event.

### WF-016 · Suppression & Decline
Source Golden Rule 7, QA check 4. On decline: send the approved close, write a **permanent** suppression entry, terminate every open item for that contact across all channels and brands. Autonomy L4 — suppression is never optional and never waits for approval.

### WF-017 · Duplicate Defence
Source `LinkedIn Outreach` AN, `UPGRADE NOTES` #4. Runs before create and before queue. Surfaces the earlier record *and its outcome*, because the outcome is what decides whether re-contact is even legal.

---

## 24.5 Catalogue — the remaining workflows

Specified with the same attribute set in the platform's workflow registry; summarised here.

| ID | Name | Workbook source | Trigger | Agents | Approval | Ships |
|---|---|---|---|---|---|---|
| WF-005 | Weekly Growth Review | Weekly Pulse, Objective Performance, Team Scorecard | Week-start 07:30 | ANA → EXP → ORCH → EXEC | Strategy changes only | **Enabled** |
| WF-006 | Friday Review Pack | Weekly Review | Fri 15:00 | EXEC | None | **Enabled** |
| WF-014 | Reply Triage | Playbook step 5; M6/M7/M15 | On reply recorded | OUT → COMP | Always; fees escalate | **Enabled** |
| WF-018 | Handoff to Closer | Outreach AD–AM | Meeting booked | ORCH | Yes | **Enabled** |
| WF-021 | Syndication Waterfall | Playbook 7; Publishing Plan | Original indexed | PUB | Non-canonical channels | Disabled |
| WF-022 | Schedule Coverage Control | Content Scheduler | Daily | OPS | None | **Enabled** |
| WF-023 | SEO Cluster Refresh | SEO Clusters; Playbook 1, 20 | Monthly | SEO | Site changes | Disabled |
| WF-024 | Keyword Re-verification | Keyword Plan | Monthly P1, quarterly full | SEO | Priority changes | Disabled |
| WF-025 | Answer-Engine & Entity Audit | Playbook 3, 14 | Monthly | AEO | Profile edits | Disabled |
| WF-026 | Link Building | Link Building; Playbook 23 | Weekly | LINK | Yes | Disabled |
| WF-027 | Original Research Study | Playbook 17 | Quarterly | MKT + CWRITE + PR | Yes | Disabled |
| WF-028 | Geo × Role Pages | Playbook 22 | Per batch, indexation-gated | SEO + CWRITE | Per batch | Disabled |
| WF-030 | Partnership Discovery | Partnership Pipeline; PR Directory | Weekly | PART | To contact | Disabled |
| WF-031 | Partnership Stage Management | Partnership Pipeline | On stage change | PART | Commercial terms | Disabled |
| WF-032 | PR Route Working | PR & Target Directory | Monthly | PR | Yes | Disabled |
| WF-033 | Journalist Request Response | Playbook 4 | Twice daily | PR + COMP | **2 h fast lane** | Disabled |
| WF-034 | Community Answer Preparation | Community & PR; Playbook 6 | 3–5/week | COMM + CQA | Yes; human posts | Disabled |
| WF-035 | Webinar → Content Engine | Playbook 8 | Monthly | EVT + CSTRAT + PUB | Yes | Disabled |
| WF-036 | Authority Registry Submissions | PR Directory | Quarterly | AEO + PR | Yes | Disabled |
| WF-037 | Directory & Review Estate | Playbook 11; QA 9 | Quarterly | AEO | Yes | Disabled |
| WF-040 | Email Nurture & Newsletter | Playbook 9 | Fortnightly | DIR + CQA + COMP | Per campaign | Disabled |
| WF-041 | WhatsApp / Telegram / SMS | How-To 19 | As needed, ≤1/week | DIR + COMP | Yes | Disabled |
| WF-042 | Analytics Collection | Playbook 13 | 07:15 daily | ANA | None | **Enabled** |
| WF-043 | KPI Threshold Reaction | Weekly Pulse; Benchmarks | On breach | ANA → ORCH | Strategy changes | **Enabled** |
| WF-044 | Experiment Lifecycle | Experiments | Per test | EXP | Playbook changes | Disabled |
| WF-045 | Cost & Budget Control | Channel Costs | Continuous | COST | Budget increases | **Enabled** |
| WF-046 | Compliance Attestation | QA & Compliance | Daily/weekly/monthly | COMP | Human signs | **Enabled** |
| WF-047 | Platform Account Onboarding | Platform Setup | Per account | OPS | Yes | Disabled |
| WF-048 | Workbook Import & Diff | UPGRADE NOTES; Lists | On upload | KNOW | **Always** | **Enabled** |
| WF-049 | Job Posting Distribution | Job Postings | Per role | OPS | Yes | Disabled |
| WF-050 | Emergency Publishing Lock | Golden Rule 6 | Manual or platform warning | COMP | None to engage | **Enabled** |

**Sixteen enabled, twenty-four disabled.** Every disabled workflow is fully defined; enabling one is a configuration change with an approval, not a development task.

---

## 24.6 Failure paths, stated once

| Class | Path |
|---|---|
| Agent returns `escalate` | Task → `WaitingForApproval` with the typed reason; workflow parks on a Temporal signal |
| Agent returns `blocked` | Workflow takes its failure path; no retry |
| Policy denial | Terminal for that attempt; the rule is returned to the caller and recorded |
| Approval expires | Per workflow: cancel, or return for regeneration. News-linked content always regenerates — a post approved two days late is not the same post |
| External write times out | `Unknown` → reconciliation query → resolve. **Never a blind retry** |
| Connector unhealthy | Work queues; the orchestrator sees it as a planning input tomorrow |
| Budget exhausted | Low-priority executions pause; critical workflows configurably exempt |
| Kill switch engaged | Everything parks, including approved-but-unexecuted items |
| Five consecutive workflow failures | Workflow auto-disables and alerts. A workflow failing repeatedly is worse than one not running |
