# 01 — Workbook Analysis

**Artefact analysed:** `PCI_AI_Growth_OS.xlsx`
**Sheets:** 42 · **Largest sheet:** Article Bank (5,683 briefs) · **Total populated rows across log sheets:** ~13,000 (mostly empty template rows awaiting first use)
**State at time of analysis:** the workbook is a *fresh, unrun instance*. Every log tab is empty; the Dashboard reads zero everywhere; the roster holds one placeholder ("Employee 1"). The workbook self-declares this on `Dashboard!A3` ("FIRST RUN — every figure below is zero because nothing has been logged yet").

## 1.1 What the workbook actually is

The workbook is **not** a marketing spreadsheet. It is a complete, self-consistent **operating system for a growth department**, already containing:

- a **constitution** (`START HERE` §4 Golden Rules, `QA & Compliance` 15 controls),
- a **strategy layer** (`GROWTH PLAYBOOK` 23 techniques with cadence, KPI and anti-patterns),
- an **estate model** (`Platform Setup` / `PLATFORM GUIDE` — 133 platforms across 13 areas, each value-ranked 1–133),
- a **work breakdown** (`Master Tasks` — 63 workstreams with frequency and weekly target),
- an **execution protocol** (`LinkedIn Playbook` A–C + steps 1–10 with clock times; `Summary` §"THE DAILY RHYTHM WE EXPECT" 09:00→17:00),
- a **content supply chain** (7 SEO pillars → 76 keywords → 5,683 article briefs each carrying a ready AI writing prompt → Content Calendar → Content Scheduler coverage),
- **approved copy with enforced limits** (`Message Bank` M1–M15, 200/300-character caps, Status/Approved-by/Approved-date),
- a **measurement layer** (Dashboard 9 sections, Weekly Pulse 14 KPIs, Objective Performance by campaign and by brand, Team Scorecard, Employee Score out of 100),
- a **data-quality layer** (`Dashboard` §9 DATA HEALTH — 10 named integrity checks that must all read zero),
- and a **changelog with audit evidence** (`UPGRADE NOTES` — 47 findings across six audit passes, each with severity and residual manual action).

**Consequence for the software:** the platform's job is *not* to invent a growth strategy. It is to (a) hold this operating model as governed configuration, (b) execute the deterministic parts reliably, (c) apply AI only where judgement is genuinely required, and (d) preserve every control the workbook already encodes. The workbook is the specification; the software is the enforcement.

## 1.2 The organisation the workbook describes

| Attribute | Value | Source |
|---|---|---|
| Organisation | PCI AI — Project Controls Institute | `START HERE!A1` |
| Brands / properties (7 tag values) | PCI AI - Institute (umbrella); PCL-AI certification; PFL-AI certification; PML-AI certification; PCI World; Certuvo (exam prep); All / shared | `Lists!O4:O10`, `START HERE` §7 |
| Web domains (5) | projectcontrolsinstitute.org (main institute); pciai.org (short + email, admin@pciai.org); pciglobal.ai (global/AI brand); pciworld.org (PCI World community); mypci.org (candidate portal) | `START HERE` §11 |
| Programme start | 2026-08-17 (a Monday) | `START HERE!B20` |
| Working days / week | 5 | `START HERE!B21` |
| Week-start convention | 2 = Monday, configurable to 1 = Sunday for Saudi/Qatar/Kuwait/Bahrain | `START HERE!B22` |
| Team model | Manager (owner, cell B19) + up to 10 roster slots | `START HERE` §2, §6 |
| Core commercial motion | "Honorary certification" outreach to senior practitioners → criteria → application → certification revenue; plus enterprise/association partnerships | `Message Bank` M2, `LinkedIn Playbook`, `Dashboard` §8 |
| Priority markets | UK, US, Gulf (KSA/UAE/Qatar), India, Australia | `Platform Setup!Q`, `Keyword Plan`, `GROWTH PLAYBOOK` 19 |
| Competitors named | AACE, PMI, RICS, APMG, Project Control Academy, EVMi, **projectcontrolsinstitute.com** (a training provider that currently outranks PCI for its own name) | `GROWTH PLAYBOOK` 23, `Keyword Plan!A2` + P1 list |

## 1.3 The eleven objectives, ranked by business value

`Objective Performance!A4:K14` — this is the workbook's own prioritisation function and must become the orchestrator's objective function.

