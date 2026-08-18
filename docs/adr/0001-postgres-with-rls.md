# ADR 0001 — PostgreSQL with row-level security for tenant isolation

**Status:** Accepted · **Date:** 2026-08-18

## Context

A multi-tenant platform holding every customer's business data needs isolation that survives an
application-layer defect. The realistic failure is not a sophisticated attack; it is a query written
without a tenant filter, shipped by a competent engineer on a Friday.

Options considered:

1. **Application-layer filtering only.** Every query includes `tenant_id`. Simple, and one omission
   is a breach.
2. **Database per tenant.** Strongest isolation. At the target of 5,000 tenants it means 5,000
   migrations per release and 5,000 connection pools.
3. **Schema per tenant.** Better than a database each, still hundreds of schemas to migrate, and
   cross-tenant platform queries become painful.
4. **Shared schema with row-level security.**

## Decision

Shared schema with PostgreSQL row-level security, `FORCE`d, plus EF Core global query filters, plus
an ambient tenant scope resolved once from signed token claims.

Three layers, each independently sufficient for the common case:

- The ambient scope stops a handler from choosing its own tenant.
- Query filters catch a forgotten `WHERE`.
- RLS catches an ORM bypass, a raw SQL mistake, or an application-layer check that was removed.

`FORCE` matters as much as enabling RLS: without it the table owner bypasses every policy, and the
migration role is usually the owner.

## Consequences

**Good.** One schema, one migration per release. Isolation holds when application code is wrong,
which is the case that actually happens. Verified by execution: cross-tenant reads return nothing and
cross-tenant writes are rejected by the policy.

**Costly.** Every connection must set `app.tenant_id`, and forgetting it means the request sees
nothing — a confusing failure the first time an engineer meets it. The application must connect as a
non-owner, non-superuser role, which is easy to get wrong in a development environment and then
never exercise the policies at all. Policies add a small per-query cost. Cross-tenant platform
queries need a deliberate, audited escalation path rather than being incidentally possible.

**Revisit when.** A single tenant's data volume justifies its own database, or a regulated customer
requires physical separation. The model supports moving one tenant out without moving all of them.
