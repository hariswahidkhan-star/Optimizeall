# 21 — Agent Specifications

Twenty-four agents. Each specifies role, objectives, input and output contracts, tools and permissions, knowledge and memory access, model class, execution budget, schedule, trigger, workflow, approval rules, retry, failure and escalation rules, and evaluation criteria.

Shared defaults live in [20 — Agent Foundations](20-agent-foundations.md); only deviations are stated here. Every output extends the universal envelope, so `result` alone is described.

---

# Division 0 — Command

## ORCH · Chief Growth Orchestrator

| | |
|---|---|
| **Role** | AI Chief Growth Officer. Decides what the workforce does today. |
| **Objectives** | Allocate the day's capacity against the eleven value-ranked objectives; close the gap between value rank and share of effort; keep high-value platforms from going quiet; never plan work that cannot be executed. |
| **Input** | `{ plan_date, kpi_snapshot, objective_ranks[], goal_progress[], backlog[], platform_attention[], approval_backlog{count,oldest_age}, budget_state, capacity{human_hours, agent_slots}, data_health{failing_checks[]}, yesterday{completed,failed,blocked} }` |
| **Output `result`** | `{ plan_rationale, priorities[{objective_id, rank, why, share_target}], tasks[{lane, agent_code, title, objective_id, brand_id, priority, executor: "agent"\|"human", estimated_cost, depends_on[]}], deferred[{what, why}], risks[] }` |
| **Tools** | `get_kpi_data` · `get_budget_state` · `get_config` · `get_calendar` · `search_company_knowledge` · `create_task` |
| **Tool permissions** | Read-only except `create_task`. **No external write tool of any kind.** |
| **Knowledge** | Company memory, strategy documents, Growth Playbook, objective rationales |
| **Memory** | Performance memory (what produced results), agent memory (its own prior plans and their outcomes) |
| **Model** | `reason.deep` · Orchestration ceilings |
| **Schedule** | 07:45 org time, working days only, after data health and analytics complete |
| **Trigger** | Scheduled; also on `KpiThresholdBreached(severity=critical)` for an intraday re-plan |
| **Workflow** | `WF-001` Daily Growth Planning |
| **Approval** | The plan is visible, not approved. Individual dispatched items carry their own approval requirements. |
| **Escalation** | Capacity shortfall against a critical objective; a goal that cannot be met by its deadline; budget insufficient for the planned day |
| **Evaluation** | Plan respects value-rank ordering when capacity is constrained · human-executed steps budgeted against human capacity only · no task created for a disabled workflow or degraded connector · deferred items carry a reason · rationale references actual KPI values, not generalities |

**Deliberate limit:** ORCH cannot execute anything. It creates tasks; other agents and humans do the work. An orchestrator with execution tools would concentrate the whole system's blast radius in one prompt.

## OPS · Scheduler & Operations

| | |
|---|---|
| **Role** | Operations manager for time, queues and dependencies. |
| **Objectives** | Fire what is due, in the organisation's time zone; recover missed jobs inside the window; keep queues from starving; gate work on integration health. |
| **Input** | `{ due_jobs[], missed_jobs[], queue_depths{}, integration_health[], calendar_state, kill_switch_state }` |
| **Output `result`** | `{ dispatch[{job_id, queue_class, reason}], defer[{job_id, until, reason}], skip[{job_id, reason}], alerts[] }` |
| **Tools** | `get_calendar` · `get_config` · `update_task` |
| **Model** | `classify` · Deterministic-assist ceilings — **most of this agent's job is deterministic and it is deliberately given the cheapest model**; it exists to handle the judgement cases (which of three conflicting deferrals matters most), not to run the scheduler |
| **Schedule** | Continuous, every 60 s |
| **Workflow** | `WF-022`, `WF-047`, plus dispatch for all scheduled workflows |
| **Escalation** | A job missed beyond its recovery window on a critical workflow; a queue above threshold for more than 15 minutes |
| **Evaluation** | Never dispatches on a non-working day unless explicitly marked · never dispatches to a degraded connector · records intended and actual time on every dispatch |

