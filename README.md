# OptimizeAll

An enterprise AI operating system: a governed execution platform where a workforce of specialised AI
agents performs real business work — marketing, SEO, content, sales, finance, HR, engineering,
compliance — under a central orchestrator, with human approval gates on every consequential action
and an immutable audit trail covering everything the system does.

This is not a chatbot. Agents here publish, send, spend and deploy. The engineering problem is
therefore not making them capable; it is bounding what they can do and proving afterwards what they
did.

---

## The three guarantees

Everything in this repository exists to support three properties. Each is enforced in the domain and,
where it matters most, in the database as well.

**1. A tenant cannot observe or affect another tenant.**
Ambient scope from signed token claims, EF Core query filters, and PostgreSQL row-level security with
`FORCE`. Verified by execution against a live database as the non-superuser application role:
cross-tenant reads return nothing, cross-tenant writes are rejected, and an unset scope yields zero
rows rather than an error.

**2. No consequential action happens without a specific human authorising that specific action.**
Publishing, outreach, spend and deployment are gated by risk class, with no configuration path that
disables the gate. An agent can never approve. A requester can never approve their own request. And
the payload is re-hashed at execution: if it differs from what was approved by a single byte,
execution is refused.

**3. What happened cannot be silently altered.**
Per-tenant SHA-256 hash chain, append-only by database permission and by trigger, verified nightly.
Tampering is detectable — which is the property an auditor needs.

---

## Repository layout

```
docs/                      Specification, architecture, security, operations
  01-srs.md                Requirements with stable, traceable IDs
  02-architecture.md       C4 views, layering, isolation and approval models
  03-data-model.md         Schema, RLS policies, retention
  04-agent-catalog.md      29 agent specifications
  06-security.md           Threat model and controls
  07-testing-strategy.md   What is proven, and what is not yet
  08-deployment-azure.md   Topology, rollout, runbooks
  09-cost-model.md         Estimates with stated assumptions
  10-rollout-plan.md       Honest current state and phasing
  adr/                     Decisions, alternatives, and what each one costs

backend/
  src/OptimizeAll.SharedKernel     Result, Entity, ValueObject, IClock — no dependencies
  src/OptimizeAll.Domain           Aggregates and invariants — no infrastructure
  src/OptimizeAll.Application      Use cases, ports, pipeline, agent runtime
  src/OptimizeAll.Infrastructure   EF Core, Redis, AI providers, Key Vault
  src/OptimizeAll.Api              REST surface
  src/OptimizeAll.Worker           Orchestrator, agent runtime, scheduler, sweeps
  tests/                           Domain, Application, Infrastructure, Architecture

frontend/                  React 19 + TypeScript
infra/terraform/           Azure infrastructure as code
.github/workflows/         CI and security pipelines
```

Layering is enforced by tests, not convention: the domain cannot reference EF Core, the application
cannot reference a database provider, and no AI provider SDK type may cross the infrastructure
boundary.

---

## Running it locally

```bash
docker compose up --build
```

- Web: http://localhost:5173
- API: http://localhost:8080 (Swagger at `/swagger` in development)

Set `ANTHROPIC_API_KEY`, `OPENAI_API_KEY` or `GOOGLE_API_KEY` to enable the corresponding provider.
Providers that are not configured are simply absent from the router's rotation.

The stack connects to PostgreSQL as `optimizeall_app`, deliberately — not as `postgres`. Superusers
bypass row-level security unconditionally, so developing as one means the isolation policies are
never actually exercised until production.

### Backend without Docker

```bash
cd backend
dotnet restore && dotnet build && dotnet test
dotnet dotnet-ef database update \
  --project src/OptimizeAll.Infrastructure \
  --startup-project src/OptimizeAll.Infrastructure
dotnet run --project src/OptimizeAll.Api
```

### Frontend

```bash
cd frontend
npm ci
npm run dev        # http://localhost:5173, proxying /api to localhost:8080
npm test
```

---

## Technology

| Layer | Choice | Why |
|---|---|---|
| Frontend | React 19, TypeScript, Vite, TanStack Query | Server state is not client state |
| Backend | .NET 9, DDD, Clean Architecture | Layering enforced by tests |
| Database | PostgreSQL 16 + pgvector | RLS is a first-class isolation primitive; one datastore, one backup |
| Cache & queue | Redis 7 Streams | Consumer groups give at-least-once with acknowledgement |
| AI | OpenAI, Anthropic, Google — interchangeable | No vendor type crosses the infrastructure boundary |
| Runtime | Docker, Azure Container Apps | Queue-depth autoscaling, scale to zero in non-production |

---

## Testing

```bash
cd backend && dotnet test    # 165 tests
cd frontend && npm test      # 12 tests
```

The suite is weighted deliberately toward the three guarantees above. `docs/07-testing-strategy.md`
states what is proven, how, and — equally — what is not yet covered.

---

## Current status

Built and verified: the domain model and its invariants, the agent runtime and tool authorisation
chokepoint, the database schema with row-level security proven by execution, the modular provider
layer, the REST API and workers, the web application, containers, Azure infrastructure as code, CI/CD
with security scanning, and the 29-agent seeded workforce validated against the domain's own rules.

Not yet done, and blocking general availability: an automated integration suite, an end-to-end
approval journey test, load testing against the stated targets, provider contract tests, a restore
drill, and an independent security review. These are listed as acceptance criteria in the SRS and are
phased in `docs/10-rollout-plan.md`.

The performance figures in the SRS are targets, not measurements, and the cost figures are arithmetic
on stated assumptions. Both say so where they appear.

---

## A note on the source workbook

The stakeholder brief referenced an accompanying workbook of worksheets — schedules, KPIs, agent
responsibilities — as the single source of truth. That workbook was not present in the repository.
Every requirement that would have derived from it is marked `[ASSUMED]` in the SRS with a documented
default, so that supplying the workbook is a reconciliation exercise rather than an archaeology one.
