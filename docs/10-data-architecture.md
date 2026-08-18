# 10 — Data Architecture

Architecture, not schema. Stores, ownership, aggregates, consistency, lineage, retention and isolation are settled here; column-level DDL, indexes and migrations are Phase 4.

## 10.1 Storage topology

| Store | Holds | Why here |
|---|---|---|
| **PostgreSQL** (primary) | All business state, outbox, audit chain, vector index, read models | One transactional boundary makes the outbox, the audit chain and permission-scoped retrieval simple. Splitting them would make each one a distributed problem |
| **pgvector** (same instance) | Knowledge chunks, content embeddings | Retrieval must join to organisation, permission, document version and classification. In-database that is a `WHERE`; out-of-database it is a consistency problem (D-05) |
| **Redis** | Cache, rate-limit token buckets, distributed locks, short-lived idempotency guards | Ephemeral by design. **Nothing whose loss changes a business outcome lives only here** — durable idempotency is in PostgreSQL |
| **Object storage** | Source documents, exports, report PDFs, evaluation artefacts, webhook raw bodies | Large, immutable, versioned |
| **Temporal persistence** | Workflow history and timers | Owned by the engine; the platform never reads it as business data |
| **Vault** | Connector credentials, provider keys | Referenced, never copied (`SEC-011`) |

## 10.2 Isolation

Every business row carries `org_id uuid not null`. Row-level security is enabled on every table in every module schema, with the session's organisation set per request:

```sql
CREATE POLICY org_isolation ON <table>
  USING (org_id = current_setting('app.org_id')::uuid);
```

This makes `SEC-003` and `AC-08` structural rather than diligent — a forgotten `WHERE org_id = …` returns nothing instead of returning another organisation's data. The application never sets `app.org_id` from a request body; it comes from the authenticated principal's claims.

Knowledge retrieval carries the same predicate plus a permission join (`KB-004`), so a vector search cannot surface a chunk the requesting agent is not entitled to — including across organisations, which is the multi-tenant requirement arriving early at no extra cost (D-12).

## 10.3 Aggregates and invariants

The aggregates that matter, with the invariant each one exists to protect. These are the boundaries of a transaction.

| Module | Aggregate | Invariant it protects |
|---|---|---|
| `worksurface` | **Task** | State transitions are legal; a task cannot complete with an unresolved dependency; every task carries objective + brand (`FR-004`) |
| `worksurface` | **Campaign** | A campaign brief is complete before campaign work can be dispatched |
| `pipeline` | **Lead** | Score is derived, never written; funnel stage is derived; `Converted` requires a PCI order reference (`FR-028`); a suppressed contact cannot be attached to an active outreach item |
| `pipeline` | **OutreachItem** | Template is Approved; length is within limit; personal line present with cited evidence; at most two follow-ups (`FR-034`) |
| `content` | **ContentItem** | Status and published date move together (`K-094`/`K-095`); a platform is always named (`K-098`); a non-canonical channel cannot receive a copy of a rankable page (`FR-046`) |
| `approval` | **ApprovalItem** | Exactly one terminal decision; approver ≠ generator ≠ reviewer; expiry is honoured (`APR-014`) |
| `integration` | **ExternalAction** | One idempotency key, one effect (`WF-030`) |
| `policy` | **SuppressionEntry** | Append-only; removal requires an audited administrative act |
| `insight` | **KpiObservation** | Immutable once written; carries its computation reference |
| `audit` | **AuditEvent** | Append-only, hash-chained |

Cross-aggregate consistency is **eventual, via events** — never a multi-aggregate transaction. Where a business process spans aggregates (content production, outreach preparation), the saga is a Temporal workflow, which is exactly what durable execution is for.

## 10.4 Events, outbox, idempotent consumption

```
BEGIN
  … aggregate change …
  INSERT INTO messaging.outbox (org_id, event_type, payload, correlation_id, occurred_at)
COMMIT

relay → publishes → consumers
consumer: INSERT INTO messaging.processed (consumer, event_id) ON CONFLICT DO NOTHING
          if inserted → handle
```

