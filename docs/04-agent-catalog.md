# OptimizeAll — Agent Workforce Catalog

- **Document ID:** OA-AGT-001
- **Version:** 1.0

This catalog is the human-readable form of the seed data in
`backend/src/OptimizeAll.Infrastructure/Seed/agents/`. The seed files are authoritative for the
running system; this document is authoritative for intent. CI verifies that every agent in the seed
appears here and vice versa.

---

## 1. Specification template

Every agent definition carries exactly these fields:

| Field | Meaning |
|---|---|
| **Key** | Stable machine identifier. Never reused, never renamed. |
| **Mission** | One sentence. What this agent is accountable for. |
| **Inputs** | Typed payload the agent accepts. |
| **Outputs** | Typed artefact the agent produces. |
| **Tools** | The closed set of capabilities granted. Anything absent is denied. |
| **Permissions** | Platform permissions held by the agent's service principal. |
| **Memory** | `short` (run), `episodic` (prior runs), `semantic` (knowledge base). |
| **Schedule** | Default cadence. Tenants may override. |
| **Approval** | Which produced actions are gated, and by which role. |
| **KPIs** | What this agent is measured on. |
| **Escalation** | What it does when blocked or uncertain. |
| **Risk class** | Highest action risk class the agent can propose. |

## 2. Action risk classes

| Class | Definition | Default gate |
|---|---|---|
| `Read` | Retrieves data. No state change anywhere. | None |
| `Write` | Changes state inside OptimizeAll only. | None (audited) |
| `External` | Causes an effect in a third-party system. | Always gated |
| `Financial` | Commits money or contractual obligation. | Always gated, threshold-tiered |
| `Irreversible` | Cannot be undone by the platform. | Always gated, two approvers |

**No tenant configuration can remove a gate from `External`, `Financial`, or `Irreversible`.**
Tenants may only make gating *stricter*.

## 3. Tool registry

Tools are declared centrally and granted per agent. Grants are a closed allow-list.

| Tool key | Risk | Description |
|---|---|---|
| `knowledge.search` | Read | Semantic search over the tenant knowledge base |
| `knowledge.write` | Write | Add or update a knowledge document |
| `web.search` | Read | External web search |
| `web.fetch` | Read | Fetch and extract a public URL |
| `analytics.query` | Read | Query the platform's metric store |
| `crm.read` | Read | Read CRM records |
| `crm.write` | External | Create or update CRM records |
| `content.draft` | Write | Produce a content draft artefact |
| `content.publish` | External | Publish to a CMS or channel |
| `email.send` | External | Send email on the tenant's behalf |
| `linkedin.message` | External | Send a LinkedIn message or connection request |
| `ads.spend` | Financial | Create or modify a paid campaign budget |
| `invoice.issue` | Financial | Issue an invoice |
| `repo.read` | Read | Read a source repository |
| `repo.propose` | Write | Open a pull request (never merge) |
| `deploy.trigger` | Irreversible | Trigger a deployment |
| `ticket.write` | Write | Create or update an internal ticket |
| `report.generate` | Write | Produce a report artefact |
| `data.export` | External | Export data outside the platform |
| `agent.delegate` | Write | Ask the orchestrator to schedule work for another agent |
| `schedule.manage` | Write | Create or modify schedules |
| `policy.evaluate` | Read | Evaluate a compliance or legal policy |

`agent.delegate` is the **only** inter-agent pathway, and it routes through the orchestrator. No
agent can invoke another agent directly.

---

## 4. Control-plane agents

### 4.1 `master-orchestrator`

| | |
|---|---|
| **Mission** | Convert a business objective into an executable, governed task graph and drive it to completion. |
| **Inputs** | `Objective { title, description, desiredOutcome, deadline, constraints, budgetCap }` |
| **Outputs** | `WorkflowPlan { tasks[], dependencies[], gates[], assignedAgents[], estimatedCost }` |
| **Tools** | `agent.delegate`, `knowledge.search`, `analytics.query`, `schedule.manage` |
| **Permissions** | `workflow:create`, `workflow:advance`, `task:assign`, `agent:read` |
| **Memory** | short + episodic (prior plans and their outcomes) + semantic |
| **Schedule** | Event-driven; also every 60 s to advance in-flight workflows |
| **Approval** | Plans exceeding the workspace budget cap require Tenant Owner approval before execution |
| **KPIs** | Objective completion rate, plan revision count, cost variance vs estimate, gate compliance = 100% |
| **Escalation** | If no capable agent exists for a task, mark `NeedsHumanDesign` and notify Administrators |
| **Risk class** | `Write` — the orchestrator itself never touches external systems |

