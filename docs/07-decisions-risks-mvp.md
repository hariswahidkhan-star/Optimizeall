# 07 — Architecture Decisions, Risks, Gaps, MVP, Phase 2 Prerequisites

---

## A. Architecture Decisions That Need Approval

Each decision gives Option A, Option B, a recommendation, reasoning and trade-offs. **None should be implemented before approval.**

### D-01 — Overall backend shape

| | |
|---|---|
| **Option A** | Single modular monolith in one language |
| **Option B** | Modular monolith (platform/domain) **+ a separate agent-runtime service** (AI/RAG) |
| **Option C** | Microservices per bounded context |
| **Recommendation** | **Option B** |
| **Reasoning** | The deterministic half of this system (identity, RBAC, scheduling, approvals, policy, integrations, audit, KPIs) is ordinary transactional software that benefits enormously from one database, one transaction boundary and one deployment. The AI half has a completely different dependency profile (model SDKs, tokenizers, embedding libraries, evaluation harnesses), a different failure mode (slow, non-deterministic, expensive) and a different scaling curve. Splitting exactly once, along that seam, gets the isolation without distributed-systems tax. |
| **Trade-offs** | Two deployables and one internal contract to version. Rejected C because at one organisation and one administrator, microservices would multiply operational burden without a scaling need — and §133 of the brief already argues this. |

### D-02 — Language split

| | |
|---|---|
| **Option A** | .NET 9 platform + Python/FastAPI agent runtime |
| **Option B** | Python/FastAPI end to end |
| **Option C** | .NET 9 end to end |
| **Recommendation** | **Option A**, *conditional on team skills* — see Phase 2 prerequisite P2-01 |
| **Reasoning** | .NET gives strong typing, EF Core migrations, first-class background processing and a mature security stack for the governance core, which is where correctness matters most. Python is where the AI ecosystem actually lives (provider SDKs, evaluation tooling, document parsing, embeddings). |
| **Trade-offs** | Two ecosystems, two CI pipelines, two dependency-audit surfaces. **If PCI AI's engineering team is Python-only, Option B is the better decision** — a single-language team shipping reliably beats a theoretically optimal split nobody can maintain. This is a people question, not a technology question, and I need the answer before Phase 2. |

### D-03 — Workflow orchestration

| | |
|---|---|
| **Option A** | Temporal |
| **Option B** | Hangfire (or Quartz/Celery) + custom state machine |
| **Option C** | Build a workflow engine |
| **Recommendation** | **Option A (Temporal)** |
| **Reasoning** | The defining characteristic of these workflows is that they *wait on humans* — often for hours or days — and must survive restarts without duplicating external actions. That is durable execution: exactly Temporal's problem. Timers (day-4/day-10 follow-ups), approval waits, workflow versioning mid-flight, retry policy and visibility all come free. Building this on a job queue means reinventing all of it, badly, which is how "workflow state lives in an open AI conversation" happens. |
| **Trade-offs** | Temporal is real operational weight (server, database, UI) for one organisation. Option B is materially cheaper to run and is a defensible MVP choice if the team wants to avoid a new infrastructure dependency; the cost is that WF-003/WF-004 (persisted state, safe resume) become code you own. **Option C is rejected outright** — §134 of the brief asks for evaluation before building one, and the evaluation says don't. |

### D-04 — Queueing and messaging

| | |
|---|---|
| **Option A** | PostgreSQL transactional outbox + Temporal task queues; Redis for cache and rate limiting only |
| **Option B** | RabbitMQ / Azure Service Bus as the event backbone |
| **Recommendation** | **Option A** |
| **Reasoning** | Domain events must be published atomically with the state change that caused them, or WF-022 (idempotent consumers) becomes guesswork. An outbox in the same database gives that for free. At this volume — hundreds to low thousands of tasks per day — a broker adds a moving part without adding throughput anyone needs. |
| **Trade-offs** | Outbox polling adds latency (seconds, not milliseconds — irrelevant here). Revisit at multi-tenant scale. |

### D-05 — Vector search