---

# Division 1 — Intelligence

## MKT · Market & Competitor Intelligence

| | |
|---|---|
| **Role** | Standing watch on the market PCI competes in. |
| **Objectives** | Maintain competitor profiles; surface industry and AI-in-project-controls developments; find opportunities; never present stale information as current. |
| **Input** | `{ scope: "competitors"\|"industry"\|"market"\|"opportunity", entities[], since, focus_markets[] }` |
| **Output `result`** | `{ findings[{headline, summary, relevance, so_what, entities[], recency_days}], competitor_updates[{competitor, field, before, after}], opportunities[{type, description, impact, confidence, effort, strategic_fit}] }` |
| **Tools** | `search_web` · `search_company_knowledge` · `get_kpi_data` |
| **Tool permissions** | Read-only. No write tool. |
| **Knowledge** | Competitor profiles, positioning, Growth Playbook technique 14 (entity authority), market documents |
| **Memory** | Company, performance |
| **Model** | `research` · Research ceilings |
| **Schedule** | 07:30 daily; deep competitor sweep quarterly |
| **Workflow** | `WF-001` input; `WF-027` research studies |
| **Escalation** | A competitor development that changes PCI's positioning; a source conflict on a material fact |
| **Evaluation** | Every finding carries source, retrieval date and publication date · findings older than the freshness threshold are labelled · no finding asserted without a source · relevance reasoning references PCI's actual market, not generic industry commentary |

## LEAD · Lead Intelligence

| | |
|---|---|
| **Role** | Find and qualify the people PCI's highest-value objective depends on. |
| **Objectives** | Apply the workbook's four qualification tests honestly; rate ICP fit and intent with evidence; never lower the bar to hit a volume target. |
| **Input** | `{ candidates[{name, role_title, company, country, profile_url, years_experience, activity_signals}], segment_code, icp_definition, campaign_brief? }` |
| **Output `result`** | `{ qualified[{candidate_ref, icp_fit, intent, disqualification_reason: null, personal_line, personal_line_source}], disqualified[{candidate_ref, disqualification_reason, note}] }` |
| **Contract note** | **No `score` and no `band` field.** The agent supplies two 1–5 judgements with evidence; the database generates the arithmetic. |
| **Tools** | `search_web` · `search_company_knowledge` · `check_suppression` · `search_leads` (duplicate check) |
| **Tool permissions** | Read-only. `create_lead` is **not** granted — the workflow creates the record after validation, so a malformed qualification cannot become a row. |
| **Knowledge** | ICP definition, lead segments, LinkedIn Playbook step 1, personas |
| **Memory** | Prospect memory (prior interactions), performance memory (which segments converted) |
| **Model** | `research` for discovery, `extract` for enrichment · Research ceilings |
| **Schedule** | 09:00 working days |
| **Workflow** | `WF-010`, `WF-011` |
| **Approval** | None to qualify. Approval is required before any outreach against the lead. |
| **Escalation** | A candidate matching a suppression entry by fuzzy identity but not exactly · conflicting employer information |
| **Evaluation** | **ICP match** — does the qualification apply all four tests · **evidence quality** — every rating has a specific, cited, dated claim · **role and company relevance** · **calibration** — confidence tracks accuracy against human re-qualification · **personal line** is specific and true, never generic flattery |

## SEO · Search Optimisation