**Design note.** The orchestrator plans and assigns; it does not execute. This separation means a
compromised planner still cannot cause an external effect without passing a gate.

### 4.2 `scheduler`

| | |
|---|---|
| **Mission** | Turn declared cadences into exactly-once, timezone-correct occurrences. |
| **Inputs** | `ScheduleDefinition { cron, timezone, missPolicy, target }` |
| **Outputs** | `ScheduleOccurrence { scheduleId, occurrenceUtc, dispatchedRunId }` |
| **Tools** | `schedule.manage`, `agent.delegate` |
| **Permissions** | `schedule:read`, `schedule:fire`, `workflow:create` |
| **Memory** | short only — schedules must be stateless and deterministic |
| **Schedule** | Continuous leader-elected loop, 15 s tick |
| **Approval** | None; firing a schedule is `Write`. Whatever it fires carries its own gates. |
| **KPIs** | Occurrence accuracy (fired within ±30 s), duplicate rate = 0, missed occurrences = 0 |
| **Escalation** | Backlog beyond 5 minutes raises a platform alert |
| **Risk class** | `Write` |

---

## 5. Executive and management agents

### 5.1 `ceo-agent`

| | |
|---|---|
| **Mission** | Maintain the executive view of the business and propose the highest-leverage next objectives. |
| **Inputs** | `ExecutiveContext { period, kpiSnapshots, strategicGoals, riskRegister }` |
| **Outputs** | `ExecutiveBrief { situation, risks, recommendedObjectives[], rationale, tradeoffs }` |
| **Tools** | `analytics.query`, `knowledge.search`, `report.generate`, `agent.delegate` |
| **Permissions** | `kpi:read`, `report:create`, `objective:propose` |
| **Memory** | episodic (prior briefs + which recommendations were adopted and what happened) + semantic |
| **Schedule** | Weekly, Monday 07:00 tenant-local; monthly deep review on the 1st |
| **Approval** | Recommendations are advisory. Converting one into a funded objective requires Tenant Owner approval. |
| **KPIs** | Recommendation adoption rate, adopted-recommendation outcome vs forecast, brief timeliness |
| **Escalation** | Conflicting strategic goals → present the conflict explicitly rather than silently resolving it |
| **Risk class** | `Write` |

**Design note.** This agent proposes; it never commits. An AI with unilateral strategic authority is
a governance failure, not a feature.

### 5.2 `project-manager-agent`

| | |
|---|---|
| **Mission** | Keep committed work on schedule by tracking progress, surfacing blockers, and re-sequencing. |
| **Inputs** | `PortfolioState { workflows[], tasks[], deadlines[], dependencies[], capacity }` |
| **Outputs** | `StatusReport { onTrack[], atRisk[], blocked[], proposedResequencing[] }` |
| **Tools** | `ticket.write`, `analytics.query`, `agent.delegate`, `report.generate`, `knowledge.search` |
| **Permissions** | `task:read`, `task:update`, `workflow:read`, `ticket:write` |
| **Memory** | episodic (estimation accuracy history) + semantic |
| **Schedule** | Daily 08:00 tenant-local; on every task state change |
| **Approval** | Re-sequencing that moves a committed external deadline requires Operator approval |
| **KPIs** | On-time delivery rate, blocker mean time to surface, estimate accuracy |
| **Escalation** | Blocked > 48 h → escalate to Administrator; > 96 h → Tenant Owner |
| **Risk class** | `Write` |

---

## 6. Growth agents

### 6.1 `marketing-strategy-agent`

| | |
|---|---|
| **Mission** | Own the marketing plan: positioning, audience, channel mix, and campaign themes. |
| **Inputs** | `MarketContext { icp, positioning, competitors, budget, priorPerformance }` |
| **Outputs** | `MarketingPlan { themes[], channels[], calendar, budgetAllocation, successMetrics }` |
| **Tools** | `web.search`, `knowledge.search`, `analytics.query`, `agent.delegate`, `report.generate` |
| **Permissions** | `campaign:draft`, `kpi:read`, `knowledge:read` |
| **Memory** | episodic (campaign performance) + semantic (brand guidelines, ICP) |
| **Schedule** | Monthly plan; weekly adjustment review |
| **Approval** | Budget allocation is `Financial` — Tenant Owner approval, tiered by amount |
| **KPIs** | Pipeline influenced, cost per qualified lead, plan-to-execution adherence |
| **Escalation** | Budget insufficient for stated goals → return an explicit shortfall analysis, do not silently descope |
| **Risk class** | `Financial` |

### 6.2 `seo-agent`

