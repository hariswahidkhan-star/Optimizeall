# 05 — Requirements Traceability Matrix

**Rule `AC-15`:** every production feature must trace to (1) a workbook requirement, (2) a security requirement, (3) a platform/API requirement, (4) a technical requirement, or (5) an approved future requirement. This matrix is the control that prevents the development team losing PCI AI's original operating logic.

**Columns:** Workbook Sheet → Requirement ID → Workflow → Agent → Database Entity → API → UI → Test

## A. Sheet → destination decision (workbook migration mapping, `MIG-001`)

| Sheet | Becomes |
|---|---|
| START HERE | **Policy** + **Configuration** (org, calendar, targets, roster, brands, domains) + **Knowledge** (Golden Rules text) |
| MAP | **UI information architecture** (no data) |
| TEAM GUIDE | **Knowledge** + **Onboarding workflow** |
| GROWTH PLAYBOOK | **Workflow definitions** (23) + **Knowledge** + **Policy** (technique 15 prohibitions) |
| PLATFORM GUIDE | **Database entity** (Platform) + **Workflow** (per-channel weekly play) + **Knowledge** |
| PR & Target Directory | **Database entity** (PRRoute, incl. skip list) + **Workflow** + **Knowledge** |
| Glossary | **Knowledge** + UI help text |
| UPGRADE NOTES | **Archived reference** + **migration acceptance criteria** |
| DAILY ENTRY | **Database entity** (ActivityRecord) — mostly auto-generated |
| LinkedIn Outreach | **Database entities** (Account, Contact, Lead, Interaction, OutreachItem) + **Workflows** |
| Partnership Pipeline | **Database entity** (Partnership) + **Workflow** |
| Content Calendar | **Database entities** (ContentItem, ContentVersion, ContentMetric) + **Workflow** |
| Content Scheduler | **Database entity** (ContentSchedule) + **KPI** (coverage) + **Schedule** |
| Community & PR | **Database entity** (CommunityAction / PRPlacement) + **Workflow** |
| Job Postings | **Database entity** (JobPosting) + **Workflow** |
| Link Building | **Database entity** (LinkProspect) + **Workflow** |
| Experiments | **Database entity** (Experiment) + **Workflow** |
| UTM Builder | **Platform service** (deterministic link minting) |
| SEO Clusters | **Database entity** (SEOCluster) + **Workflow** + **KPI** |
| Keyword Plan | **Database entity** (SEOKeyword) + **Workflow** |
| Article Bank | **Database entity** (ArticleBrief) + **Prompt templates** |
| Daily Log | **Report/query** (not a table) |
| Weekly Pulse | **KPI set** + **Report** |
| Dashboard | **Dashboard** + **Validation service** (§9) |
| Summary | **Report** |
| Objective Performance | **KPI set** + **Orchestrator objective function** |
| Team Scorecard | **Dashboard** (human) + **Agent scorecard** |
| Employee Score | **Restricted HR dataset** |
| Weekly Review | **Workflow** + **Report** |
| Platform Progress | **Dashboard** (estate health) |
| Who Did What | **Dashboard** (coverage matrix) |
| Accounts Register | **Database entity** (PlatformAccount, CredentialReference) |
| Master Tasks | **Database entity** (Task/Workstream) + **Schedules** |
| Platform Setup | **Database entity** (Platform, PlatformAccount) + **Workflow** (onboarding) |
| Publishing Plan | **Policy** (canonical + channel rules) |
| Channel Costs | **Database entity** (CostLine) + **KPI** |
| QA & Compliance | **Policy** + **ComplianceCheck/Attestation entities** |
| Message Bank | **Template Registry** (versioned, approved) |
| LinkedIn Playbook | **Workflow definition** + **Knowledge** |
| How-To Guides | **Knowledge** + **agent prompt source** + **capacity model** |
| Benchmarks | **KPI thresholds with provenance** |
| Lists | **Canonical enumerations** + **dedup rules** + **verification expiry** |

## B. Traceability rows (representative — full set maintained in the repository as the build proceeds)

