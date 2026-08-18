# 11 — Security Architecture

## 11.1 Trust zones and the egress matrix

| Zone | Contains | May reach |
|---|---|---|
| **Z0 Public** | Browser | Z1 only |
| **Z1 Edge** | TLS termination, WAF, rate limiting | Z2 only |
| **Z2 Application** | `api`, `web` | Z4, Z3 |
| **Z3 Compute** | `worker`, `agent-runtime` | Z4; `worker` → allow-listed external hosts; `agent-runtime` → **model providers only** |
| **Z4 Data** | PostgreSQL, Redis, object storage, Temporal, vault | Nothing outbound |
| **Z5 External** | Third-party APIs, model providers | Reached only from Z3, never initiated inward except signed webhooks via Z1 |

The single most important line in that table: **`agent-runtime` cannot reach any third-party business system.** It processes untrusted text as its normal job; giving it outbound access to the CMS, CRM or mail system would turn a content problem into an execution path. Research fetching runs in `worker`, behind the same gate as every other connector.

## 11.2 Identity and authorisation

**Human principals** authenticate through OIDC (Entra ID under D-06), with MFA and conditional access inherited from the organisation's existing identity estate — which also gives joiner/leaver control, an explicit workbook concern.

**Agent principals** are non-human identities in `identity`. An agent principal is never interactive, holds no password or token, and its effective permission set is computed from its `AgentVersion` grants at run time. It cannot be broadened by anything the agent does (`SEC-035`).

**Service accounts** exist per connector, scoped to that connector's operations only.

Authorisation is layered, and every layer is server-side:

1. **Organisation scope** — row-level security (§10.2)
2. **Role permission** — RBAC on the operation
3. **Field-level authorisation** — computed and manager-owned fields reject writes from unauthorised principals (`SEC-021`, replacing the workbook's sheet protection)
4. **Tool grant** — the agent's allow-list, deny by default
5. **Scope binding** — the request's target entities must lie in the task's scope graph
6. **Policy** — deterministic business rules
7. **Approval** — human decision where required

A request must clear all seven. Layers 4 and 5 are what make an agent's authority *narrower than its role*: even a correctly-permissioned agent cannot act outside the task it was given.

## 11.3 Secrets

Credentials live in the vault. The database holds references — the same discipline the workbook already enforces in its Accounts Register, where only the vault entry name is recorded.

The invariant (`SEC-013`): **no secret is ever placed in a prompt, returned to a model, or transmitted to `agent-runtime`.** An agent requests `publish_content(content_id)`; the connector in `worker` resolves the credential at the moment of execution, inside Z3, and discards it. There is no code path by which a model can observe a token, because the process that holds tokens and the process that talks to models are different processes with different network policies.

Rotation is supported without downtime: credential references are stable while the underlying secret version changes; connectors resolve at call time, so a rotated secret takes effect on the next call.

## 11.4 The kill switch

`FR-112` requires emergency stop to catch **approved-but-unexecuted** items — the case that matters, because an approval granted before a problem was discovered would otherwise still fire.

It is checked in three places, because one check is a single point of failure:

1. At the **execution gate**, step 6 — no tool request proceeds
2. At **activity start** in `worker` — in-flight workflows halt before their next external effect
3. At **approval dispatch** — approved items are held, not executed

Plus a Temporal signal to running workflows so they park rather than spin. Release requires the Owner (`A20`). The switch has granularity: all external writes, one channel, one workflow, or one agent (`FR-111`).

## 11.5 Prompt injection — four layers, no single point of trust

The workbook's research agents read hostile content by definition, and reply triage ingests text written by strangers. Defence is layered because any single layer can be defeated:

| Layer | Control | What it stops |
|---|---|---|
| **Ingestion** | File-type validation, size limits, scanning, isolated parsing, provenance capture (`KB-020`) | Malicious documents at the door |
| **Framing** | Untrusted-source envelopes; standing rule that enveloped content is data (`KB-021`, §9.3) | Naive instruction-following |
| **Capability** | Tool allow-list per agent version; write tools deny-by-default | An agent persuaded to act cannot reach a tool it was never granted |
| **Scope** | Scope binding — targets must lie in the task's scope graph | "Email everyone in the CRM" fails structurally, whatever the model believed |
| **Approval** | Every external write to a person or the public is human-approved | The last line, and the one the workbook already relies on |

Denied invocations are logged with agent, version, tool, matched rule and correlation ID, and repetition raises a security alert (`SEC-032`). A rising denial rate is treated as a defect in the agent version, not as the policy engine working well (§9.9).

## 11.6 Sandboxing

Where an agent needs code execution or advanced tooling, it runs in an isolated sandbox with resource limits, no network, an execution timeout, a read-only filesystem except a scratch mount, full logging, and per-task isolation (`SEC-034`). No agent receives shell, browser, arbitrary network or direct database access under any configuration (`AI-005`).

## 11.7 API security

TLS everywhere; encryption at rest for database, object storage and backups; short-lived revocable sessions; rate limiting per principal and per organisation; request size limits; server-side input validation as the only validation that counts (`DAT-006`); CORS and CSRF policy; secure headers; signed webhooks with replay protection; audit logging on every sensitive operation.

## 11.8 Audit integrity

The audit log is append-only and hash-chained per organisation: each event carries the hash of its predecessor, and a daily anchor is written to object storage. Tampering is therefore detectable, not merely discouraged (`AUD-003`). The chain records the full `AUD-001` set — agent, versions, prompt version, trigger, input, retrieved knowledge with document versions, model, output, tool calls, approval, execution result, error, cost, timestamp.

What the audit log deliberately does **not** contain: hidden chain-of-thought. `AUD-007` requires a decision rationale — evidence, rules applied, KPIs consulted, sources retrieved, assumptions stated — which is reconstructable from the recorded inputs and the structured output, and is more useful in a review than a transcript would be.
