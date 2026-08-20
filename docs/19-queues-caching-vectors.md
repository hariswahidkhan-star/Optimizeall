# 19 — Queue, Caching & Vector-Storage Architecture

## 19.1 Queue architecture

Two mechanisms with different jobs, deliberately not merged:

| Mechanism | Carries | Why |
|---|---|---|
| **Temporal task queues** | Workflow and activity execution | Durable, retried, timer-aware, human-wait-capable |
| **Outbox relay** | Domain events to in-process consumers | Transactional with the state change that produced them |

### Queue classes

Six classes, as separate Temporal task queues with independent worker pools. Separation — not priority numbers within one queue — is what actually prevents starvation.

| Queue | Carries | Workers | Concurrency | Rationale |
|---|---|---|---|---|
| `critical` | Emergency stop propagation, kill-switch checks, security alerts | 2 | 8 | Must never queue behind anything |
| `standard` | Planning, drafting, QA, compliance, scoring | 4 | 16 | The bulk of the work |
| `research` | Web research, competitor scans, answer-engine audits | 2 | 6 | Slow and bursty; isolating it stops one 90-second fetch blocking a draft |
| `external-write` | Publishing, sending, CRM writes | 2 | **4** | Deliberately narrow. External writes are rate-limited by the *provider*, and more concurrency here buys nothing but 429s |
| `approval-dependent` | Work resuming after a human decision | 2 | 8 | Bursty by nature — a manager clearing twelve approvals should not stall the standard queue |
| `retry` | Backoff re-attempts | 1 | 4 | Isolated so a failing integration cannot consume the standard pool |

Workflow *timers* — the day-4 and day-10 follow-ups, approval expiry, the 30-day indexation gate — are Temporal timers, not queue entries. They cost nothing while waiting and survive restarts, which is precisely why the workflow engine exists.

### Backpressure

Depth per queue is a first-class metric with alert thresholds. When `external-write` depth exceeds its threshold the platform slows *upstream* preparation rather than accumulating approved work that cannot be executed — the alternative is a queue of approvals that expire before they are reached.

---

## 19.2 Caching strategy

**The governing rule: cache keys embed a version, so invalidation is unnecessary.** A key like `agentver:{agent_version_id}` addresses immutable content — a new version is a new key, and the old entry expires on its own. Most cache bugs come from invalidation logic, so the design removes the need for it wherever possible.

| Cached | Key | TTL | Invalidation |
|---|---|---|---|
| Agent version + grants | `agentver:{id}` | 1 h | None needed — immutable by key |
| Prompt version | `promptver:{id}` | 1 h | None needed |
| Org config | `config:{org}:{config_version}` | 1 h | Version bump on any config write |
| Enumerations | `enum:{org}:{type}:{version}` | 6 h | Version bump |
| Connector health | `health:{org}:{connector}` | 30 s | Pub/sub on state change |
| KPI read models | `kpi:{org}:{code}:{period}` | 5 min | Event-driven refresh |
| Session | `sess:{token_hash}` | Token lifetime | Immediate on revocation |
| Idempotency guard | `idem:{org}:{key}` | 24 h | Expiry only |
| Rate-limit buckets | `rl:{org}:{connector}:{endpoint}:{window}` | Window | Expiry only |

### Never cached, and why

**Approval state, external-action state, and suppression entries.** Each is a decision point where a stale read causes a real-world error: publishing something whose approval was withdrawn, sending twice, or contacting someone who declined. These are single indexed reads on a well-indexed table; the microseconds saved are not worth the failure mode.

### The kill switch — a deliberate exception

It is checked on every tool call, so a database read per call is wasteful, but a stale *off* value is the dangerous direction. So: Redis-backed with pub/sub invalidation and a **2-second** maximum staleness, **plus an unconditional database read immediately before connector execution**. Cheap in the common path, correct in the path that matters.

---

## 19.3 Vector-storage architecture

### Chunking

| Source | Strategy |
|---|---|
| Prose documents (PDF, Word, web) | Structure-aware: split at headings, then 600–900 tokens with 100-token overlap, never mid-sentence |
| Spreadsheets | **One chunk per logical row group**, carrying the header row as context. A 5,683-row brief bank chunked by token count would produce fragments that mean nothing; chunked by row it produces 5,683 individually retrievable briefs |
| Presentations | One chunk per slide, with speaker notes appended |
| Policies and playbooks | One chunk per numbered technique or rule, so a retrieval returns a complete rule rather than half of one |

Every chunk carries `document_version_id`, `ordinal`, `token_count`, source location and classification.

### Storage

```sql
CREATE TABLE knowledge.chunk (
  id                  uuid PRIMARY KEY,
  org_id              uuid NOT NULL,
  document_version_id uuid NOT NULL REFERENCES knowledge.document_version(id),
  ordinal             integer NOT NULL,
  text                text NOT NULL,
  token_count         integer NOT NULL,
  classification      text NOT NULL,
  embedding_model_key text NOT NULL,
  embedding           vector(1536) NOT NULL,
  metadata            jsonb NOT NULL DEFAULT '{}',
  superseded_at       timestamptz,
  UNIQUE (document_version_id, ordinal)
);

CREATE INDEX chunk_ann_idx ON knowledge.chunk
  USING hnsw (embedding vector_cosine_ops)
  WHERE superseded_at IS NULL;

CREATE INDEX chunk_filter_idx ON knowledge.chunk (org_id, classification)
  WHERE superseded_at IS NULL;
```

### Retrieval

```
filter (org_id via RLS · classification vs agent scope · document ACL · not superseded)
  → ANN search over the surviving set
  → optional rerank for the top 30
  → return top k with citations {document_id, document_version_id, chunk_id, ordinal}
```

**Filter before ANN, never after.** Retrieving fifty neighbours and then discarding the ones the agent may not see silently degrades recall — and, worse, leaks the existence of restricted material through result counts.

### Model changes are additive, never in place

`embedding_model_key` is stored per chunk. Changing the embedding model means writing new rows under the new key and building a second index, then cutting over — never re-embedding in place. Re-embedding in place makes the index temporarily incoherent, with old and new vectors in one space, and there is no way to detect that from the results.

### Version awareness

Retrieval targets the current version by default. Every execution records the exact `document_version_id` it used in `knowledge.knowledge_citation`, which is what allows the audit question — *what did the agent actually know when it wrote this?* — to be answered from records rather than reconstructed by inference.

### Similarity for duplication control

`content.content_item.embedding` serves a different purpose: near-duplicate detection before drafting. Cosine similarity above a configurable threshold against published PCI content blocks the draft and returns the colliding item, implementing `FR-043` — one of the three controls standing between the 5,683-brief bank and the thin-content failure mode the workbook itself warns about.