| | |
|---|---|
| **Option A** | pgvector in the primary PostgreSQL |
| **Option B** | Dedicated vector database |
| **Recommendation** | **Option A** |
| **Reasoning** | KB-004 (permission-scoped retrieval) and KB-005 (version-aware retrieval) require joining chunks to documents, organisations, roles and versions. In pgvector that is a `WHERE` clause; in a separate store it is a distributed consistency problem. At ≤5M chunks pgvector is comfortably sufficient. |
| **Trade-offs** | Index build times and memory pressure on the primary database at large scale; mitigated by a separate read replica if needed. |

### D-06 — Authentication provider

| | |
|---|---|
| **Option A** | Microsoft Entra ID (OIDC) |
| **Option B** | Auth0 / Clerk |
| **Option C** | Self-hosted identity |
| **Recommendation** | **Option A** |
| **Reasoning** | The workbook already lives in SharePoint/OneDrive (`START HERE` sharing rule 1), so the organisation is on Microsoft identity. Reusing it gives MFA, conditional access and joiner/leaver control immediately — and joiner/leaver control is an explicit workbook concern (`Accounts Register`). |
| **Trade-offs** | Ties the MVP to Microsoft identity; SaaS multi-tenancy later needs a broker. Option C rejected — never build identity. |

### D-07 — Deployment target

| | |
|---|---|
| **Option A** | Docker Compose on a single managed VM, then Azure Container Apps |
| **Option B** | Kubernetes from day one |
| **Recommendation** | **Option A** |
| **Reasoning** | One organisation, one administrator, predictable load. §131 asks for `docker compose up` to work locally; making that the same shape as production reduces the number of things that can be broken in only one environment. |
| **Trade-offs** | A later migration to Kubernetes if the SaaS phase arrives. That migration is cheap if services are stateless and configuration is externalised — both of which are already requirements. |

### D-08 — Cloud provider

| | |
|---|---|
| **Option A** | Azure |
| **Option B** | AWS |
| **Recommendation** | **Option A** if D-06 selects Entra ID; otherwise no strong preference |
| **Reasoning** | Identity, storage and the existing document estate are already Microsoft. Data residency for Gulf/UK operations must be checked against the chosen region — this is an open question (Q-07). |
| **Trade-offs** | Provider lock-in at the infrastructure layer; mitigated by containers and S3-compatible storage abstraction. |

### D-09 — Model providers

| | |
|---|---|
| **Option A** | Primary Anthropic + secondary OpenAI, with an embeddings provider, all behind the gateway |
| **Option B** | Single provider |
| **Recommendation** | **Option A** |
| **Reasoning** | AI-060 requires task-class routing and fallback; §122 requires fallback chains. Two approved providers is the minimum that makes fallback real. Model *classes* — not model names — should be referenced in agent configuration so upgrades are configuration changes. |
| **Trade-offs** | Two vendor relationships, two sets of rate limits, and SEC-040 data-classification routing rules to maintain. |

### D-10 — Agent orchestration framework

| | |
|---|---|
| **Option A** | Direct provider SDKs + platform-owned orchestration |
| **Option B** | LangGraph / Semantic Kernel / Agents SDK |
| **Recommendation** | **Option A** |
| **Reasoning** | AI-063 is explicit: business workflows must be owned by PCI AI's platform, not trapped in a framework. Temporal already owns control flow; a second orchestration layer with its own opinions about state, retries and memory would compete with it. Use provider SDKs for the model call, and the platform for everything around it. |
| **Trade-offs** | More code for tool-calling loops and structured-output validation. That code is small, and it is exactly where the audit and policy hooks must live anyway. |

### D-11 — Social scheduling approach (interim)

| | |
|---|---|
| **Option A** | Native platform schedulers + the workbook's recommended free stack (Metricool Free for 9 networks) |
| **Option B** | Build direct API publishing for every social platform |
| **Recommendation** | **Option A for v1**, direct API only for the website/CMS and (later) LinkedIn Company Page and YouTube |
| **Reasoning** | Doc 03 §3.4 shows most platforms schedule natively with real limits; building ten write connectors would consume the entire MVP budget to automate the *least* valuable part of the chain. The valuable part is deciding what to publish and proving it is safe. |
| **Trade-offs** | Coverage tracking depends partly on human confirmation until direct connectors exist. |

