# PCI AI Autonomous Growth OS — Phase 0/1 Specification Set

**Status:** Phase 1 (Software Requirements Specification) — AWAITING APPROVAL
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
