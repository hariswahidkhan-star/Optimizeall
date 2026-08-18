# OptimizeAll — Testing Strategy

- **Document ID:** OA-TST-001
- **Version:** 1.0

---

## 1. What we are actually trying to prove

Coverage percentage is a weak proxy for confidence. The questions this strategy exists to answer are:

1. Can one tenant reach another tenant's data? (Must be provably no.)
2. Can a consequential action happen without a specific human authorising *that* action?
3. Can the record of what happened be altered without detection?
4. Does the platform stay up and correct when a dependency fails?

Everything else is ordinary engineering hygiene.

---

## 2. Current state

| Suite | Tests | What it proves |
|---|---|---|
| `OptimizeAll.Domain.Tests` | 126 | Business invariants without any infrastructure |
| `OptimizeAll.Application.Tests` | 14 | The tool authorisation chokepoint |
| `OptimizeAll.Infrastructure.Tests` | 14 | The seeded workforce satisfies the domain's own rules |
| `OptimizeAll.Architecture.Tests` | 11 | Layering and conventions, enforced as a build failure |
| Frontend (`vitest`) | 12 | Formatting correctness and approval-queue behaviour |
| **Total** | **177** | |

Database-level controls were additionally verified by executing against a live PostgreSQL 16 with
`pgvector`, as the non-superuser application role. Those checks are described in §4 and are being
converted into an automated suite (§7).

---

## 3. The test pyramid, and where we deliberately deviate

```
        ┌─────────────┐
        │  E2E (few)  │   Approval journey, kill switch, dry run
        ├─────────────┤
        │ Integration │   RLS, audit chain, migrations, provider failover
        ├─────────────┤
        │    Unit     │   Domain invariants, tool gate, formatting
        └─────────────┘
```

**The deviation.** Tenant isolation and approval integrity get disproportionate integration
coverage relative to their share of the code. A unit test can prove the aggregate refuses
self-approval; only an integration test proves the database refuses it too, and the database is the
layer that still holds when application code has a defect.

---

## 4. Security properties under test

These are the tests that would fail loudly if the platform's central claims stopped being true.

### Tenant isolation

| Property | Method | Status |
|---|---|---|
| Tenant A sees only tenant A's rows | Live PostgreSQL, application role | Verified |
| Cross-tenant `INSERT` rejected by policy | Live PostgreSQL | Verified |
| Cross-tenant `UPDATE` affects zero rows | Live PostgreSQL | Verified |
| Unset tenant scope yields zero rows, not an error | Live PostgreSQL | Verified — this found and fixed a defect where the uuid cast raised instead |
| Vector search cannot return another tenant's chunks | Scope predicates in the same `WHERE` as the ANN ordering | Code-verified; integration test pending |

### Approval integrity

| Property | Method | Status |
|---|---|---|
| An agent can never approve | Domain unit test | Verified |
| A requester can never approve their own request | Domain unit test **and** database trigger | Verified at both layers |
| One approver cannot satisfy a two-approver rule | Domain unit test + unique index | Verified |
| A payload changed after approval is refused | Domain + application tests | Verified |
| Reordered but identical JSON still authorises | Domain unit test | Verified |
| Expiry fails closed | Domain unit test | Verified |
| A missing approval policy still gates | Application test | Verified |
| The kill switch blocks external actions | Application test | Verified |

### Audit integrity

| Property | Method | Status |
|---|---|---|
| An intact chain verifies end to end | Domain unit test | Verified |
| Editing a stored field is detected | Reflection-based tamper test | Verified |
| Removing an entry breaks the chain | Domain unit test | Verified |
| Splicing another tenant's entry is rejected | Domain unit test | Verified |
| `UPDATE`/`DELETE` denied to the application role | Live PostgreSQL | Verified |

---

## 5. What each layer is responsible for

**Unit.** Domain invariants, in memory, no I/O. Every branch of the approval, budget and workflow
logic. These run in under a second, so nobody is tempted to skip them.

**Integration.** Anything whose correctness depends on the database or on a real dependency:
row-level security, the audit chain under concurrency, migrations applying and reversing, EF
mappings actually round-tripping, provider failover behaviour. Testcontainers, real PostgreSQL, real
Redis — never an in-memory substitute, because an in-memory provider does not have row-level
security and would silently pass tests the real database would fail.

**Contract.** The published OpenAPI document is diffed against the previous release; a breaking
change without a version bump fails the build. The frontend's hand-written types are checked against
the same document.

**End-to-end.** Deliberately few, and only for journeys where the integration of the parts is the
thing being tested: an agent proposing a gated action → the approver seeing it → approving →
execution proceeding with the exact payload; the kill switch halting work mid-flight; a dry run
recording everything and applying nothing.

**Load.** NFR-PRF targets sustained for 60 minutes: 500 RPS read, 2,000 concurrent runs, p95 within
budget, no queue growth.

**Chaos.** Kill a worker mid-run and assert the lease is reclaimed exactly once. Make every provider
fail and assert the platform stays up and queues work. Partition Redis and assert no approved action
is lost.

---

## 6. Rules the suite follows

- **No test asserts an implementation detail.** Tests name behaviour: `A_requester_can_never_approve_their_own_request`, not `Decide_returns_false_when_ids_match`.
- **No shared mutable fixture.** Each test builds what it needs, so a failure means what it says.
- **No randomised inputs in the default suite.** A test that fails only sometimes costs more to
  diagnose than it ever saves. Property-based testing is used deliberately, in a separate suite, for
  the canonicalisation and hash-chain logic where it earns its place.
- **Time is injected.** Nothing calls `DateTimeOffset.UtcNow` directly, so expiry, lease and
  scheduling behaviour are tested rather than waited for.
- **A flaky test is a bug.** Quarantining is not an option; a test that cannot be trusted is deleted
  or fixed within one working day.

---

## 7. Gaps, stated plainly

These are known and scheduled, not overlooked:

1. **Integration suite not yet automated.** The database-level controls were verified by direct
   execution against a live PostgreSQL during development, and the evidence is recorded above. They
   need converting to a Testcontainers suite so they run on every pull request rather than on
   demand.
2. **No end-to-end suite yet.** The API and worker are in place, so the approval journey is now
   testable end to end.
3. **Provider adapters are compile-verified, not contract-verified.** They need recorded-interaction
   tests against captured provider responses.
4. **Load and chaos testing have not been run.** The NFR targets in the SRS are therefore stated
   targets, not measured results, and the SRS says so.

None of these gaps affect a released system, because nothing is released yet. All four block GA per
the acceptance criteria in `docs/01-srs.md` §7.

---

## 8. Definition of done for a change

A change is not done until: it has tests naming the behaviour it adds; the full suite passes; the
architecture tests pass; scanning reports no new High or Critical finding; and, if it touches
approval, isolation or audit logic, a second engineer has reviewed it with a written note on the
threat considered.
