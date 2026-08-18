# ADR 0002 — Hand-rolled dispatcher instead of a mediator library

**Status:** Accepted · **Date:** 2026-08-18

## Context

The request pipeline is where authorisation and audit happen. Every command passes through it, and
the ordering of its behaviours is load-bearing: authorisation must run after tenant resolution and
before any transaction opens, and audit must commit inside the transaction it describes.

MediatR is the conventional choice. Its licensing changed in 2024, which prompted the question, but
the licence is not the main reason.

## Decision

Own the dispatcher. It is roughly 100 lines: resolve the handler, compose the registered behaviours,
invoke.

## Consequences

**Good.** The ordering semantics are ours and are visible in one file. No behavioural surprise can
arrive in a package update to the component that enforces authorisation. No licence question. The
registration code reads in the same order the pipeline executes.

**Costly.** We maintain it, including the reflection-based invocation, which is less pleasant than
consuming a well-tested library. We forgo the ecosystem — no third-party behaviours, no tooling that
expects MediatR. A new engineer who knows MediatR has to learn ours instead, even though it is
simpler.

**Revisit when.** The pipeline grows features we would otherwise be reimplementing (streaming,
notification fan-out, complex handler resolution). At that point a library is the better trade.
