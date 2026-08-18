# 02 — Workflow & Agent Model

## 2.1 Identified Business Workflows

Every workflow below is derived from named workbook content. `Source` cites the sheet(s). `Autonomy` uses the levels defined in [06-srs §15](06-srs.md#15-autonomy-levels-apr).

### Cadence: continuous / event-driven

| ID | Workflow | Source | Trigger | Participating agents | Autonomy | Human approval |
|---|---|---|---|---|---|---|
| WF-001 | **Daily Growth Planning** | Summary §daily rhythm; Objective Performance value ranks; Weekly Pulse; Dashboard §9 | 07:30 org time, working days | OPS → ANA → DQ → MKT → ORCH | L2 | Plan is visible, not approved; individual dispatched items carry their own levels |
| WF-002 | **Data Health & Reconciliation** | Dashboard §9 (10 checks); UPGRADE NOTES #45/#46 | 07:00 daily + after every bulk write | DQ | L4 (read-only detection) | No — but blocks WF-020 report publication if any check ≠ 0 |
| WF-003 | **Executive Daily Brief** | Summary; Dashboard; Weekly Pulse | 08:00 org time | EXEC (reads ANA, ORCH, approval queue) | L1 | No |
| WF-004 | **End-of-Day Report** | Summary; Dashboard §1–§9; Channel Costs | 18:00 org time | EXEC ← all agents' task records | L1 | No |
| WF-005 | **Weekly Growth Review** | Weekly Pulse; Weekly Review; Objective Performance; Team Scorecard col Q | Monday 07:30 (org week-start setting) | ANA → EXP → ORCH → EXEC | L2 | Strategy changes require approval (APR-011) |
| WF-006 | **Friday Team Review pack** | Weekly Review | Friday 15:00 | EXEC | L1 | Humans write their own notes; agent pre-fills numbers only |

### Lane 1 — Outreach (highest business value, highest platform risk)

| ID | Workflow | Source | Steps | Agents | Autonomy | Approval |
|---|---|---|---|---|---|---|
| WF-010 | **Lead Discovery & Qualification** | LinkedIn Playbook B–C, step 1; LinkedIn Outreach A–L; Lists Lead Segment (17 values) | Saved-search segment → candidate accounts/contacts → qualification tests (8+ yrs; real current employer; visible seniority; active in last 6 months) → duplicate & suppression check → ICP(1–5)/Intent(1–5) → score `ICP×12+Intent×8` → band A/B/C/D → Lead record | LEAD | L1 (Draft) — **research only** | No (creating a lead record); yes to progress a lead to outreach |
| WF-011 | **Personalisation Line** | Playbook step 2; Golden Rule 3; Outreach col L | Retrieve evidence from permitted sources → produce one specific, true line with citation → confidence | LEAD | L1 | Included in outreach approval |
| WF-012 | **Outreach Preparation** | Message Bank M1–M15; Playbook steps 3–4; Golden Rules 1,3,4 | Select approved template → merge personal line → **hard length check (200/300)** → claim check → four-eyes QA → approval item | OUT → COMP → CQA | L2 (Recommend) | **Always** — APR-001 |
| WF-013 | **Outreach Execution** | Playbook steps 3–4; Golden Rule 5 | **Human-assisted only.** Approved item enters a human send queue with copy-ready text, target profile link, and a one-click "sent / not sent" confirmation that writes back the send timestamp | (no agent write) | L0 for the agent | Already approved; the human is the actor |
| WF-014 | **Reply Triage** | Playbook step 5; Message Bank M6/M7/M15; Outcome list (11 values) | Human pastes/relays reply → classify outcome → propose response from M6/M7/M8/M15 → escalate anything uncertain | OUT → COMP | L2 | Always; **any fee statement or legitimacy claim escalates to the manager** |
| WF-015 | **Follow-up Sequence** | Playbook step 6; Outreach cols U/V/AF; UPGRADE NOTES #8 | Timer at `message_sent_date + 4` (M4) and `+10` (M5); **maximum two, ever**; then Outcome = No Response and stop | OUT | L2 | Always (each message) |
| WF-016 | **Suppression & Decline** | Golden Rule 7; QA check 4; Outcome "Declined"/"Do Not Contact / Unsubscribed" | On decline → send M15 (approved) → permanent suppression across **all** channels and brands → block any future queue entry | COMP | L4 (enforcement) | No — suppression is never optional |
| WF-017 | **Duplicate Defence** | Outreach col AN; UPGRADE NOTES #4; TEAM GUIDE FAQ | Before any lead is created or queued: match on profile URL, email, name+company; surface the earlier record and its outcome | LEAD | L4 | No |
| WF-018 | **Handoff to Closer** | Outreach cols AG–AM; Glossary "Handoff" | Meeting booked → assign PCI closer → application → certification → purchase date → revenue → **PCI order reference required before Converted** | ORCH | L2 | Yes — commercial record |

### Lane 2 — Content & SEO (highest compounding value, safest automation)

| ID | Workflow | Source | Steps | Agents | Autonomy | Approval |
|---|---|---|---|---|---|---|
| WF-020 | **Content Production** | Article Bank; Keyword Plan; SEO Clusters; Content Calendar; Publishing Plan; Playbook 1,2 | Select brief (P1 first, Easy difficulty first) → duplicate/semantic-similarity check against prior PCI content → research with citations → draft from the brief's AI prompt → **brand QA** → **claim verification** → compliance → human approval → publish → record URL → measure | CSTRAT → CWRITE → CQA → COMP → PUB → ANA | L2 (L3 possible per APR-013 once evaluated) | Yes for all external publication; configurable per APR-012 modes |
| WF-021 | **Syndication Waterfall** | Playbook 7; Publishing Plan canonical column | Original on own site → verify indexation (2–10 days) → per-platform rule: Medium via Import (canonical auto); LinkedIn Articles/Substack/Vocal **never a copy** (no canonical support) → each copy its own Content Calendar row, status Repurposed | PUB | L3 for canonical-safe targets; L2 otherwise | Yes for non-canonical platforms |
| WF-022 | **Schedule Coverage Control** | Content Scheduler; Dashboard tiles | Compute planned vs published per schedule window; coverage <100% raises a task before Friday | OPS | L4 | No |
| WF-023 | **SEO Cluster Refresh** | SEO Clusters; Playbook 1, 20 | Monthly: pull GSC impressions/clicks/position per cluster row; detect orphan pages; propose internal links | SEO | L2 | Yes for site changes |
| WF-024 | **Keyword Re-verification** | Keyword Plan (quarterly re-verify; P1 SERPs monthly) | Re-sample SERPs for P1 keywords; re-grade Easy/Medium/Hard; flag movement on brand-defence keyword #1 | SEO | L2 | Yes to change priorities |
| WF-025 | **Answer-Engine / Entity Audit** | Playbook 3, 14 | Monthly 20-prompt audit across AI engines; record which domains are cited; check crawler allow-list, Bing/IndexNow, entity facts parity across Wikidata/Crunchbase/LinkedIn/GBP | AEO | L1 (audit) / L2 (recommendations) | Yes for site or profile edits |
| WF-026 | **Link Building** | Link Building sheet; Playbook 23 | Weekly: 10 prospects (competitor gap, unlinked mentions, resource pages) → qualify → draft outreach → approval → human sends → track to Date live + link type | LINK | L2 | Yes |
| WF-027 | **Original Research Study** | Playbook 17 | Quarterly/annual survey → analysis → report → Zenodo DOI → press pitches | MKT + CWRITE + PR | L2 | Yes |
| WF-028 | **Programmatic Geo × Role Pages** | Playbook 22 (guardrailed) | Only from a Keyword Plan row; batches of 10; **indexation-gated for 30 days** before the next batch; rewrite or noindex anything templated | SEO + CWRITE | L2 | Yes per batch |

### Lane 3 — Partnerships, PR, Community, Events

| ID | Workflow | Source | Agents | Autonomy | Approval |
|---|---|---|---|---|---|
| WF-030 | **Partnership Discovery & Scoring** | Partnership Pipeline; PR & Target Directory; How-To Guides row 15 (7 associations, 4 universities, 7 employers, 4 podcasts, 3 media / week = 25) | PART | L1 research / L2 proposal | Yes to contact |
| WF-031 | **Partnership Stage Management** | Partnership Pipeline Stage/Next step/Deal value/Contract signed | PART | L2 | Yes for commercial terms |
| WF-032 | **PR Route Working** | PR & Target Directory (publications, podcasts, expos, speaker directories) | PR | L2 | Yes |
| WF-033 | **Journalist Request Response** | Playbook 4 (SOS, Help a B2B Writer, ResponseSource, #journorequest twice daily); 80–120 words, answer within 2 hours | PR + COMP | L2 | Yes — **time-boxed approval (APR-014)** |
| WF-034 | **Community Answer Preparation** | Community & PR; Playbook 6; Golden Rule 8; platform ToS | COMM + CQA | L1 draft only | Yes; **human posts** |
| WF-035 | **Webinar → Content Engine** | Playbook 8 | EVT + CSTRAT + PUB | L2 | Yes |
| WF-036 | **Authority Registry Submissions** | PR & Target Directory (Credential Engine, CareerOneStop, D-U-N-S, ISNI, Wikidata, Tracxn/Dealroom/Magnitt) | AEO + PR | L2 | Yes — these are institutional claims |
| WF-037 | **Directory & Review Estate** | Playbook 11; How-To Guides row 14; QA check 9 | AEO | L2 | Yes; **review solicitation is never incentivised or gated** |

### Lane 4 — Direct channels, analytics, governance

| ID | Workflow | Source | Agents | Autonomy | Approval |
|---|---|---|---|---|---|
| WF-040 | **Email Nurture & Newsletter** | Playbook 9; How-To Guides row 18 (SPF/DKIM/DMARC; consent only; unsubscribe same day) | DIR + CQA + COMP | L2 | Yes per campaign; **campaign pre-approval mode available (APR-012c)** |
| WF-041 | **WhatsApp / Telegram / SMS** | How-To Guides row 19; consent recorded per contact; ≤1 value broadcast/week | DIR + COMP | L2 | Yes |
| WF-042 | **Analytics Collection** | Playbook 13; GA4 / GSC / Clarity; UTM Builder | ANA | L4 (read-only) | No |
| WF-043 | **KPI Threshold Reaction** | Weekly Pulse; Dashboard; Benchmarks | ANA → ORCH | L2 | Yes for any strategy change |
| WF-044 | **Experiment Lifecycle** | Experiments sheet incl. the ≥30/group, ≥5pp guard and the <100/group "directional only" caveat | EXP | L2 | Yes to roll into the playbook |
| WF-045 | **Cost & Budget Control** | Channel Costs; extended to AI spend | COST | L4 (enforce caps) | Yes to raise a budget |
| WF-046 | **Compliance Attestation Cycle** | QA & Compliance 15 checks (Daily/Weekly/Monthly, each with an owner and a signature) | COMP | L1 evidence gathering | Yes — a named human signs |
| WF-047 | **Platform Account Onboarding** | Platform Setup steps; Accounts Register; 2FA rule | OPS | L2 | Yes — credentials are human-held |
| WF-048 | **Workbook Import & Diff** | UPGRADE NOTES; Lists; the whole config surface | KNOW | L2 | **Always** — MIG-004 |
| WF-049 | **Job Posting Distribution** | Job Postings; PR & Target Directory job-board verdicts | OPS | L2 | Yes |
| WF-050 | **Emergency Publishing Lock** | Golden Rule 6 (platform warning → stop for the day); APR-012d | COMP/human | L4 (enforcement) | No — any admin may trigger |

## 2.2 AI Agent Workforce

24 agents in six divisions. Each is specified in full (mission, allowed/forbidden actions, tools, schemas, memory, schedule, KPIs, escalation, approval, failure handling, audit, system prompt) in Phase 5 — this document fixes the **roster, boundaries and permissions**, which is what Phase 1 must settle.

### Division 0 — Command

| ID | Agent | Business role | Mission | Reads | Writes | Never |
|---|---|---|---|---|---|---|
| **ORCH** | Chief Growth Orchestrator | AI Chief Growth Officer | Read goals + KPIs + value ranks; decide today's priorities; allocate work against capacity; resolve dependencies; escalate | Goals, KPIs, Objective Performance ranks, Platform Progress attention flags, approval backlog, budget | DailyPlan, Task assignments, escalations | Execute external actions; approve anything; change policy |
| **OPS** | Scheduler / Operations | Operations manager | Own the business calendar, recurring jobs, missed-job recovery, retries, dependencies, priority queues, concurrency, integration health gating | Schedules, calendar, integration health, queue depth | Job/Task state | Decide business priority; write to external platforms |

### Division 1 — Intelligence

| ID | Agent | Mission | Notes |
|---|---|---|---|
| **MKT** | Market & Competitor Intelligence | Competitor profiles (AACE, PMI, RICS, APMG, Project Control Academy, EVMi, projectcontrolsinstitute.com), industry and AI-in-project-controls developments, Gulf market signals, opportunity discovery | Every finding carries source, URL, retrieval date, publication date, extracted evidence, relevance, confidence (`AI-031`) |
| **LEAD** | Lead Intelligence | Discover and qualify prospects against the ICP; enrich; score `ICP×12+Intent×8`; duplicate and suppression check | **Read-only against people-data sources; no scraping connectors ever** |
| **SEO** | SEO | Keyword re-verification, cluster health, internal linking, technical SEO, ranking analysis, opportunity scoring | Evidence-driven; distinguishes technical / content / optimisation / authority / local opportunity types |
| **AEO** | Answer-Engine & Entity Authority | Monthly 20-prompt AI-citation audit; entity-graph parity; crawler allow-list; schema; registry submissions | Playbook techniques 3 and 14 |
| **ANA** | Analytics & Attribution | Collect GA4/GSC/Clarity/platform metrics; compute KPIs with lineage; distinguish **correlation from attribution** | Never fabricates attribution (`KPI-020`) |

### Division 2 — Content

| ID | Agent | Mission |
|---|---|---|
| **CSTRAT** | Content Strategy | Choose topic/pillar/cluster/campaign/audience/funnel/format/channel/CTA from Keyword Plan + Article Bank + Content Scheduler + performance memory |
| **CWRITE** | Content Writer | Produce drafts from the Article Bank brief and prompt, with citations; British English; word-count band per brief |
| **CQA** | Brand & Editorial QA | Brand voice, terminology, duplication/semantic similarity, grammar, CTA quality, audience fit, structure (FAQ block, meta description <155 chars) |
| **PUB** | Publishing & Syndication | Execute approved publication through official APIs only; enforce canonical rules and the own-site-first order; idempotent publish |

### Division 3 — Relationships

| ID | Agent | Mission |
|---|---|---|
| **OUT** | Outreach Composer | Compose from approved Message Bank templates only; enforce character limits; require a personal line with a citation |
| **PART** | Partnership | Discover, score (`ICP×12+Intent×8`), stage-manage, prepare proposals, track next actions |
| **PR** | PR & Media | Journalist requests, contributed articles, podcast pitches, press materials, expo/speaker applications |
| **COMM** | Community | Draft genuinely useful answers for Quora/Reddit/Planning Planet/PMI Community/groups; enforce "useful without the link" |
| **EVT** | Events & Webinars | Webinar topic selection, speaker invitations, listings, promotion plan, post-event content plan |
| **LINK** | Link Building | Competitor backlink gap, unlinked-mention reclaim, resource-page and broken-link prospecting |
| **DIR** | Direct Channels | Email/WhatsApp/Telegram/SMS campaign preparation with consent and frequency enforcement |

### Division 4 — Governance

| ID | Agent | Mission |
|---|---|---|
| **COMP** | Compliance & Safety | Pre-execution check of every external action against the Golden Rules, QA & Compliance checks, platform policy, suppression, consent, frequency caps, claim rules |
| **CQA** *(shared)* | (see Content) | Second pair of eyes for the four-eyes principle |
| **DQ** | Data Health | The 10 Dashboard §9 checks as continuous validation, plus reconciliation against systems of record |
| **COST** | Cost & Budget | Track model/API/channel spend; enforce daily/monthly/agent/workflow caps; recommend model downgrades that do not degrade quality |

### Division 5 — Memory & Reporting

| ID | Agent | Mission |
|---|---|---|
| **KNOW** | Knowledge | Ingest and version the workbook, brand documents, policies, standards; maintain company/campaign/prospect/content/performance memory; serve retrieval with citations and permissions |
| **EXP** | Experimentation | Register hypotheses, baselines, guardrail metrics; apply the workbook's own significance guard; never over-claim |
| **EXEC** | Executive Reporting & Executive AI | Daily brief, end-of-day report, weekly review, and the natural-language interface answering **only from system records, with citations** |

### Explicitly rejected agent designs

| Rejected | Why |
|---|---|
| "LinkedIn Automation Agent" performing connects/DMs/comments | Golden Rule 5, QA check 5, Playbook step 3 and technique 15 all forbid it; LinkedIn's own terms forbid it; no official API exists for these actions. Replaced by **WF-012/WF-013** (prepare → approve → human sends). |
| "Auto-Poster" that publishes because a clock struck | The workbook's Content Scheduler is a *commitment tracker*, not a trigger. Publishing is the tail of WF-020, gated by QA + compliance + approval. |
| A single "Growth Agent" with all tools | Violates least privilege (`SEC-030`) and makes the four-eyes principle impossible. |
| Agents holding platform credentials | `START HERE` Golden Rule 9 + Accounts Register design: vault reference only. Agents call `publish_content(content_id)`; the execution layer resolves credentials (`SEC-041`). |

## 2.3 Automation Classification

Every workbook activity classified. **Prohibited** means the platform must not build it, and the conflict is reported rather than worked around (`CMP-001`).

| Activity | Workbook source | Classification | Rationale |
|---|---|---|---|
| Lead research & qualification | Playbook step 1 | **AI-assisted, L1** | Judgement task; official/permitted data sources only |
| Personal line generation | Playbook step 2 | **AI-assisted, L1** | Must cite the profile evidence used |
| Lead scoring | Outreach AA/AB | **Deterministic** | `ICP×12+Intent×8` is arithmetic; AI supplies the two 1–5 inputs *with evidence* |
| Funnel stage | Outreach AC | **Deterministic** | Derived from outcome fields — never typed, never inferred by a model |
| Connection request send | Playbook step 3 | **PROHIBITED (automated)** | Golden Rule 5; LinkedIn ToS; no API |
| Connection note drafting | Message Bank M1 | **AI-assisted, L2** | Template merge + personalisation, approval required |
| First message / follow-ups send | Playbook steps 4, 6 | **PROHIBITED (automated)** | As above |
| Message length enforcement | Golden Rule 4; Message Bank | **Deterministic** | Hard validation, not a model judgement |
| Duplicate detection | Outreach AN | **Deterministic** | Identity resolution |
| Suppression enforcement | Golden Rule 7 | **Deterministic, L4** | Never a model decision |
| Reply classification | Outcome list | **AI-assisted, L2** | Low-confidence ⇒ escalate |
| Content ideation & brief selection | Article Bank, Keyword Plan | **AI-assisted, L2** | Ranked by value + difficulty |
| Content drafting | Article Bank prompts | **AI-assisted, L1→L2** | Prompt bans invented statistics |
| Brand/claim QA | QA & Compliance 1,13 | **AI + deterministic hybrid** | Banned-phrase lists deterministic; nuance by model |
| Website publication | Publishing Plan | **Automatable, L2/L3** | Official CMS API; idempotent |
| LinkedIn Page post publication | Platform Guide row 4 | **Automatable, L2** | Official Marketing/Community Management API with granted scopes only |
| LinkedIn personal-profile posting | Playbook step 8 | **Human, L1 prep** | Personal-profile posting is not a supported app action for this use case |
| Reddit / Quora / DEV / Stack Exchange posting | Playbook 6; Publishing Plan rank 9; How-To row 8 | **PROHIBITED (automated)** | Explicit anti-promotion terms; workbook says read every rule set, bans are permanent |
| Medium syndication | Publishing Plan rank 2 | **Automatable, L2** | Import tool / API sets canonical |
| Email campaign send | Playbook 9 | **Automatable, L2/L3** | ESP API; consent + unsubscribe enforced |
| WhatsApp/SMS send | How-To row 19 | **Automatable, L2** | Provider API; opt-in mandatory; ≤1 broadcast/week |
| Press release distribution | Playbook 16 | **Semi-automatable, L2** | PRLog/openPR have submission constraints (openPR 1 per 30 days) |
| Review solicitation | Playbook 11; QA check 9 | **Human, L2 prep** | Never incentivised, never gated |
| Analytics collection | Playbook 13 | **Fully automatable, L4** | Read-only official APIs |
| UTM minting | UTM Builder | **Fully automatable, L4** | Deterministic, unit-tested |
| KPI computation | Dashboard, Weekly Pulse | **Fully automatable, L4** | Deterministic with lineage |
| Data health checks | Dashboard §9 | **Fully automatable, L4** | Becomes write-time validation |
| Reporting | Summary, Weekly Pulse | **AI-assisted, L1** | Narrative only over verified numbers |
| Strategy change | Playbook; Objective ranks | **NEVER autonomous** | APR-011: recommendation only |
| Policy / permission change | Golden Rules; QA sheet | **NEVER autonomous** | `SEC-035`: agents cannot modify their own governance |
| Scraping any platform | Golden Rule 5; technique 15 | **PROHIBITED** | Bans are permanent; official API first (`INT-001`) |
| Paid links, PBNs, link swaps | Playbook 23 | **PROHIBITED** | Google link-spam policy; burns the domain |
| Engagement pods, AI-comment tools | Playbook 5, 15 | **PROHIBITED** | Detected and reach-penalised |
| Identical answers across threads | Playbook 15 | **PROHIBITED** | Shadowban |
| AI-generated thin content at scale | Playbook 15 | **PROHIBITED as a mode of operation** | Directly demoted by 2025–26 core updates. See risk R-002 — this constrains how the 5,683-brief Article Bank may be used |

## 2.4 Human Approval Matrix

Configurable per `APR-010`; the defaults below are derived from the workbook's own control set.

| # | Action | Default requirement | Approver role | Four-eyes | Expiry | Source |
|---|---|---|---|---|---|---|
| A01 | Any outbound message to a named individual (LinkedIn, email, WhatsApp) | Approve every item | Manager | Agent-generated + agent-reviewed + human-approved | 48h then regenerate | Golden Rules 1,3,4; Message Bank |
| A02 | Any statement about fees | Manager approval, always, in writing | Manager | Yes | 24h | M7 rule; QA check 3 |
| A03 | Any claim about PCI (members, accreditation, recognition, partnerships) | Approval + evidence citation | Manager | Yes | — | Golden Rule 2; QA check 13 |
| A04 | External publication (any platform) | Approve every item (Manual mode default) | Content owner | CQA + COMP before human | 72h for evergreen; **24h for news-linked** | Publishing Plan; APR-014 |
| A05 | Publication to a non-canonical platform (LinkedIn Articles, Substack, Vocal) | Approval + confirmation it is original/rewritten, not a copy | Content owner | Yes | 72h | Publishing Plan governing rule |
| A06 | Community answer posting (Reddit/Quora/etc.) | Approval; **human posts** | Content owner | CQA | 72h | Playbook 6; platform ToS |
| A07 | Partnership or PR communication | Approval | Manager | Yes | 7 days | Partnership Pipeline; PR Directory |
| A08 | Journalist-request response | Fast-lane approval (2-hour SLA) | Manager or delegate | COMP only | **2 hours** | Playbook 4 |
| A09 | Campaign launch | Approval of the campaign brief | Manager | Yes | — | Playbook; campaign model |
| A10 | Strategy change (objective ranks, ICP rules, cadence, playbook edits) | Approval, always | Owner (manager, `START HERE!B19`) | Yes | — | §17 learning governance; UPGRADE NOTES #37 |
| A11 | Budget increase (AI, channel, ads) | Approval | Admin | Yes | — | Channel Costs; COST-005 |
| A12 | Message Bank template change / new template | Approval; enters as Draft | Manager | Yes | — | Message Bank Status column; UPGRADE NOTES #12 |
| A13 | Agent prompt/model/tool/policy change | Offline evaluation → staging → approval | Admin | Yes | — | §95 release process |
| A14 | Deletion of records | Approval + audit | Admin | Yes | — | Sharing rule 6 ("rows are never deleted") |
| A15 | Workbook re-import applying configuration changes | Diff review + approval | Owner | Yes | — | MIG-004 |
| A16 | Integration connect/disconnect, credential rotation | Approval | Admin | — | — | Accounts Register; SEC-040 |
| A17 | Autonomy level increase for any workflow | Approval + evaluation evidence | Owner | Yes | — | APR-013 |
| A18 | Contacting a high-value prospect (band A) | Approval by manager personally | Manager | Yes | 48h | Lead bands; Playbook honesty rules |
| A19 | Anything an agent flags as low confidence | Automatic escalation to approval | Manager | — | — | AI-040 |
| A20 | Emergency stop / publishing lock release | Approval by owner | Owner | — | — | APR-012d |

**Never approvable by an agent:** A01–A20 all require a human principal. `SEC-036`: an agent identity can never satisfy an approval requirement, including as the second pair of eyes on its own output.