| | |
|---|---|
| **Mission** | Grow qualified organic traffic through technical health, keyword strategy, and content briefs. |
| **Inputs** | `SeoContext { domain, currentRankings, crawlReport, competitorSet, targetKeywords }` |
| **Outputs** | `SeoPlan { technicalFixes[], keywordTargets[], contentBriefs[], internalLinkPlan }` |
| **Tools** | `web.search`, `web.fetch`, `analytics.query`, `knowledge.search`, `agent.delegate` |
| **Permissions** | `content:brief`, `kpi:read` |
| **Memory** | episodic (ranking movement after each change) + semantic |
| **Schedule** | Weekly audit Tuesday 06:00; monthly strategy review |
| **Approval** | Any change to a live site is `External` — Operator approval. Recommendations are `Write`. |
| **KPIs** | Organic sessions, ranking positions for target set, indexation health, Core Web Vitals |
| **Escalation** | Detected penalty or de-indexation → immediate Critical notification, do not wait for cadence |
| **Risk class** | `External` |

### 6.3 `content-planning-agent`

| | |
|---|---|
| **Mission** | Convert marketing and SEO strategy into a concrete, resourced editorial calendar. |
| **Inputs** | `PlanningContext { marketingPlan, seoPlan, capacity, brandGuidelines, priorPerformance }` |
| **Outputs** | `EditorialCalendar { items[{ title, brief, format, channel, owner, dueDate, keywords }] }` |
| **Tools** | `knowledge.search`, `analytics.query`, `agent.delegate`, `schedule.manage` |
| **Permissions** | `content:plan`, `schedule:write`, `kpi:read` |
| **Memory** | episodic (which formats and topics performed) + semantic |
| **Schedule** | Weekly Wednesday 09:00 |
| **Approval** | Calendar publication to the team requires Operator approval |
| **KPIs** | Calendar adherence, content velocity, per-item performance vs forecast |
| **Escalation** | Capacity below plan → propose a prioritised subset with explicit cut list |
| **Risk class** | `Write` |

### 6.4 `content-agent`

| | |
|---|---|
| **Mission** | Produce on-brand, factually grounded drafts against an approved brief. |
| **Inputs** | `ContentBrief { title, angle, audience, keywords, format, wordCount, sources[], tone }` |
| **Outputs** | `ContentDraft { body, title, metaDescription, citations[], assetRequests[] }` |
| **Tools** | `content.draft`, `knowledge.search`, `web.search`, `web.fetch` |
| **Permissions** | `content:write`, `knowledge:read` |
| **Memory** | episodic (QA rejections and reviewer edits on prior drafts) + semantic (style guide) |
| **Schedule** | Event-driven from the editorial calendar |
| **Approval** | Drafting is `Write`, ungated. Publication is a separate gated step. |
| **KPIs** | First-pass QA acceptance rate, citation coverage, revision cycles per piece |
| **Escalation** | Cannot source a factual claim → omit it and flag the gap; never fabricate a citation |
| **Risk class** | `Write` |

**Hard rule.** Every factual claim must carry a citation resolvable to a retrieved source. A draft
with an unresolvable citation is rejected mechanically before it reaches QA.

### 6.5 `content-qa-agent`

| | |
|---|---|
| **Mission** | Independently verify drafts for accuracy, brand compliance, legal risk, and SEO fitness. |
| **Inputs** | `ContentDraft` + `ContentBrief` + `BrandGuidelines` + `LegalConstraints` |
| **Outputs** | `QaVerdict { decision: Pass\|Revise\|Reject, findings[{severity, location, issue, fix}], score }` |
| **Tools** | `knowledge.search`, `web.fetch`, `policy.evaluate` |
| **Permissions** | `content:review`, `knowledge:read`, `policy:read` |
| **Memory** | episodic (finding patterns per author agent) + semantic |
| **Schedule** | Event-driven on draft completion |
| **Approval** | A `Pass` verdict does not authorise publication; it only makes the item eligible for the publication gate. |
| **KPIs** | Defect escape rate to human review, false-positive rate, review turnaround |
| **Escalation** | Any legal or regulatory finding → route to `legal-agent` before proceeding |
| **Risk class** | `Read` |

**Design note.** QA must not be the same agent instance that wrote the draft, and must not share its
short-term memory. Self-review is not review.

### 6.6 `publishing-agent`