| | |
|---|---|
| **Role** | Evidence-driven search work across seven pillars and 76 keywords. |
| **Objectives** | Keep clusters healthy; re-verify keyword difficulty; find and type opportunities; protect the brand-defence keyword. |
| **Input** | `{ mode: "cluster_refresh"\|"keyword_reverify"\|"opportunity_scan"\|"internal_links", clusters[], keywords[], gsc_data, period }` |
| **Output `result`** | `{ cluster_health[{cluster, position, clicks, impressions, trend, orphans[]}], keyword_updates[{keyword, difficulty_before, difficulty_after, who_ranks_now, rationale}], opportunities[{type: "technical"\|"content"\|"optimisation"\|"authority"\|"local", evidence[], score, recommended_action}], internal_link_proposals[{from_url, to_url, anchor, rationale}] }` |
| **Tools** | `retrieve_search_console_data` · `search_web` · `get_cluster_health` · `search_company_knowledge` |
| **Model** | `research` · Research ceilings |
| **Schedule** | Cluster refresh monthly; P1 keyword SERP check monthly; full re-verification quarterly |
| **Workflow** | `WF-023`, `WF-024`, `WF-028` |
| **Approval** | Every site change and every priority change |
| **Escalation** | Brand-defence keyword position worsens; a Hard keyword recommended for direct attack (which the workbook forbids for a new domain) |
| **Evaluation** | Volume bands **always labelled an editorial estimate, never presented as tool data** · opportunities correctly typed · difficulty re-grades cite the live SERP sampled · no recommendation to attack a Hard term without a pillar |

## AEO · Answer-Engine & Entity Authority

| | |
|---|---|
| **Role** | PCI's visibility in AI answers and its entity graph. |
| **Objectives** | Run the monthly citation audit; keep entity facts identical everywhere; verify crawler and index access; never overstate what an audit proves. |
| **Input** | `{ prompt_set_version, engines[], entity_facts{site, wikidata, crunchbase, linkedin, gbp}, crawler_state, index_state }` |
| **Output `result`** | `{ audit[{prompt, engine, brand_mentioned, cited_domains[]}], mention_rate, parity_issues[{field, source_a, value_a, source_b, value_b}], access_issues[], recommendations[] }` |
| **Tools** | `search_web` · `run_answer_engine_audit` · `retrieve_search_console_data` · `search_company_knowledge` |
| **Model** | `research` · Research ceilings |
| **Schedule** | Monthly audit; quarterly entity-graph review |
| **Workflow** | `WF-025`, `WF-036` |
| **Escalation** | Entity facts diverge across two authoritative profiles; a crawler is blocked at the CDN |
| **Evaluation** | Audit results reported as observations, never as guaranteed citations · parity issues name both sources and both values · no recommendation that requires an unevidenced claim about PCI |

## ANA · Analytics & Attribution

| | |
|---|---|
| **Role** | Turn collected data into KPIs that can be defended. |
| **Objectives** | Compute KPIs with lineage; distinguish correlation from attribution; disclose freshness; never fabricate a number. |
| **Input** | `{ period, kpi_codes[], raw_observations[], source_freshness{}, prior_period, four_week_average }` |
| **Output `result`** | `{ kpis[{code, value, target, status, delta_vs_last, delta_vs_4wk, computation_id, source_freshness}], narrative[{kpi_code, reading, evidence_refs[]}], attribution_claims[{claim, basis: "attribution"\|"correlation", confidence}], cannot_compute[{kpi_code, reason}] }` |
| **Tools** | `get_kpi_data` · `retrieve_analytics_data` · `retrieve_search_console_data` · `get_report` |
| **Tool permissions** | Read-only. KPI values are computed by the platform; this agent **interprets** them and may not write them. |
| **Model** | `extract` for collection, `reason.deep` for attribution reasoning · Research ceilings |
| **Schedule** | 07:15 collection; weekly and monthly analysis |
| **Workflow** | `WF-042`, `WF-043`, `WF-005` |
| **Escalation** | A KPI moves past a critical threshold; a source is stale beyond its threshold; two sources disagree materially |
| **Evaluation** | **Never states attribution where only correlation exists** · every narrative claim references a computation id · stale sources disclosed in the narrative, not just in metadata · `cannot_compute` used rather than a guessed value |

---

# Division 2 — Content

## CSTRAT · Content Strategy

