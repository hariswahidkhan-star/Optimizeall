# 06 — Software Requirements Specification
## PCI AI Autonomous Growth OS — Version 1.0 (Phase 1, awaiting approval)

**Source of truth:** `PCI_AI_Growth_OS.xlsx`, 42 worksheets, analysed in full.
**Convention:** every requirement is testable. Where the workbook does not determine an answer, the requirement says **"Not defined in workbook — decision required"** and is listed in [07 §B](07-decisions-risks-mvp.md#b-workbook-gaps).

---

## 1. Executive Summary

PCI AI operates a growth department through a 42-sheet Excel workbook that is, in substance, a complete operating model: 133 platforms, 23 growth techniques, 63 workstreams, 15 approved outreach templates, 7 SEO pillars feeding 5,683 article briefs, 11 value-ranked objectives across 7 brands, a 15-check compliance regime, a 10-check data-health regime, and an hour-by-hour daily rhythm from 09:00 research to 17:00 logging.

The workbook works, but it does not scale: it holds ~1,200 outreach rows (about eight working days at full-team volume), it cannot enforce its own rules (all 54 validation rules originally had error alerts disabled), it cannot execute anything, and it depends entirely on humans remembering to log work the same day.

This specification defines the **PCI AI Autonomous Growth OS**: a governed, auditable platform in which a workforce of 24 specialised AI agents performs the research, drafting, analysis and coordination described in the workbook, while deterministic software owns identity, scheduling, permissions, state, budgets, approvals, integrations, compliance and audit.

The central design commitment is stated plainly because it constrains everything else: **the workbook forbids automating the single highest-value activity it describes.** Golden Rule 5 — "No bulk sending, no automation tools, no bots, no scraping" — is repeated in the LinkedIn Playbook, QA & Compliance check 5, and Playbook technique 15. LinkedIn also provides no official API for connection requests, personal messages or comments. Therefore the outreach lane is designed as **prepare → verify → approve → human executes → confirm → measure**, and the platform's value in that lane is the 90% of the work that is research, qualification, personalisation, sequencing, compliance and measurement — not the send.

Version 1 succeeds when the owner opens the platform in the morning and finds that the AI workforce has already reviewed yesterday's performance, produced today's plan against the value-ranked objectives, run permitted research, drafted the day's content and outreach, prepared everything requiring a decision in a single approval inbox, executed the low-risk authorised work, measured the results, and recorded every step in an audit trail that answers "why did the system do this?".

---

## 2. Business Context

PCI AI (Project Controls Institute) issues three credentials — PCL-AI (flagship), PML-AI (premium), PFL-AI (entry) — plus an honorary certification for senior practitioners, and operates adjacent properties PCI World (community) and Certuvo (exam prep) across five domains. Its buyers are project controls managers, planners, cost engineers, estimators, PMO leads, risk analysts and their employers, concentrated in the UK, US, Gulf, India and Australia.

Its market position is contested at the most basic level: a competitor training provider currently owns PCI's own name in Google (`Keyword Plan`, P1 attack keyword #1), and established bodies (PMI, AACE, RICS, APMG) own the head terms. The workbook's response is a compounding-authority strategy — topical SEO clusters, entity/answer-engine authority, digital PR, community credibility, partnerships — combined with a direct outreach motion that converts individual senior practitioners.

The commercial risk the workbook is most alert to is reputational: overstating a credential to a senior professional. Its first Golden Rule, several QA checks, the Employee Score compliance component and the exact wording of every approved template all exist to prevent it. **Any system that generates outreach or public content for PCI inherits that risk as its primary design constraint.**

---

## 3. Workbook Analysis

Summarised here; complete in [01 — Workbook Analysis](01-workbook-analysis.md).

- **Governance layer (6 sheets):** START HERE (rulebook, targets, roster, Golden Rules, sharing rules, domains), MAP (IA), TEAM GUIDE (onboarding + where-to-log), GROWTH PLAYBOOK (23 techniques), PLATFORM GUIDE (133 weekly plays), PR & Target Directory (~100 verified routes plus a verified skip list).
- **Execution layer (14 sheets):** DAILY ENTRY (the single activity ledger), LinkedIn Outreach (42 columns, per-person pipeline to revenue), Partnership Pipeline, Content Calendar, Content Scheduler, Community & PR, Job Postings, Link Building, Experiments, UTM Builder, SEO Clusters, Keyword Plan, Article Bank (5,683 briefs), Daily Log.
- **Measurement layer (10 sheets):** Dashboard (9 sections incl. DATA HEALTH), Summary, Weekly Pulse, Objective Performance, Team Scorecard, Employee Score, Weekly Review, Platform Progress, Who Did What, Accounts Register.
- **Management layer (5 sheets):** Master Tasks (63), Platform Setup (133), Publishing Plan (10 ranked with canonical rules), Channel Costs, QA & Compliance (15 checks).
- **Reference layer (7 sheets):** Message Bank (M1–M15), LinkedIn Playbook, How-To Guides, Benchmarks (sourced, with vendor-bias caveat), Glossary, Lists (all enumerations + per-platform dedup rules + 6-monthly verification), UPGRADE NOTES (47 audit findings).

---

## 4. Requirements Traceability Matrix

See [05 — Traceability Matrix](05-traceability-matrix.md). Every functional requirement in §10 traces to at least one workbook sheet, or is explicitly labelled Security / Platform / Technical / Future.

---

## 5. Business Objectives

| ID | Objective | Measure |
|---|---|---|
| BO-01 | Preserve every control the workbook encodes, with zero regression | All 10 Golden Rules and 15 QA checks enforced in software; A/B tested in `TEST-SEC-*` |
| BO-02 | Remove same-day manual logging as a dependency | ≥90% of activity records created automatically by the executing agent or connector |
| BO-03 | Produce a decision-ready daily plan before the working day starts | Daily brief published by 08:00 org time on ≥95% of working days |
| BO-04 | Increase content throughput without triggering thin-content demotion | Published items/week up, with editorial quality gate pass-rate and indexation rate tracked |
| BO-05 | Make effort allocation match the value ranks | Share of minutes per objective converges toward value rank order |
| BO-06 | Make every reported number defensible | 100% of dashboard KPIs expose lineage; data-health checks read zero |
| BO-07 | Keep AI cost proportional to output | Cost per published item and per prepared outreach item trend down |
| BO-08 | Be able to answer "why did the system do this?" for any action, six months later | 100% of external actions have a complete audit chain |

---

## 6. Scope

**In scope for the product:** organisation/user/RBAC; agent registry, versioning and control centre; business calendar and scheduler; durable workflow engine with human-approval waits; approval inbox; model gateway with fallback and cost accounting; knowledge base/RAG over PCI documents and the workbook; policy and compliance engine; integration layer for official APIs; content engine; lead engine; outreach preparation engine; partnership, PR, community, link-building, events and direct-channel modules; SEO/AEO workspace; analytics, KPI and attribution engine; experiment engine; goals/OKRs; campaigns; executive command centre and Executive AI; reporting; notifications; audit and error centres; workbook importer; cost control; observability; multi-tenancy foundations.

**In scope as human-assisted (not automated):** all LinkedIn personal-profile actions; community posting; review solicitation; anything the platform's ToS forbids automating.

## 7. Out of Scope

| ID | Excluded | Reason |
|---|---|---|
| OOS-01 | Any automation of LinkedIn connection requests, DMs, comments or profile scraping | Golden Rule 5; LinkedIn ToS; no API — **permanently out of scope** |
| OOS-02 | Any scraping connector for any platform | Golden Rule 5; Playbook 15 |
| OOS-03 | Automated posting to Reddit, Quora, DEV, Stack Exchange or moderated communities | Platform anti-promotion terms; Playbook 6, 15 |
| OOS-04 | Paid link acquisition, PBNs, reciprocal swaps, engagement pods, AI-comment tools | Playbook 15, 23 |
| OOS-05 | Incentivised, gated or internally written reviews | QA check 9 |
| OOS-06 | Paid media buying and bid management | Deliberate MVP exclusion; spend controls first |
| OOS-07 | Replacing the PCI platform as the order/revenue ledger | Dashboard §8 reconciliation rule |
| OOS-08 | Replacing the password vault | Golden Rule 9 |
| OOS-09 | Automated ISO/IEC 17024 or regulatory accreditation activity | `PR & Target Directory` row 61: a leadership programme, not a marketing task |
| OOS-10 | Automated HR decisions from Employee Score | Human process; see CMP-014 |

---

## 8. Stakeholders

| Stakeholder | Interest | Key screens |
|---|---|---|
| Owner / Managing Director | Does the AI workforce produce commercial results within policy? | Executive dashboard, Daily brief, Weekly review, Approvals, Costs |
| Marketing Manager (workbook owner, `START HERE!B19`) | Daily plan, approvals, coaching, compliance sign-off | All operational screens |
| Marketing team members | Their queue, their drafts, their scorecard | Task inbox, Content, Outreach queue, Weekly review |
| PCI closer | Warm handoffs with full context | Lead detail, Handoff |
| Compliance / legal reviewer | Are claims, consent and platform rules respected? | Compliance centre, Audit centre |
| Platform administrator | Agents, prompts, budgets, integrations, security | Agent Control Center, Integrations, Admin, Audit |
| Future tenant organisations | Isolation and configurability | (Architecture requirement only) |

## 9. User Roles

| Role | Capabilities |
|---|---|
| `Owner` | Everything, including approving strategy changes, autonomy increases and emergency stop release |
| `Admin` | Agents, prompts, models, integrations, budgets, feature flags, RBAC; cannot approve their own high-risk actions |
| `Manager` | Approvals, planning, coaching, compliance attestation, roster and target configuration |
| `Operator` (team member) | Execute assigned tasks, submit drafts, confirm human-executed sends, write weekly review notes |
| `Closer` | Read leads handed over; write commercial outcome fields |
| `Analyst` | Read-only across analytics, KPIs, lineage, reports; export |
| `Compliance` | Read everything; sign attestations; trigger emergency stop; cannot publish |
| `Agent` (non-human principal) | Only the tools its version grants; **can never approve, never hold credentials, never change policy** |
| `ServiceAccount` | Integration execution only, scoped per connector |

---

## 10. Functional Requirements

### 10.1 Organisation, identity, configuration

| ID | Requirement |
|---|---|
| FR-001 | The system shall represent PCI AI as an `Organization` with time zone, working days, week-start day (1 = Sunday, 2 = Monday), programme start date and public holiday set, sourced from `START HERE` §2. |
| FR-002 | The system shall store the seven brands/properties (PCI AI Institute, PCL-AI, PFL-AI, PML-AI, PCI World, Certuvo, All/shared) and the five domains, and shall require a brand tag on every work item. |
| FR-003 | The system shall store the eleven objectives with editable value ranks (1–11) and require an objective tag on every work item. |
| FR-004 | The system shall reject any work item lacking either tag, and shall surface an `(untagged)` queue for Monday review. |
| FR-005 | The system shall maintain a roster of human actors and agent actors; every activity record shall reference a known actor (removing the workbook's "Unattributed" failure mode). |
| FR-006 | The system shall store per-person and per-agent daily targets (defaults: 30 leads, 20 connections, 15 first messages, 10 follow-ups, 2 posts, 3 community answers, 5 partnership contacts, 15 engagements) and compute weekly targets as `target × working_days`. |
| FR-007 | Expected-to-date figures shall be computed as `daily_target × working_days_elapsed × active_capacity`, where capacity counts active human and agent actors (fixing `UPGRADE NOTES` #3). |
| FR-008 | All business behaviour listed in §60 of the brief (posting times, cadences, thresholds, approval limits, schedules, models, ICP rules, notification rules, budgets, retry limits, workflow enablement) shall be configuration, changeable without deployment, and versioned. |

### 10.2 Goals, campaigns, tasks

| ID | Requirement |
|---|---|
| FR-010 | The system shall support `Goal` entities with KPI, target, deadline, owner, responsible agents, workflows, current progress and status, and shall support Objective → Key Result → Initiative → Task decomposition. |
| FR-011 | The orchestrator shall prioritise work against goals and objective value ranks, and shall record the reason a task was prioritised. |
| FR-012 | The system shall support `Campaign` entities with a Campaign Brief (objective, audience, geography, offering, message, positioning, channels, KPIs, start/end, budget, constraints, required approvals, knowledge references, CTA). |
| FR-013 | Every agent performing campaign work shall retrieve the campaign brief before producing output, and the retrieval shall be recorded in the execution trace. |
| FR-014 | Every meaningful unit of work shall be a `Task` with: id, organisation, created-by, assigned actor, workflow, campaign, objective, brand, priority, status, due date, scheduled time, dependencies, input, output, approval status, confidence, cost, retry count, started/completed timestamps, failure reason, audit references. |
| FR-015 | Task states shall be exactly: `Planned`, `Queued`, `Running`, `WaitingForDependency`, `WaitingForApproval`, `Completed`, `Failed`, `Cancelled`, `Skipped`. |
| FR-016 | No agent shall perform work outside the task system; any model execution without an owning task shall be rejected by the gateway. |
| FR-017 | Task priority shall be `Critical/High/Normal/Low`, computed from configurable rules (revenue opportunity, deadline, campaign importance, KPI deviation, executive request, lead score, time sensitivity, dependency), with AI able to recommend but not override deterministic constraints. |
| FR-018 | The system shall import the 63 Master Tasks with area, platform, definition of done, priority, frequency and weekly target, and shall require an owner and due date before a task can be activated (`UPGRADE NOTES` #13). |

### 10.3 Lead engine

| ID | Requirement |
|---|---|
| FR-020 | The system shall model `Account` and `Contact` separately, with industry, geography, company size, role, seniority, ICP attributes, pain-point hypothesis, source, evidence, qualification score, intent signals, engagement, relationship history, owner and next action. |
| FR-021 | Lead qualification shall apply the workbook's tests: ≥8 years in project controls or related discipline; a real current employer; visible seniority or specialism; account active within 6 months. Failing any test shall mark the lead unqualified, and the system shall not lower the bar to hit a volume target. |
| FR-022 | Lead score shall be computed deterministically as `ICP_fit × 12 + Intent_signals × 8` (each 1–5), banded A ≥80 "call first", B ≥60, C ≥40, D <40. |
| FR-023 | Every AI-supplied ICP or intent rating shall carry evidence (source, extract, URL, retrieval date) and a confidence value; ratings without evidence shall be rejected. |
| FR-024 | Funnel stage (1 Awareness … 9 Closed-Lost) shall be **derived** from outcome fields exactly as the workbook derives it, never typed and never inferred by a model. |
| FR-025 | Before creating or queueing a lead, the system shall check for duplicates on profile URL, email and name+company, and shall surface the earlier record and its outcome. |
| FR-026 | The system shall maintain suppression lists — do-not-contact, unsubscribed, declined, bounced, existing customers, active opportunities, blocked domains — enforced at queue-entry time across all channels and brands. |
| FR-027 | A lead marked `Declined` shall be permanently suppressed; any attempt to queue an item against it shall be denied and logged as a policy violation. |
| FR-028 | The system shall record handoff to a PCI closer, meeting date, application date, certification, purchase date, revenue and **PCI order reference**, and shall prevent a lead being marked Converted without the order reference. |

### 10.4 Outreach engine

| ID | Requirement |
|---|---|
| FR-030 | Outreach content shall only be composed from templates in the Template Registry whose status is `Approved`; free-form outreach shall be impossible. |
| FR-031 | The system shall enforce hard character limits before an item can enter the approval queue: 200 for connection notes, 300 for messages, per template configuration. |
| FR-032 | An outreach item shall be rejected if the personalisation line is empty or is not supported by cited profile evidence (Golden Rule 3). |
| FR-033 | The system shall block any outreach containing award/selection/guarantee language, using a maintained banned-phrase set plus a model-based claim classifier; blocks shall be logged with the matched rule. |
| FR-034 | Follow-up timers shall be anchored to the **message-sent event** at +4 days (M4) and +10 days (M5), with a hard maximum of two follow-ups per person, after which the outcome is set to `No Response` and the sequence stops. |
| FR-035 | The system shall provide a **human send queue**: approved item, target profile link, copy-ready text, character count, and a confirmation control that records the actual send time, the exact text sent, and the sender. |
| FR-036 | The system shall never perform an automated send, connection request, comment or profile visit on LinkedIn (`OOS-01`). Any tool attempting it shall be denied and raise a security event. |
| FR-037 | Frequency caps shall be configurable per channel, per identity and per day, defaulting to the workbook's targets, and shall pause a channel when a platform warning is recorded (Golden Rule 6). |
| FR-038 | The system shall classify replies into the workbook's outcome set and propose an approved response (M6 legitimacy, M7 cost, M8 referral, M15 polite close); any reply concerning fees, legitimacy or an unanswerable question shall escalate to the manager rather than auto-propose. |

### 10.5 Content engine

| ID | Requirement |
|---|---|
| FR-040 | `ContentItem` shall carry: title, topic, pillar, cluster, campaign, audience, persona, funnel stage, platform, format, objective, brand, keywords (primary + supporting), CTA, draft, version, author agent, QA status, approval status, scheduled time, publication status, published URL, and performance metrics (impressions, engagements, clicks, leads). |
| FR-041 | The system shall import the 5,683 Article Bank briefs with pillar, cluster, format, audience, funnel, effort, priority, keywords, word count and writing prompt, preserving the prompt's prohibition on invented statistics. |
| FR-042 | Content selection shall order by priority (P1 first) and difficulty (Easy first, per Keyword Plan), and shall respect the pillar/cluster structure shared by SEO Clusters, Keyword Plan and Article Bank. |
| FR-043 | Before drafting, the system shall run a semantic-similarity check against previously published PCI content and reject or re-brief near-duplicates. |
| FR-044 | Every factual claim in a draft shall be classified as opinion, internal business fact, public factual claim, statistical claim, product capability claim, or customer/result claim; claims requiring evidence shall be checked against approved sources, and unsupported material claims shall block approval. |
| FR-045 | The Brand QA gate shall assess brand voice, terminology (approved and prohibited), duplication, grammar, CTA quality, audience fit and structural requirements (FAQ block, meta description <155 characters, named author with credentials). |
| FR-046 | Publication shall enforce the Publishing Plan rules per platform: own-site-first ordering, indexation verification before syndication, canonical set where supported, and **refusal to publish a copy of a rankable page to LinkedIn Articles, Substack or Vocal** (no canonical support). |
| FR-047 | Repurposing shall create a distinct `ContentItem` per surface with status `Repurposed`, linked to the original, and shall enforce the configured maximum number of derivative items per original to prevent repetitive cross-channel output. |
| FR-048 | The system shall track schedule commitments (`platform × cadence × window`), compute planned vs published coverage, and raise a remediation task when coverage falls below target before the review day. |
| FR-049 | The system shall respect the native-scheduling capability table (doc 03 §3.4): where a platform cannot schedule a format, the item shall be routed to a human task instead of a scheduled publish. |
| FR-050 | The system shall support multilingual content with source language, target language, translation status and localisation status held separately, and shall not treat translation as localisation. |

### 10.6 SEO, AEO and link building

| ID | Requirement |
|---|---|
| FR-060 | `SEOKeyword` shall carry keyword, cluster, intent, funnel, volume band (labelled an editorial estimate, never presented as tool data), difficulty, who ranks today, asset to build, priority, owner, status, published URL, pillar and Article Bank brief ID. |
| FR-061 | `SEOOpportunity` shall be typed as technical issue, content opportunity, optimisation opportunity, backlink/authority opportunity, or local/search visibility opportunity, and shall carry evidence, opportunity score, recommended action, status and resulting performance. |
| FR-062 | The system shall pull impressions, clicks and position per cluster row from Search Console on the configured cadence and store them with a freshness timestamp. |
| FR-063 | The system shall detect orphan pages and propose internal links (pillar exact-phrase anchor plus two siblings) as approvable recommendations. |
| FR-064 | The system shall run a monthly answer-engine audit over a configurable prompt set (default 20), recording which domains are cited and whether PCI is mentioned, and shall never present audit output as a guaranteed citation. |
| FR-065 | The system shall verify crawler allow-list state, Bing/IndexNow submission health, and entity-fact parity across the site, Wikidata, Crunchbase, LinkedIn and Google Business Profile. |
| FR-066 | Link Building shall track tactic, target site, their page, our target URL, contact route, planned anchor, status, link type (dofollow/nofollow) and date live, and shall refuse to model paid, reciprocal or PBN acquisition. |
| FR-067 | Programmatic geo × role page generation shall require a backing Keyword Plan row, ship in batches of ten, and block the next batch until a 30-day indexation gate passes; any page that reads templated shall be rewritten or noindexed. |

### 10.7 Partnerships, PR, community, events

| ID | Requirement |
|---|---|
| FR-070 | `Partnership` shall carry organisation, type, country, contact, why-them, ICP and intent ratings, computed score and band, stage, potential value, what-they-get, last contact, next step and due date, status, deal value and contract signed date. |
| FR-071 | Partnership stages shall be configurable, defaulting to: Discovered → Researching → Qualified → Outreach Ready → Contacted → Responded → Meeting → Evaluating → Proposal → Negotiation → Active → Rejected/Closed, mapped onto the workbook's stage list. |
| FR-072 | PR routes from the PR & Target Directory shall be first-class records including the **verified skip list with reasons**, and the system shall prevent work being planned against a skip-listed route without an explicit override and justification. |
| FR-073 | Every route, benchmark and platform rule shall carry a `verified_on` date and a verification interval (default 6 months), and shall be flagged as stale when the interval elapses. |
| FR-074 | Journalist-request handling shall support a fast lane with a 2-hour target, a shortened approval expiry, and a word-count constraint (80–120 words), and shall require the answer to be on-expertise. |
| FR-075 | Community answers shall be drafted only, shall pass a "useful if the link were removed" QA gate, shall enforce per-community link ratios (e.g. max 1 link per 5 answers on Reddit, none on Stack Exchange), and shall be posted by a human. |
| FR-076 | The system shall model events and webinars with registration links carrying UTMs, promotion schedule, recording, derivative content plan and speaker invitations logged as partnership records. |

### 10.8 Direct channels

| ID | Requirement |
|---|---|
| FR-080 | Email, WhatsApp, Telegram and SMS sends shall require a recorded lawful basis and consent per contact, and shall be blocked without one. |
| FR-081 | Unsubscribes shall be honoured the same day and shall propagate to the global suppression list. |
| FR-082 | Broadcast frequency shall be capped (default: ≤1 value broadcast per week per channel) and enforced deterministically. |
| FR-083 | The system shall verify domain authentication state (SPF, DKIM, DMARC) before permitting the first campaign send and shall block sending if authentication is missing. |

### 10.9 Analytics, KPI, experiments, reporting

| ID | Requirement |
|---|---|
| FR-090 | The KPI engine shall support definition, formula, data source, frequency, target, warning threshold, critical threshold, owner and historical values, seeded with the catalogue in doc 04. |
| FR-091 | Every KPI shall expose lineage: source → raw observation → transformation → formula → displayed value, with drill-through to the underlying records. |
| FR-092 | KPIs shall be referenced by stable identifier; a regression test shall assert that every dashboard tile resolves to its intended KPI (preventing `UPGRADE NOTES` #45). |
| FR-093 | A KPI shall have exactly one definition system-wide; two screens shall never compute the same named KPI differently (preventing `UPGRADE NOTES` #46). |
| FR-094 | The system shall distinguish **correlation** from **attribution** in all outputs and shall not assert causality where data cannot support it. |
| FR-095 | Every external campaign link shall be minted through the deterministic UTM service; the system shall detect and flag any campaign link that is untagged or off-estate (not on the five approved domains). |
| FR-096 | `Experiment` shall carry hypothesis, baseline, proposed action, audience, duration, success metric, guardrail metric, samples, results, rates, difference, significance guard, result, conclusion and next action. |
| FR-097 | The significance guard shall implement the workbook's own rule (≥30 per group and ≥5 percentage-point gap to be actionable; under 100 per group labelled directional only) and shall state plainly that it is a heuristic, not a statistical test. |
| FR-098 | Rolling an experiment result into the playbook shall require human approval. |
| FR-099 | The system shall generate a **Daily Brief** (yesterday's results, today's priorities, KPI alerts, opportunities, risks, agent schedule, pending approvals) and an **End-of-Day Report** (tasks attempted/completed/failed, content produced, leads generated, outreach prepared/sent, partnerships, SEO work, campaign activity, KPI movement, AI/API cost, pending approvals, blockers, recommended actions). |
| FR-100 | The system shall generate a **Weekly Growth Review** comparing target vs actual vs previous period and the four-completed-week average, identifying wins, losses, bottlenecks, emerging opportunities, underperforming channels, agent performance, experiments and budget utilisation, with strategic changes presented as recommendations only. |
| FR-101 | Reports shall be exportable to PDF, Excel and CSV; exports shall never become the operational store. |
| FR-102 | The Executive AI shall answer questions only from system records, shall cite the internal records used, shall disclose data freshness, and shall refuse to state a business metric it cannot source. |
| FR-103 | Executive override commands (e.g. "pause LinkedIn activities today", "do not publish until Friday") shall be translated into explicit, visible, time-bounded policy or configuration changes, shown to the user for confirmation, and recorded — never held in conversation memory alone. |

### 10.10 Administration and control

| ID | Requirement |
|---|---|
| FR-110 | Administrators shall be able to create, edit, disable, pause, clone and roll back agents; change schedule, autonomy, model, tools and prompt; view execution history; test an agent; and run it manually. |
| FR-111 | Humans shall be able to: pause all agents, pause one agent, pause one workflow, cancel a task, reschedule a task, change priority, replace agent output, approve/reject, run manually, disable an integration, and trigger **emergency stop**. |
| FR-112 | Emergency stop shall prevent all pending external write actions, including already-approved items not yet executed, and shall require an owner to release. |
| FR-113 | The Error Center shall show failed workflows, failed tasks, tool errors, integration errors, AI validation errors, authentication failures, retries and dead-letter jobs, with safe retry for authorised users. |
| FR-114 | Repeatedly failing tasks shall move to a dead-letter state with failure reason, inputs, attempt count and last error, and shall not retry indefinitely. |
| FR-115 | All major management views shall provide filtering and search (leads by score/geography/industry/status/campaign/owner/date; tasks by agent/workflow/status/date/priority/campaign/error state), plus a global search across leads, companies, campaigns, tasks, agents, content, workflows, reports and partnerships. |
| FR-116 | The system shall support authorised export of tasks, leads, content, agent logs, KPIs and reports. |
| FR-117 | Feature flags shall gate controlled rollout (autonomous publishing, new agents, new providers, experimental workflows, new tenants) and shall be auditable. |

---

## 11. Non-Functional Requirements

| ID | Requirement |
|---|---|
| NFR-001 | Interactive read screens shall render p95 < 1.5 s at 100k task records and 50k content records. |
| NFR-002 | Approval actions shall commit p95 < 500 ms, excluding downstream execution. |
| NFR-003 | The scheduler shall dispatch a due job within 60 s of its scheduled time under normal load. |
| NFR-004 | Agent task throughput shall scale horizontally by adding worker processes without code change. |
| NFR-005 | The system shall support ≥100 concurrent workflow executions and ≥50 concurrent agent executions on the target MVP deployment. |
| NFR-006 | The platform shall be operable by one administrator; every recurring operational action shall have a runbook. |
| NFR-007 | A single AI provider outage shall not stop the platform; queued work shall resume automatically. |
| NFR-008 | All code shall be strongly typed, layered (domain / application / infrastructure / presentation), dependency-injected, linted, formatted and statically analysed in CI. |
| NFR-009 | No critical functionality shall ship as a TODO or as a mock; any mock shall be explicitly labelled and listed in the release notes. |

## 12. Agent Requirements (AI-*)

| ID | Requirement |
|---|---|
| AI-001 | Every agent shall have a formal definition: name, business role, mission, responsibilities, allowed actions, forbidden actions, required tools, required APIs, knowledge sources, input schema, output schema, memory scope, context requirements, schedule, triggers, dependencies, KPIs, escalation rules, approval requirements, failure handling, retry policy, audit requirements and system prompt. |
| AI-002 | The agent roster shall be exactly the 24 agents in doc 02 §2.2 at v1; adding an agent shall require the same approval path as a prompt change. |
| AI-003 | Every agent output that the platform consumes shall be a schema-validated structured object; the system shall not depend on parsing free-form natural language. |
| AI-004 | Schema validation failures shall trigger a bounded regeneration loop (default 2 attempts) and then escalate; invalid output shall never become trusted application data. |
| AI-005 | Agents shall interact with the outside world only through registered tools; no agent shall have shell, browser, arbitrary network or direct database access. |
| AI-006 | Every tool shall declare purpose, input schema, output schema, required permission, side effects, risk classification, audit behaviour, timeout and retry behaviour. |
| AI-007 | Tools shall be partitioned into read tools and write tools, with stronger authorisation, approval and audit requirements on write tools. |
| AI-008 | The LLM shall never perform a side effect directly: the agent proposes a structured tool request → policy engine checks → authorisation checks → approval check → execution service performs → result returns to the agent. |
| AI-010 | Agent configuration shall be versioned, including system prompt, model configuration, tools, permissions, knowledge configuration, memory configuration, output schema and policies, with lifecycle Draft → Test → Staging → Production → Retired and rollback. |
| AI-011 | Prompts shall live in a Prompt Registry with versioning, variables, testing, approval, rollback and performance comparison — never in application source. |
| AI-012 | Every important output shall record which prompt version, agent version, model and retrieved knowledge produced it. |
| AI-013 | A materially changed prompt or model shall not enter production without offline evaluation results. |
| AI-020 | Agents shall have hard limits: maximum steps, maximum tool calls, maximum execution time, maximum token budget, maximum cost, retry count and escalation threshold. On breach: stop, preserve state, log the reason, notify the responsible person. |
| AI-021 | Agent memory shall be layered: company, campaign, prospect, content, agent and performance memory, retrieved by relevance rather than by injecting unbounded history. |
| AI-022 | Performance memory shall record what worked historically and shall inform recommendations, but shall never silently modify core rules. |
| AI-030 | Judgement-based outputs shall include decision, confidence, evidence, assumptions and missing information. |
| AI-031 | External research records shall store source, URL/reference, retrieval date, publication date where available, extracted evidence, relevance and confidence; agents shall not present undated information as current. |
| AI-032 | Model-generated confidence shall not be presented as a calibrated probability unless calibration has actually been implemented and measured. |
| AI-040 | Outputs below the configured confidence threshold shall automatically escalate to human review. |
| AI-041 | Where external data conflicts, the agent shall surface the conflict rather than choosing silently. |
| AI-050 | Evaluation datasets shall exist for the Content Writer (brand alignment, factual accuracy, originality, relevance, CTA quality, compliance), Lead Qualification (ICP match, company relevance, role relevance, evidence quality, confidence) and Research agents (source quality, recency, factual consistency, relevance, citation quality). |
| AI-051 | Evaluation results shall be stored per agent version and comparable across versions. |
| AI-060 | The model gateway shall support multiple providers, select models by task class (reasoning / classification / research / embedding), and implement configurable fallback chains. |
| AI-061 | The gateway shall record provider, model, agent, workflow, task, input tokens, output tokens, cached tokens, cost, latency and result for every call. |
| AI-062 | Sensitive data shall not be sent to a fallback provider unless that provider is approved for the relevant data classification. |
| AI-063 | Agents shall not be dependent on a single vendor's agent framework; business workflows shall be owned by the platform. |

## 13. Workflow Requirements (WF-*)

| ID | Requirement |
|---|---|
| WF-001 | Workflows shall be composed of typed nodes: trigger, AI agent, API action, condition, loop, approval, delay, transformation, database action, notification, sub-workflow and failure path. |
| WF-002 | Workflow definitions shall be versioned; a running execution shall complete on the version it started with. |
| WF-003 | Workflow execution state shall be persisted: current node, completed nodes, pending nodes, context, outputs, errors, human approvals, scheduled resume time. |
| WF-004 | A workflow shall survive process restart and resume safely; no workflow shall depend on an open model conversation. |
| WF-005 | Every workflow shall declare an autonomy level, an owning objective and brand, a KPI, an expected duration and an expected AI cost. |
| WF-006 | Every workflow shall define a failure path and a retry policy; unhandled failure shall not leave external systems in an ambiguous state. |
| WF-007 | Every important workbook activity shall be representable as an executable workflow; the catalogue in doc 02 §2.1 is the v1 set. |
| WF-020 | Domain events shall include at minimum: DailyPlanningStarted, TaskAssigned, TaskCompleted, TaskFailed, ResearchCompleted, DraftReady, QARejected, ApprovalRequested, ApprovalGranted, ApprovalRejected, ContentApproved, ContentPublished, LeadQualified, OutreachPrepared, PartnershipQualified, CampaignCompleted, KpiThresholdBreached, IntegrationFailed, BudgetThresholdReached, AgentFailed. |
| WF-021 | Timers shall be anchored to the domain event that logically starts them, never to record-creation time. |
| WF-022 | Event consumers shall be idempotent; duplicate delivery shall never produce a duplicate external action. |
| WF-023 | Agents shall communicate only through structured tasks and events; free-form agent-to-agent conversation shall not be a control path. |
| WF-030 | All external write operations shall use idempotency keys and execution locks, preventing duplicate publish, duplicate outreach, duplicate CRM records, duplicate reports and duplicate approvals. |
| WF-031 | If a publishing API times out after the write succeeded, retry shall detect the prior success and shall not duplicate the post. |

## 14. Scheduler Requirements (SCH-*)

| ID | Requirement |
|---|---|
| SCH-001 | The scheduler shall support exact-time, recurring (daily/weekly/monthly), event-triggered, dependency-triggered and conditional jobs. |
| SCH-002 | The scheduler shall support pause/resume, retry with backoff, job timeout, missed-job recovery, priority and concurrency controls. |
| SCH-003 | All schedules shall be expressed in the organisation's time zone; the system shall never operate on server UTC without explicit conversion. |
| SCH-004 | The Business Calendar shall model time zone, working days, weekends, public holidays, campaign dates, content deadlines, events, product launches and blackout periods, and no job shall run on a non-working day unless explicitly marked. |
| SCH-010 | Every job execution shall record both the intended scheduled time and the actual execution time. |
| SCH-011 | A missed job shall be recoverable within a configurable window, and shall be skipped with a logged reason beyond it — never silently dropped. |
| SCH-012 | Scheduling a content workflow shall trigger the workflow, not a publish: the clock never causes an external write on its own. |
| SCH-020 | The system shall track verification expiry for platform rules, benchmarks and PR routes (default 6 months) and shall raise tasks when they go stale. |
| SCH-021 | Work queues shall be separable by class: high priority, standard, background research, external writes, approval-dependent and retry, so one expensive task cannot block the system. |

## 15. Autonomy Levels (APR-*)

| Level | Name | Definition |
|---|---|---|
| **L0** | Observe | May read and analyse; may not create or modify anything |
| **L1** | Draft | May research and create drafts and internal records; no external effect |
| **L2** | Recommend | May propose actions; every action requires human approval before execution |
| **L3** | Execute Approved Workflows | May execute actions pre-authorised by an approved workflow template within stated bounds |
| **L4** | Autonomous Within Policy | May execute low-risk operations automatically inside defined limits (read-only integrations, internal computation, enforcement of deterministic rules) |

| ID | Requirement |
|---|---|
| APR-001 | Every workflow shall carry an assigned autonomy level; execution shall be denied if an action exceeds it. |
| APR-010 | Approval rules shall be configurable per action type, channel, brand, value threshold and lead band, seeded with the matrix in doc 02 §2.4. |
| APR-011 | Changes to strategy, ICP rules, objective ranks, cadence, policies or the playbook shall always require human approval; they shall never be applied by an agent. |
| APR-012 | Content approval shall support four configurable modes: (a) Manual — everything approved; (b) Trusted Workflow — specified content types auto-publish after QA; (c) Campaign Pre-Approval — an approved campaign template permits bounded variations; (d) Emergency Lock — all external publishing stops immediately. Mode changes shall require no deployment and shall be audited. |
| APR-013 | Raising a workflow's autonomy level shall require documented evaluation evidence plus owner approval. |
| APR-014 | Approvals shall support no expiry, expire-at-time and expire-after-duration; on expiry the task shall be cancelled or returned to the agent for regeneration (e.g. a news-linked social post approved two days late is no longer relevant). |
| APR-015 | An approval item shall display: agent, proposed action, reason, content/action preview, target, risk, expected benefit, supporting research and citations, plus Approve / Reject / Edit / Request revision controls. |
| APR-016 | Editing an approved item shall capture structured feedback (wrong tone, incorrect information, poor prospect fit, duplicate idea, too generic, unsupported claim, wrong strategic priority) for evaluation and learning. |
| APR-017 | High-risk workflows shall enforce separation of duties: one agent generates, a different agent reviews, a human approves, an execution service performs. |
| APR-018 | No AI identity shall satisfy any approval requirement, including as the reviewing party for its own output. |

## 16. Knowledge / RAG Requirements (KB-*)

| ID | Requirement |
|---|---|
| KB-001 | The knowledge base shall ingest Excel, PDF, Word, PowerPoint, website content, policies, sales documents, product documents, case studies, brand guidelines and historical content. |
| KB-002 | Ingestion shall produce chunks with metadata, embeddings, source tracking, version and permissions. |
| KB-003 | Retrieval shall return citations; agents shall be able to state where each fact came from. |
| KB-004 | Retrieval shall respect permissions and organisation boundaries; an agent shall retrieve only what it is authorised to access, and never another organisation's knowledge. |
| KB-005 | When a source document changes, prior versions shall be preserved, affected chunks re-indexed, old versions marked superseded, and prior executions shall remain traceable to the version they used. |
| KB-006 | The system shall support multiple search modes — exact database lookup, full-text, semantic/vector and external web search — and the orchestration layer shall select the appropriate mode rather than sending every question to vector search. |
| KB-010 | A structured Brand System shall hold brand personality, tone, preferred terminology, prohibited terminology, positioning, target audiences, products/services, differentiators, proof points, approved claims, claims requiring evidence, CTA library, content pillars and competitor-reference policy; content agents shall retrieve it rather than rely on a large static prompt. |
| KB-011 | Approved-terminology dictionaries shall be maintained and used by the QA agent for consistency. |
| KB-012 | Competitor profiles shall record company, website, positioning, services, target market, public messaging, content themes, search presence, announcements and strategic observations. |
| KB-020 | Uploaded files shall be treated as untrusted input: file-type validation, size limits, malware scanning where infrastructure permits, parsing isolation, prompt-injection mitigation, metadata extraction and permission checks. |
| KB-021 | Instructions embedded in ingested documents or fetched web content shall never be executed as agent instructions; retrieved content shall be delivered to the model as clearly delimited data. |

## 17. Integration Requirements (INT-*)

See doc 03 for the full matrix.

| ID | Requirement |
|---|---|
| INT-001 | Official APIs first: where an official API exists for the required action, no alternative mechanism shall be used. |
| INT-002 | Where a platform's current automation policy or API capability is uncertain, the platform documentation shall be verified before implementation, and the verification date recorded. |
| INT-003 | No connector shall implement scraping, bulk automation, or any action prohibited by the target platform's terms. |
| INT-004 | Where automation is unavailable or prohibited, the system shall provide a human-assisted workflow with prepared content, an execution confirmation step and evidence capture. |
| INT-010 | Every connector shall declare the fields listed in doc 03 §3.5, including restricted operations it must refuse to expose. |
| INT-011 | Integrations shall report health as Connected / Degraded / RateLimited / AuthenticationExpired / Unavailable / Disabled, and agents shall check health before attempting work. |
| INT-012 | Rate limits shall be managed centrally per organisation, integration, endpoint, credential and time window; agents shall never call an external API directly. |
| INT-013 | As limits approach, the system shall queue work, slow execution, use cached data where safe, and alert if operations are affected. |
| INT-014 | Incoming webhooks shall verify signatures, record the raw event, deduplicate, normalise, process asynchronously, retry safely and keep an audit record, acknowledging quickly rather than performing AI work synchronously. |
| INT-015 | Integrations shall be abstracted behind interfaces (social publisher, CRM connector, analytics connector, search provider, email provider, AI model provider) so providers can be replaced without touching business logic. |
| INT-020 | CRM integration, when enabled, shall define initial sync, incremental sync, conflict resolution, external IDs, last-synchronised timestamp, webhooks where available, duplicate prevention and deletion/archive behaviour, and shall never blindly overwrite CRM records from AI-generated assumptions. |
| INT-021 | The system shall reconcile revenue against PCI platform order references and shall mark unreferenced conversions as unverified. |

## 18. Analytics Requirements

Covered by FR-090…FR-098 and the KPI catalogue (doc 04), plus:

| ID | Requirement |
|---|---|
| KPI-020 | The system shall never present correlation as attribution, and shall label attribution confidence explicitly. |
| KPI-021 | The system shall report which activities produce results, which agents produce value, which campaigns perform, which channels work, which content generates engagement, which activity creates leads, which activity contributes to opportunities/revenue, what each agent costs and the approximate return. |
| KPI-022 | Every external data source shall expose freshness (e.g. "GA4 updated 15 minutes ago", "Search Console updated yesterday"). |
| KPI-023 | The Executive AI shall disclose stale data when relevant to the answer. |
| KPI-030 | Data-health checks shall be enforced at write time; any residual non-zero count shall block publication of the executive report until acknowledged. |

## 19. Reporting Requirements (RPT-*)

| ID | Requirement |
|---|---|
| RPT-001 | Daily Brief published each working morning by the configured time (default 08:00 org time). |
| RPT-002 | End-of-Day Report published each working evening. |
| RPT-003 | Weekly Growth Review published on the configured week-start day. |
| RPT-004 | A one-page management summary suitable for forwarding upward, mirroring the workbook's `Summary` sheet. |
| RPT-005 | Every report shall state its data sources and freshness, and shall not contain a number without lineage. |
| RPT-006 | Reports shall be retained and comparable over time. |

## 20. Security Requirements (SEC-*)

| ID | Requirement |
|---|---|
| SEC-001 | Authentication via an approved identity provider with MFA readiness; sessions shall be short-lived, revocable and bound to device/IP metadata. |
| SEC-002 | Authorisation shall be role-based with organisation-scoped permission checks on every request, enforced server-side. |
| SEC-003 | Organisation isolation shall be enforced at the data layer (row-level security or equivalent), not only in application code. |
| SEC-010 | All traffic shall use TLS; data at rest shall be encrypted, including database, object storage and backups. |
| SEC-011 | Secrets shall be stored in a credential vault; the database shall hold credential *references* only. |
| SEC-012 | API keys and OAuth tokens shall support rotation without downtime, with expiry monitoring. |
| SEC-013 | **No secret shall ever be included in a prompt or returned to a model.** Agents request abstract tool actions (e.g. `publish_content(content_id)`); the execution layer resolves credentials. |
| SEC-020 | Rate limiting, request-size limits, input validation, CORS policy, CSRF protection where applicable and secure headers shall be applied to all APIs. |
| SEC-021 | Field-level write authorisation shall prevent modification of computed or manager-owned fields by unauthorised principals (replacing the workbook's sheet protection). |
| SEC-030 | Agents shall receive least-privilege tool access; a content-writing agent shall have no access to financial, credential or CRM-write tools. |
| SEC-031 | The system shall prevent an AI agent from invoking any external write tool unless the agent has explicit tool permission and all configured policy and approval requirements are satisfied. |
| SEC-032 | Any denied tool invocation shall be logged with agent, agent version, tool, arguments summary, denying rule and correlation ID, and shall raise a security alert when the pattern is repeated. |
| SEC-033 | Prompt-injection defences shall separate instructions from data, sanitise retrieved content, and refuse instruction-shaped content originating from external sources. |
| SEC-034 | Where code execution or advanced tools are required, execution shall be sandboxed with resource limits, network restrictions, execution timeout, file-system restrictions, logging and per-task isolation. |
| SEC-035 | Agents shall not be able to modify their own permissions, policies, budgets, approval rules or security configuration. |
| SEC-036 | An agent identity shall never satisfy an approval requirement. |
| SEC-040 | Data classification (Public / Internal / Confidential / Restricted) shall govern model access, tool access and provider routing. |
| SEC-041 | Personal data (prospects, customers, users, interactions, uploaded documents) shall have defined retention and deletion mechanisms and shall be exportable and erasable on request. |
| SEC-050 | Environments shall be separated (local, test, staging, production); production credentials shall never be used outside production; test-environment agents shall be incapable of publishing to or contacting real audiences, using sandbox connectors where available. |

## 21. Audit Requirements (AUD-*)

| ID | Requirement |
|---|---|
| AUD-001 | Every meaningful AI action shall record: agent, agent version, prompt version, user/request, trigger, input, retrieved knowledge (with document versions), model, output, tool calls, approval, execution result, error, cost and timestamp. |
| AUD-002 | An administrator shall be able to answer "why did the system do this?" for any action from the audit record alone. |
| AUD-003 | Audit events shall be immutable or strongly tamper-resistant, with append-only storage and integrity verification. |
| AUD-004 | Audit search shall filter by date, user, agent, workflow, action, entity, integration, result and risk level. |
| AUD-005 | A correlation ID shall link one business workflow across services, agents, tool calls and model calls. |
| AUD-006 | The Operations Timeline shall present a human-readable chronological record of the day's agent activity suitable for management review. |
| AUD-007 | For significant AI decisions the interface shall offer a "why" explanation showing evidence, rules applied, relevant KPIs, knowledge sources and assumptions — **without exposing hidden chain-of-thought**. |

## 22. Compliance Requirements (CMP-*)

| ID | Requirement |
|---|---|
| CMP-001 | Where a workbook instruction conflicts with a platform's permitted automation, the system shall surface the conflict to administrators and shall not implement a workaround. |
| CMP-002 | The ten Golden Rules shall be implemented as deterministic, testable policies (doc 01 §1.4). |
| CMP-003 | The fifteen QA & Compliance checks shall exist as controls with owner, frequency, evidence, result and human sign-off; an unsigned control shall report as failing, exactly as the workbook does today. |
| CMP-004 | The system shall never generate or permit content claiming an individual has been awarded, selected, approved or guaranteed a certification. |
| CMP-005 | Fee statements shall require manager approval and shall be issued in writing before any decision is requested. |
| CMP-006 | No communication shall be permitted to a suppressed, declined or unsubscribed contact, on any channel, for any brand. |
| CMP-007 | Consent and lawful basis shall be recorded at capture for every contactable person; processing without it shall be blocked. |
| CMP-008 | Personal data shall be stored only in approved locations with defined retention; prospect research data shall be subject to erasure requests. |
| CMP-009 | Reviews shall never be incentivised, gated or internally authored; the system shall not model any such flow. |
| CMP-010 | Canonical rules shall be enforced as hard gates on publication, not warnings. |
| CMP-011 | Community rules shall be recorded per community and checked before any answer is prepared. |
| CMP-012 | Every external campaign link shall carry a UTM and land on one of the five approved domains. |
| CMP-013 | Claims about PCI shall be evidenced from approved sources before publication. |
| CMP-014 | Employee Score data shall be treated as restricted HR data with limited RBAC, shall never be used for an automated employment decision, and shall retain its activity gate (no grade below the configured data threshold). |
| CMP-015 | Agent-produced work shall be attributed to the agent, not to a human, in every scorecard and audit record. |

## 23. Data Requirements (DAT-*)

| ID | Requirement |
|---|---|
| DAT-001 | The platform, not the spreadsheet, shall be the operational store for tasks, leads, content, partnerships, links, experiments, KPIs, approvals and audit. |
| DAT-002 | The data model shall include at minimum the entities listed in §14 of the governing brief, plus workbook-derived entities: Objective, Brand, Platform (estate), PlatformAccount, MessageTemplate, ArticleBrief, SEOCluster, ContentSchedule, ComplianceCheck, ComplianceAttestation, SuppressionEntry, BusinessCalendarEntry, VerificationRecord. |
| DAT-003 | All entities shall carry organisation ownership, timestamps, created-by/updated-by, and soft deletion where appropriate. |
| DAT-004 | Enumerations (platforms, areas, activity types, content types, outcomes, statuses, lead segments, org types, templates, objectives, brands) shall have a single canonical source with referential integrity; a value shall not be selectable if it is invisible to reporting. |
| DAT-005 | Per-platform logging/dedup rules from the `Lists` sheet shall be enforced so the same work is never counted twice. |
| DAT-006 | Validation shall be server-side and authoritative; client-side validation shall never be the only check. |
| DAT-007 | Dates and quantities shall be strongly typed; text dates, future dates and negative quantities shall be impossible to persist. |
| DAT-010 | Every KPI number shall be traceable to raw observations (data lineage). |
| DAT-011 | Records shall not be hard-deleted by ordinary users; corrections shall be edits with version history. |
| DAT-020 | Systems of record shall be defined and respected (doc 03 §3.6); the platform shall not create competing copies of business-critical data without synchronisation rules. |

## 24. Availability / Reliability Requirements

| ID | Requirement |
|---|---|
| NFR-020 | Target availability 99.5% for the web application during business hours in the organisation's time zone. |
| NFR-021 | Scheduled work shall be durable: a restart shall not lose, duplicate or silently skip a job. |
| NFR-022 | RPO ≤ 15 minutes; RTO ≤ 4 hours for the MVP deployment. |
| NFR-023 | Database backups daily with point-in-time recovery; object storage and configuration backed up; prompt and agent-version history backed up; recovery tested at least quarterly. |
| NFR-024 | Runbooks shall exist for: AI provider outage, database outage, queue failure, external API outage, authentication expiration, publishing failure, duplicate execution, unexpected AI cost spike, prompt-injection incident, compromised credential, bad agent release, incorrect mass-generated content. |

## 25. Performance Requirements
Covered by NFR-001…NFR-005.

## 26. Scalability Requirements

| ID | Requirement |
|---|---|
| NFR-030 | The architecture shall support extraction of the agent runtime and workers into separately scaled services without redesign. |
| NFR-031 | The data model shall be multi-tenant-ready from day one (organisation key on every row) without premature sharding. |
| NFR-032 | Content and knowledge storage shall scale to ≥100k documents and ≥5M chunks without architectural change. |

## 27. Observability Requirements (OBS-*)

| ID | Requirement |
|---|---|
| OBS-001 | Infrastructure observability: CPU, memory, queue depth, database health, external API latency. |
| OBS-002 | Application observability: errors, requests, jobs, workflow failures, dead letters. |
| OBS-003 | AI observability: model calls, latency, tokens, cost, tool calls, retrieval sources, validation failures and evaluation signals. |
| OBS-004 | Structured logs, metrics and traces with correlation IDs spanning services, agents and model calls. |
| OBS-005 | Alerting on: agent failure rate, approval backlog age, integration health degradation, KPI threshold breach, budget threshold, data-health non-zero, dead-letter growth. |

## 28. AI Cost-Control Requirements (COST-*)

| ID | Requirement |
|---|---|
| COST-001 | Administrators shall set daily and monthly AI budgets, plus per-agent and per-workflow budgets and model restrictions. |
| COST-002 | The system shall record cost per model execution and aggregate by day, agent, workflow, campaign, organisation and model. |
| COST-003 | When a budget threshold is reached, low-priority AI executions shall pause and the administrator shall be notified; critical workflows shall be configurable as exempt. |
| COST-004 | Premium reasoning models shall not be used for trivial tasks; the gateway shall enforce model-class rules per task type. |
| COST-005 | Budget increases shall require administrator approval. |
| COST-006 | The system shall recommend cost optimisations (cheaper model for a classification step, caching a repeatedly reused research output, trimming redundant prompt context) without silently degrading workflow quality. |
| COST-007 | The system shall compute approximate ROI where data supports it, and shall decline to compute it where it does not. |

## 29. UX Requirements (UX-*)

| ID | Requirement |
|---|---|
| UX-001 | The application shall present as an AI Workforce Command Center, prioritising clarity, information hierarchy, operational awareness, actionability, fast approvals, confidence and auditability. |
| UX-002 | The home screen shall show: today's plan, live operations, approval inbox, agent workforce status (Idle/Working/Waiting/Blocked/Failed/Completed), growth KPIs, and system health. |
| UX-003 | Each agent shall display name, role, current task, status, today's completed tasks, success rate, cost, last error and next scheduled run. |
| UX-004 | An Operations Timeline shall render the day's events in plain language. |
| UX-005 | Mobile shall fully support approvals, notifications, dashboard, agent status, daily report and emergency pause. |
| UX-006 | The interface shall meet modern accessibility guidelines: keyboard navigation, semantic components, sufficient contrast, screen-reader support, focus states, accessible forms. |
| UX-007 | Every screen shall define empty, loading and error states. |
| UX-008 | A command palette shall be evaluated during Phase 3 for power-user actions. |
| UX-009 | Notifications shall be configurable for: approval required, agent failed, workflow blocked, integration disconnected, KPI threshold breached, high-value lead discovered, partnership opportunity discovered, content ready, budget threshold reached, daily report ready — in-app first, with connectors architected for email, Slack and Teams. |

## 30. Workbook Migration Requirements (MIG-*)

| ID | Requirement |
|---|---|
| MIG-001 | Each sheet shall migrate to its determined destination: database entity, dashboard, workflow, agent configuration, schedule, KPI, knowledge source, policy, template, report, or archived reference — per the mapping in doc 05. |
| MIG-002 | The importer shall: upload workbook → detect sheets → validate structure → preview extracted data → compare against the prior version → show differences → allow administrator approval → apply approved configuration changes → preserve audit history. |
| MIG-003 | The importer shall never automatically rewrite production configuration from an uploaded spreadsheet. |
| MIG-004 | Configuration changes derived from an import shall require explicit approval (A15). |
| MIG-005 | Historic workbook content (Playbook, How-To Guides, Glossary, Benchmarks, PR Directory) shall be ingested into the knowledge base with source attribution and version. |
| MIG-006 | Every residual manual action listed in `UPGRADE NOTES` shall be represented as an onboarding task in the platform (owner assignment for 63 tasks, 15 QA owners, message approvals, roster population, vault access, cost baseline). |
| MIG-007 | The import preview shall prove row-range alignment and reject template/example rows, preventing the workbook's own dead-zone defect. |
| MIG-008 | Export back to Excel shall remain available so management can continue to receive familiar reports during transition. |

## 31. Future Phases

| Phase | Contents |
|---|---|
| **P2** | Lead intelligence at scale, CRM integration, partnership module, LinkedIn Company Page publishing, email/ESP, WhatsApp/SMS |
| **P3** | Full SEO/AEO workspace, link building, PR routes, community preparation, events |
| **P4** | Paid media (with spend guardrails), advanced attribution, Arabic/Gulf localisation pipeline |
| **P5** | Multi-tenant SaaS: tenant-specific AI policy (allowed models, tools, data residency, retention, approval thresholds, automation levels), billing, self-service onboarding |

## 32. Risks
See [07 §C Automation Risk Register](07-decisions-risks-mvp.md#c-automation-risk-register).

## 33. Assumptions

| ID | Assumption |
|---|---|
| AS-01 | The workbook analysed is the current, authoritative version of PCI AI's operating model. |
| AS-02 | The organisation will name an accountable owner (currently blank in `START HERE!B19`) before go-live. |
| AS-03 | The team will continue to perform LinkedIn actions manually; the platform supports rather than replaces them. |
| AS-04 | PCI AI controls its website/CMS sufficiently to permit API publication. |
| AS-05 | Platform capabilities recorded in the workbook as "verified August 2026" require re-verification before implementation. |
| AS-06 | Benchmarks are vendor-sourced sanity checks, not contractual targets, per the workbook's own caveat. |
| AS-07 | The PCI platform remains the ledger for orders and revenue. |

## 34. Open Questions
See [07 §B and §E](07-decisions-risks-mvp.md#b-workbook-gaps).

## 35. Acceptance Criteria

| ID | The system is accepted when… |
|---|---|
| AC-01 | The daily brief is produced automatically before 08:00 org time on 10 consecutive working days without human initiation. |
| AC-02 | A content item flows Strategy → Research → Draft → Brand QA (reject once) → revision → QA pass → Compliance → Approval → Publish → Analytics capture, with a complete audit chain. |
| AC-03 | A publishing API timeout after a successful write does not create a duplicate post. |
| AC-04 | A lead already present is detected and no duplicate is created. |
| AC-05 | An agent attempting an unauthorised tool is denied, the attempt is logged, and a security alert is raised. |
| AC-06 | An AI provider outage causes fallback where permitted, and queued work resumes on recovery. |
| AC-07 | Budget exhaustion pauses low-priority AI executions and notifies the administrator. |
| AC-08 | An attempt by Organisation A to read Organisation B's data is blocked at the data layer. |
| AC-09 | An outreach item exceeding 300 characters, lacking a personal line, or containing award language cannot enter the approval queue. |
| AC-10 | A declined contact cannot be queued for any channel or brand. |
| AC-11 | A copy of a rankable site page cannot be published to LinkedIn Articles, Substack or Vocal. |
| AC-12 | Every dashboard KPI resolves to its intended definition under a row-insert regression test, and every data-health check reads zero. |
| AC-13 | The Executive AI answers eight benchmark questions from real records with citations and refuses to answer one unanswerable question. |
| AC-14 | Emergency stop halts all pending external writes, including approved-but-unexecuted items. |
| AC-15 | Every production feature traces to a workbook requirement, a security requirement, a platform requirement, a technical requirement or an approved future requirement. |

## 36. Requirements Traceability Back to Workbook Sheets
See [05 — Traceability Matrix](05-traceability-matrix.md).