| Workbook Sheet | Requirement ID | Workflow | Agent | Entity | API | UI | Test |
|---|---|---|---|---|---|---|---|
| START HERE §2 | FR-001, SCH-003, SCH-004 | WF-ADMIN-CONFIG | — | Organization, BusinessCalendarEntry | `GET/PUT /orgs/{id}/settings` | Admin → Organisation | TEST-CFG-001 timezone/week-start propagation |
| START HERE §3 | FR-006, FR-007, K-066 | WF-001 | ORCH | Target, Capacity | `GET /targets` | Dashboard → Activity vs target | TEST-KPI-007 expectation × capacity |
| START HERE §4 (Golden Rules) | CMP-002, FR-031, FR-032, FR-033, FR-036, FR-037, CMP-006, SEC-013 | all outbound | COMP | Policy, PolicyViolation | `POST /policy/evaluate` | Compliance centre | TEST-SEC-010…019 (one per rule) |
| START HERE §6 | FR-005, DAT-003 | — | — | Actor (Human/Agent) | `GET /actors` | Admin → Roster | TEST-DAT-002 no unattributed activity |
| START HERE §7, §11 | FR-002, CMP-012 | WF-UTM-MINT | — | Brand, Domain | `POST /utm` | UTM builder | TEST-INT-030 off-estate link rejected |
| START HERE §9 | DAT-011, SEC-021 | — | — | AuditEvent | — | Audit centre | TEST-SEC-021 protected-field write denied |
| MAP | UX-001, UX-002 | — | — | — | — | Global IA | TEST-UX-001 nav coverage |
| TEAM GUIDE | MIG-005, FR-115 | WF-ONBOARD | KNOW | KnowledgeDocument | `GET /knowledge/search` | Knowledge base | TEST-KB-004 citation returned |
| GROWTH PLAYBOOK 1,20 | FR-042, FR-063, K-029, K-033 | WF-020, WF-023 | CSTRAT, SEO | SEOCluster, ContentItem | `GET /seo/clusters` | SEO workspace | TEST-SEO-001 orphan detection |
| GROWTH PLAYBOOK 3,14 | FR-064, FR-065, K-032 | WF-025 | AEO | AEOAudit, EntityFact | `POST /aeo/audit` | SEO workspace → AEO | TEST-SEO-010 audit prompt set |
| GROWTH PLAYBOOK 4,16 | FR-074, K-045 | WF-033, WF-032 | PR | PRRoute, PRPlacement | `GET /pr/routes` | PR workspace | TEST-PR-002 2-hour fast lane |
| GROWTH PLAYBOOK 5 | OOS-01, FR-036, INT-003 | WF-013 | LI | — | — | Outreach queue | TEST-SEC-030 LinkedIn write tool absent |
| GROWTH PLAYBOOK 6 | FR-075, CMP-011 | WF-034 | COMM | CommunityAction | `POST /community/draft` | Community workspace | TEST-CMP-011 link ratio enforced |
| GROWTH PLAYBOOK 7 | FR-046, FR-047, CMP-010 | WF-021 | PUB | ContentItem(Repurposed) | `POST /content/{id}/syndicate` | Content command centre | TEST-PUB-005 canonical gate blocks |
| GROWTH PLAYBOOK 9 | FR-080…FR-083 | WF-040 | DIR | Campaign, Consent | `POST /campaigns/{id}/send` | Campaigns | TEST-CMP-007 no consent → blocked |
| GROWTH PLAYBOOK 13 | FR-095, KPI-022 | WF-042 | ANA | KPIObservation | `GET /kpi/{id}` | Analytics | TEST-KPI-020 freshness surfaced |
| GROWTH PLAYBOOK 15 | OOS-02…OOS-05, CMP-001 | — | COMP | Policy | — | Compliance centre | TEST-CMP-001 prohibited tactic modelling absent |
| GROWTH PLAYBOOK 17,18 | FR-096, WF-027 | WF-027 | MKT, CWRITE | Experiment, ContentItem | — | Experiments | TEST-EXP-001 guard heuristic |
| GROWTH PLAYBOOK 22 | FR-067 | WF-028 | SEO, CWRITE | ContentItem | — | SEO workspace | TEST-SEO-020 indexation gate blocks batch 2 |
| GROWTH PLAYBOOK 23 | FR-066, K-030 | WF-026 | LINK | LinkProspect | `GET /links` | Link building | TEST-SEO-030 paid-link tactic unavailable |
| PLATFORM GUIDE / Platform Setup | FR-018, INT-010, INT-011, K-062, K-063 | WF-047 | OPS | Platform, PlatformAccount, Integration | `GET /platforms` | Integration centre | TEST-INT-001 health gating; TEST-SEC-005 2FA count |
| PR & Target Directory | FR-072, FR-073, SCH-020 | WF-030, WF-032, WF-036 | PART, PR, AEO | PRRoute, VerificationRecord | `GET /pr/routes` | PR workspace | TEST-PR-010 skip-list override requires justification |
| DAILY ENTRY | FR-014, FR-015, DAT-005, DAT-007 | WF-ACTIVITY-LOG | OPS | ActivityRecord | `POST /activities` | Task inbox | TEST-DAT-007 text date rejected |
| LinkedIn Outreach A–L | FR-020…FR-025 | WF-010, WF-011, WF-017 | LEAD | Account, Contact, Lead | `POST /leads/qualify` | Lead command centre | TEST-LEAD-001 dedup; TEST-LEAD-002 evidence required |
| LinkedIn Outreach M–T | FR-030…FR-036, A01 | WF-012, WF-013, WF-014 | OUT, COMP, CQA | OutreachItem, Approval | `POST /outreach/prepare` | Approval inbox + send queue | TEST-OUT-001 length gate; TEST-OUT-002 award language blocked |
| LinkedIn Outreach U–W | FR-034, WF-021 | WF-015 | OUT | Timer | — | Outreach queue | TEST-WF-021 anchored to send event |
| LinkedIn Outreach Y–AC | FR-022, FR-024 | WF-010 | LEAD | Lead.score, Lead.stage | `GET /leads/{id}` | Lead detail | TEST-LEAD-010 score arithmetic; TEST-LEAD-011 stage derivation |
| LinkedIn Outreach AD–AM | FR-028, INT-021, K-072 | WF-018 | ORCH | Interaction, Conversion | `POST /leads/{id}/handoff` | Lead detail | TEST-FIN-001 Converted requires order ref |
| Partnership Pipeline | FR-070, FR-071 | WF-030, WF-031 | PART | Partnership | `GET /partnerships` | Partnership pipeline | TEST-PART-001 stage transitions |
| Content Calendar | FR-040, K-021 | WF-020 | CSTRAT…PUB | ContentItem, ContentMetric | `GET /content` | Content command centre | TEST-CNT-001 status/date invariant |
| Content Scheduler | FR-048, FR-049, K-027 | WF-022 | OPS | ContentSchedule | `GET /schedules` | Scheduler | TEST-SCH-010 coverage computation |
| Community & PR | FR-075, K-040 | WF-034 | COMM | CommunityAction | `POST /community` | Community workspace | TEST-CMP-011 |
| Job Postings | FR-115, K-053 | WF-049 | OPS | JobPosting | `GET /jobs` | Admin → Hiring | TEST-JOB-001 |
| Link Building | FR-066 | WF-026 | LINK | LinkProspect | `GET /links` | Link building | TEST-SEO-030 |
| Experiments | FR-096…FR-098 | WF-044 | EXP | Experiment | `GET /experiments` | Experiments | TEST-EXP-001 |
| UTM Builder | FR-095, CMP-012 | WF-UTM-MINT | — | TrackedLink | `POST /utm` | UTM builder | TEST-INT-030 malformed-link regression (`UPGRADE NOTES` #2) |
| SEO Clusters | FR-062, K-029 | WF-023 | SEO | SEOCluster | `GET /seo/clusters` | SEO workspace | TEST-SEO-001 |
| Keyword Plan | FR-060, WF-024 | WF-024 | SEO | SEOKeyword | `GET /seo/keywords` | SEO workspace | TEST-SEO-005 volume band labelled as estimate |
| Article Bank | FR-041…FR-044 | WF-020 | CWRITE, CQA | ArticleBrief, Prompt | `GET /briefs` | Content command centre | TEST-CNT-010 similarity block; TEST-CNT-011 unsupported claim blocks approval |
| Dashboard §1–§8 | FR-090…FR-093 | — | ANA | KPI, KPIObservation | `GET /kpi` | Executive dashboard | TEST-KPI-001 lineage; TEST-KPI-002 tile identity regression |
| Dashboard §9 | KPI-030, DAT-006, DAT-007 | WF-002 | DQ | ValidationResult | `GET /data-health` | System health | TEST-DAT-090…099 (one per check) |
| Summary | RPT-004 | WF-003 | EXEC | Report | `GET /reports/summary` | Reports | TEST-RPT-001 |
| Weekly Pulse | RPT-003, K-004…K-078 | WF-005 | ANA, EXEC | KPIObservation | `GET /kpi/pulse` | Analytics | TEST-RPT-003 4-week average excludes current week |
| Objective Performance | FR-003, FR-011, K-068 | WF-001, WF-005 | ORCH | Objective, MinutesByObjective | `GET /objectives/performance` | Analytics | TEST-ORC-001 prioritisation respects value rank |
| Team Scorecard / Employee Score | CMP-014, CMP-015, K-100…K-106 | WF-005 | EXEC | Scorecard | `GET /scorecards` | Admin (restricted) | TEST-SEC-040 RBAC on HR data |
| Weekly Review | RPT-003, FR-100 | WF-005, WF-006 | EXEC | Report | `GET /reports/weekly` | Reports | TEST-RPT-004 |
| Platform Progress / Who Did What | K-067, FR-115 | WF-001 | ORCH | Platform, Coverage | `GET /platforms/progress` | Integration centre | TEST-PLT-001 attention flag |
| Accounts Register | SEC-011, SEC-013, K-063 | WF-047 | OPS | PlatformAccount, CredentialReference | `GET /integrations` | Integration centre | TEST-SEC-013 no secret in prompt |
| Master Tasks | FR-018, FR-014 | all | ORCH | Task | `GET /tasks` | Task inbox | TEST-TSK-001 owner+due required to activate |
| Publishing Plan | FR-046, CMP-010 | WF-020, WF-021 | PUB | ChannelPolicy | — | Content command centre | TEST-PUB-005 |
| Channel Costs | COST-002, K-073…K-076 | WF-045 | COST | CostLine, ModelExecution | `GET /costs` | Costs | TEST-COST-001 aggregation |
| QA & Compliance | CMP-003, WF-046 | WF-046 | COMP | ComplianceCheck, Attestation | `GET /compliance` | Compliance centre | TEST-CMP-003 unsigned control reports failing |
| Message Bank | FR-030, AI-011, A12 | WF-012 | OUT | MessageTemplate, PromptVersion | `GET /templates` | Admin → Templates | TEST-OUT-005 only Approved templates usable |
| LinkedIn Playbook | WF-010…WF-016 | WF-010…WF-016 | LEAD, OUT | — | — | Outreach workspace | TEST-OUT-010 sequence limits |
| How-To Guides | MIG-005, AI-001 | — | KNOW | KnowledgeDocument | — | Knowledge base | TEST-KB-004 |
| Benchmarks | KPI thresholds | WF-043 | ANA | KPIThreshold | `GET /kpi/{id}/thresholds` | Analytics | TEST-KPI-030 threshold provenance shown |
| Lists | DAT-004, DAT-005 | — | — | Enumeration, DedupRule | `GET /enums` | Admin | TEST-DAT-004 unusable value cannot be selected |
| UPGRADE NOTES | MIG-006 | WF-048 | KNOW | OnboardingTask | — | Admin → Setup | TEST-MIG-006 residual actions present |

## C. Non-workbook requirement sources

| Category | Requirements | Justification |
|---|---|---|
| Security | SEC-001…SEC-050 | Enterprise baseline; not derivable from a spreadsheet |
| Platform / API | INT-001…INT-021, OOS-01…OOS-05 | Third-party terms outrank the workbook (source-of-truth hierarchy) |
| Technical | NFR-*, OBS-*, WF-003, WF-030 | Durable execution, idempotency, observability |
| AI governance | AI-001…AI-063, APR-* | Required because the workbook assumes human operators throughout |
| Cost | COST-001…COST-007 | The workbook tracks channel cost only; AI cost is new |
| Future | Phases P2–P5 | Approved future scope |
