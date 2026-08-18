# 15 — Screen Specifications

Twenty-five screens. Each specifies purpose, primary user, data, actions, filters, search, tables, cards, charts, notifications, empty/error/loading states, mobile behaviour and permissions.

Cross-cutting behaviour is defined once in [14 §14.6–14.7](14-information-architecture.md) and referenced here as **standard**; only deviations and screen-specific instances are spelled out.

---

## S-01 · Executive Command Center

**Purpose** — Answer, in under sixty seconds and without opening a log: what the workforce did, what needs me, what happens next. This is the screen that has to make the Phase 1 success criterion true.
**Primary user** — Owner. Secondary: Manager.

**Layout, top to bottom** — the order is the argument:

1. **Status line.** "Good morning. 24 agents operational · 43 tasks planned · 18 complete · **4 need you**." The only bolded figure is the one requiring action.
2. **Decision strip** — four tiles, each a link to work, not a statistic: *Awaiting your approval* (count + age of the oldest), *Blocked* (count + what is blocked on), *KPI alerts* (count + worst breach), *Spend today* (against daily budget).
3. **Needs you** — the merged decision list: approvals, blockers, low-confidence escalations, integration failures. Ordered by cost of delay, not by arrival.
4. **Today's plan** — collapsed by lane (Content · Outreach · SEO · Partnerships · Analytics), each showing planned / running / done / failed, expandable to tasks. Human-executed steps are marked distinctly from agent-executed steps.
5. **Live operations** — what is running now, with elapsed time and step, and a cancel control.
6. **Yesterday's results** — six KPI tiles with sparkline, delta against last week and against the four-week average, each drilling to lineage.
7. **Operations timeline** — the last 24 hours in plain language (see S-04).
8. **System health strip** — integrations, data-health checks, queue depth, dead letters.

**Data displayed** — agent count and status distribution; today's plan and completion; approval backlog with ages; blocked items; KPI values with deltas, targets and thresholds; discovered leads and partnership opportunities; content ready; SEO opportunities; integration health; yesterday's AI operating cost.

**Actions** — approve or reject inline from *Needs you*; open any item; cancel a running task; pause an agent or workflow; run a workflow manually; **emergency stop**; change date range; drill any number to lineage.

**Filters** — date (today · yesterday · last 7 days); lane; brand; objective. Filters persist per user.
**Search** — global (`⌘K`) only; this screen has no local search.
**Tables** — Today's plan (lane, agent, task, status, started, cost) and Live operations (agent, task, step, elapsed, cost so far). Both scroll within their own container.
**Cards** — decision strip tiles; KPI tiles with sparkline and threshold marker; agent-status summary card.
**Charts** — sparklines on KPI tiles; one stacked bar of tasks by state across the last 14 days. Deliberately restrained: this screen is for decisions, and analysis lives in S-17.
**Notifications** — inline banner for integration failure, budget threshold and data-health non-zero. Approval count is a badge, not a banner.

**Empty states** — *First run:* the plan panel explains that the orchestrator produces a plan each working morning and shows the next scheduled run time; KPI tiles show their definition and target with no value. *Nothing needs you:* "Nothing awaiting your approval" plus the next scheduled agent run — a genuinely good state, stated without celebration.
**Error states** — standard. Additionally: if the data-health sweep is non-zero, a banner states that the executive report is held until acknowledged, with a link to the failing checks. If planning did not run, the panel says so and offers to run it manually rather than showing an empty plan.
**Loading** — progressive: status line and decision strip first, then plan, then KPIs, then timeline. Standard skeletons.
**Mobile** — full. Vertical stack in the same order; decision strip becomes a two-by-two grid; timeline collapses to the last ten events; emergency stop reachable from the top bar.
**Permissions** — Owner and Manager see everything. Operator sees their own tasks and the shared plan, not costs or scorecards. Analyst read-only. Compliance sees health, audit and policy panels. Costs require Owner or Admin.

---

## S-02 · Approval Inbox

**Purpose** — Turn the human-in-the-loop requirement into something fast enough that it does not become the bottleneck the whole platform is judged by.
**Primary user** — Manager. Secondary: Owner, Compliance.

**Layout** — two panes. Left: the queue, grouped by *Expiring soon* → *High risk* → *Standard*, each row showing type icon, subject, agent, target, risk pill and expiry countdown. Right: the full item.

**The approval item anatomy** — every item shows all of it, always:

| Element | Why it is mandatory |
|---|---|
| Agent + version + prompt version | Attribution and rollback if a pattern of bad output appears |
| Proposed action, stated as an effect | "Publish to the website" — not "approve content #812" |
| **Target** | Who receives this, on what channel. Visible before any action is possible |
| Reason | Why the agent proposed it now |
| Content or action preview | Exactly what will happen, rendered as it will appear |
| Risk classification | With the specific rule that set it |
| Expected benefit | The KPI it serves |
| Supporting research | Evidence with sources, retrieval dates and publication dates |
| Why panel | Rules applied, KPIs consulted, knowledge retrieved with versions, assumptions, missing information |
| Compliance checks | Each named check with pass state — character count against limit, banned phrases, suppression, consent, canonical rule |
| Expiry | Countdown, and what happens at zero (cancel or regenerate) |
| Four-eyes trail | Generating agent, reviewing agent, and that neither can approve |

**Actions** — Approve · Reject · **Edit** · Request revision. Reject and Request revision both require structured feedback (wrong tone, incorrect information, poor prospect fit, duplicate idea, too generic, unsupported claim, wrong strategic priority) plus optional free text — this feedback is training data for evaluation, so making it optional would waste the loop. Edit opens the content inline with a diff against the agent's version; approving an edited item records both versions.

**Bulk** — reject only, within a single action type. **No bulk approve** for anything reaching a person or the public (see 14 §14.8).

**Filters** — type · risk · agent · campaign · brand · objective · expiry window · requires-me-specifically.
**Search** — within the queue by subject, target, agent.
**Tables** — the queue is a list, not a table; a table view is available for audit-style review with columns for type, agent, target, risk, created, expires.
**Cards** — the item pane is a single composed card; compliance checks are chips.
**Charts** — one: approval throughput and median decision time over 14 days, shown at the top of the queue. It exists to make a growing backlog visible before it becomes a crisis.
**Notifications** — push for expiring items; escalation as expiry approaches; digest at the daily brief.

**Empty** — "Nothing awaiting your approval." Plus the count decided today and the next scheduled agent run.
**Error** — if an approved item fails to execute, it returns to the queue marked *Execution failed* with the reason and whether the external effect occurred. An item whose target was suppressed after approval is withdrawn automatically with an explanation.
**Loading** — the queue skeletons; the item pane loads independently so navigation stays instant.
**Mobile** — full, and the highest-value mobile screen. One item per screen, actions as a fixed bottom bar. Swipe is not bound to approve or reject.
**Keyboard** — `j`/`k` navigate · `a` approve · `r` reject · `e` edit · `⌘↵` confirm · `?` help.
**Permissions** — Manager and Owner approve. Compliance may view all and reject on policy grounds but cannot approve. Operator sees only items they generated, read-only. **No agent principal can appear as an approver anywhere in this screen.**

---

## S-03 · Send Queue

**Purpose** — The human-assisted lane. Approved outreach, prepared to the point where sending takes seconds, with the confirmation that closes the loop.
**Primary user** — Operator. Secondary: Manager.

**Layout** — a worklist of approved items ready to send, grouped by channel and by target platform, with a daily-target progress bar at the top drawn from the workbook's own figures (20 connection requests, 15 first messages, 10 follow-ups).