| Value rank | Objective (campaign tag) | Workbook rationale |
|---|---|---|
| 1 | Certification Sales - PCL-AI | Revenue driver — flagship credential |
| 2 | Certification Sales - PML-AI | Revenue driver — premium tier |
| 3 | Certification Sales - PFL-AI | Revenue driver — entry funnel |
| 4 | Honorary Certification Outreach | Strategic pipeline — fellows legitimise, refer, open doors |
| 5 | Partnerships & PR | Multiplier — one deal moves whole cohorts |
| 6 | Authority & Entity Building | Compounding — lifts every other conversion |
| 7 | Content & SEO Growth | Compounding — owned traffic that keeps paying |
| 8 | Events & Webinars | Pipeline builder — concentrated lead capture |
| 9 | Certuvo (Exam Prep) | Adjacent revenue + feeder into certifications |
| 10 | Community Presence | Trust builder — slow burn, defends reputation |
| 11 | General Brand Awareness | Support — spend spare capacity only |

Every logged row in every tab carries **two mandatory tags**: `Objective` (which campaign) and `For (brand)` (which property). Untagged rows fall into a red `(no objective set)` line reviewed each Monday. **This is a first-class data-model requirement, not a reporting nicety.**

## 1.4 The ten Golden Rules (the policy engine, verbatim intent)

`START HERE` §4. These are not guidance — the workbook treats breaches as "a serious issue", and several map directly to live checks on `QA & Compliance` and to Employee Score deductions.

| # | Rule | Software consequence |
|---|---|---|
| 1 | Never tell anyone they have been awarded, approved or guaranteed an honorary certification — you are inviting them to be *considered* | Hard content guardrail; banned-phrase classifier; four-eyes on outreach |
| 2 | Never invent facts about PCI (member numbers, accreditations, recognition, partnerships) | Claim-verification layer against an approved evidence store |
| 3 | Personalise every message with something real from the profile | `Personal line` is a required field; empty ⇒ send blocked |
| 4 | Outreach < 300 chars; connection notes < 200 chars | Deterministic validation before an item can enter the send queue |
| 5 | **No bulk sending, no automation tools, no bots, no scraping** | **Prohibits any automated LinkedIn action; forbids scraping connectors** |
| 6 | Respect limits; stop at the daily target; stop for the day on any platform warning | Rate-limit + circuit-breaker service, per platform per identity |
| 7 | If someone says no or asks to stop — thank them, never contact again, mark Declined | Suppression list, permanent, enforced at queue-entry time |
| 8 | Answer the question first, promote PCI second (Quora/Reddit/groups) | Community drafts must pass a "useful without the link" QA gate |
| 9 | Never post PCI passwords in the file — only the vault reference; 2FA everywhere | Credential vault with reference-only storage; no secret ever reaches a model |
| 10 | Log everything the same day, with evidence (URL/screenshot) | Evidence URL mandatory on completion; unevidenced work does not count |

## 1.5 The daily operating rhythm the workbook already defines

Derived from `Summary!A29:C38` and `LinkedIn Playbook!B7:B16` (organisation time zone; **not** server UTC).

| Time | Activity | Target (per person/day) | Logged to |
|---|---|---|---|
| 09:00 | Research qualified leads in Sales Navigator | 30 | LinkedIn Outreach |
| 09:30 | Write one personal line per lead from their profile | 30 | LinkedIn Outreach col L |
| 10:00 | Send personalised connection requests (M1, <200 chars) | 20 | LinkedIn Outreach col M |
| 11:00 | Message new acceptances (M2, <300 chars, within 24h) | 15 | LinkedIn Outreach cols P–R |
| 11:30 | Reply to every response the same day; escalate anything uncertain | — | LinkedIn Outreach cols S–T |
| 12:00 | Follow-ups: day 4 (M4), day 10 (M5), then stop | 10 | LinkedIn Outreach cols U–V |
| 14:00 | Engage — 15 genuine comments on target-audience posts | 15 | DAILY ENTRY |
| 15:00 | Publish content; answer on Quora/Reddit/groups | 2 posts + 3 answers | Content Calendar / Community & PR |
| 16:00 | Partnership / PR approaches | 5 | Partnership Pipeline / Community & PR |
| 17:00 | Log the day in DAILY ENTRY | all rows | DAILY ENTRY |
| Friday | Weekly Review — wins, misses, next week's focus | 1 | Weekly Review |
| Monday | Manager review: Weekly Pulse → Objective Performance → Team Scorecard col Q → Dashboard §9 Data Health | 1 | — |

