# 14 — Information Architecture, Navigation & Interaction Patterns

**Phase 3 · Status: awaiting approval.** No components, styles or front-end code are produced in this phase.

## 14.0 Assumption carried

`P3-02` (who approves, on what device, in what window) is unanswered. **Assumption A-06: desktop-first, with full mobile support for approval, notification, dashboard, agent status, daily report and emergency pause.** If approvals in practice happen on a phone between meetings — which the two-hour journalist-request SLA implies — the Approval Inbox becomes a mobile-first design and the desktop version becomes the secondary layout. That inverts the layout work for one screen; it changes nothing else.

The five Phase 2 assumptions still stand. `A-03` (CMS) matters here too: if the CMS has no write API, the Content Command Center's publish control becomes a send-queue hand-off identical to outreach.

---

## 14.1 What this product is, in one line

**An AI Workforce Command Center** — an operations console, not a document system and not a chatbot. That distinction sets the whole design: screens are *scanned and operated*, not read top to bottom, so the craft is information design first and typography second.

## 14.2 The three questions the home screen must answer

Every design decision below is subordinate to these, in this order:

1. **What did the workforce do?** — since I last looked, without opening a log.
2. **What needs me?** — decisions only I can make, ordered by cost of delay.
3. **What happens next?** — today's plan, and whether it is at risk.

A dashboard that answers these badly is a wall of charts. A dashboard that answers them well is short.

## 14.3 Design principles

| # | Principle | Consequence |
|---|---|---|
| P1 | **Summary before detail** | Every workspace opens on state, not on a table. The table is one click down |
| P2 | **No number without provenance** | Every metric is click-through to its lineage: source → observation → formula → value. A figure that cannot be traced is not displayed |
| P3 | **State in form, not only colour** | Pills, severity stripes and icons carry status; colour reinforces, never carries alone. This is an accessibility requirement, and it also survives printing |
| P4 | **Every recommendation shows why** | Evidence, rules applied, KPIs consulted, sources retrieved, assumptions stated — never hidden chain-of-thought |
| P5 | **Distinguish research · draft · recommendation · approval · execution** | These are different states with different visual weight. A draft must never look like something that has been sent |
| P6 | **The target is always visible before the action** | You see who receives it, on what channel, before you can approve it |
| P7 | **Emergency stop is always reachable** | Persistent in the top bar, two-step confirm, never buried in settings |
| P8 | **Empty states teach** | The workbook starts empty and so does the platform. First-run emptiness explains what will fill it |
| P9 | **Freshness is stated** | Every panel fed by an external source shows when that source was last read |
| P10 | **Agent work is labelled as agent work** | Attribution to the agent and its version, everywhere a human would otherwise assume a person did it |

## 14.4 Navigation

A persistent left rail, five groups. The grouping deliberately echoes the workbook's own `MAP` structure — read-first, daily work, results, management, reference — so a team that already knows the workbook can find things without retraining.

```
TODAY          Command Center          the home screen
               Approvals          ●4   the decision queue
               Send Queue         ●12  the human-assisted lane
               Timeline                chronological operations record

WORK           Content                 calendar · briefs · drafts · schedules
               Leads                   pipeline · qualification · suppression
               Partnerships            12-stage pipeline
               SEO                     clusters · keywords · opportunities · links
               Campaigns               briefs and coordination
               Experiments             hypotheses and results

WORKFORCE      Agents                  24 agents, status and scorecards
               Workflows               catalogue and editor
               Scheduler               business calendar and job state

INSIGHT        Analytics               KPIs with lineage
               Reports                 daily brief · end of day · weekly
               Costs                   AI and channel spend against budget

SYSTEM         Knowledge               documents, versions, workbook importer
               Integrations            connector health and scopes
               Audit                   the immutable record
               Errors                  failures and dead letters
               Admin                   users, agents, policies, budgets, templates
               Settings                org, calendar, targets, objectives
```