| | |
|---|---|
| **Role** | Decide what gets written and why. |
| **Objectives** | Select from the brief bank by priority and difficulty; respect the throughput cap; keep the pillar structure coherent; avoid repeating what already exists. |
| **Input** | `{ available_briefs[], cluster_health[], keyword_priorities[], schedule_coverage[], published_recent[], throughput_cap{used, limit}, campaign_brief? }` |
| **Output `result`** | `{ selection[{brief_id, pillar, cluster, audience, funnel_stage, platform, format, cta, objective_id, brand_id, why_now}], not_selected[{brief_id, why}], coverage_actions[] }` |
| **Tools** | `get_brief` · `get_cluster_health` · `get_keywords` · `check_similarity` · `get_campaign_brief` |
| **Model** | `draft.short` · Composition ceilings |
| **Schedule** | 13:00 working days |
| **Workflow** | `WF-020` |
| **Escalation** | Throughput cap reached with a schedule below coverage — a genuine conflict between two workbook rules, and a human decides which yields |
| **Evaluation** | P1 and Easy prioritised as the workbook directs · similarity checked before selection · never selects beyond the cap · `why_now` references cluster health or coverage, not preference |

## CWRITE · Content Writer

| | |
|---|---|
| **Role** | Produce drafts an expert would sign. |
| **Objectives** | Write from the brief and its prompt; cite every factual claim; label illustrative figures; hit the word band; never invent a statistic. |
| **Input** | `{ brief{title, pillar, cluster, format, audience, funnel, primary_keyword, supporting_keywords[], word_min, word_max, prompt_text}, brand_system, approved_claims[], research[], prior_versions[], qa_feedback? }` |
| **Output `result`** | `{ title, body_markdown, word_count, meta_description, faq[{question, answer}], claims[{text, type: "opinion"\|"internal_fact"\|"public_fact"\|"statistic"\|"capability"\|"customer_result", evidence_ref}], internal_link_targets[], cta, keywords_used[] }` |
| **Tools** | `get_brief` · `search_company_knowledge` · `search_web` · `check_similarity` |
| **Tool permissions** | Read-only. `create_content_draft` is called by the workflow, not the agent. |
| **Knowledge** | Brand system, approved claims store, prior published content, standards references |
| **Memory** | Content memory (what exists, what performed), campaign memory |
| **Model** | `draft.long` · Drafting ceilings |
| **Schedule** | Triggered by `WF-020` |
| **Approval** | Every external publication |
| **Escalation** | A material claim it cannot evidence — **this will be common until the approved-claims store is populated (A-10)** |
| **Evaluation** | **Brand alignment** · **factual accuracy** — every public claim cited · **originality** — similarity below threshold · **relevance** to the stated audience and funnel stage · **CTA quality** · **compliance** — no award language, no invented statistics, meta description under 155 characters, FAQ block present |

## CQA · Brand & Editorial QA

| | |
|---|---|
| **Role** | The second pair of eyes, and the first line against thin content. |
| **Objectives** | Judge against named criteria; reject with specific, actionable feedback; never pass something to protect throughput. |
| **Input** | `{ content_version, brief, brand_system, terminology{approved[], prohibited[]}, similarity_result, prior_feedback[] }` |
| **Output `result`** | `{ verdict: "pass"\|"reject", criteria[{name, verdict, finding, location}], feedback_code, revision_guidance, severity }` |
| **Criteria** | Brand voice · terminology · duplication · grammar · CTA quality · audience fit · structure (FAQ, meta description, named author) · claim support · keyword usage without stuffing |
| **Tools** | `search_company_knowledge` · `check_similarity` |
| **Model** | `classify` for deterministic checks, `judge`-adjacent reasoning for voice · Review ceilings |
| **Workflow** | `WF-020`, `WF-034` |
| **Escalation** | Third rejection of the same item — a signal the brief is wrong, not the draft |
| **Evaluation** | Rejections cite a location and a criterion, never "improve the tone" · deterministic checks match the platform's own computation exactly · does not reject for style preference where the brand system is silent |

**Four-eyes note:** CQA is a different principal from CWRITE, and neither can approve. The database enforces it.

## PUB · Publishing & Syndication

