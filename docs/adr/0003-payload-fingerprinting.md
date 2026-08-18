# ADR 0003 — Cryptographic payload binding for approvals

**Status:** Accepted · **Date:** 2026-08-18

## Context

The platform's central promise is that a human approves a specific action and that action is what
executes. The gap between the two is real: an agent proposes a payload, a human reviews it, time
passes, and then something executes. Nothing in a naive design forces those to be the same bytes.

The failure does not require malice. An agent that regenerates its output before executing produces
a *different* payload with the same intent, and a system comparing only the approval's id would
execute the ungoverned version.

Options considered:

1. **Compare by approval id only.** Trivially defeated by regeneration.
2. **Compare payloads structurally at execution.** Requires defining equality; the definition drifts.
3. **Hash the payload and compare hashes.**

## Decision

Canonicalise the payload deterministically (RFC 8785-aligned JSON), hash it with SHA-256, store the
digest on the approval, and re-derive it from the payload presented for execution. Any difference
refuses execution and raises a security event.

Canonicalisation is required, not optional: JSON serialisers are free to reorder object members, so
without it a semantically identical payload would fail the check and the pressure would be to compare
loosely instead.

## Consequences

**Good.** Time-of-check/time-of-use substitution fails closed, including when the substitution is
accidental. The property is testable, and it is tested: reordered-but-identical payloads authorise;
a single changed character does not.

**Costly.** Payloads must be valid JSON, which constrains what a tool can accept. An agent that
legitimately needs to adjust a payload after approval must raise a new approval — correct, and
occasionally annoying. The canonicaliser is security-critical code we own; a bug in it is a bug in
the guarantee. Number canonicalisation in particular needed care, and a decimal formatting defect was
found by test.

**Revisit when.** Payloads need to carry binary content, at which point the canonical form needs
extending rather than replacing.
