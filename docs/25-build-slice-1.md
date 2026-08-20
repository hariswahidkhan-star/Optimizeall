# 25 — Build Slice 1: Authentication, Organisation, RBAC

**Status: implemented, tested, running.** This is the first vertical slice of the build phase, not a prototype.

## 25.1 Goal

Prove the spine everything else hangs from: a request arrives with a credential, resolves to a principal in exactly one organisation, and can reach only what its role permits — with tenant isolation enforced by PostgreSQL rather than by application discipline.

Nothing above this slice is safe until this one is right, which is why it is first.

## 25.2 The stack decision, made

`P2-01` was never answered, so Phase 2 carried **A-01** (.NET plus Python). Building required resolving it. **Built as Variant B — Python 3.12 / FastAPI end to end.**

Reasoning, in order:

1. Phase 2 designed the boundaries to be language-neutral, and explicitly recorded that Variant B changes no module boundary, contract, data model or figure. That claim is now tested rather than asserted.
2. A single-language stack is materially more maintainable by a small team, and Phase 2 said so: *"a single-language team shipping reliably beats a theoretically optimal split nobody can maintain."*
3. It removes a blocking decision from the critical path.

If PCI AI's engineering team is in fact .NET-strong, the swap is the one recorded in Phase 2 §8.6 — EF Core for SQLAlchemy, the .NET Temporal SDK for the Python one. The schema, the API contracts, the security model and the tests are unchanged.

## 25.3 Files

```
apps/api/
  pyproject.toml                        deps, ruff, mypy strict, pytest
  alembic.ini · alembic/env.py          async migration environment
  alembic/versions/0001_identity_and_config.py
  scripts/bootstrap_db.sql              roles: owner (BYPASSRLS) and app (never)
  .env.example
  src/pciai/
    settings.py                         fails closed on a production misconfiguration
    ids.py                              UUIDv7
    db/base.py                          org_id, timestamps, row_version conventions
    db/session.py                       SET LOCAL app.org_id — the isolation mechanism
    domain/identity/models.py           organisation, actor, roles, permissions, sessions
    domain/config/models.py             settings, brands, objectives
    security/permissions.py             the permission vocabulary and role matrix
    security/tokens.py                  dev issuer + verifier, refused outside local/test
    security/principal.py               token → authorised principal
    api/errors.py                       RFC 9457 problem documents
    api/middleware.py                   correlation id
    api/deps.py                         scoped transaction, principal, permission check
    api/schemas.py · api/routers/*      health, me, organisation, objectives, actors
    bootstrap.py                        seeding, incl. the workbook's 11 objectives
    devtoken.py                         local token minting
  tests/                                49 tests against real PostgreSQL
infrastructure/docker-compose.yml · Dockerfile.api · initdb/01-roles.sql
Makefile · .github/workflows/ci.yml
```

## 25.4 What the code enforces that a specification could only ask for

| Phase 1–5 requirement | How it is now enforced |
|---|---|
| `SEC-003`, `AC-08` tenant isolation | `ENABLE` + **`FORCE ROW LEVEL SECURITY`** on every org-scoped table, with `USING` and `WITH CHECK`. The app role owns nothing and is `NOBYPASSRLS` |
| Scope comes from the credential, never the body | `get_session` sets `app.org_id` from verified token claims only |
| An unbound session fails closed | `NULLIF(current_setting('app.org_id', true), '')::uuid` — no scope, no rows |
| `CMP-015` agent work attributed to agents | One `actor` table with `actor_type`; `/api/v1/actors` labels every row |
| No agent holds an approving permission | Agents are created with **no role-matrix permissions at all**; privileged roles are refused at creation, and `resolve_principal` subtracts the forbidden set as a second guard |
| `403` names the missing permission | `PermissionDenied` carries `rule` |
| `404` hides existence across tenants | Cross-org reads return nothing, so they 404 identically to genuinely-absent records |
| `AUD-005` correlation | `X-Correlation-Id` accepted, generated, echoed, and present in every problem document |
| Dev credentials cannot reach production | `verify_startup()` refuses the default secret and missing OIDC config in staging/production; the dev verifier refuses to run there at all |

### A finding from writing the tests

The agent guard rejected the test fixture itself. `Operator` grants `outreach.confirm_send` — and only a human confirms a human send, so that permission is on the agent-forbidden list. The fixture had given an agent the Operator role.

The fix was not to relax the guard. Phase 5 says agents hold **no** role-matrix permissions; capability comes from tool grants, and no tool maps to approving anything. The code now says that too, and four parametrised tests assert that Manager, Operator, Admin and Compliance are each refused for an agent principal.