**Badges** appear on Approvals, Send Queue and Errors only — the three places where an unattended number means work is not happening. Badging everything trains people to ignore badges.

**Global search** (`⌘K` / `Ctrl-K`) spans lead, company, campaign, task, agent, content, workflow, report and partnership, and doubles as a command palette: run workflow, pause agent, create campaign, open approvals, jump to a report.

**Top bar** carries, always: organisation and time zone (so nobody misreads a schedule), the notification bell, the current user, and **Emergency Stop**.

## 14.5 Screen inventory

| # | Screen | Group | Primary user | Mobile |
|---|---|---|---|---|
| S-01 | Executive Command Center | Today | Owner | **Full** |
| S-02 | Approval Inbox | Today | Manager, Owner | **Full** |
| S-03 | Send Queue | Today | Operator | **Full** |
| S-04 | Operations Timeline | Today | Owner, Manager | Read |
| S-05 | Content Command Center | Work | Manager, Operator | Read + approve |
| S-06 | Content Item Detail | Work | Operator, Manager | Read + approve |
| S-07 | Lead Command Center | Work | Manager, Operator | Read |
| S-08 | Lead Detail | Work | Operator, Closer | Read |
| S-09 | Partnership Pipeline | Work | Manager | Read |
| S-10 | SEO Workspace | Work | SEO owner | Read |
| S-11 | Campaigns | Work | Manager | Read |
| S-12 | Experiments | Work | Manager | Read |
| S-13 | Agent Workforce | Workforce | Owner, Admin | Status only |
| S-14 | Agent Detail & Versions | Workforce | Admin | Read |
| S-15 | Workflow Catalogue & Editor | Workforce | Admin | **Desktop only** |
| S-16 | Scheduler & Business Calendar | Workforce | Manager, Admin | Read |
| S-17 | Analytics Workspace | Insight | Owner, Analyst | Read |
| S-18 | Reports | Insight | Owner | **Full** |
| S-19 | Costs & Budget | Insight | Owner, Admin | Read |
| S-20 | Knowledge Base & Importer | System | Admin | Read |
| S-21 | Integration Center | System | Admin | Status only |
| S-22 | Audit Center | System | Compliance, Admin | Read |
| S-23 | Error Center | System | Admin | Read |
| S-24 | Administration | System | Admin, Owner | **Desktop only** |
| S-25 | Settings | System | Owner, Manager | Read |

**S-03 Send Queue is not in the original brief's screen list.** It is added because Phase 1 established that the highest-value lane ends in a human action, and Phase 2 made human-assisted channels first-class. Without this screen the outreach lane has no interface, and the most valuable thing the platform does has nowhere to happen.

## 14.6 Cross-cutting states

Specified once here rather than repeated for all 25 screens.

### Loading

| Case | Treatment |
|---|---|
| List or table | Skeleton rows matching final row height, so nothing reflows. Header, filters and actions render immediately and stay usable |
| Metric tile | Tile renders with its label and threshold; the value area shows a muted placeholder. **The label never moves** |
| Chart | Axes, gridlines and legend render; the plot area is muted until data lands |
| Long agent run | Progress by step, not a spinner: "Drafting · step 3 of 6 · 41s" with a cancel control |
| Slow external source | The panel renders from cache and marks itself stale rather than blocking |

Nothing blocks the whole page. The command centre in particular renders progressively, top panel first.

### Empty — three genuinely different things

| Kind | Message pattern | Example |
|---|---|---|
| **First run** | Explains what will appear and what produces it | "No content published yet. Once a brief passes QA, compliance and approval, published items and their performance appear here." |
| **Filtered to nothing** | States the filter and offers to clear it | "No leads match band A · Gulf · last 7 days. Clear filters" |
| **Genuinely nothing, and that is good** | Confirms the good state without celebration | "Nothing awaiting your approval." · "Data health: all ten checks read zero." |

Conflating these is the most common dashboard failure. "No data" on an empty first run reads as a bug; on a cleared approval queue it reads as a loss.

### Error