Weekly per-person totals (`START HERE` §3, ×5 working days): 150 leads, 100 connections, 75 first messages, 50 follow-ups, 10 posts, 15 community answers, 25 partnership/PR contacts, 75 engagements.

**The Dashboard multiplies these by filled roster slots** (`UPGRADE NOTES` #3) — target expectation is a function of headcount, and must become a function of *active agent + human capacity* in the platform.

## 1.6 Sheet-by-Sheet Requirements Map

Legend for **Can automate** / **AI suitable** / **API required** / **Approval**: Y = yes, N = no, P = partial. **Risk**: L/M/H/P (P = Prohibited or requires redesign).

### Group A — Read-first / governance (6 sheets)

| Sheet | Purpose & business process | Tasks contained | Freq. | Role | Inputs | Outputs | KPIs | Dependencies | Platforms | Auto | AI | API | Appr. | Risk | Proposed agent | Proposed workflow | Schedule |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **START HERE** | The rulebook: team settings, daily targets, Golden Rules, roster, sharing rules, Day-1 checklist, domain map | Set team config; publish targets; enforce rules; onboard joiners | One-off + on change | Manager | Manager decisions | Org config, targets, policy set, roster, domains | Coverage of Day-1 checklist | Feeds every sheet | — | Y (as config) | N | N | Y | L | *Policy Engine* (deterministic, not an agent) | `WF-ADMIN-CONFIG` | On change |
| **MAP** | Navigation and ownership index: what each sheet is, who types on it | Route users; define who-writes-where | Reference | All | — | Information architecture | — | All sheets | — | Y | N | N | N | L | — (becomes app IA) | — | — |
| **TEAM GUIDE** | Onboarding: "I did X, where do I log it?"; scoring explanation; FAQ | Where-to-log mapping (15 rows); Day-1 sequence; daily 20-min logging habit | Reference | All | — | Activity→record routing rules | Onboarding completion | DAILY ENTRY, all logs | — | Y | P | N | N | L | Knowledge Agent (KNOW) | `WF-ONBOARD` | On joiner |
| **GROWTH PLAYBOOK** | 23 techniques: why, numbered steps, cadence, proving KPI, anti-patterns, target log sheet | SEO clusters; E-E-A-T; AEO; digital PR; LinkedIn engine; community; syndication; webinars; email; Credly loop; directories; YouTube; measurement; entity authority; **dead/dangerous tactics**; free publishing & news; original research; free tools; Arabic localisation; internal linking; employee advocacy; programmatic geo pages; off-page link engine | Per-technique (daily→annual) | All | Strategy | Executable technique definitions | Per-technique KPI (col E) | Every execution sheet | All | P | Y | P | Y | M | Multiple (technique → agent map in doc 02) | `WF-*` (one per technique) | Per technique cadence |
| **PLATFORM GUIDE** | Weekly play per platform (133 rows): logging rule, step-by-step, proving KPI, time/week | Per-platform weekly operating instruction | Weekly | Owner per platform | Platform Setup identity | Weekly play + KPI + time budget | Per-platform KPI | Platform Setup (identity by formula) | 133 | P | P | P | P | M | Channel agents | `WF-CHANNEL-WEEK` | Weekly |
| **PR & Target Directory** | ~100 named, verified routes: course directories, CPD accreditors, podcasts, publications, journalist services, expos, speaker directories, partnership targets, authority registries, startup listings, Gulf job boards, news wires, job boards — **plus a verified SKIP list with reasons** | Approach a named route; track owner/status/result | Per route | Manager | Research | Target list + verdicts + skip list | Placements/month; referring domains | Partnership Pipeline, Community & PR, Link Building | Many | P | Y | P | Y | M | PR Agent (PR), Partnership Agent (PART) | `WF-PR-ROUTE`, `WF-AUTHORITY-REGISTRY` | Monthly + 6-monthly re-verify |

### Group B — Daily work / write sheets (14 sheets)

| Sheet | Purpose | Key fields | Freq. | Role | Outputs | KPIs | Auto | AI | API | Appr. | Risk | Agent | Workflow |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **DAILY ENTRY** (1,203 rows) | The single activity ledger — one row per activity | Date, Employee, Platform, Activity type (15 values), Description, How many, Minutes, Evidence link, Result, Blocker, **Objective**, **For (brand)** | Daily | Everyone | Activity facts feeding every rollup | Minutes, activities, attainment vs target | Y | P | N | N | L | Ops Agent (OPS) auto-logs agent work; humans log theirs | `WF-ACTIVITY-LOG` |
| **LinkedIn Outreach** (1,204 rows, 42 cols) | One row per **person**: research → connect → message → reply → meeting → application → revenue | Lead identity (A–L), send/receive (M–T), follow-ups (U–W), scoring Y–AC (`ICP×12 + Intent×8`, bands A/B/C/D, funnel stage 1–9 derived), outcomes AD–AM (meeting, closer, application, certification, purchase, revenue, PCI order ref), AN duplicate flag, AO/AP tags | Daily | Everyone | Lead pipeline, funnel, revenue attribution | Acceptance %, reply %, meetings, conversions | **P — research/draft only** | Y (research, scoring, drafting) | **N for LinkedIn writes** | **Y** | **H** | Lead Intelligence (LEAD), Outreach Composer (OUT) | `WF-LEAD-DISCOVERY`, `WF-OUTREACH-PREP` |
| **Partnership Pipeline** (404 rows) | One row per **organisation**: associations, universities, employers, training partners | Org, Type (13 values), Contact, Why-them, ICP/Intent → Score/Band, Stage, Potential value, What they get, Next step + due, Deal value, Contract signed date | Weekly | Everyone | Partnership pipeline + signed value | Contacts/week (25), meetings, signed value | P | Y | P | Y | M | Partnership Agent (PART) | `WF-PARTNER-DISCOVERY` |
| **Content Calendar** (404 rows) | Every content item idea → published URL → performance | Plan date, Employee, Platform, Content type (18), Topic, Objective, Hook, CTA, Asset link, Status (7), Published date, Published URL, Impressions/Engagements/Clicks/Leads, Repurpose to, Brand | Daily/weekly | Everyone | Content records + performance | Published count, engagement rate ≥2%, clicks, leads | Y | Y | Y | Y | M | Content Strategy (CSTRAT), Writer (CWRITE), QA (CQA), Publishing (PUB) | `WF-CONTENT-PRODUCTION` |
| **Content Scheduler** (125 rows) | One row per platform × cadence × window; computes planned vs published = **coverage** | Platform, Brand, Objective, Cadence (5), Posts/cycle, Format, Tool, Start/End, Planned (auto), Published (auto), Coverage (auto), Owner, Status. **Plus reference table: which platforms schedule natively, windows and limits (rows 106–125)** | Weekly | Everyone | Schedule commitments + slippage | Coverage % (<100% = slipping) | Y | N (deterministic) | Y | N | L | Scheduler/Ops (OPS), Publishing (PUB) | `WF-SCHEDULE-COVERAGE` |
| **Community & PR** (404 rows) | Every helpful-expert appearance: forum answers, community threads, press mentions, podcasts, journalist requests | Date, Employee, Platform, Org/community, Type, Contact, URL, Why targeted, Action, Template, What you said, Status, Outcome, Follow-up due, Meeting date, Evidence URL, Next action | 3–5/week/person | Everyone | Community + PR record | Answers/week, views, upvotes, referral sessions | **P — draft only** | Y | P | **Y** | **H** | Community Agent (COMM), PR Agent (PR) | `WF-COMMUNITY-ANSWER`, `WF-PR-PITCH` |
| **Job Postings** (204 rows) | One row per platform per open role | Date, Poster, Position, Type, Brand, Platform, Country, URL, Status, Applicants, Shortlisted, Hired | Per role | Everyone | Hiring reach record | Roles on 3+ platforms; applicants | P | P | P | Y | L | Ops Agent (OPS) | `WF-JOB-POST` |
| **Link Building** (404 rows) | Off-page SEO system of record | Date, Employee, Tactic, Target site, Their page, Our target URL, Contact route, Planned anchor, Status, Link type (do/nofollow), Date live | Weekly (10 prospects, 5 outreach) | Everyone | Referring-domain pipeline | Referring domains trend; links live | P | Y | P | Y | M | Link Building Agent (LINK) | `WF-LINK-PROSPECTING` |
| **Experiments** (55 rows) | A/B tests: hypothesis → variants → samples → rates → significance guard → decision | ID, Start, Owner, Channel, Hypothesis, A/B, Metric, Samples/results/rates, Difference, **Big enough to act on?** (≥30/group and ≥5pp; <100/group = directional only), Status, Decision, Rolled into playbook | Per test | Everyone | Validated learning | Experiments concluded; playbook changes | Y | P | N | Y (to change playbook) | M | Experimentation Agent (EXP) | `WF-EXPERIMENT` |
| **UTM Builder** (105 rows) | Generates tracked links; a bare URL is invisible in analytics | Landing URL, Source, Medium, Campaign, Content, Tracked link, Used where, Date. **Historic defect: SEARCH('?') wildcard bug produced malformed links (UPGRADE NOTES #2)** | Per link | Everyone | Canonical tracked links | 100% of campaign links tagged | **Y (fully deterministic)** | N | N | N | L | Platform service (not an agent) | `WF-UTM-MINT` |
| **SEO Clusters** (103 rows) | 7 pillars × supporting articles with GSC numbers | Cluster, Pillar URL, Supporting article, Target phrase, Search intent, **Article Bank ID**, Status, Published URL, Links-to-pillar?, Republished to, Canonical set?, Impressions, Clicks, Position | Monthly refresh | SEO owner | Cluster map + search performance | Pillar position; clicks/month | Y | Y | Y (GSC) | P | L | SEO Agent (SEO) | `WF-SEO-CLUSTER-REFRESH` |
| **Keyword Plan** (93 rows) | 76 researched keywords graded Easy/Medium/Hard from 33 live SERP samples; 10 P1 attack keywords with reasons | Keyword, Cluster, Intent, Funnel, Volume band (editorial estimate — **explicitly not tool data**), Difficulty, Who ranks today, Asset to build, Priority, SERP✓, Owner, Status, Published URL, Pillar, Article Bank ID | Quarterly re-verify; P1 SERPs monthly | SEO owner | Attack order for content | Easy keywords top-10 in 90 days | P | Y | Y (GSC/SERP) | P | M | SEO Agent (SEO) | `WF-KEYWORD-REVERIFY` |
| **Article Bank** (5,683 briefs) | The editorial engine: every brief carries title, pillar, cluster (27), format (11), audience (29), funnel, effort, priority, primary + supporting keywords, word count, **and a complete AI writing prompt that bans invented statistics and demands cited sources**, plus Owner/Status/Published URL | Continuous | Writers claim rows | Ready-to-execute content briefs | Articles published; rankings | Y | **Y (core AI use case)** | N | Y | **M–H (thin-content risk)** | Content Writer (CWRITE) | `WF-CONTENT-PRODUCTION` |
| **Daily Log** (403 rows) | Optional per-day digest, fully derived from DAILY ENTRY | Date, Employee, hours, 9 activity counts, best result, blocker, targets met (of 8), day rating | Daily (auto) | Nobody | Convenience view | Targets met/day | Y | N | N | N | L | — (becomes a query, not a table) | — |

### Group C — Results / read-only calculated (10 sheets)

| Sheet | Purpose | What it computes | Consumers | Software treatment |
|---|---|---|---|---|
| **Dashboard** (117 rows, 9 sections) | The cumulative record | §1 Task execution; §2 Platform presence (incl. **live accounts without 2FA**); §3 LinkedIn funnel (acceptance/reply rates vs benchmarks, over-limit messages, follow-ups due); §4 Activity vs target × headcount; §5 Content & community output; §6 By-area completion; §7 Funnel stages 1–9; §8 Revenue & channel cost; **§9 DATA HEALTH — 10 integrity checks that must all read 0** | Manager | Executive Command Center screens + a **write-time validation service** replacing §9 after-the-fact counting |
| **Summary** (38 rows) | One page for board/investor forwarding | Six headline numbers; volume delivered; risks needing a decision; the daily rhythm | Manager, board | Daily Brief + exportable report |
| **Weekly Pulse** (36 rows) | This week vs last week vs 4-completed-week average; 14 KPIs; minutes per person; unattributed row | Early-warning trend | Manager (Monday, first stop) | KPI engine + Weekly Growth Review |
| **Objective Performance** (49 rows) | Results by campaign and by brand; person × objective minutes matrix; **value rank vs share of minutes** | Capital-allocation signal | Manager | Orchestrator objective function + Analytics workspace |
| **Team Scorecard** (16 rows, 22 cols) | Per-person outreach numbers side by side + one-sentence verdict (col Q) | Coaching | Manager | Human scorecard **and** an analogous Agent Performance Scorecard |
| **Employee Score** (15 rows) | Weighted /100: lead quality 15, acceptance 20 (target 30%), reply 20 (target 15%), positive replies 10 (target 10), meetings & applications 20 (target 5), downstream 10 (target 3), compliance 5 (−2.5 per over-length message); grades A–E; **activity gate: no grade below ~20 connections** | Reviews | Manager | Retained as human HR data with restricted RBAC; see CMP-014 |
| **Weekly Review** (35 rows) | Friday: wins, misses, next week's focus with numbers pre-filled; per-person selector; 3 selector-independent all-team trend columns | Team + manager | Weekly review workflow with agent-drafted, human-edited notes |
| **Platform Progress** (136 rows) | Per platform: entries, activity, minutes, last worked, days since, who last logged, blocked items, **Attention** flag, value rank | Finding neglected high-value platforms | Estate health view; drives orchestrator backlog |
| **Who Did What** (136 rows) | Platform × person coverage grid | Reallocation, absence cover | Coverage matrix (person **and** agent) |
| **Accounts Register** (139 rows) | Every account: URL, login, **vault entry name only**, 2FA, owner, status — derived by formula from Platform Setup | Joiner/leaver control | Credential vault references + integration registry |

### Group D — Management setup (5 sheets)

| Sheet | Purpose | Key content | Software treatment |
|---|---|---|---|
| **Master Tasks** (63 tasks) | Every workstream with area, platform, task, definition of done, priority, frequency, weekly target, owner, status, progress %, due date, evidence, objective, brand | T-001…T-063 spanning LinkedIn (13), Social (8), Publishing (12), Community (8), Directory/Review (7), Events (3), Podcast (3), Partnership/PR (6), Analytics (3) | Seeds the **Workflow Catalogue** and the recurring **Schedule** set; owner may be human *or* agent |
| **Platform Setup** (133 platforms) | Setup steps in order, profile URL, login, **vault reference**, 2FA, status, profile completeness %, setup date, owner, brand, value rank 1–133, strongest-in countries | The estate's identity + onboarding runbook | Integration registry + account onboarding workflow |
| **Publishing Plan** (10 ranked platforms) | Per platform: what it is for, outbound link behaviour, **canonical support**, the publishing rule, cadence, owner, status. **Governing rule: publish on own site first → wait for indexation → syndicate with canonical home. LinkedIn Articles, Substack and Vocal cannot set canonicals and must therefore receive original or rewritten content only** | Prevents self-cannibalisation | Deterministic publishing policy per channel (blocks a publish action, not a warning) |
| **Channel Costs** (23 rows) | Monthly cost per tool/channel; computes total cost, cost per meeting, revenue per $1 of cost | Unit economics | Cost engine (extended to AI/API spend) |
| **QA & Compliance** (15 checks) | Honesty of claims; character limits (live signal); fees in writing; declined-lead re-contact (live signal); **no automation tools/bots/scrapers**; daily send limits; 2FA; no passwords in file; genuine non-incentivised reviews; canonicals present; community rules read; UTM on every link; PCI claims evidenced; personal data stored only where agreed; **a named person has signed the checklist** | Frequency Daily/Weekly/Monthly with Owner, Last checked, Result, Action | Becomes the **Policy Engine + Compliance Agent + control attestation register** |

### Group E — Reference (7 sheets)

| Sheet | Purpose | Software treatment |
|---|---|---|
| **Message Bank** | 15 approved templates M1–M15 (+ Custom) with use-case, exact text, live character count, limit (200/300), rules, **Status / Approved by / Approved date** | Prompt & Template Registry with versioning, approval workflow, and hard length validation. *Nothing not on this sheet may be sent.* |
| **LinkedIn Playbook** | Setup A–C (profile, 5 saved searches with named filters, 3+ lead lists) then daily steps 1–10 with clock times, hard limits, and the two safety rows (platform warning → stop + escalate same day; honesty → never imply selection) | The canonical outreach workflow definition |
| **How-To Guides** | 19 workstream training rows: why it matters, first-time setup, weekly work, what good looks like, common mistakes, evidence to log, time/week — **with the honest caveat that the cadences are per active platform and do not sum to one person's week** | Agent system-prompt source material + capacity model |
| **Benchmarks** | 2026 published figures with named sources and an explicit caveat that vendor benchmarks skew to heavy automated senders: acceptance 27–30% avg / 30–45% good; reply ~10% avg / 15–25% good; note-reply ~2%; meetings per accepted connection ~2%; acceptance timing 63%/24h, 88%/week, 99%/30d; safe volume 100–150 connects/week (up to ~200 strong account); engagement rates per platform | KPI targets and thresholds — **as configuration with provenance, never hard-coded** |
| **Glossary** | ~70 terms grouped by area | Knowledge base seed + UI help text |
| **Lists** | Every dropdown's source values **plus the per-platform "logging rule (dedup)"** that prevents double counting, and a `Verified` column ("Aug 2026 — re-verify 6-monthly") | Canonical enumerations, deduplication rules, and a **verification-expiry model** |
| **UPGRADE NOTES** | 47 audit findings across six passes with severity and residual manual actions | Migration acceptance criteria — every "What the team must still do" becomes a platform requirement (see MIG-*) |

## 1.7 What the workbook proves about failure modes (mined from UPGRADE NOTES)

These are *already-observed* failures in the manual system. They are the strongest evidence for specific software controls.

| Finding | Failure | Requirement it justifies |
|---|---|---|
| #2 | UTM builder used `SEARCH('?')` — every campaign link malformed, GA4 blind | `FR-141` deterministic link minting with unit tests |
| #3 | Team targets compared against one person's target | `KPI-012` capacity-aware expectation |
| #4 | No duplicate-lead defence; two people could contact the same person, including someone who declined | `FR-072`, `CMP-006` suppression + identity resolution |
| #5 | Platform lists disagreed; 3 platforms selectable but invisible to all rollups | `DAT-004` single canonical enumeration with referential integrity |
| #8 | Follow-ups fired from the wrong date (date logged +4, not date messaged) | `WF-021` timers anchored to the *event*, not the record |
| #9 | Formula sheets unprotected — anyone could overwrite a score | `SEC-021` field-level write authorisation |
| #22 | All 54 validation rules had error alerts switched off — Excel showed the list and silently accepted anything | `DAT-006` server-side validation is the only validation that counts |
| #23 | Example rows sat in a dead zone above the reporting range | `MIG-007` import preview must prove row-range alignment |
| #45 | Adding a Weekly Pulse row silently shifted Dashboard tiles — revenue tile displayed meetings | `KPI-004` KPIs referenced by stable identifier, never by position; regression assertions |
| #46 | "Content published" meant different things on two sheets | `KPI-002` one definition per KPI, one place |
| #18 | Capacity ceilings: outreach log ~1,200 rows ≈ 8 working days at full-team volume | `DAT-001` the platform replaces the spreadsheet as the operational store |

## 1.8 Ambiguities found in the workbook (not invented, explicitly flagged)

Recorded here and carried into [07 — Workbook Gaps](07-decisions-risks-mvp.md#b-workbook-gaps).

1. `START HERE!B18/B19` (team name, manager) are blank — no named accountable owner exists yet.
2. Roster holds only the placeholder "Employee 1" — actual headcount, names and time zone per person are undefined.
3. `Channel Costs` yellow cells are empty — no budget baseline exists; cost-per-meeting is uncomputable.
4. `Master Tasks` has 63 rows with **no owners and no due dates** (`UPGRADE NOTES` #13 confirms this is a deliberate management act, not an omission by the workbook).
5. `QA & Compliance` has 15 checks with **no owners** — check 15 self-reports FAIL.
6. `Message Bank` rows are grandfathered "Approved" but `Approved by` / `Approved date` are blank.
7. `SEO Clusters` pillar page URLs are empty — the site's actual URL structure is unknown to the workbook.
8. Fee levels for the honorary certification are referenced (M7: "administrative fees") but never stated.
9. No CRM is named, though `UPGRADE NOTES` #18 states the lead log "belongs in a CRM" at full-team volume.
10. No ESP, webinar, badge or analytics *vendor* is named — only categories (`Email Marketing (ESP)`, `Zoom Webinars`, `Credly`, `GA4`, `GSC`, `Clarity`).
11. Certification price points, cohort sizes and revenue targets are absent — `Dashboard` §8 records revenue but no target exists to measure against.
12. Data-retention periods for prospect personal data are not stated (QA check 14 says "stored only where agreed" — the agreement is undefined).