| | |
|---|---|
| **Mission** | Execute approved publications correctly, on time, and reversibly where the channel permits. |
| **Inputs** | `PublicationRequest { draftId, channel, scheduledAt, approvalId, payloadHash }` |
| **Outputs** | `PublicationResult { externalId, url, publishedAt, rollbackToken }` |
| **Tools** | `content.publish`, `knowledge.write` |
| **Permissions** | `content:publish`, `integration:invoke` |
| **Memory** | short only — publication must be deterministic, not inferential |
| **Schedule** | Event-driven at the approved publication time |
| **Approval** | **Always gated.** Requires a valid, unexpired approval whose payload hash matches exactly. |
| **KPIs** | Publication success rate, on-time rate, rollback rate |
| **Escalation** | Channel rejects the payload → do not retry with a modified payload; return for re-approval |
| **Risk class** | `External` |

**Hard rule.** This agent has no creative latitude. It publishes exactly the approved bytes. If the
payload does not hash-match the approval, it refuses and raises a security event.

### 6.7 `linkedin-outreach-agent`

| | |
|---|---|
| **Mission** | Run compliant, personalised, rate-limited outreach that a human has approved. |
| **Inputs** | `OutreachCampaign { icp, targetList[], messageTemplates[], cadence, dailyCap }` |
| **Outputs** | `OutreachBatch { messages[{ recipient, body, sendAt }], complianceNotes }` |
| **Tools** | `linkedin.message`, `crm.read`, `crm.write`, `knowledge.search` |
| **Permissions** | `outreach:draft`, `crm:read`, `crm:write` |
| **Memory** | episodic (reply-rate by template and segment) + semantic |
| **Schedule** | Weekday batches, tenant-local business hours only |
| **Approval** | **Always gated.** Every batch requires Operator approval; message bodies are reviewed as sent. |
| **KPIs** | Reply rate, positive-reply rate, complaint rate (hard ceiling), meetings booked |
| **Escalation** | Complaint rate above threshold → auto-pause the campaign and notify immediately |
| **Risk class** | `External` |

**Compliance constraints.** Respects platform rate limits and terms of service; honours
suppression and do-not-contact lists before drafting; never contacts a recipient who has opted out;
enforces a per-recipient contact frequency cap across all campaigns in the tenant.

### 6.8 `lead-generation-agent`

| | |
|---|---|
| **Mission** | Identify and qualify accounts matching the ICP, from permitted sources only. |
| **Inputs** | `LeadSearch { icp, firmographics, signals[], exclusions[], sourceAllowList }` |
| **Outputs** | `QualifiedLeads[{ account, contacts[], score, evidence[], sourceProvenance }]` |
| **Tools** | `web.search`, `web.fetch`, `crm.read`, `crm.write`, `knowledge.search` |
| **Permissions** | `lead:create`, `crm:read`, `crm:write` |
| **Memory** | episodic (which signals predicted conversion) + semantic |
| **Schedule** | Daily 07:00 |
| **Approval** | Writing leads to the CRM is `External` — Operator approval per batch |
| **KPIs** | Lead-to-opportunity conversion, ICP-fit precision, duplicate rate, source compliance = 100% |
| **Escalation** | A source outside the allow-list looks valuable → request an allow-list change; never source from it unilaterally |
| **Risk class** | `External` |

**Data protection constraint.** Only lawfully obtainable business contact data from allow-listed
sources. No scraping of sources whose terms forbid it. Every lead records its provenance so a
deletion request can be honoured at the source level.

### 6.9 `partnerships-agent`

| | |
|---|---|
| **Mission** | Find, evaluate, and progress strategic partnerships. |
| **Inputs** | `PartnershipThesis { objectives, idealPartnerProfile, valueExchange, constraints }` |
| **Outputs** | `PartnerPipeline[{ partner, fitAnalysis, proposedStructure, nextAction, risks }]` |
| **Tools** | `web.search`, `web.fetch`, `crm.read`, `crm.write`, `email.send`, `knowledge.search` |
| **Permissions** | `partner:manage`, `crm:read`, `crm:write` |
| **Memory** | episodic + semantic |
| **Schedule** | Weekly Thursday 10:00 |
| **Approval** | Any outbound contact is gated. Any commercial term is `Financial` — Tenant Owner approval. |
| **KPIs** | Qualified partner conversations, partnerships signed, sourced pipeline value |
| **Escalation** | Exclusivity, revenue share, or IP terms → mandatory `legal-agent` review before human approval |
| **Risk class** | `Financial` |

### 6.10 `pr-agent`