| Kind | Treatment |
|---|---|
| Permission denied | Says what is restricted and who can grant it. Never a blank screen, never a 403 code |
| Integration degraded | Inline banner on the affected panel only, naming the connector, its state and what still works. The rest of the screen keeps functioning |
| Stale data | The panel shows its freshness stamp with a marker past the threshold. The number is still shown — hiding it is worse than labelling it |
| Action failed | States what failed, whether it was retried, whether the external effect happened, and the safe next step. **For external writes it always states whether the action landed** — "not sent" and "may have sent" are different messages |
| Validation | At the field, on blur, with the rule stated in business terms: "298 of 300 characters" not "maxLength exceeded" |
| Agent failure | Names the agent and version, the ceiling breached or the error, preserved state, and what the platform did next |

### Notifications

In-app first, with connectors architected for email, Slack and Teams. Configurable per event: approval required, agent failed, workflow blocked, integration disconnected, KPI threshold breached, high-value lead discovered, partnership opportunity discovered, content ready, budget threshold reached, daily report ready.

Two rules: **an approval nearing expiry escalates its notification**, and **nothing notifies twice for the same item** unless its state changed.

### Permissions in the interface

Permission is expressed by presence and state, not by failure:

- Actions a role can never perform are **absent**, not disabled.
- Actions a role could perform but not right now are **disabled with the reason on hover and focus** ("Awaiting compliance check", "Requires a second approver").
- Data a role cannot see is absent; the interface never renders a redaction placeholder that reveals the shape of what is hidden.
- **Restricted HR data** (Employee Score) sits behind an explicit role and is never shown in aggregate views that a wider audience can reach.

### Mobile

Full: Command Center, Approvals, Send Queue, Reports, agent status, emergency pause, notifications.
Read-only: most workspaces.
Desktop only: Workflow editor, Administration, bulk operations, the knowledge importer's diff view.

On mobile the approval card is the whole screen: preview, target, risk, evidence, and the four actions as a fixed bottom bar within thumb reach. Swipe is **not** bound to approve or reject — a gesture that publishes content or messages a person is the wrong affordance.

## 14.7 Interaction patterns used across screens

| Pattern | Where | Behaviour |
|---|---|---|
| **Why panel** | Any agent output | Evidence with sources and dates, rules applied, KPIs consulted, knowledge retrieved with document versions, assumptions, missing information. Never chain-of-thought |
| **Lineage drill** | Any metric | Source → raw observation → transformation → formula version → value, with a link to the underlying rows |
| **Freshness stamp** | Any externally-sourced panel | "Search Console · read 4h ago" |
| **Agent attribution** | Any generated artefact | Agent code, version, prompt version, cost, duration |
| **Diff view** | Prompts, agent versions, workbook imports, edited approvals | Side-by-side with change highlighting; required before any promotion |
| **Confirm-with-target** | Every external action | Restates recipient or destination and the effect before committing |
| **Keyboard-first queues** | Approvals, Send Queue | `j`/`k` navigate · `a` approve · `r` reject · `e` edit · `⌘↵` confirm · `?` shortcuts |
| **Saved views** | Every list | Named filter sets per user, shareable within the organisation |

## 14.8 Two deliberate refusals

**No bulk approve for anything that reaches a person or the public.** Bulk actions exist for *reject* and for internal, low-risk item types only. The workbook's entire quality thesis is that personalisation and review are what make outreach work; a "select all → approve" control would quietly undo it, and would make the four-eyes principle theatre. This will be asked for. The answer is no, and the reason is in the workbook.

**No free-form workflow canvas.** Phase 2 established that workflow definitions are versioned, tested and owned by the platform. A builder that lets anyone assemble arbitrary graphs at runtime contradicts that: it produces untested execution paths with real external effects. S-15 is therefore a **visual editor over versioned definitions** with a restricted node palette, validation, dry-run against sandbox connectors, and a diff plus approval before promotion — closer to a schema editor than to a drawing canvas. See S-15 for the full argument.