| | |
|---|---|
| **Role** | Execute approved publication correctly, once. |
| **Objectives** | Respect every channel rule; never publish a copy where canonicals are unsupported; never publish twice. |
| **Input** | `{ content_item, channel_policy{canonical_support, link_behaviour, publishing_rule, max_derivatives}, approval, original_index_state, existing_derivatives_count }` |
| **Output `result`** | `{ decision: "publish"\|"block", channel, canonical_url, block_reason?, syndication_plan[{channel, mode: "canonical"\|"original"\|"excerpt"\|"skip", rationale}] }` |
| **Tools** | `publish_content` · `check_index_state` · `get_config` |
| **Tool permissions** | The only content agent with a **write** tool, and it fires only on an item carrying a granted approval. |
| **Model** | `classify` · Review ceilings — this is a rule-application job, not a creative one |
| **Workflow** | `WF-020`, `WF-021` |
| **Approval** | Already granted before this agent runs |
| **Escalation** | Channel policy and campaign plan conflict |
| **Evaluation** | **Never proposes publishing a rankable page's copy to LinkedIn Articles, Substack or Vocal** · waits for indexation before syndicating · respects the derivative cap · block reasons quote the rule |

---

# Division 3 — Relationships

## OUT · Outreach Composer

| | |
|---|---|
| **Role** | Compose the message a senior practitioner will actually answer. |
| **Objectives** | Use only approved templates; personalise from real profile evidence; stay inside the character limit; never imply an award. |
| **Input** | `{ lead{name, role, company, country, years_experience}, template{code, body, char_limit, rules}, personal_line, personal_line_evidence, prior_interactions[], follow_up_number }` |
| **Output `result`** | `{ template_code, composed_text, char_count, personalisation_used, brackets_filled{}, rationale }` |
| **Contract note** | The agent **cannot choose to write freely**: `template_code` is required and `composed_text` must be the template with brackets substituted. Divergence beyond substitution fails validation. |
| **Tools** | `get_message_template` · `check_suppression` · `search_company_knowledge` |
| **Model** | `draft.short` · Composition ceilings |
| **Schedule** | 10:00 connection notes; 11:00 first messages; 12:00 follow-up timers |
| **Workflow** | `WF-012`, `WF-014`, `WF-015` |
| **Approval** | **Always.** Every item, without exception. |
| **Escalation** | **Any question about fees or cost** (A-12) · a request for something PCI cannot evidence · a reply it cannot classify confidently |
| **Evaluation** | **Adversarial cases are the priority here:** never produces award or selection language · never exceeds the limit after substitution · never composes for a suppressed or declined contact · escalates every fee question · personalisation is specific and true |

## COMM · Community

| | |
|---|---|
| **Role** | Draft answers that would be useful even with the PCI link removed. |
| **Objectives** | Answer the question completely first; respect each community's rules; never post. |
| **Input** | `{ thread{platform, url, question, context}, community_rules{link_policy, self_promotion, disclosure}, prior_answers_in_community[], link_budget_used }` |
| **Output `result`** | `{ answer_markdown, includes_link, link_justification?, disclosure_line, rules_checked[], useful_without_link: boolean }` |
| **Tools** | `search_company_knowledge` · `search_web` |
| **Tool permissions** | **No posting tool exists for any community platform.** Not restricted — absent. |
| **Model** | `draft.short` · Composition ceilings |
| **Workflow** | `WF-034` |
| **Approval** | Always; a human posts |
| **Evaluation** | `useful_without_link` must be defensible · link ratio respected per community · affiliation disclosed · never the same answer across two threads |

## PART · Partnership · PR · PR & Media · EVT · Events · LINK · Link Building · DIR · Direct Channels

These five share a structure: research a named target, score fit, prepare an approach, never contact.

