# PCI AI Autonomous Growth OS — Phase 0/1 Specification Set

**Status:** Phase 5 (Agent Design) — AWAITING APPROVAL. Phase 1 delivered; five blocking prerequisites still unanswered and carried as explicit assumptions in document 08.
**Source of truth:** `PCI_AI_Growth_OS.xlsx` (42 worksheets, read in full 18 Aug 2026)
**Rule:** No application code, schema migrations, frontend, or deployment artefacts are produced until Phase 1 is explicitly approved.

## Document set

| # | Document | Contains |
|---|---|---|
| 01 | [Workbook Analysis](01-workbook-analysis.md) | Workbook Analysis Summary; Sheet-by-Sheet Requirements Map (all 42 sheets) |
| 02 | [Workflow & Agent Model](02-workflow-and-agent-model.md) | Identified Business Workflows; AI Agent Workforce; Automation Classification; Human Approval Matrix |
| 03 | [Platform & Integration Matrix](03-platform-integration-matrix.md) | 133-platform estate, API availability, permitted/forbidden automation, connector tiers |
| 04 | [KPI & Scheduling Catalogue](04-kpi-and-scheduling-catalogue.md) | KPI Catalogue with formulas and lineage; Scheduling Catalogue (daily/weekly/monthly/quarterly) |
| 05 | [Requirements Traceability Matrix](05-traceability-matrix.md) | Workbook sheet → Requirement ID → Workflow → Agent → Entity → API → UI → Test |
| 06 | [Software Requirements Specification](06-srs.md) | The full SRS, sections 1–36, with testable requirement IDs |
| 07 | [Decisions, Risks, Gaps, MVP](07-decisions-risks-mvp.md) | Architecture Decisions Requiring Approval; Automation Risk Register; Workbook Gaps; Open Questions; Recommended MVP; Phase 2 Prerequisites |
| — | [`phase-1-specification.html`](phase-1-specification.html) | The whole of Phase 1 as a single readable document, in the 17-section order the brief requires (published version of documents 01–07) |
| 08 | [System & Deployment Architecture](08-system-architecture.md) | Assumptions taken; four containers; 13 modules with ownership and event contracts; the execution gate; named seams; environments, release, backup, observability |
| 09 | [Agent Architecture](09-agent-architecture.md) | AgentVersion as data; the execution loop; six memory layers; the model gateway; structured output contracts; prompt promotion; four-eyes as a predicate |
| 10 | [Data Architecture](10-data-architecture.md) | Storage topology; row-level isolation; aggregates and invariants; outbox; the external action ledger; KPI lineage; health checks as constraints; retention |
| 11 | [Security Architecture](11-security-architecture.md) | Trust zones and egress matrix; seven authorisation layers; secrets; the three-point kill switch; four-layer injection defence; hash-chained audit |
| 12 | [Integration Architecture](12-integration-architecture.md) | Connector manifest; health state machine; human-assisted channels as first-class; rate limiting; retry taxonomy; MVP connector set |
| 13 | [Sequences & Workflows](13-sequences-and-workflows.md) | Seven sequence diagrams and three workflow state machines |
| — | [`phase-2-architecture.html`](phase-2-architecture.html) | The whole of Phase 2 as a single readable document with six architecture figures (published version of documents 08–13) |
| 14 | [Information Architecture](14-information-architecture.md) | Product stance; the three questions; ten design principles; navigation; screen inventory; cross-cutting loading, empty, error, notification, permission and mobile patterns |
| 15 | [Screen Specifications](15-screen-specifications.md) | All 25 screens with purpose, primary user, data, actions, filters, search, tables, cards, charts, notifications, empty/error/loading states, mobile behaviour and permissions |
| — | [`phase-3-ux.html`](phase-3-ux.html) | The whole of Phase 3 as a single readable document with five wireframes (published version of documents 14–15) |
| 16 | [Data Model & Schema](16-data-model.md) | Conventions; 17 schemas; row-level security; DDL with the workbook's rules as constraints and generated columns; lineage; indexes; partitioning; migration strategy |
| 17 | [API Design](17-api-design.md) | API architecture; error model; idempotency at two levels; ~70 endpoints with method, route, purpose, auth, permission, request, response, validation, errors and idempotency |
| 18 | [Auth, Events & Webhooks](18-auth-events-webhooks.md) | Authentication per principal; the seven authorisation layers concretely; permission set and role matrix; 23 domain events; inbound and reserved outbound webhook architecture |
| 19 | [Queues, Caching & Vectors](19-queues-caching-vectors.md) | Six queue classes; the version-keyed caching rule and what is never cached; chunking, retrieval, model-change and version-awareness for vector storage |
| — | [`phase-4-data-api.html`](phase-4-data-api.html) | The whole of Phase 4 as a single readable document with three figures (published version of documents 16–19) |
| 20 | [Agent Foundations](20-agent-foundations.md) | Four-layer prompt architecture; the standing preamble in full; the universal output envelope; model task classes; ceilings; shared failure, retry and escalation defaults; the evaluation harness |
| 21 | [Agent Specifications](21-agent-specifications.md) | All 24 agents with role, objectives, contracts, tools, permissions, knowledge, memory, model, budget, schedule, trigger, workflow, approval, escalation and evaluation criteria |
| 22 | [System Prompt Bodies](22-agent-prompts.md) | Layer-2 prompt text for all 24 agents |
| 23 | [Evaluation & Test Cases](23-evaluation-and-tests.md) | Scoring method by criterion type; 15 adversarial cases; golden and boundary cases per agent; regression-set discipline; what evaluation cannot tell you |
| — | [`phase-5-agents.html`](phase-5-agents.html) | The whole of Phase 5 as a single readable document with two figures (published version of documents 20–23) |

## Requirement ID namespaces

| Prefix | Domain |
|---|---|
| `FR-nnn` | Functional |
| `NFR-nnn` | Non-functional (performance, availability, scalability) |
| `AI-nnn` | Agent, model, prompt, memory, evaluation |
| `WF-nnn` | Workflow and orchestration |
| `SCH-nnn` | Scheduler and business calendar |
| `APR-nnn` | Approval and autonomy |
| `KB-nnn` | Knowledge base / RAG |
| `INT-nnn` | Integration and connectors |
| `KPI-nnn` | KPI engine, analytics, attribution |
| `RPT-nnn` | Reporting |
| `SEC-nnn` | Security |
| `AUD-nnn` | Audit |
| `CMP-nnn` | Compliance, legal, platform policy |
| `DAT-nnn` | Data model, retention, lineage |
| `OBS-nnn` | Observability |
| `COST-nnn` | AI cost control |
| `UX-nnn` | User experience |
| `MIG-nnn` | Workbook migration and importer |

## Source-of-truth hierarchy applied throughout

1. Security and legal requirements
2. Third-party platform / API restrictions
3. Explicit approved PCI AI policies (workbook Golden Rules, QA & Compliance)
4. Approved software requirements
5. `PCI_AI_Growth_OS.xlsx`
6. Agent recommendations
7. Model assumptions

Where the workbook conflicts with (1)–(3), the conflict is recorded in [07 — Automation Risk Register](07-decisions-risks-mvp.md) rather than silently resolved.