| | |
|---|---|
| **Mission** | Build earned media coverage and protect reputation. |
| **Inputs** | `PrContext { narrative, milestones[], journalistTargets[], embargoes[], sentimentFeed }` |
| **Outputs** | `PrPlan { pitches[], pressReleases[], mediaList[], crisisResponses[] }` |
| **Tools** | `web.search`, `web.fetch`, `content.draft`, `email.send`, `knowledge.search` |
| **Permissions** | `pr:draft`, `content:write` |
| **Memory** | episodic (pitch acceptance by outlet) + semantic |
| **Schedule** | Weekly; continuous sentiment monitoring |
| **Approval** | **Always gated, elevated.** All external communication requires Tenant Owner or delegated Communications approval. |
| **KPIs** | Placements secured, share of voice, sentiment trend, response time on negative coverage |
| **Escalation** | Reputational incident detected → immediate notification with a draft holding statement, never auto-send |
| **Risk class** | `External` |

**Hard rule.** No public statement is ever auto-sent. Public communication is irreversible in
practice, so it always waits for a human.

---

## 7. Revenue agents

### 7.1 `sales-agent`

| | |
|---|---|
| **Mission** | Advance qualified opportunities with well-researched, personalised engagement. |
| **Inputs** | `Opportunity { account, stage, history, stakeholders, competitors, closeDate }` |
| **Outputs** | `SalesActions { nextBestAction, draftedCommunications[], riskFlags[], forecastDelta }` |
| **Tools** | `crm.read`, `crm.write`, `email.send`, `knowledge.search`, `analytics.query` |
| **Permissions** | `opportunity:manage`, `crm:read`, `crm:write` |
| **Memory** | episodic (what advanced similar deals) + semantic (playbooks, pricing) |
| **Schedule** | Daily 07:30; event-driven on stage change |
| **Approval** | Every outbound communication is gated. Any discount or non-standard term is `Financial`. |
| **KPIs** | Stage conversion, cycle length, forecast accuracy, win rate |
| **Escalation** | Discount beyond policy or non-standard terms → `finance-agent` and `legal-agent` before human approval |
| **Risk class** | `Financial` |

### 7.2 `crm-agent`

| | |
|---|---|
| **Mission** | Keep CRM data complete, accurate, deduplicated, and compliant. |
| **Inputs** | `CrmHygieneScope { objects[], rules[], dedupeStrategy, enrichmentSources }` |
| **Outputs** | `HygieneReport { corrections[], merges[], enrichments[], dataQualityScore }` |
| **Tools** | `crm.read`, `crm.write`, `web.fetch`, `knowledge.search` |
| **Permissions** | `crm:read`, `crm:write`, `crm:merge` |
| **Memory** | episodic + semantic |
| **Schedule** | Nightly 02:00 |
| **Approval** | Record merges are `Irreversible` — always gated, two approvers. Field corrections are gated per batch. |
| **KPIs** | Data quality score, duplicate rate, field completeness, erroneous-merge count = 0 |
| **Escalation** | Ambiguous merge candidate → never auto-propose; queue for human disambiguation |
| **Risk class** | `Irreversible` |

---

## 8. Corporate function agents

### 8.1 `finance-agent`

| | |
|---|---|
| **Mission** | Maintain financial visibility, control spend, and flag variance early. |
| **Inputs** | `FinancialContext { budgets, actuals, commitments, forecasts, currency }` |
| **Outputs** | `FinanceDashboard { burnRate, runway, variances[], forecasts, recommendations[] }` |
| **Tools** | `analytics.query`, `report.generate`, `invoice.issue`, `knowledge.search` |
| **Permissions** | `finance:read`, `budget:read`, `report:create`, `invoice:draft` |
| **Memory** | episodic (forecast accuracy) + semantic (policy) |
| **Schedule** | Daily 06:00 snapshot; weekly review; monthly close support |
| **Approval** | Reporting is `Write`. Issuing an invoice or committing spend is `Financial` — always gated, tiered. |
| **KPIs** | Forecast accuracy, budget variance detection lead time, close cycle time |
| **Escalation** | Projected budget breach → alert at 80% consumed, hard-stop proposals at 100% |
| **Risk class** | `Financial` |

**Threshold policy `[ASSUMED]`.** Default tiers, tenant-configurable upward only:
≤ $1,000 → one Approver; ≤ $10,000 → Administrator; ≤ $100,000 → Tenant Owner;
> $100,000 → Tenant Owner plus a second named approver.

### 8.2 `hr-agent`

| | |
|---|---|
| **Mission** | Support hiring, onboarding, and people operations without ever deciding about a person. |
| **Inputs** | `HrContext { openRoles[], candidates[], onboardingPlans[], policies }` |
| **Outputs** | `HrArtefacts { jobDescriptions[], screeningSummaries[], onboardingChecklists[], policyDrafts[] }` |
| **Tools** | `content.draft`, `knowledge.search`, `ticket.write`, `report.generate` |
| **Permissions** | `hr:draft`, `knowledge:read`, `ticket:write` |
| **Memory** | episodic + semantic. **Candidate-identifying data is excluded from episodic memory.** |
| **Schedule** | Event-driven; weekly pipeline summary |
| **Approval** | Every candidate-facing communication is gated. Screening output is advisory only. |
| **KPIs** | Time to fill, offer acceptance, onboarding completion, adverse-impact audit pass rate |
| **Escalation** | Any bias or fairness concern → block and route to `compliance-agent` |
| **Risk class** | `External` |

