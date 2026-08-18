# 04 — KPI & Scheduling Catalogue

## 4.1 KPI Catalogue

Every KPI below exists in the workbook. Each carries: definition, formula, source, frequency, target, thresholds, owner. **Rule `KPI-004`: a KPI is referenced by stable identifier, never by sheet position** — the workbook's own worst live defect (`UPGRADE NOTES` #45) was a Dashboard tile silently displaying the wrong KPI after a row insert.

### A. Outreach funnel (Dashboard §3, Weekly Pulse, Team Scorecard, Benchmarks)

| ID | KPI | Formula | Source | Freq | Target | Warning | Critical |
|---|---|---|---|---|---|---|---|
| K-001 | Leads researched & logged | count(Lead) | Outreach | Daily | 30/person/day; 150/week | <80% | <50% |
| K-002 | Connection requests sent | count(connection_sent) | Outreach + Daily Entry | Daily | 20/person/day | <80% | <50% |
| K-003 | Connections accepted | count(accepted) | Outreach | Daily | — | — | — |
| K-004 | **Acceptance rate** | K-003 / K-002 | Outreach | Weekly | 30–45% | <27% (2026 avg) | **<20% — stop and fix targeting, profile or note** |
| K-005 | First messages sent | count(message_sent) | Outreach | Daily | 15/person/day | <80% | <50% |
| K-006 | Positive replies | count(outcome ∈ {Interested, Info Requested}) | Outreach | Weekly | — | — | — |
| K-007 | **Reply rate** | K-006 / K-005 | Outreach | Weekly | 15%+ (strong); ~10% average | <10% | <5% |
| K-008 | Meetings booked | count(meeting_date ≠ null) — **counted by meeting date** | Outreach + Community & PR | Weekly | — | — | — |
| K-009 | Applications started / converted | count(funnel ≥ 7) | Outreach | Weekly | — | — | — |
| K-010 | Declined — do not contact | count(outcome = Declined) | Outreach | Continuous | — | — | any re-contact = **critical breach** |
| K-011 | **Messages over 300 characters** | count(len > 300) | Outreach | Continuous | **0** | ≥1 | ≥1 (compliance breach; −2.5 Employee Score each) |
| K-012 | Follow-ups due today or earlier | count(follow_up_due ≤ today ∧ no outcome) | Outreach | Daily | 0 | >0 | — |
| K-013 | Meetings per accepted connection | K-008 / K-003 | derived | Monthly | ~2% floor (Benchmarks) | — | — |

### B. Content & SEO (Dashboard §5, Content Calendar, SEO Clusters, Content Scheduler)