### D-12 — Multi-tenancy strategy

| | |
|---|---|
| **Option A** | Single database, `organization_id` on every row, PostgreSQL row-level security |
| **Option B** | Database per tenant |
| **Recommendation** | **Option A** |
| **Reasoning** | Meets SEC-003 and NFR-031 without operational multiplication, and makes cross-org queries impossible by construction rather than by developer discipline. |
| **Trade-offs** | Noisy-neighbour risk and a heavier migration story at scale; acceptable for one organisation plus a small number of future tenants. |

### D-13 — CRM strategy

| | |
|---|---|
| **Option A** | Build the lead engine inside the platform, integrate a CRM later |
| **Option B** | Integrate a CRM from day one as the lead system of record |
| **Recommendation** | **Option A for MVP, with the entity model designed for B** |
| **Reasoning** | No CRM is named in the workbook (gap G-009), and `UPGRADE NOTES` #18 only says the lead log "belongs in a CRM" at sustained full-team volume. Building an integration to an unchosen product is not possible; building the pipeline with external-ID fields and sync-ready contracts is. |
| **Trade-offs** | A future migration of lead ownership. Mitigated by INT-020 being specified now. |

### D-14 — Frontend

| | |
|---|---|
| **Option A** | Next.js (App Router) + TypeScript + server components |
| **Option B** | SPA + separate API only |
| **Recommendation** | **Option A** |
| **Reasoning** | Dashboard-heavy, read-mostly, permission-sensitive screens benefit from server rendering; approvals and mobile approval flows benefit from fast first paint. |
| **Trade-offs** | Node runtime in the deployment; acceptable. |

---

## B. Workbook Gaps

Things the software needs that the workbook does not define. **These are not invented here — they are listed for decision.**

| ID | Gap | Impact if unresolved | Needed from PCI AI |
|---|---|---|---|
| G-001 | **No named accountable owner** (`START HERE!B18/B19` blank) | Approval routing, compliance sign-off and escalation have no destination | Name the owner and the deputy |
| G-002 | **Roster is a placeholder** — headcount, names, roles, time zones unknown | Capacity model, target expectation and task assignment cannot be computed | Real roster with roles and time zones |
| G-003 | **63 Master Tasks have no owners or due dates** | Nothing can be scheduled or escalated | Owner + due date per task (a management act the workbook explicitly leaves open) |
| G-004 | **15 QA checks have no owners**; check 15 self-reports FAIL | Compliance attestation cannot be signed | Owner per check |
| G-005 | **Message Bank approvals unsigned** (`Approved by` / `Approved date` blank) | FR-030 has no valid approver record | Manager signs each template; new ones enter as Draft |
| G-006 | **No fee schedule** for honorary certification or credentials | M7 responses and A02 approvals cannot be pre-checked | Written fee positions per credential |
| G-007 | **No revenue targets or price points** | Dashboard §8 records revenue with nothing to measure against; ROI (COST-007) is uncomputable | Targets per credential per period |
| G-008 | **Channel cost baseline empty** | Cost per meeting, revenue per £ of cost and budget guardrails cannot be set | Monthly cost per tool/channel |
| G-009 | **No CRM selected** | D-13 remains provisional | Decision, or explicit deferral |
| G-010 | **No ESP, webinar, badge, or analytics vendor named** — only categories | Connectors cannot be built | Vendor per category |
| G-011 | **Website/CMS platform and API capability unknown**; SEO Clusters pillar URLs empty | The anchor integration (T1) cannot be specified | CMS name, hosting, API access, URL structure |
| G-012 | **Data-retention periods undefined** (QA check 14 says "only where agreed" — the agreement is missing) | SEC-041 and CMP-008 cannot be configured | Retention per data class; legal basis per market (UK GDPR, EU GDPR, Saudi PDPL, UAE) |
| G-013 | **No definition of a "qualified" agent-generated lead** vs a human-researched one | Scorecards mix populations | Policy decision |
| G-014 | **LinkedIn seat model unknown** (free vs Premium vs Sales Navigator, how many seats) | Volume targets in the workbook may be unreachable; `PLATFORM GUIDE` row 5 flags this explicitly | Seat inventory |
| G-015 | **No brand asset/claims evidence store exists yet** | CMP-013 and FR-044 have nothing to verify against | Approved claims + evidence library |
| G-016 | **Arabic/localisation ownership undefined** (Playbook 19 requires native speakers) | FR-050 cannot be operationalised | Named translator/reviewer resource |
| G-017 | **Approval SLA expectations undefined** | Approval expiry defaults (A01–A20) are my proposal, not PCI's | Confirm or amend |
| G-018 | **AI budget undefined** | COST-001 cannot be configured | Monthly AI budget ceiling |