**Hard rule.** This agent MUST NOT produce a hire, reject, promote, or terminate decision. It may
summarise against stated criteria; a human decides. Protected characteristics are never inputs.

### 8.3 `legal-agent`

| | |
|---|---|
| **Mission** | Identify legal risk in proposed actions and artefacts, and surface it before commitment. |
| **Inputs** | `LegalReviewRequest { artefact, jurisdiction, contractType, riskAppetite }` |
| **Outputs** | `LegalReview { risks[{severity, clause, issue, suggestedLanguage}], verdict, escalate }` |
| **Tools** | `policy.evaluate`, `knowledge.search`, `web.fetch`, `content.draft` |
| **Permissions** | `legal:review`, `knowledge:read`, `policy:read` |
| **Memory** | episodic (prior positions taken) + semantic (contract templates, policy library) |
| **Schedule** | Event-driven |
| **Approval** | Output is advisory. It never constitutes legal advice or authorises execution. |
| **KPIs** | Issue detection rate against human counsel review, false-negative rate on High severity |
| **Escalation** | Any High severity finding → mandatory human counsel review; the workflow cannot proceed without it |
| **Risk class** | `Read` |

**Disclaimer, surfaced in the UI on every output.** Automated review is a triage aid, not legal
advice, and does not replace qualified counsel.

### 8.4 `compliance-agent`

| | |
|---|---|
| **Mission** | Continuously verify that platform activity satisfies applicable regulatory and internal policy. |
| **Inputs** | `ComplianceScope { frameworks[], controls[], evidenceSources[], period }` |
| **Outputs** | `ComplianceReport { controlStatus[], gaps[], evidence[], remediationPlan[] }` |
| **Tools** | `policy.evaluate`, `analytics.query`, `report.generate`, `knowledge.search`, `ticket.write` |
| **Permissions** | `compliance:read`, `audit:read`, `report:create`, `ticket:write` |
| **Memory** | episodic + semantic |
| **Schedule** | Daily control checks; monthly report; continuous on policy-relevant events |
| **Approval** | Reports are `Write`. **This agent can unilaterally block a workflow** on a hard-fail control. |
| **KPIs** | Control coverage, gap remediation time, audit findings, false-block rate |
| **Escalation** | Hard-fail control → block the affected workflow and raise Critical immediately |
| **Risk class** | `Write` |

**Design note.** This is the one agent with unilateral blocking authority. Blocking is fail-safe;
the asymmetry is deliberate.

---

## 9. Technology agents

### 9.1 `dev-agent`

| | |
|---|---|
| **Mission** | Implement well-scoped changes as reviewable pull requests. |
| **Inputs** | `DevTask { repository, issue, acceptanceCriteria, constraints, targetBranch }` |
| **Outputs** | `PullRequest { branch, diff, description, testsAdded[], selfReview }` |
| **Tools** | `repo.read`, `repo.propose`, `knowledge.search`, `ticket.write` |
| **Permissions** | `repo:read`, `repo:propose`, `ticket:write` |
| **Memory** | episodic (review feedback on prior PRs) + semantic (conventions) |
| **Schedule** | Event-driven from the backlog |
| **Approval** | Opening a PR is `Write`. **Merging is never available to this agent.** |
| **KPIs** | PR acceptance rate, review cycles per PR, defect escape rate, test coverage delta |
| **Escalation** | Ambiguous acceptance criteria → ask, do not guess |
| **Risk class** | `Write` |

**Hard rule.** No merge permission, ever. Human review is the gate, and it is not optional.

### 9.2 `qa-agent`

| | |
|---|---|
| **Mission** | Verify changes against acceptance criteria and guard against regression. |
| **Inputs** | `QaScope { pullRequest, acceptanceCriteria, testPlan, riskAreas }` |
| **Outputs** | `QaReport { testResults[], defects[{severity, repro, expected, actual}], coverageDelta, verdict }` |
| **Tools** | `repo.read`, `ticket.write`, `knowledge.search`, `analytics.query` |
| **Permissions** | `repo:read`, `ticket:write`, `test:execute` |
| **Memory** | episodic (historical defect clusters) + semantic |
| **Schedule** | Event-driven on PR open and update |
| **Approval** | Verdicts are advisory to the human reviewer |
| **KPIs** | Defect detection rate, escaped defects, false-positive rate, verification turnaround |
| **Escalation** | Critical defect → block the release candidate and notify immediately |
| **Risk class** | `Write` |

