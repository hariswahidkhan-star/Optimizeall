# PCI AI Autonomous Growth OS

A governed AI workforce for the Project Controls Institute, derived from
`PCI_AI_Growth_OS.xlsx` — 42 worksheets analysed in full and turned into a
specification, an architecture, and now a running platform.

**The central design commitment:** deterministic software owns identity,
permissions, schedules, state, budgets, approvals, integrations and audit. AI owns
reasoning, research, synthesis, drafting, classification and bounded recommendation.
An agent proposes a structured tool request; the platform validates, authorises,
policy-checks, approval-checks, rate-limits, deduplicates, executes and audits it.

## Status

| Phase | Deliverable | State |
|---|---|---|
| 0–1 | Workbook analysis and Software Requirements Specification | Delivered |
| 2 | System, agent, data, security and integration architecture | Delivered |
| 3 | Information architecture and 25 screen specifications | Delivered |
| 4 | Schema, API contracts, auth, events, queues, vectors | Delivered |
| 5 | 24 agent specifications, prompts and evaluation suite | Delivered |
| 6 | Workflow catalogue — 40 workflows, 16 enabled at launch | Delivered |
| **Build** | **Slice 1 — authentication, organisation, RBAC** | **Running, 49 tests passing** |

Specifications live in [`docs/`](docs/README.md). Slice 1 is documented in
[docs/25](docs/25-build-slice-1.md).

## Quick start

```bash
cd infrastructure && docker compose up      # everything, one command
```

Or against a local PostgreSQL 16 with `pgvector`:

```bash
make install
make db-roles db-create
make migrate seed
make run          # http://127.0.0.1:8080/api/v1/docs
make token        # mint a development token
make check        # lint + strict types + tests — exactly what CI runs
```

## Repository

```
apps/api/           FastAPI platform core — 13 modules, 17 schemas
docs/               Phases 0–6 specifications and the build log
infrastructure/     docker compose, Dockerfile, database bootstrap
.github/workflows/  CI: lint, strict types, tests, migration round-trip
```

## Two things worth knowing before reading the code

**Tenant isolation is a database feature, not a convention.** Every org-scoped table
has `FORCE ROW LEVEL SECURITY`; the application role owns nothing and cannot bypass
it. A query that forgets its organisation predicate returns nothing rather than
another organisation's data — and an unbound session sees nothing at all.

**The workbook forbids automating its own highest-value activity.** Golden Rule 5 —
*no bulk sending, no automation tools, no bots, no scraping* — is repeated in the
LinkedIn Playbook, QA check 5 and Growth Playbook technique 15. The outreach lane is
therefore built as *prepare → verify → approve → human executes → confirm → measure*.
That is not a reduced feature: it is roughly 90% of the labour and all of the
leverage, minus the one step the platform must not take.