---

## C. Automation Risk Register

Classification: **Low / Medium / High / Prohibited-or-Redesign**.

| ID | Process | Risk | Why | Mitigation / redesign |
|---|---|---|---|---|
| R-001 | Automated LinkedIn connection requests, DMs, comments, profile scraping | **PROHIBITED** | Golden Rule 5, QA check 5, Playbook step 3, technique 15 — and LinkedIn's own terms. Accounts are banned permanently; the workbook says a restricted account "cannot be recovered" | Never build. Human-assisted queue (WF-012/013). The platform's value is research, personalisation, sequencing, compliance, measurement |
| R-002 | Mass AI content generation from 5,683 briefs | **HIGH** | Playbook 15 names "AI-generated thin content at scale" as exactly what 2025–26 core updates demote. The Article Bank is a *supply* of briefs, not a mandate to publish them all. Publishing at brief-bank scale would damage the domain the strategy depends on | Configurable editorial throughput cap; mandatory expert human edit; semantic-similarity rejection (FR-043); indexation-rate monitoring; publish-rate tied to measured quality, not to brief availability. **Recommend ≤2–3 publications/week initially, matching the workbook's own cadence, not 5,683** |
| R-003 | Automated posting to Reddit / Quora / DEV / Stack Exchange | **PROHIBITED** | Explicit anti-promotion terms; DEV removes marketing articles; Reddit shadowbans identical answers; bans are permanent | Draft-only, human posts, per-community link ratios, rules recorded per community |
| R-004 | Auto-publishing content because a schedule fired | **HIGH** | The scheduler firing is not evidence the content is correct, accurate or still relevant | SCH-012: the clock triggers a workflow, never a write. Publication is the tail of QA → compliance → approval |
| R-005 | AI stating fees, legitimacy or certification outcomes | **HIGH** | Golden Rules 1 and 2; QA checks 1, 3, 13; reputationally and potentially legally actionable | Banned-phrase gate + claim classifier + mandatory manager approval + four-eyes; no fee statement without a written approved position (G-006) |
| R-006 | Contacting a suppressed or declined person | **HIGH** | Golden Rule 7; QA check 4; legal exposure under UK/EU/Gulf rules | Deterministic suppression enforced at queue-entry across all channels and brands; never a model judgement |
| R-007 | Prospect research and enrichment (personal data) | **MEDIUM–HIGH** | Prospect lists are personal data (QA check 14). Multi-jurisdiction: UK GDPR, EU GDPR, Saudi PDPL, UAE | Lawful basis recorded at capture; purpose limitation; retention schedule; erasure workflow; restricted access; no sensitive-category inference |
| R-008 | Automated email / WhatsApp / SMS sending | **MEDIUM** | Consent, deliverability and platform rules; buying lists is illegal and kills deliverability (How-To row 18) | Consent required; SPF/DKIM/DMARC gate; frequency caps; same-day unsubscribe propagation |
| R-009 | Programmatic geo × role page generation | **MEDIUM** | Playbook 22 warns this is the exact thin-content spam pattern unless each page has genuine local substance | Keyword-row backing required; batches of 10; 30-day indexation gate; rewrite-or-noindex rule |
| R-010 | Press release distribution | **LOW–MEDIUM** | Links are nofollow by policy; value is corroboration only; openPR limits to 1 per 30 days | Rate-aware scheduling; own-newsroom-first ordering; no keyword stuffing |
| R-011 | Review solicitation | **MEDIUM** | Incentivised or gated reviews are illegal in several jurisdictions and get profiles removed (QA check 9) | Never model incentives or gating; solicitation is a human action against genuinely satisfied customers |
| R-012 | Agent modifying strategy, ICP rules or objective ranks from observed performance | **HIGH** | "The system learns" must never mean the AI rewrites the business rules | Learning is bounded: store results, analyse patterns, generate recommendations, adjust only pre-authorised configuration; strategy changes go to approval (APR-011); policies and permissions are never self-modifiable (SEC-035) |
| R-013 | Uncontrolled AI spend | **MEDIUM** | 5,683 briefs × premium reasoning models is a large, easily triggered bill; research agents with web access can loop | Hard per-agent/workflow/day/month budgets; model-class routing; step/tool/time/token/cost ceilings (AI-020); pause-on-threshold |
| R-014 | Prompt injection via researched web pages, uploaded documents or reply text pasted by staff | **HIGH** | Research agents read hostile content by definition; reply-triage input is externally authored | Instruction/data separation; retrieved content delimited and never executed; tool allow-lists; deny-by-default write tools; SEC-032 alerting on denied invocations |
| R-015 | Duplicate external actions on retry | **MEDIUM** | Duplicate posts, duplicate outreach, duplicate CRM records — all named in the brief and observable in practice | Idempotency keys + execution locks + provider-side dedup checks (WF-030/031) |
| R-016 | KPI misreporting | **MEDIUM** | Already happened in the workbook: a row insert made the revenue tile display meetings (`UPGRADE NOTES` #45) | KPIs by stable identifier; one definition per KPI; lineage on every number; tile-identity regression test |
| R-017 | Applying an employee scoring engine to humans | **MEDIUM** | Employment-law and fairness exposure if an automated score drives decisions | CMP-014: restricted RBAC, activity gate retained, never an automated employment decision, human review mandatory |
| R-018 | Agent-generated work credited to humans (or vice versa) | **MEDIUM** | Corrupts the scorecards the workbook uses for coaching and pay conversations | CMP-015: actor type is first-class; separate human and agent scorecards |
| R-019 | Over-reliance on vendor benchmarks as targets | **LOW** | The workbook itself warns they skew to heavy automated senders | Thresholds carry provenance; the organisation's own four-week trend supersedes them |
| R-020 | Stale platform rules | **MEDIUM** | Every platform fact in the workbook is stamped "Aug 2026 — re-verify 6-monthly"; acting on an expired rule can breach ToS | SCH-020 verification expiry with tasks and warnings; connectors carry a verification date |
| R-021 | Emergency stop failing to catch approved-but-unexecuted items | **HIGH** | An approval granted before a problem is discovered would otherwise still fire | FR-112: emergency stop cancels pending external writes including approved items |
| R-022 | Single AI provider outage halting all operations | **MEDIUM** | Provider outages are routine | Fallback chains with data-classification routing; queue and resume; alert if deadline-critical |

---

## D. Recommended MVP

**Principle:** the MVP must prove the architecture on the two lanes with the best ratio of business value to platform risk — and must not begin with the lane the workbook forbids automating.

The workbook's value ranks put certification sales first, but the *mechanism* for certification sales is LinkedIn outreach, which is human-executed by policy. So the MVP delivers **the full governed loop end-to-end on content/SEO (where the platform can act) and the full preparation loop on outreach (where the platform must not act)** — proving planning, agents, approvals, execution, measurement, cost and audit in one vertical slice each.

### MVP scope

| # | Capability | Why it is in the MVP |
|---|---|---|
| 1 | Organisation, business calendar, users, RBAC, Entra ID auth | Nothing else is safe without it |
| 2 | Workbook importer with diff + approval; knowledge base seeded from the workbook | The workbook is the source of truth; this is how it stays that way |
| 3 | Agent registry + versioning + prompt registry + Agent Control Center | Agents must be governable from day one, not retrofitted |
| 4 | Scheduler + business calendar + durable workflow engine | The "wakes up by itself" requirement |
| 5 | Model gateway with two providers, task-class routing, fallback, full cost accounting | Cost and vendor risk are day-one risks |
| 6 | Policy & compliance engine (10 Golden Rules + the enforceable QA checks) | Every other feature depends on it |
| 7 | Approval inbox with the full item view, edit/reject/revise, structured feedback, expiry | The human-in-the-loop spine |
| 8 | **Content lane:** CSTRAT → CWRITE → CQA → COMP → approval → PUB to the website/CMS → analytics capture | The one lane the platform can legitimately execute end to end |
| 9 | **Outreach lane (preparation only):** LEAD qualification + scoring + dedup + suppression → OUT drafting from approved templates → COMP → approval → human send queue → confirmation → measurement | The highest-value lane, in the only safe shape |
| 10 | Analytics: GA4 + Search Console + UTM minting; KPI engine with lineage; data-health validation | Numbers must be defensible before they are quoted |
| 11 | Executive command centre: today's plan, live operations, approvals, agent workforce, KPIs, costs, system health, operations timeline | The "open it in the morning" experience |
| 12 | Daily brief, end-of-day report, weekly review | The management rhythm the workbook already runs |
| 13 | Complete audit trail + error centre + emergency stop | Non-negotiable |

### Explicitly deferred from the MVP

Partnerships module, PR routes, community preparation, link building, events, email/ESP, WhatsApp/SMS, job postings, paid media, CRM sync, Arabic localisation, YouTube/social write connectors, AEO audit automation, experiments UI, multi-tenant self-service. All are specified; none is required to prove the architecture.

### MVP success test

For ten consecutive working days, with no manual initiation: the daily brief lands before 08:00; the plan reflects objective value ranks and yesterday's KPIs; at least one content item completes the full chain including a QA rejection and revision; the outreach queue is populated with compliant, personalised, in-limit items and nothing non-compliant reaches it; every KPI on the dashboard drills through to raw observations; data-health checks read zero; AI cost per output is recorded and within budget; and every external action has a complete audit chain answering "why did the system do this?".

---

## E. Phase 2 Prerequisites

Phase 2 (Architecture) cannot start without the following. Items marked **blocking** genuinely prevent design; the rest can be assumed with a stated default and corrected later.

| ID | Needed | Type | Default I will assume if not provided |
|---|---|---|---|
| P2-01 | **Engineering team composition and language competence** (drives D-02) | **Blocking** | — |
| P2-02 | **Approval or amendment of D-01…D-14** | **Blocking** | — |
| P2-03 | **Website/CMS platform, hosting, API access and URL structure** (G-011) | **Blocking** — it is the MVP's anchor integration | — |
| P2-04 | Named owner + deputy (G-001) | **Blocking** for approval routing | — |
| P2-05 | Cloud provider, region and data-residency constraints for UK/EU/Gulf personal data (G-012, D-08) | **Blocking** for security architecture | — |
| P2-06 | Roster with roles and time zones (G-002) | High | 1 manager + 3 operators, org time zone |
| P2-07 | Owners and due dates for the 63 Master Tasks and 15 QA checks (G-003, G-004) | High | Assign all to the owner |
| P2-08 | Monthly AI budget ceiling (G-018) | High | To be proposed with a costed estimate in Phase 2 |
| P2-09 | Vendor decisions: ESP, webinar, badge platform, analytics (G-010) | Medium — deferred to P2/P3 modules | Category-level interfaces only |
| P2-10 | CRM decision or explicit deferral (G-009) | Medium | Defer; build sync-ready |
| P2-11 | Fee positions and revenue targets (G-006, G-007) | Medium | Approval gate blocks all fee statements until provided |
| P2-12 | Approved claims and evidence library (G-015) | Medium | All material claims blocked pending evidence |
| P2-13 | LinkedIn seat inventory (G-014) | Medium | Assume free accounts; flag that workbook volume targets are then unreachable |
| P2-14 | Retention periods per data class (G-012) | Medium | 24 months for prospect data, 7 years for commercial records — **to be confirmed with legal** |
| P2-15 | Confirmation of the approval SLAs and expiry windows in doc 02 §2.4 (G-017) | Medium | As proposed |
| P2-16 | Decision on the content throughput cap (R-002) | High | 2–3 published items/week, matching the workbook's own cadence |

---

## F. Challenges to the brief's assumptions

Raised as required by §56 of the governing instructions. **Problem → Why it matters → Recommended solution.**

1. **The brief's example schedule ("10:00 content agents work on scheduled content") implies clock-driven publishing.**
   *Why it matters:* the workbook's Content Scheduler is a coverage *tracker*, not a trigger; publishing on a clock is how unreviewed content reaches the public.
   *Solution:* adopted in SCH-012 — the clock triggers a workflow, never an external write. The brief's own §7 says the same; this makes it a hard requirement.

2. **The brief asks for a "LinkedIn Agent". The workbook forbids the actions such an agent would perform.**
   *Why it matters:* building it would breach Golden Rule 5, QA check 5 and LinkedIn's terms, risking permanent account loss — the single largest operational risk in this business.
   *Solution:* the LinkedIn lane is preparation-only, with a human send queue. This is not a reduced feature: qualification, personalisation, sequencing, compliance and measurement are ~90% of the labour and 100% of the leverage.

3. **The Article Bank's 5,683 briefs read like a throughput target. Using them that way would execute the exact anti-pattern the workbook documents.**
   *Why it matters:* Playbook 15 names AI-generated thin content at scale as demoted by current core updates; the whole compounding-authority strategy depends on the domain's quality signal.
   *Solution:* R-002 — a configurable editorial cap, mandatory expert edit, similarity rejection, and indexation-rate gating. Throughput rises only as measured quality holds.

4. **The brief's data model treats leads as platform-owned. The workbook says the PCI platform is the ledger and a CRM should own leads at scale.**
   *Why it matters:* competing systems of record produce two different revenue numbers and destroy trust in the dashboard — the exact failure the workbook's data-health section exists to prevent.
   *Solution:* DAT-020 and doc 03 §3.6 define systems of record explicitly; conversions require a PCI order reference; the lead model is built sync-ready (D-13).

5. **The brief asks for agent "confidence" as a first-class signal.**
   *Why it matters:* model-reported confidence is not calibrated probability, and treating it as such produces false safety.
   *Solution:* AI-032 — confidence is used for routing and escalation only, and is never presented as a probability unless calibration is implemented and measured.

6. **The brief proposes .NET or Python as an architectural choice. It is primarily a staffing choice.**
   *Why it matters:* a split-stack system maintained by a single-stack team accumulates an unmaintained half.
   *Solution:* D-02 is conditional on P2-01 — I need the team composition before recommending.

7. **The brief's cost controls focus on model spend; the workbook's economics are cost-per-meeting.**
   *Why it matters:* optimising AI cost in isolation can degrade the output that produces meetings, making the real unit economics worse.
   *Solution:* COST-007 plus K-078 — cost is reported per unit of *output*, and optimisation recommendations must not degrade workflow quality (COST-006).

8. **The brief's Employee Score carry-over deserves scrutiny.**
   *Why it matters:* once agents perform much of the work, a human scorecard built on outreach volume measures the wrong thing, and an automated score influencing employment decisions carries legal risk.
   *Solution:* CMP-014 and CMP-015 — restricted access, no automated employment decisions, actor-type separation, and a distinct agent scorecard that measures quality and rejection rate rather than volume.

9. **Every platform fact in the workbook is stamped "verified August 2026" with a 6-monthly re-verify obligation.**
   *Why it matters:* implementing against an expired fact can breach a platform's current terms.
   *Solution:* SCH-020 — verification expiry is a first-class scheduled obligation, and INT-002 requires re-verification before implementation.

10. **The brief asks for a workflow builder, an Executive AI, a knowledge base, 24 agents and 13 workspaces in the same product.**
    *Why it matters:* attempting all of it at once produces a prototype presented as enterprise software — explicitly listed in §53 as what not to build.
    *Solution:* the MVP in §D proves the spine on two lanes. Everything else is specified, sequenced and deferred.