The outbox writes in the same transaction as the state change, so an event can never describe a change that did not happen, and a change can never fail to emit its event. The `processed` table makes every consumer idempotent by construction (`WF-022`), which is the precondition for `WF-030`: retries must be free.

Twenty domain events form the contract between modules (`WF-020`). They are versioned; a consumer tolerates unknown fields and never breaks on additions.

## 10.5 The external action ledger

This is the mechanism behind "a publish timeout cannot produce a duplicate post" (`AC-03`), and it lives in `integration`:

```
ExternalAction
  idempotency_key   unique (org_id, key)      key = hash(org, action_type, entity_id, content_hash)
  state             Reserved → InFlight → Succeeded | Failed | Unknown
  provider_ref      the id the external system returned
  attempts, last_error, reconciliation_state
```

The state that earns its place is **`Unknown`**: the connector call timed out, so the platform does not know whether the write landed. A retry from `Unknown` is *not* a re-send — it first runs the connector's **reconciliation query** (does an object with this content or client reference already exist?) and only sends if the answer is no. Connectors that cannot support a reconciliation query declare it in their manifest, and actions on those connectors require human confirmation before retry rather than retrying blind.

## 10.6 Lineage

`FR-091` — "every KPI exposes lineage" — is a data-model obligation, not a UI feature:

```
KpiObservation ──> Computation ──> formula_version
                       │            window (from, to)
                       └──> input_refs[]  → (module, aggregate, id) …
```

Every displayed number therefore drills through to the rows it was computed from, and the formula version it was computed with. This is also what prevents a repeat of the workbook's own defect: KPIs are addressed by stable identifier, never by position, and a regression test asserts that every dashboard tile resolves to its intended KPI (`FR-092`, `AC-12`).

Dashboards read **materialised read models** refreshed by event handlers — never raw aggregates at request time. That is what makes `NFR-001` (p95 under 1.5 s at 100k tasks) achievable without denormalising the write model.

## 10.7 Data-health checks become constraints

The workbook counts ten logging defects after the fact. The platform prevents nine of them at write time and detects the tenth continuously (`KPI-030`).

| Workbook check | Mechanism |
|---|---|
| Text dates | Typed column; parse at the boundary |
| Future-dated rows | `CHECK` constraint |
| Accepted without a logged request | State-machine transition guard |
| Minutes under an unknown name | Foreign key to `Actor` |
| Published with no date / dated but not published | Aggregate invariant |
| Signed deal with no value | Aggregate invariant |
| Duplicate leads | Unique identity resolution + pre-create check |
| Published with no platform | `NOT NULL` foreign key |
| Negative values | `CHECK` constraint |
| Reconciliation against systems of record | Continuous, by the Data Health agent — the one that cannot be a constraint, because the truth is in another system |

## 10.8 Classification, retention, erasure

Every entity type carries a data classification (`Public`, `Internal`, `Confidential`, `Restricted`) which drives model routing, tool access and provider eligibility (`SEC-040`).

| Class | Examples | Default retention (assumed — G-012 unresolved) |
|---|---|---|
| Public | Published content, public competitor facts | Indefinite |
| Internal | Plans, KPIs, agent traces, evaluation results | 24 months rolling |
| Confidential | Prospect and contact personal data, interaction history, consent records | **24 months from last interaction** |
| Restricted | Credentials references, Employee Score, HR notes | Life of employment + statutory period |

Erasure is a first-class operation, not a delete: a subject-erasure request tombstones the personal data, preserves the audit chain's integrity by replacing payloads with hashes, and records the erasure itself as an audit event. Aggregate KPIs computed before erasure remain valid because they do not depend on the erased fields.

**These retention defaults are mine, not PCI's.** They are the stated assumption behind `P2-14` and must be confirmed by legal before production.

## 10.9 The one deferred design

Under A-05 (single EU/UK region) residency is a deployment property. If PCI later needs a **Gulf data boundary** — plausible given the Saudi and UAE market focus and PDPL — residency becomes a *data* property: an org-level residency attribute, storage routing per organisation, and provider eligibility filtered by region. That is a change to §10.1 and §10.2, not to the module boundaries, and it is cheaper to make before there is data than after. It is the strongest reason P2-05 is worth answering early.