| ID | KPI | Formula | Source | Freq | Target |
|---|---|---|---|---|---|
| K-020 | Content items planned | count(ContentItem) | Content Calendar | Weekly | per Content Scheduler |
| K-021 | **Content published** | count(status ∈ {Published, Repurposed}) — **one definition only** (`UPGRADE NOTES` #46) | Content Calendar | Weekly | 2/person/day; 10/week |
| K-022 | Total impressions | Σ impressions | Content Calendar | Weekly | — |
| K-023 | Total engagements | Σ engagements | Content Calendar | Weekly | — |
| K-024 | **Engagement rate** | K-023 / K-022 | derived | Weekly | ≥2% (LinkedIn 2–4% avg, 5%+ good) |
| K-025 | Clicks to PCI | Σ clicks | Content Calendar | Weekly | — |
| K-026 | Leads from content | Σ leads | Content Calendar | Weekly | — |
| K-027 | **Schedule coverage** | published_in_window / planned_posts per schedule | Content Scheduler | Weekly | 100%; **<100% = slipping, fix before Friday** |
| K-028 | Active schedules | count(status = active) | Content Scheduler | Weekly | — |
| K-029 | GSC impressions / clicks / position per cluster row | GSC API | SEO Clusters | Monthly | Easy keywords top-10 in 90 days |
| K-030 | Backlinks gone live | count(Link Building where date_live ≠ null) | Link Building | Weekly | ~5 outreach/week → live links |
| K-031 | Referring domains trend | GSC links report + earned-live rows | SEO | Monthly | rising |
| K-032 | **AI brand-mention rate** | mentions / 20 audit prompts | AEO audit | Monthly | rising |
| K-033 | Orphan pages | crawl | SEO | Quarterly | **0** |
| K-034 | Indexation rate (programmatic batches) | indexed / shipped | GSC | Per batch (30-day gate) | 100% |

### C. Community, partnerships, PR, events

| ID | KPI | Formula | Source | Freq | Target |
|---|---|---|---|---|---|
| K-040 | Community answers | count(Community & PR rows, substantive) | Community & PR | Weekly | 3–5/person/week |
| K-041 | Partnership / PR contacts approached | count(approaches) | Partnership Pipeline + Community & PR | Weekly | 5/person/day; 25/week |
| K-042 | Partnership pipeline value (open) | Σ potential value where not signed | Partnership Pipeline | Monthly | — |
| K-043 | Enterprise value signed | Σ deal_value where contract_signed_date ≠ null | Partnership Pipeline | Monthly | — |
| K-044 | Partnership meetings booked | count(meeting_date) | Partnership Pipeline | Weekly | — |
| K-045 | Linked placements / month | count(PR placements with link) | Community & PR | Monthly | 1 article + 1 podcast/month |
| K-046 | Journalist responses | count(responses) | Community & PR | Weekly | 3–5/week |

### D. Direct channels & hiring

| ID | KPI | Formula | Source | Freq | Target |
|---|---|---|---|---|---|
| K-050 | Email campaigns sent (recipients) | Σ how_many | Daily Entry ('Email campaign sent') | Weekly | fortnightly sends |
| K-051 | Email open / click / unsubscribe | ESP API | ESP | Per campaign | >35% open, >2.5% click, <0.3% unsub |
| K-052 | WhatsApp/Telegram/SMS delivered | Σ how_many | Daily Entry | Weekly | ≤1 broadcast/week; opt-outs <1% |
| K-053 | Job posts published | count | Job Postings | Weekly | every role on 3+ platforms |

### E. Execution, capacity and estate

| ID | KPI | Formula | Source | Freq | Target |
|---|---|---|---|---|---|
| K-060 | Task completion rate | complete / total (63 baseline) | Master Tasks | Weekly | 100% |
| K-061 | Blocked tasks | count(status = Blocked) | Master Tasks | Daily | **0** |
| K-062 | Platform coverage | live_and_complete / 133 | Platform Setup | Weekly | 100% within 6 weeks |
| K-063 | **Live accounts without 2FA** | count(status live ∧ 2FA ≠ Yes) | Platform Setup | Daily | **0 — security** |
| K-064 | Days with activity logged | count(distinct days) | Daily Entry | Weekly | working days |
| K-065 | Minutes logged | Σ minutes | Daily Entry | Weekly | — |
| K-066 | **Attainment vs target** | actual / (daily_target × person_days × headcount) | derived | Weekly | ≥100% (`UPGRADE NOTES` #3) |
| K-067 | Platform attention flags | days_since_last_activity vs value rank | Platform Progress | Weekly | no high-value platform gone quiet |
| K-068 | **Share of minutes vs value rank** | minutes per objective ÷ total, compared to rank | Objective Performance | Weekly (Monday) | high-value objectives get high share |

### F. Commercial & cost

| ID | KPI | Formula | Source | Freq | Target |
|---|---|---|---|---|---|
| K-070 | Certification revenue recorded | Σ revenue where purchase_date ≠ null | Outreach | Monthly | — |
| K-071 | Total revenue recorded | K-070 + K-043 | derived | Monthly | — |
| K-072 | Converted rows missing a PCI order ref | count | Outreach | Continuous | **0 — unverified revenue** |
| K-073 | Monthly channel cost | Σ Channel Costs | Channel Costs | Monthly | budget |
| K-074 | Cost per meeting | K-073 / K-008 | derived | Monthly | falling |
| K-075 | Revenue per $1 of monthly cost | K-071 / K-073 | derived | Monthly | >1.0 |
| K-076 | Net position | K-071 − one month channel cost | Summary | Monthly | positive |
| **K-077** | **AI operating cost** *(new — not in workbook)* | Σ ModelExecution.cost | Model gateway | Daily | within budget |
| **K-078** | **Cost per published item / per prepared outreach item** *(new)* | AI cost ÷ output count | derived | Weekly | falling |

### G. Data health — all must read zero (Dashboard §9)

| ID | Check | Becomes |
|---|---|---|
| K-090 | Rows with an unreadable (text) date | typed column + write-time validation |
| K-091 | Future-dated rows | constraint |
| K-092 | Accepted without a logged connection request | state-machine invariant |
| K-093 | Minutes logged under a name not on the roster | FK to Actor |
| K-094 | Content marked Published with no published date | invariant |
| K-095 | Published with a date but not marked published | invariant |
| K-096 | Signed deals with no deal value | invariant |
| K-097 | Duplicate leads | unique identity resolution |
| K-098 | Published content with no platform named | FK not null |
| K-099 | Impossible (negative) values | CHECK constraint |

**`KPI-030`: the platform reports these as prevented-at-write, and any non-zero value blocks publication of the Executive report until acknowledged.**

### H. Agent scorecard (new, mirroring Employee Score's philosophy — quality over volume)

| ID | KPI |
|---|---|
| K-100 | Tasks completed / attempted per agent |
| K-101 | Human rejection rate of agent output |
| K-102 | Human edit distance on approved output |
| K-103 | Retry rate; tool failure rate |
| K-104 | Average cost and latency per task |
| K-105 | Evaluation score per agent version |
| K-106 | Compliance denials triggered (attempted unauthorised actions) — target **0** |

## 4.2 Scheduling Catalogue

All times are **organisation time zone** with the org's week-start setting (`START HERE!B22`: 2 = Monday, 1 = Sunday for Gulf teams). `SCH-010`: every job stores both **intended scheduled time** and **actual execution time**.

### Daily (working days only, per Business Calendar)

| Time | Job | Workflow | Autonomy | Notes |
|---|---|---|---|---|
| 07:00 | Data health sweep | WF-002 | L4 | Must complete before planning |
| 07:15 | Analytics collection (GA4/GSC/Clarity/platform) | WF-042 | L4 | Freshness stamped |
| 07:30 | Market intelligence scan | (MKT) | L1 | Competitor + industry + AI trend |
| 07:45 | **Daily growth planning** | WF-001 | L2 | Reads KPIs, value ranks, backlog, capacity, budget |
| 08:00 | **Daily brief published** | WF-003 | L1 | Yesterday's results, today's priorities, KPI alerts, approvals pending |
| 09:00 | Lead discovery & qualification batch | WF-010 | L1 | Target 30/person/day equivalent |
| 09:30 | Personalisation lines | WF-011 | L1 | Each with cited evidence |
| 10:00 | Outreach preparation (connection notes, M1) | WF-012 | L2 | Into approval inbox |
| 11:00 | Outreach preparation (first messages, M2) | WF-012 | L2 | Triggered by human-confirmed acceptances |
| 11:30 | Reply triage | WF-014 | L2 | Same-day rule |
| 12:00 | Follow-up timers fire (day 4 → M4, day 10 → M5) | WF-015 | L2 | Max 2, ever |
| 13:00 | Content production batch | WF-020 | L2 | Draft → QA → compliance → approval |
| 14:00 | Engagement target preparation | (LI prep) | L1 | Suggested posts + draft comments for humans |
| 15:00 | Publishing window (approved items only) | WF-020/021 | L2/L3 | Idempotent; respects native scheduler limits |
| 16:00 | Partnership & PR research batch | WF-030/032 | L1/L2 | 25/week pacing |
| 17:00 | Activity reconciliation | WF-002 | L4 | Agent work auto-logged; human prompts issued |
| 18:00 | **End-of-day report** | WF-004 | L1 | Tasks, content, leads, outreach, KPIs, cost, blockers, tomorrow |
| Twice daily | Journalist-request monitoring | WF-033 | L2 | 2-hour response SLA (Playbook 4) |

### Weekly

| When | Job | Source |
|---|---|---|
| Monday 07:30 | **Weekly Growth Review**: this week vs last vs 4-completed-week average; value rank vs share of minutes; per-person minutes; verdicts | Weekly Pulse, Objective Performance, Team Scorecard |
| Monday 09:00 | Rebalance: reallocate effort where a high-value objective is under-served | Objective Performance |
| Weekly | GSC query pull → SEO Clusters positions | Playbook 13 |
| Weekly | GA4 acquisition by channel → Weekly Review | Playbook 13 |
| Weekly | Link Building: 10 new prospects, 5 outreach, follow-ups cleared | Playbook 23 |
| Weekly | Schedule coverage check (before Friday) | Content Scheduler |
| Weekly | QA & Compliance weekly checks (1,2,3,4,5,10,11,12,15) | QA sheet |
| Friday 15:00 | Weekly Review pack for human notes | Weekly Review |

### Fortnightly / monthly

| When | Job |
|---|---|
| Fortnightly | LinkedIn Newsletter issue; Substack issue (if native); email newsletter |
| Monthly | AEO 20-prompt audit; entity-fact parity check (Playbook 3, 14) |
| Monthly | Unlinked-mention reclaim sweep (Playbook 23) |
| Monthly | 1 pillar page; 5 pages restructured for AEO; 1 contributed article; 1 podcast pitch |
| Monthly | Channel Costs update; cost-per-meeting review |
| Monthly | QA & Compliance monthly checks (7,8,9,13,14) |
| Monthly | Workbook/archive snapshot; data retention pass |
| Monthly | P1 keyword SERP re-check |

### Quarterly / annual

| When | Job |
|---|---|
| Quarterly | Competitor backlink gap analysis; orphan-page crawl; directory citation verification; keyword re-verification; one free tool/calculator; one CC-licensed primer + Zenodo DOI; one geo×role batch (indexation-gated); dead-tactics review |
| Every 6 months | **Re-verify every platform rule, benchmark and PR route** (`Lists!W` "Aug 2026 — re-verify 6-monthly"; `Benchmarks!F`; `PR & Target Directory!A2`) — `SCH-020` makes this a first-class recurring obligation with expiry warnings |
| Annually | "State of Project Controls & AI" survey → report → DOI → press (Playbook 17) |

### Event-triggered

| Event | Reaction |
|---|---|
| `ConnectionAccepted` (human-confirmed) | Queue M2 preparation within 24h |
| `ReplyReceived` | Reply triage same day |
| `KpiThresholdBreached` (e.g. acceptance <20%, organic traffic drop) | Diagnostic workflow + orchestrator re-plan |
| `ContentPublished` | Analytics capture schedule at +1d, +7d, +30d |
| `IntegrationFailed` / `AuthenticationExpired` | Block dependent jobs; notify; queue work |
| `BudgetThresholdReached` | Pause low-priority AI executions; notify admin |
| `PlatformWarningReceived` | **Stop that channel for the day; escalate same day** (Golden Rule 6; Playbook safety row) |
| `LeadDeclined` | Permanent suppression across all channels/brands |
| `ScheduleCoverageBelowTarget` | Raise remediation task before Friday |
| `AgentFailedRepeatedly` | Disable workflow, preserve state, alert |