**Each item shows** — target name, role, company, profile link; the approved message with a copy control and a live character count against its limit; the personal line and the evidence it came from; template code; approval trail; and the send-window guidance (the workbook's rule that requests are spread through the morning, not burst).

**Actions** — Copy · Open target profile · **Mark sent** · Defer · Return to manager · Report a platform warning.

**Mark sent** opens a confirmation carrying the approved text pre-filled and editable. If the operator amends it before sending, they paste the actual text; the platform records the divergence, re-runs the character and phrase checks, and flags a breach if the sent version broke a rule — exactly as the workbook's own compliance scoring does. **Reporting a platform warning immediately pauses that channel for the day and notifies the manager**, implementing the workbook's safety rule rather than trusting people to remember it.

**Filters** — channel · campaign · brand · lead band · assigned to me · expiring approval.
**Search** — by target name or company.
**Tables** — a compact table view for reviewing what was sent today: time, target, template, characters, outcome.
**Cards** — one card per item; a daily-target progress card.
**Charts** — none. This is a doing screen.
**Notifications** — approval expiring before send; daily target at risk; channel paused.
**Empty** — *First run:* explains that approved outreach appears here and that the platform never sends automatically. *Cleared:* "Queue clear — 15 sent today, 5 remaining against target."
**Error** — an item whose approval expired before sending is withdrawn with the reason and a one-click return for regeneration. An item whose target was suppressed after approval is removed and the reason stated.
**Loading** — standard.
**Mobile** — full, and genuinely useful: copy, open the platform app, come back, confirm.
**Permissions** — Operator sees items assigned to them; Manager sees all and can reassign. Nobody can mark an item sent that has not been approved.

---

## S-04 · Operations Timeline

**Purpose** — The audit history a manager can actually read: what happened today, in order, in plain language.
**Primary user** — Owner, Manager. Secondary: Compliance.

**Data** — chronological events: agent runs started and completed, tasks created, QA rejections, approvals requested and decided, publications, sends confirmed, KPI breaches, integration failures, policy denials, cost thresholds. Each entry carries time, actor (human or agent with version), object, and outcome.
**Actions** — open any referenced object; filter; export the day; jump to the same entry in the Audit Center for the full technical record.
**Filters** — actor type (human · agent) · agent · lane · outcome · risk · time range.
**Search** — free text across entry summaries.
**Tables** — a dense table alternative for review; the default is the narrative list.
**Cards** — day header cards with a one-line summary (tasks, approvals, publications, sends, cost).
**Charts** — an activity density strip across the day, useful for spotting a stalled afternoon.
**Notifications** — none; this screen is consulted, not pushed.
**Empty** — first run explains that entries appear as agents work, and shows the next scheduled run.
**Error** — standard.
**Loading** — reverse-chronological pagination, most recent first.
**Mobile** — read, last ten entries with load-more.
**Permissions** — all roles see the timeline; entries referencing restricted data are summarised without the restricted content.

---

## S-05 · Content Command Center

**Purpose** — Run the content supply chain the workbook defines: 7 pillars → keywords → briefs → drafts → QA → approval → publication → performance, with schedule coverage visible before it slips.
**Primary user** — Manager, Operator.

**Views** — Calendar (default) · Board by status · List · Schedules.

**Data** — planned and published items with platform, format, brand, objective, campaign, funnel stage, author agent, QA status, approval status, scheduled and published dates, published URL, impressions, engagements, clicks, leads; brief bank with pillar, cluster, difficulty and priority; schedule rows with cadence, window, planned, published and **coverage**; the editorial throughput cap and how much of it this week has used.

**Actions** — create from a brief; claim a brief; request a draft; open an item; approve; schedule; publish; repurpose (with the derivative cap enforced); pause a schedule; change cadence; export the calendar.
**Filters** — status · platform · brand · objective · campaign · pillar · cluster · funnel · difficulty · owner · date range · coverage below target.
**Search** — title, keyword, URL, brief ID.
**Tables** — the brief bank (ID, title, pillar, cluster, format, funnel, effort, priority, owner, status) with virtualised scrolling for its 5,683 rows; the schedule table with a coverage column.
**Cards** — calendar entries; a coverage card per active schedule; a "this week against cap" card.
**Charts** — coverage bars per schedule; published-per-week against cadence; engagement rate against the 2% benchmark with its provenance shown on hover.
**Notifications** — coverage below target before Friday; draft ready for QA; approval expiring; publication failed.
**Empty** — *First run:* "5,683 briefs imported. Nothing published yet — pick a P1 brief to start." *Filtered:* standard.
**Error** — publication failure states whether the item was published; canonical-rule violations are shown as a blocked action with the rule quoted, not as a validation error after the fact.
**Loading** — calendar renders its grid immediately; entries fill in.
**Mobile** — read and approve. Calendar collapses to an agenda list.
**Permissions** — Operator creates and drafts; Manager approves and publishes; Analyst read-only.

---

## S-06 · Content Item Detail

**Purpose** — Everything about one piece: brief, draft, versions, QA, compliance, approval, publication, performance.
**Primary user** — Operator, Manager.

**Data** — the full content record; version history with diffs; QA findings by criterion; claim classification with evidence status per claim; compliance results; approval trail; canonical and syndication plan; published URL; performance at +1, +7 and +30 days; cost to produce.
**Actions** — edit; request revision; run QA again; approve; schedule; publish; repurpose; view the brief; view the agent's why panel; roll back to a previous version.
**Filters / Search** — within version history only.
**Tables** — claim table (claim, type, evidence, status); performance by date.
**Cards** — brief card; QA result card per criterion; compliance card.
**Charts** — performance over time after publication.
**Notifications** — QA complete; approval decided; performance milestone.
**Empty** — a briefed-but-undrafted item shows the brief and a "request draft" action.
**Error** — an unsupported claim blocks approval and names the claim; the canonical rule blocks the channel and quotes the rule.
**Loading** — standard.
**Mobile** — read and approve; editing is desktop.
**Permissions** — as S-05. Rollback requires Manager.

---

## S-07 · Lead Command Center

**Purpose** — The pipeline the workbook's highest-value objective depends on, with qualification evidence and suppression always visible.
**Primary user** — Manager, Operator.

**Views** — Funnel (default, the workbook's nine stages) · Table · Map by geography.

**Data** — leads with account, contact, role, seniority, geography, industry, ICP fit, intent, computed score, band, funnel stage, source, evidence, owner, next action, last interaction, suppression state, duplicate flag; per-stage counts and conversion between stages; acceptance and reply rates against benchmark.
**Actions** — open lead; qualify or re-qualify; request outreach preparation; assign owner; hand off to closer; mark declined (which suppresses permanently, behind a confirm that states the permanence); merge duplicates; export.
**Filters** — score · band · funnel stage · geography · industry · company size · role · seniority · status · campaign · brand · owner · created date · has evidence · flagged duplicate.
**Search** — name, company, profile URL, email.
**Tables** — the lead table with sortable score and band; duplicate candidates surfaced above the table, not hidden in a column.
**Cards** — funnel stage cards with counts and conversion; a benchmark card showing acceptance and reply rates against the workbook's sourced ranges, with the source named.
**Charts** — funnel bars; acceptance and reply rate trend against benchmark bands.
**Notifications** — high-value lead discovered; duplicate detected; suppression conflict attempted.
**Empty** — *First run:* explains that qualified leads appear here once discovery runs, and shows the ICP tests being applied.
**Error** — a qualification without evidence is rejected upstream and never appears; if a source is degraded, the panel says which and what is missing.
**Loading** — standard; the funnel renders before the table.
**Mobile** — read.
**Permissions** — Operator sees assigned leads; Manager all; Closer sees handed-over leads; Analyst read-only aggregate. Personal data fields respect classification.

---

## S-08 · Lead Detail

**Purpose** — One person, with the evidence behind every judgement the platform made about them.
**Primary user** — Operator, Closer.

**Data** — identity and firmographics; ICP and intent ratings **with the evidence that produced each**, including source, retrieval date and publication date; computed score and band with the arithmetic shown; funnel stage and how it was derived; interaction history across all channels; outreach items and their approval state; suppression and consent state; duplicate links; handoff record; commercial outcome and PCI order reference.
**Actions** — request outreach; view why; edit qualification inputs (which recomputes the score and records who changed it); record an interaction; hand off; mark declined; request erasure.
**Search / Filters** — within interaction history.
**Tables** — interaction history (date, channel, direction, template, outcome, actor).
**Cards** — score card showing `ICP × 12 + Intent × 8` with both inputs; evidence cards; consent card.
**Charts** — none.
**Notifications** — reply received; follow-up due; approval expiring.
**Empty** — a newly discovered lead shows research in progress with the agent and step.
**Error** — if the score cannot be computed because an input is missing, the card says which input, rather than showing zero.
**Loading** — standard.
**Mobile** — read.
**Permissions** — Operator and Closer as assigned; Manager all. Erasure requires Admin and is audited.

---

## S-09 · Partnership Pipeline

**Purpose** — Organisations, not people: the twelve-stage pipeline from discovered to active.
**Primary user** — Manager.

**Views** — Kanban by stage (default) · Table.
**Data** — organisation, type, country, contact, fit rationale, ICP and intent with evidence, score, band, stage, potential value, what they get, last contact, next step and due date, deal value, contract signed date, objective and brand tags.
**Actions** — open; advance or regress stage (with a reason); request outreach preparation; schedule next step; record a meeting; attach a proposal; mark signed (which requires a deal value — the invariant made visible).
**Filters** — stage · type · country · band · owner · value range · next-step due · brand · objective.
**Search** — organisation, contact.
**Tables** — table view with all columns; overdue next steps surfaced at the top.
**Cards** — kanban cards showing organisation, band, value, next step and due date, with an overdue marker.
**Charts** — stage distribution; signed value by month.
**Notifications** — next step overdue; stage stalled beyond threshold; opportunity discovered.
**Empty** — first run explains that discovery runs weekly against the 25-contacts-per-week pacing from the workbook.
**Error** — marking signed without a value is blocked with the reason stated at the field.
**Loading** — standard.
**Mobile** — read.
**Permissions** — Manager and Owner; Operator sees assigned; Analyst read-only.

---

## S-10 · SEO Workspace

**Purpose** — Evidence-driven search work: clusters, keywords, opportunities, answer-engine visibility and links, in one place.
**Primary user** — SEO owner (Manager or Operator).

**Tabs** — Clusters · Keywords · Opportunities · Answer engines · Links.

**Data** — seven pillars with pillar URL, supporting articles, target phrase, brief ID, status, published URL, canonical state, and Search Console impressions, clicks and position; 76 keywords with intent, funnel, volume band **labelled an editorial estimate**, difficulty, who ranks today, asset to build, priority, owner and status; typed opportunities with evidence and score; the monthly answer-engine audit with per-prompt citation results; link prospects from first contact to live with link type.
**Actions** — open a cluster; commission an article from a brief; re-verify a keyword; accept or dismiss an opportunity (with a reason); run the answer-engine audit; add a link prospect; mark a link live.
**Filters** — pillar · cluster · difficulty · priority · intent · funnel · status · owner · position band · opportunity type.
**Search** — keyword, URL, cluster.
**Tables** — keyword table; cluster table; link prospect table; audit results table.
**Cards** — pillar cards with position and clicks; opportunity cards with type, evidence and score.
**Charts** — position trend per pillar phrase; impressions and clicks over time; brand-mention rate across the audit prompt set.
**Notifications** — position drop past threshold; verification expired; indexation gate blocking a batch.
**Empty** — *First run:* the seven pillars are shown with no URLs and an explanation that Search Console data appears once the site is verified and pages are live.
**Error** — Search Console degraded shows on the affected panels only, with the last successful read time; volume bands always carry their "editorial estimate, not tool data" label so they cannot be quoted as measurements.
**Loading** — standard.
**Mobile** — read.
**Permissions** — SEO owner edits; Manager approves site changes; Analyst read-only.

---

## S-11 · Campaigns · S-12 · Experiments

**S-11 Purpose** — Coordinate agents around a shared brief so work is not disconnected. **Primary user** — Manager.
**Data** — objective, audience, geography, offering, message, positioning, channels, KPIs, dates, budget, constraints, required approvals, knowledge references, CTA; and everything produced under the campaign across content, leads, partnerships and SEO.
**Actions** — create; edit brief (versioned, approval-gated); activate; pause; close; view contributions by lane; view spend.
**Filters** — status, brand, objective, date. **Search** — name.
**Tables** — contributions by lane. **Cards** — brief card; KPI progress cards. **Charts** — KPI progress against target over the campaign window.
**Notifications** — campaign KPI at risk; budget threshold; end date approaching.
**Empty / Error / Loading** — standard. **Mobile** — read. **Permissions** — Manager creates; Owner approves brief changes.

**S-12 Purpose** — Keep the platform honest about what it has actually learned. **Primary user** — Manager.
**Data** — hypothesis, baseline, action, audience, duration, success metric, guardrail metric, samples, results, rates, difference, the significance guard, conclusion, next action, whether it was rolled into the playbook.
**Actions** — create; start; conclude; roll into playbook (approval-gated); abandon.
**Filters** — status, channel, owner, date. **Search** — hypothesis text.
**Tables** — experiment table with rates and guard verdict. **Cards** — one per running experiment. **Charts** — variant comparison with sample sizes shown at the same visual weight as the rates.
**Critical display rule** — the guard verdict is always rendered with its caveat: **"heuristic, not a statistical test"**, and anything under 100 per group is labelled *directional only*. The workbook is explicit that most marketing tests are called too early; the interface must not undo that.
**Empty / Error / Loading** — standard. **Mobile** — read. **Permissions** — Manager; playbook changes need Owner.

---

## S-13 · Agent Workforce

**Purpose** — See the workforce as an operating entity: who is working, on what, how well, at what cost.
**Primary user** — Owner, Admin.

**Views** — Grid of agent cards (default) · Table.
**Data** per agent — code, name, division, role, current status (Idle · Working · Waiting · Blocked · Failed · Completed), current task, today's completed count, success rate, human rejection rate, average cost and latency, cost today, last error, next scheduled run, active version, autonomy level.
**Actions** — pause · resume · run manually · open detail · view execution history · **pause all** (behind confirm).
**Filters** — division · status · autonomy level · has errors · scheduled today.
**Search** — agent name or code.
**Tables** — table view with sortable scorecard columns.
**Cards** — one per agent: name, role, status pill, current task, next run, cost today, and a rejection-rate indicator. **No avatars** — they cost space and add nothing operational.
**Charts** — per agent, a 14-day sparkline of tasks completed; one cross-agent bar of cost today.
**Notifications** — agent failed; repeated denials; ceiling breached.
**Empty** — *First run:* all agents idle with their next scheduled run and a "run manually" control on each.
**Error** — a failed agent shows the failure reason, the ceiling breached if any, preserved state, and whether the workflow disabled itself.
**Loading** — cards render with names and roles immediately; live status fills in.
**Mobile** — status only: a compact list with status and current task; pause is available, editing is not.
**Permissions** — Owner and Admin control; Manager views and runs manually; others read.

---

## S-14 · Agent Detail & Versions

**Purpose** — The control surface for one agent, and the version discipline that keeps changes safe.
**Primary user** — Admin.

**Tabs** — Overview · Configuration · Versions · Executions · Evaluation.
**Data** — mission and role; system prompt (via prompt version); model policy by task class; tool grants; knowledge and memory scope; output contract; policies and approval requirements; ceilings; schedule and triggers; dependencies; KPIs; escalation rules; version history with lifecycle state; execution history with cost, duration, outcome and correlation ID; evaluation results per version.
**Actions** — edit (creates a draft version) · run evaluation · promote to staging · request approval for production · roll back · clone · disable · pause · test with a sample input · run manually.
**Filters** — executions by outcome, date, workflow, cost band.
**Search** — within execution history.
**Tables** — version table; execution table.
**Cards** — current version card; ceiling card; grant card listing every tool with read/write classification.
**Charts** — evaluation scores across versions; cost per task across versions.
**Notifications** — evaluation complete; promotion approved; regression detected.
**Empty** — a new agent shows its draft version and the evaluation it must pass.
**Error** — a promotion blocked by regression states which criterion regressed and by how much, against which version.
**Loading** — standard.
**Mobile** — read only.
**Permissions** — Admin edits; Owner approves promotion to production. **Nobody can grant an agent a tool that the connector manifest marks restricted** — the control is absent, not disabled, and the manifest reason is shown.

---

## S-15 · Workflow Catalogue & Editor

**Purpose** — See every workflow, its state, its cost and its failure history; and edit definitions safely.
**Primary user** — Admin.

**The design position** — this is a **visual editor over versioned definitions**, not a free-form canvas. Nodes come from a restricted palette (trigger, agent, API action, condition, loop, approval, delay, transformation, database action, notification, sub-workflow, failure path); every definition must validate before it can be saved; every change produces a diff and requires approval; and a dry-run against sandbox connectors is required before promotion. The reason is in Phase 2: workflow definitions carry real external effects, so an untested graph assembled at runtime is an incident waiting to happen. A drawing canvas that produces production automation would contradict the architecture it sits on.

**Data** — catalogue with workflow ID, name, workbook source, objective, trigger, schedule, participating agents, autonomy level, KPI, expected duration, expected cost, enabled state, last run, success rate; per workflow: definition graph, versions, run history, failure paths, approvals embedded.
**Actions** — enable · disable · pause · run manually · edit (draft) · validate · dry-run · request approval · promote · roll back · view runs · retry a failed run.
**Filters** — lane · autonomy level · enabled · failing · has approval steps.
**Search** — name, workbook source, agent.
**Tables** — catalogue table; run history.
**Cards** — node inspector; validation results.
**Charts** — run duration distribution; failure rate over time.
**Notifications** — workflow disabled after repeated failure; dry-run complete; validation failed.
**Empty** — the catalogue ships populated from Phase 6; an empty state appears only for a new draft.
**Error** — validation errors are shown on the offending node with the rule; a dry-run failure shows the step, the connector and the sandbox response.
**Loading** — graph renders progressively; large graphs virtualise.
**Mobile** — **not supported.** The catalogue is readable; the editor is desktop only, stated plainly rather than degraded.
**Permissions** — Admin edits; Owner approves promotion; Manager may enable, disable and run manually.

---

## S-16 · Scheduler & Business Calendar

**Purpose** — Make time explicit: what is scheduled, in whose time zone, on which working days, and what was missed.
**Primary user** — Manager, Admin.

**Views** — Day · Week · Month · Job list.
**Data** — scheduled jobs with intended time, actual time, workflow, agent, status, duration, retries; the business calendar: time zone, working days, week-start setting, public holidays, campaign dates, content deadlines, events, launches, blackout periods; verification-expiry obligations; missed jobs within and beyond the recovery window.
**Actions** — pause or resume a schedule; run now; reschedule; change cadence; add a holiday or blackout period; acknowledge a missed job; set the organisation time zone and week-start.
**Filters** — workflow · agent · status · date range · missed only · human-executed only.
**Search** — job or workflow name.
**Tables** — job list with **both intended and actual time as separate columns** — the single most useful thing this screen does when a schedule misbehaves.
**Cards** — calendar day cards; a "today at a glance" card mirroring the workbook's 09:00–17:00 rhythm.
**Charts** — dispatch latency distribution; missed-job count over time.
**Notifications** — job missed beyond the recovery window; schedule paused; verification expired.
**Empty** — first run shows the seeded schedule from the workbook's cadences with next run times.
**Error** — a job that could not run states why (integration down, budget exhausted, kill switch, dependency unmet) rather than showing "failed".
**Loading** — calendar grid first.
**Mobile** — read.
**Permissions** — Manager pauses and runs; Admin edits schedules and the calendar; Owner sets the time zone and week-start.

---

## S-17 · Analytics Workspace

**Purpose** — Answer which activities produce results, which agents produce value, and what it costs — with every number traceable.
**Primary user** — Owner, Analyst.

**Tabs** — KPIs · By objective · By brand · By channel · Attribution · Data health.
**Data** — the full KPI catalogue with value, target, warning and critical thresholds, trend, and **freshness per source**; results split by objective and by brand; **value rank against share of minutes** — the workbook's own capital-allocation signal; content, lead, partnership and SEO outcomes; agent contribution; cost per outcome; the ten data-health checks.
**Actions** — drill any number to lineage; change period; compare periods; export; save a view; open the underlying records.
**Filters** — period · objective · brand · campaign · channel · agent · actor type (human vs agent).
**Search** — KPI by name or ID.
**Tables** — KPI table with definition, formula, source, freshness, value, target, status; objective performance table with minutes share against value rank.
**Cards** — KPI cards with threshold markers.
**Charts** — trend lines with target and threshold bands; a rank-versus-share comparison chart that makes misallocation visible at a glance; funnel conversion.
**The attribution rule made visible** — panels are explicitly labelled **Correlation** or **Attribution**, and the platform declines to compute attribution where the data cannot support it, saying so rather than producing a number. Vendor-sourced benchmark bands display their source on hover.
**Notifications** — threshold breached; data-health non-zero; source stale beyond threshold.
**Empty** — *First run:* KPI definitions and targets shown with no values, and the date the first observation is expected.
**Error** — a stale source labels the panel and keeps the last value with its timestamp.
**Loading** — cards then charts.
**Mobile** — read; charts simplify to a single series.
**Permissions** — Owner and Analyst full; Manager operational KPIs; Operator their own contribution; **Employee Score restricted to Owner and Manager**.

---

## S-18 · Reports · S-19 · Costs & Budget

**S-18 Purpose** — The daily brief, end-of-day report and weekly growth review, generated and archived. **Primary user** — Owner.
**Data** — each report with its generated time, period, contents, sources, freshness and the data-health state at generation; archive by date; export state.
**Actions** — read; regenerate; export to PDF, Excel or CSV; share a link; subscribe to delivery.
**Filters** — type, period, date. **Search** — full text across archived reports.
**Tables** — the archive. **Cards** — the report itself renders as sectioned cards. **Charts** — inline within reports, each drilling to lineage.
**Notifications** — report ready; report held because data health is non-zero.
**Empty** — first run shows the schedule and next generation time.
**Error** — **a report generated while data-health checks are non-zero is watermarked and states which checks failed** rather than being silently published.
**Loading** — standard. **Mobile** — full; this is a read-on-the-phone screen. **Permissions** — Owner and Manager; Analyst read; export requires Owner, Manager or Analyst.

**S-19 Purpose** — Keep AI and channel spend proportional to output. **Primary user** — Owner, Admin.
**Data** — spend by day, agent, workflow, campaign, brand, model and provider; budget state daily and monthly; cost per published item and per prepared outreach item; channel costs; cost per meeting; revenue per unit of cost; optimisation recommendations with the quality risk of each stated.
**Actions** — set budgets (daily, monthly, per agent, per workflow); restrict models; accept or dismiss a recommendation; export.
**Filters** — period · agent · workflow · campaign · model · provider.
**Search** — agent or workflow.
**Tables** — spend table; recommendation table with estimated saving **and** quality risk side by side.
**Cards** — budget state cards with burn-down.
**Charts** — daily spend against budget; cost per output over time.
**Notifications** — threshold reached; low-priority executions paused; unusual spike.
**Empty** — first run shows budgets unset with a warning that cost control is not active until they are.
**Error** — standard. **Mobile** — read. **Permissions** — Owner and Admin; budget increases require approval.

---

## S-20 · Knowledge Base & Workbook Importer

**Purpose** — The organisational memory, and the controlled path by which the workbook updates configuration.
**Primary user** — Admin.

**Tabs** — Documents · Search · Importer · Brand system.
**Data** — documents with version history, classification, permissions, chunk count, index state, and which agent executions used which version; retrieval test console; the brand system (voice, terminology, prohibited terms, positioning, approved claims, claims requiring evidence, CTA library, pillars, competitor policy); import history.
**Actions** — upload; version; set permissions and classification; re-index; test retrieval; supersede; **run a workbook import**.
**The importer flow** — upload → detect sheets → validate structure → preview extracted data → **diff against the current configuration** → administrator approves or rejects each change group → apply → audit. Nothing is applied automatically, and the diff is the screen's centrepiece.
**Filters** — type · classification · owner · date · used-by-agent.
**Search** — full text and semantic, with citations shown.
**Tables** — document table; chunk inspector; import diff table grouped by change type.
**Cards** — brand system cards; import summary.
**Charts** — none.
**Notifications** — import proposed; re-index complete; document superseded while in use.
**Empty** — first run offers the workbook import as the primary action.
**Error** — a malformed workbook shows which sheet and which structural expectation failed; a partial import is never applied.
**Loading** — indexing shows progress by document.
**Mobile** — read.
**Permissions** — Admin; import approval requires Owner. **Uploads are treated as untrusted input** and the screen says so.

---

## S-21 · Integration Center · S-22 · Audit Center · S-23 · Error Center

**S-21 Purpose** — Connector health, scopes and verification state. **Primary user** — Admin.
**Data** — per connector: tier, health state, auth state, scopes granted, rate-limit headroom, last successful call, error rate, **verification date and expiry**, restricted operations, credential reference (never the secret), data classifications carried.
**Actions** — connect; reauthorise; test connection; disable; rotate credential reference; acknowledge verification; view recent calls.
**Filters** — area · tier · health · expiring verification. **Search** — connector name.
**Tables** — connector table with health and verification columns. **Cards** — one per connector with a health pill and headroom bar. **Charts** — call volume and error rate over 7 days.
**Notifications** — authentication expired; rate limit sustained; verification overdue; health degraded.
**Empty** — first run lists the MVP connector set unconfigured, in build order.
**Error** — an expired connector states which operations are blocked and which still work.
**Mobile** — status only. **Permissions** — Admin; Owner approves connect and disconnect. **Secrets are never displayed to anyone.**

**S-22 Purpose** — The immutable record, searchable. **Primary user** — Compliance, Admin.
**Data** — audit events with agent, versions, prompt version, user, trigger, input, retrieved knowledge with versions, model, output, tool calls, approval, result, error, cost, timestamp, correlation ID; chain integrity state.
**Actions** — filter; search; open the full record; follow a correlation ID across services; verify chain integrity; export a range.
**Filters** — date · user · agent · workflow · action · entity · integration · result · risk level.
**Search** — free text plus correlation ID.
**Tables** — the event table; the record detail is a structured view, not raw JSON, with raw available.
**Cards** — a "why did the system do this" composed view for any action.
**Charts** — denial rate over time — a leading indicator of a misconfigured agent version.
**Notifications** — chain verification failure (critical); repeated denials.
**Empty** — first run explains that entries accumulate from first agent activity.
**Error** — standard. **Mobile** — read. **Permissions** — Compliance and Admin full; Owner full; others see only their own actions. **Audit records are never editable by anyone.**

**S-23 Purpose** — Failures in one place, with safe retry. **Primary user** — Admin.
**Data** — failed workflows, tasks, tool errors, integration errors, AI validation failures, authentication failures, retries, dead letters — each with reason, inputs, attempts, last error and whether an external effect occurred.
**Actions** — retry (only where the retry taxonomy permits it); cancel; escalate; open the object; bulk retry within one class.
**Filters** — class · workflow · agent · integration · date · retryable.
**Search** — error text and correlation ID.
**Tables** — the failure table. **Cards** — dead-letter cards showing attempts and last error. **Charts** — failures by class over time.
**Notifications** — dead letter created; repeated failure disabled a workflow.
**Empty** — "No failures in this period" — a good state, stated plainly.
**Error** — a retry that is not safe is **absent**, with the taxonomy reason shown, rather than offered and refused.
**Mobile** — read. **Permissions** — Admin retries; Manager views.

---

## S-24 · Administration · S-25 · Settings

**S-24 Purpose** — The controls that change how the platform behaves without a deployment. **Primary user** — Admin, Owner. **Desktop only.**
**Sections** — Users and roles · Agent principals and grants · Approval rules · Policies and compliance checks · Message templates · Enumerations and dedup rules · Budgets · Feature flags · Data retention · Environments.
**Data** — for each: current value, who changed it, when, and the previous value.
**Actions** — create, edit, version, approve, roll back. Every change is versioned and audited; changes to approval rules, policies, budgets and templates require approval.
**Notable** — the **Message Bank** lives here: templates with live character counts against limits, status, approver and approval date. New templates enter as Draft and only an approver can set them Approved, exactly as the workbook requires. The **compliance checks** live here too, each with an owner, frequency, last result and signature — and an unsigned check reports as failing, as it does today in the workbook.
**Filters / Search** — per section. **Tables** — one per section. **Cards** — policy cards with test results. **Charts** — none.
**Empty** — first run presents a setup checklist derived from the workbook's own residual actions: name the owner, populate the roster, assign owners to the 63 tasks and 15 checks, sign the templates, set budgets, connect the CMS.
**Error** — a policy change that would break an existing invariant is blocked with the conflict named.
**Mobile** — not supported. **Permissions** — Admin; Owner approves high-risk changes; **no agent principal may modify anything in this screen**.

**S-25 Purpose** — Organisation-level configuration people change occasionally. **Primary user** — Owner, Manager.
**Sections** — Organisation and time zone · Business calendar and week-start · Daily targets · Brands and domains · Objectives and value ranks · Content approval mode (Manual · Trusted workflow · Campaign pre-approval · Emergency lock) · Notification preferences · Autonomy levels per workflow.
**Data / Actions** — as above, versioned and audited.
**Notable** — **objective value ranks are editable here**, and the screen states plainly that changing them changes what the orchestrator prioritises tomorrow. Autonomy increases require evaluation evidence and Owner approval, and the control says so.
**Empty / Error / Loading** — standard. **Mobile** — read. **Permissions** — Owner; Manager may edit targets and notification preferences.