| Agent | Objectives | Distinct output fields | Model | Cadence |
|---|---|---|---|---|
| **PART** | Discover and score organisations; stage-manage; prepare proposals | `candidates[{organisation, type, why_them, icp_fit, intent, what_they_get, stage_recommendation}]` — again **no score field**, the database generates it | `research` | 25 approaches/week pacing |
| **PR** | Work the verified route directory; respond to journalist requests within two hours | `pitches[{outlet, angle, hook, word_count, expertise_match}]`, `journalist_responses[{query, answer_80_120_words, data_cited}]` | `draft.short` | Twice-daily monitoring; monthly article and podcast |
| **EVT** | Webinar topics, speaker invitations, promotion and derivative-content plans | `event_plan{topic, speaker_targets[], promotion_schedule[], derivative_content[]}` | `draft.short` | Monthly |
| **LINK** | Competitor gap, unlinked-mention reclaim, resource-page prospecting | `prospects[{domain, their_page, our_target_url, tactic, anchor_proposal, rationale}]` | `research` | 10 prospects and 5 outreach weekly |
| **DIR** | Email, WhatsApp and SMS campaign preparation with consent and frequency enforcement | `campaign{segment, subject, body, cta, consent_basis_verified, frequency_check}` | `draft.short` | Fortnightly newsletter; ≤1 broadcast/week |

All five: read-only tools plus `create_approval_request`; approval always required; escalate on unevidenced claims and fee questions; evaluated on evidence quality, target relevance, and — for PR and DIR — on **never pitching data PCI does not hold** and **never sending without a recorded consent basis**.

**LINK carries one hard evaluation gate:** it must never propose a paid link, a private blog network, a reciprocal swap or a comment-spam tactic. Those are absent from its tool surface and any proposal of them in evaluation is an automatic promotion block.

---

# Division 4 — Governance

## COMP · Compliance & Safety

| | |
|---|---|
| **Role** | The check before anything leaves the building. |
| **Objectives** | Classify claims; verify the evidence-requiring ones; apply the Golden Rules and QA checks; block rather than warn. |
| **Input** | `{ artefact{type, content, target?, channel?}, claims[], approved_claims_store, golden_rules, qa_checks, suppression_state, consent_state, frequency_state, channel_policy }` |
| **Output `result`** | `{ verdict: "pass"\|"block", checks[{rule, verdict, finding, quote?}], claim_findings[{claim, type, evidence_status: "supported"\|"unsupported"\|"not_required", source?}], required_actions[] }` |
| **Tools** | `search_company_knowledge` · `check_suppression` · `evaluate_policy` |
| **Model** | `classify` · Review ceilings |
| **Workflow** | Every lane, before approval |
| **Approval** | COMP does not approve; it clears or blocks. Approval is human. |
| **Escalation** | A claim it cannot classify; a rule that appears to conflict with another |
| **Evaluation** | **Zero false negatives on the honesty rules is the only acceptable score** — a missed award-language instance is an automatic promotion block · findings quote the offending text · does not block for style |

## DQ · Data Health · COST · Cost & Budget

| Agent | Role | Output | Model | Schedule | Escalation |
|---|---|---|---|---|---|
| **DQ** | The ten checks as continuous validation, plus reconciliation against systems of record | `{ checks[{name, count, failing_records[]}], reconciliation[{conversions_without_order_ref[]}], blocking_report: boolean }` | `extract` · Deterministic-assist | 07:00 daily, and after every bulk write | Any non-zero check; any conversion without a PCI order reference |
| **COST** | Track spend, enforce caps, recommend savings that do not degrade quality | `{ spend_by{agent, workflow, campaign, model}, budget_state, recommendations[{change, estimated_saving, quality_risk, evidence}] }` | `classify` · Deterministic-assist | Continuous; daily summary | Budget threshold; unusual spike |

**COST carries an unusual evaluation criterion:** every recommendation must state a **quality risk** alongside its saving. A recommendation that omits the risk fails, because a saving without its cost is how cost optimisation quietly degrades the output it was meant to protect.

---

# Division 5 — Memory & Reporting

## KNOW · Knowledge

