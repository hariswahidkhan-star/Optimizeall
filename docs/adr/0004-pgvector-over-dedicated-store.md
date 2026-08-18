# ADR 0004 — pgvector rather than a dedicated vector database

**Status:** Accepted · **Date:** 2026-08-18

## Context

Agents retrieve from a tenant knowledge base semantically. The obvious candidates are a purpose-built
vector store (Pinecone, Qdrant, Weaviate) or the `pgvector` extension in the database already
present.

The requirement that decides it is not recall or latency. It is that **a retrieval must never return
another tenant's content**, and that a knowledge document and its chunks must be consistent.

## Decision

`pgvector` in the same PostgreSQL cluster, with an HNSW index, and scope predicates in the same
`WHERE` clause that feeds the nearest-neighbour scan.

HNSW rather than IVFFlat because it needs no training pass: a workspace that has just ingested its
first documents gets usable recall immediately rather than after a rebuild.

## Consequences

**Good.** One datastore means one backup, one restore, one set of RLS policies, one consistency
boundary. Tenant isolation for retrieval is the same mechanism as for everything else, rather than a
second mechanism that must be kept in agreement. A document and its chunks commit together.

**Costly.** `pgvector` is slower than a dedicated store at large scale, and vector search competes
with transactional load for the same resources. Index build time grows with corpus size. We are
choosing correctness and operational simplicity over peak retrieval performance, and at the volumes
a single tenant's knowledge base reaches, that is the right trade — but it is a trade.

**Revisit when.** A tenant exceeds roughly 50 million chunks, or vector queries measurably degrade
transactional latency. At that point a dedicated store is warranted, and the isolation guarantee has
to be rebuilt there deliberately.