### 9.3 `devops-agent`

| | |
|---|---|
| **Mission** | Keep environments healthy, deployments safe, and infrastructure drift visible. |
| **Inputs** | `OpsContext { environments[], deploymentHistory, alerts[], slos[], driftReport }` |
| **Outputs** | `OpsPlan { deploymentProposals[], remediations[], capacityChanges[], runbookUpdates[] }` |
| **Tools** | `deploy.trigger`, `repo.read`, `repo.propose`, `analytics.query`, `ticket.write` |
| **Permissions** | `deploy:propose`, `infra:read`, `ticket:write` |
| **Memory** | episodic (change failure history) + semantic (runbooks) |
| **Schedule** | Continuous monitoring; daily drift check 05:00 |
| **Approval** | **Production deployment is `Irreversible` — always gated with two approvers.** Non-prod is single-approver. |
| **KPIs** | Deployment frequency, change failure rate, MTTR, drift closure time |
| **Escalation** | SLO burn rate breach → page on-call immediately, propose (never execute) a rollback |
| **Risk class** | `Irreversible` |

### 9.4 `security-agent`

| | |
|---|---|
| **Mission** | Detect, triage, and drive remediation of security weaknesses across code, config, and behaviour. |
| **Inputs** | `SecurityScope { repositories[], dependencies[], configurations[], accessGrants[], eventStream }` |
| **Outputs** | `SecurityFindings[{ severity, cwe, asset, evidence, remediation, exploitability }]` |
| **Tools** | `repo.read`, `repo.propose`, `analytics.query`, `ticket.write`, `policy.evaluate` |
| **Permissions** | `security:read`, `audit:read`, `repo:read`, `repo:propose`, `ticket:write` |
| **Memory** | episodic (finding recurrence) + semantic (threat model, standards) |
| **Schedule** | Continuous on events; daily posture scan 03:00 |
| **Approval** | Findings are `Write`. Remediation PRs follow the normal review gate. |
| **KPIs** | Mean time to detect, mean time to remediate by severity, false-positive rate, coverage |
| **Escalation** | Suspected active compromise → immediate Critical page **and** automatic kill-switch recommendation |
| **Risk class** | `Write` |

**Design note.** This agent detects and proposes. It does not change access, revoke credentials, or
modify production configuration — an agent that can lock out its operators is a liability.

---

## 10. Insight and support agents

### 10.1 `research-agent`

| | |
|---|---|
| **Mission** | Answer defined questions with sourced, verifiable evidence and stated confidence. |
| **Inputs** | `ResearchQuestion { question, scope, depth, sourceConstraints, deadline }` |
| **Outputs** | `ResearchReport { findings[{claim, evidence[], confidence}], gaps[], sources[], synthesis }` |
| **Tools** | `web.search`, `web.fetch`, `knowledge.search`, `knowledge.write`, `report.generate` |
| **Permissions** | `research:conduct`, `knowledge:read`, `knowledge:write` |
| **Memory** | episodic + semantic |
| **Schedule** | Event-driven |
| **Approval** | Writing to the shared knowledge base requires Operator approval |
| **KPIs** | Source quality, claim verifiability, contradiction rate, turnaround |
| **Escalation** | Sources conflict → report the conflict with both positions; never silently pick one |
| **Risk class** | `Write` |

**Hard rule.** Every claim carries a resolvable source and an explicit confidence level. "No reliable
source found" is a valid, expected output.

### 10.2 `analytics-agent`

| | |
|---|---|
| **Mission** | Turn platform and business data into correct, decision-grade analysis. |
| **Inputs** | `AnalysisRequest { question, datasets[], dimensions[], period, comparisonBasis }` |
| **Outputs** | `Analysis { metrics[], trends[], anomalies[], drivers[], caveats[], confidence }` |
| **Tools** | `analytics.query`, `knowledge.search`, `report.generate` |
| **Permissions** | `analytics:read`, `kpi:read`, `report:create` |
| **Memory** | episodic (metric definitions previously agreed) + semantic |
| **Schedule** | Daily 05:00 aggregation; event-driven for ad-hoc questions |
| **Approval** | None for analysis. Data export is `External` — always gated. |
| **KPIs** | Analysis accuracy against source of truth, anomaly precision/recall, timeliness |
| **Escalation** | Sample too small or data quality too poor → refuse to conclude, state why |
| **Risk class** | `External` |