| | |
|---|---|
| **Role** | Organisational memory, and the controlled path by which the workbook updates configuration. |
| **Objectives** | Ingest and version documents; serve retrieval with citations and permissions; propose configuration changes, never apply them. |
| **Input** | `{ mode: "ingest"\|"retrieve"\|"import_diff", document?, query?, workbook?, current_config? }` |
| **Output `result`** | Retrieval: `{ chunks[{text, document_id, document_version_id, chunk_id, relevance}] }`. Import: `{ change_groups[{group, entity, before, after, impact, risk}], structural_issues[], rows_ignored[] }` |
| **Tools** | `search_company_knowledge` · `index_document` · `diff_configuration` |
| **Tool permissions** | **`apply_configuration` is not granted to any agent.** Configuration changes are applied by the platform after human approval, per change group. |
| **Model** | `extract` · Research ceilings |
| **Escalation** | A workbook import that would change a policy, an objective rank, a target or an approval rule — those groups are flagged high-impact regardless of size |
| **Evaluation** | Retrieval returns citations with document versions · import diffs are complete — **a silently dropped row is the worst possible failure here** · structural issues name the sheet and the expectation |

## EXP · Experimentation

| | |
|---|---|
| **Role** | Keep the platform honest about what it has actually learned. |
| **Objectives** | Register hypotheses with baselines and guardrails; apply the workbook's own significance guard; never over-claim. |
| **Input** | `{ experiment{hypothesis, baseline, variants, metric, guardrail_metric, samples, results}, prior_experiments[] }` |
| **Output `result`** | `{ verdict: "actionable"\|"directional"\|"inconclusive", rate_a, rate_b, difference, guard_result{n_per_group_ok, gap_ok, under_100_per_group}, caveat_text, conclusion, recommended_next_action, playbook_change_proposed? }` |
| **Contract note** | `caveat_text` is **required and non-empty** whenever `verdict` is not `inconclusive`. The schema will not accept a result stated without its caveat. |
| **Model** | `reason.deep` · Composition ceilings |
| **Approval** | Rolling anything into the playbook is always approved by a human |
| **Evaluation** | Never describes a heuristic guard as a statistical test · anything under 100 per group labelled directional · recommends more data rather than a decision when the guard fails |

## EXEC · Executive Reporting & Executive AI

| | |
|---|---|
| **Role** | Tell the owner what happened, and answer questions from records. |
| **Objectives** | Write the daily brief, end-of-day report and weekly review; answer questions with citations; refuse to state a metric it cannot source. |
| **Input** | Reporting: `{ period, kpis[], tasks{}, content[], leads[], outreach[], partnerships[], seo[], cost{}, approvals{}, blockers[], data_health }`. Executive AI: `{ question, permitted_scopes[] }` |
| **Output `result`** | Reporting: `{ sections[{heading, body, evidence_refs[]}], priorities_tomorrow[], decisions_needed[], watermark_reason? }`. Q&A: `{ answer, citations[{record_type, record_id, as_of}], freshness_disclosure, cannot_answer_reason? }` |
| **Tools** | `get_kpi_data` · `get_report` · `search_leads` · `get_cluster_health` · `get_budget_state` · `search_company_knowledge` |
| **Tool permissions** | Read-only throughout. The Executive AI cannot change anything; executive *commands* become explicit configuration changes through the platform, with confirmation — never through this agent. |
| **Model** | `reason.deep` · Orchestration ceilings for weekly, Composition for daily |
| **Schedule** | 08:00 brief · 18:00 end of day · week-start review |
| **Escalation** | Data health non-zero (the report is watermarked and says which checks failed) |
| **Evaluation** | **Every business metric carries a citation** · refuses unanswerable questions instead of estimating · discloses stale sources · never presents correlation as attribution · the daily brief fits on one screen |

---

## Coverage note

Twenty-four agents specified. Each has: role, objectives, input contract, output contract, tools, tool permissions, knowledge access, memory access, model requirements, reasoning configuration, execution budget, schedule, trigger, workflow, approval rules, retry rules, failure rules, escalation rules and evaluation criteria — with shared defaults in document 20 rather than repeated twenty-four times. Test cases are in [23 — Evaluation & Test Cases](23-evaluation-and-tests.md).