This is the value of building a slice rather than specifying one: the specification was right, and my own fixture was wrong.

## 25.5 Migrations

One revision, `0001`, creating both schemas, the `actor_type` enum, eleven tables, indexes, all row-level-security policies, and the application role's grants.

Row-level security ships **with** the tables rather than later, because retro-fitting tenant isolation onto populated tables is the migration nobody wants to run.

CI runs a **round-trip** — `downgrade base` then `upgrade head` — because a migration without a working downgrade is a migration nobody can undo.

Seed content is deliberately not in the migration: the workbook's eleven objectives and seven brands load through `bootstrap.py`, the governed path, because they are business configuration that must be diffable and approvable.

## 25.6 Tests — 49, all against real PostgreSQL

| File | Covers |
|---|---|
| `test_health.py` | Liveness with a real database round-trip; correlation id generated and echoed |
| `test_auth.py` | Missing, malformed and expired credentials; principal resolution; **a token for an actor in another organisation does not resolve**; dev tokens refused outside local/test; production settings refuse the default secret and demand OIDC |
| `test_rls_isolation.py` | Scoped reads; **explicitly naming another organisation's key still returns nothing**; unbound sessions see nothing; the organisation row is itself scoped; **cross-organisation inserts are rejected by `WITH CHECK`**; API listings are disjoint across tenants |
| `test_rbac.py` | Permission enforcement on read and write; the three role-matrix asymmetries as parametrised assertions; every granted permission exists; only Owner holds everything |
| `test_agent_principal.py` | Agents resolve as agents; hold no matrix permission; privileged roles refused at creation; task binding carried in the token; agents labelled in listings |
| `test_problem_details.py` | Problem-document shape and content type; correlation id matches the header; 403 names the permission |

The harness uses a real database on purpose. The guarantee under test is a PostgreSQL feature; a mock would prove only that the mock behaves.

## 25.7 Run it

```bash
# One command, from a clean checkout
cd infrastructure && docker compose up

# Or against a local PostgreSQL
make install
make db-roles db-create      # idempotent
make migrate seed
make run                     # http://127.0.0.1:8080/api/v1/docs

make token                   # mint a development token
make check                   # lint + strict types + tests, exactly what CI runs
```

## 25.8 Verification performed

Run in this environment against PostgreSQL 16.13, not described:

| Check | Result |
|---|---|
| `alembic upgrade head` | Applied |
| `alembic downgrade base && upgrade head` | Round-trip clean |
| `pytest` | **49 passed** |
| `ruff check` + `ruff format --check` | Clean |
| `mypy` (strict, 28 files) | **Success: no issues found** |
| `GET /health` | `{"status":"ok","database":"up"}` |
| `GET /api/v1/organisation` without a credential | `401` problem+json with `WWW-Authenticate: Bearer` |
| `GET /api/v1/me` with a minted token | Owner resolved, 18 permissions |
| `GET /api/v1/objectives` | 11 objectives, ranks 1–11, PCL-AI first — the workbook's own ordering, served from the database |

## 25.9 Remaining limitations — stated, not hidden

1. **OIDC verification is not implemented.** Slice 1 ships the dev issuer only. `verify_token` raises rather than silently accepting anything outside local/test, and `verify_startup` refuses to boot a production configuration without OIDC settings. The JWKS verifier lands with the identity-provider integration.
2. **`agent_principal.agent_code` is text, not a foreign key** to `registry.agent_version`. The registry arrives in slice 3; the column is forward-compatible and the simplification is commented in the model.
3. **No audit chain yet.** `audit.audit_event` is specified in Phase 4 and lands in slice 2, alongside the outbox. Nothing in this slice writes an audit record, which is why nothing in this slice performs an external action.
4. **No refresh-token rotation.** Sessions are modelled and checked; the rotation flow lands with OIDC.
5. **Field-level write authorisation** (layer 3 of seven) is not yet generalised — `PATCH /organisation` restricts by permission, not yet by field.
6. **No web front end.** Phase 3 specified twenty-five screens; the API is the contract they will consume. The first screens land after the approval engine, because an approval inbox with nothing to approve is a demo rather than a slice.
7. **`row_version` is returned as an ETag but `If-Match` is not yet enforced** on `PATCH`. The contract is specified in Phase 4 §17.1; the enforcement lands in slice 2 with the first concurrent-edit surface.

## 25.10 Next slice

Per the vertical order: **agent registry → scheduler → job execution → model gateway → one working agent → one working workflow → approval engine.**

Slice 2 is the audit chain, the transactional outbox and the domain-event contract — because every slice after it needs to record what it did, and adding an append-only hash-chained log to a system that already has history is harder than starting with one.