**Hard rule.** Statistical caveats are mandatory output, not optional. An analysis without stated
limitations is treated as incomplete.

### 10.3 `reporting-agent`

| | |
|---|---|
| **Mission** | Deliver accurate, on-time, audience-appropriate reporting. |
| **Inputs** | `ReportRequest { template, audience, period, kpis[], distributionList }` |
| **Outputs** | `Report { document, narrative, charts[], appendix, distributionManifest }` |
| **Tools** | `report.generate`, `analytics.query`, `knowledge.search`, `email.send` |
| **Permissions** | `report:create`, `kpi:read`, `analytics:read` |
| **Memory** | episodic (which narratives resonated) + semantic (templates) |
| **Schedule** | Weekly Friday 16:00; monthly on the 1st; quarterly |
| **Approval** | Internal distribution: Operator. **External distribution: always gated at Tenant Owner.** |
| **KPIs** | On-time delivery, factual accuracy, correction rate after issue |
| **Escalation** | Underlying metric unavailable → publish with an explicit gap marker, never interpolate silently |
| **Risk class** | `External` |

### 10.4 `support-agent`

| | |
|---|---|
| **Mission** | Resolve customer issues quickly and correctly, escalating what it cannot fully resolve. |
| **Inputs** | `SupportTicket { customer, issue, history, entitlements, severity }` |
| **Outputs** | `SupportResponse { draftReply, resolution, knowledgeGap, escalation, sentiment }` |
| **Tools** | `knowledge.search`, `knowledge.write`, `crm.read`, `ticket.write`, `email.send` |
| **Permissions** | `support:respond`, `ticket:write`, `crm:read`, `knowledge:read` |
| **Memory** | episodic (resolution effectiveness) + semantic (product docs, prior resolutions) |
| **Schedule** | Continuous; SLA-driven |
| **Approval** | Every customer-facing reply is gated until the agent's rolling quality score exceeds the tenant threshold, then sampled review applies. |
| **KPIs** | First-contact resolution, CSAT, response time vs SLA, escalation rate |
| **Escalation** | Confidence below threshold, or any commitment/refund/legal implication → human, always |
| **Risk class** | `External` |

**Graduated autonomy `[ASSUMED]`.** Default: 100% human review for the first 200 responses; then
sampled review at 20% while quality ≥ 95%; reverting to 100% on any quality regression. This is the
only place in the platform where an external-effect gate relaxes, it is per-tenant opt-in, and it
never applies to commitments, refunds, or legal matters.

### 10.5 `knowledge-management-agent`

| | |
|---|---|
| **Mission** | Keep the tenant knowledge base accurate, current, deduplicated, and well-governed. |
| **Inputs** | `KnowledgeScope { sources[], taxonomy, freshnessPolicy, retentionPolicy }` |
| **Outputs** | `KnowledgeUpdate { ingested[], updated[], deprecated[], conflicts[], taxonomyChanges[] }` |
| **Tools** | `knowledge.search`, `knowledge.write`, `web.fetch`, `report.generate` |
| **Permissions** | `knowledge:read`, `knowledge:write`, `knowledge:deprecate` |
| **Memory** | episodic (retrieval effectiveness) + semantic |
| **Schedule** | Nightly 01:00 ingestion; weekly quality review |
| **Approval** | Ingestion is `Write`. **Deprecating or deleting a document is `Irreversible` — always gated.** |
| **KPIs** | Retrieval precision, staleness rate, duplicate rate, coverage of asked questions |
| **Escalation** | Two authoritative documents contradict → flag both, deprecate neither, notify the owner |
| **Risk class** | `Irreversible` |

---

## 11. Cross-cutting agent rules

These apply to every agent without exception and are enforced by the platform, not by prompt text.

1. **Tool grants are a closed allow-list.** An ungranted tool call fails and raises a security event.
2. **No agent approves anything.** Approval is a human-only capability, structurally.
3. **No agent invokes another agent directly.** All delegation is orchestrator-mediated.
4. **Every run is budget-bounded** in wall-clock, tokens, cost, tool calls, and iteration depth.
5. **Every run is fully recorded** — prompts, completions, tool calls, retrievals, costs.
6. **Untrusted content is fenced.** Retrieved web and document content enters prompts inside
   explicit untrusted-content delimiters, and instructions found within it are never followed.
7. **Tool authorisation is server-side.** The model's output is a *request*; the platform decides.
8. **Uncertainty is reported, not hidden.** Fabrication is a defect of the highest severity.
9. **The kill switch is checked immediately before every external effect**, not only at run start.
10. **Environment determines blast radius.** A Development-scoped agent cannot reach Production
    credentials, data, or integrations — the secret reference itself does not resolve.
