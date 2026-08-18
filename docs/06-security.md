# OptimizeAll — Security Architecture and Threat Model

- **Document ID:** OA-SEC-001
- **Version:** 1.0
- **Review cadence:** quarterly, and on any change to the approval or isolation model

---

## 1. What this system is, from a security standpoint

OptimizeAll gives language models the ability to act: to publish, to send, to spend, to deploy. The
security problem is therefore not the usual one of protecting data at rest. It is **bounding what an
autonomous, partly unpredictable actor can do**, and proving afterwards what it did.

Three properties carry that weight. Everything else supports them.

1. **A tenant cannot observe or affect another tenant.**
2. **No consequential action happens without a specific human authorising that specific action.**
3. **What happened is recorded in a way that cannot be silently altered.**

---

## 2. Trust boundaries

```
Untrusted ──────────────────────────────────────────────────────────────────
  Browser, API clients, webhook senders
  Retrieved web content, ingested documents, customer messages
  MODEL OUTPUT — including tool call requests
        │
        ▼  (token validation, input validation, tool authorisation)
Semi-trusted ───────────────────────────────────────────────────────────────
  Authenticated user sessions within a resolved tenant scope
  Agent principals within their granted tool set
        │
        ▼  (RBAC, RLS, approval gates)
Trusted ────────────────────────────────────────────────────────────────────
  Application code, database, secret store
```

**Model output sits in the untrusted zone.** This is the single most important classification in the
document. A tool call is a *request* from an untrusted source. The platform decides whether it may
proceed, using state the model cannot influence.

---

## 3. Threat model

### T1 — Cross-tenant data access

*An attacker with a valid account in tenant A reads tenant B's data.*

| Control | Where |
|---|---|
| Tenant scope resolved from signed token claims only | `HttpTenantContext`, immutable per request |
| EF Core global query filters | `OptimizeAllDbContext.ApplyTenantQueryFilters` |
| PostgreSQL row-level security with `FORCE` | `002_security_hardening.sql` |
| `SET LOCAL` so scope cannot leak on a pooled connection | `UnitOfWork.ApplyTenantScopeAsync` |
| Vector search filters scope before the ANN scan | `KnowledgeRepository.SearchAsync` |

**Verified.** Executed against a live PostgreSQL as the non-superuser application role: tenant A saw
only its own rows, a cross-tenant `INSERT` was rejected by the policy, a cross-tenant `UPDATE`
affected zero rows, and an unset tenant setting yielded zero rows rather than an error.

**Residual risk.** A compromised application host could set an arbitrary tenant id. Mitigated by
workload identity, no standing database credentials, and audit correlation — not eliminated.

### T2 — Approval bypass

*An agent performs a gated action without a human decision, or performs a different action than the
one approved.*

| Attack | Control |
|---|---|
| Call a gated tool without raising an approval | Single chokepoint in `ToolInvocationService`; risk class is a property of the tool, not of the call site |
| Approve its own request | Requester ≠ approver, enforced in the aggregate **and** by a database trigger |
| Act as its own approver | `PrincipalRef.IsHuman` check; an agent principal is refused |
| Substitute the payload after approval | Payload re-canonicalised and re-hashed at execution; mismatch refuses and raises a security event |
| Satisfy a two-approver rule alone | Unique index on `(approval_request_id, approver_user_id)` |
| Wait for the approval to lapse and proceed | Expiry fails closed; the aggregate evaluates it on every access |
| Disable gating through configuration | `RequiresHumanApproval` takes no parameter; policies can only tighten |
| Slip through when policy is missing | A missing or inactive policy falls back to the platform floor, still gated |

**Verified.** Each of these has a test. The payload-substitution and self-approval cases are tested
at both the domain layer and the database layer.

### T3 — Prompt injection

*Content the model reads contains instructions, and the model follows them.*

The defence is layered, and the layers are not equally important:

1. **Tool authorisation is server-side.** A successful injection produces a tool call the platform
   evaluates against the agent's grants. An agent without `email.send` cannot be talked into sending
   email — the capability does not exist for it.
2. **Gated actions still require a human.** Injection cannot manufacture an approval.
3. **Untrusted content is fenced** in explicit delimiters with a standing instruction to treat it as
   data (`PromptComposer`).

Layer 3 is the weakest and is treated as such. It reduces the frequency of a successful injection; it
does not bound the consequences. Layers 1 and 2 do that.

**Residual risk.** An injection can still cause a *granted, ungated* action — a misleading knowledge
document, a distorted analysis. Mitigated by run traces, QA review by a separate agent instance, and
human review of published output.

### T4 — Runaway or looping agent

*An agent consumes unbounded resources or repeats an action indefinitely.*

Bounded on five independent axes, each checked **before** the step that would breach it: iterations,
tokens, monetary cost, tool calls, wall clock. Plus a workspace monthly budget checked at queue time,
and self-delegation refused so an agent cannot escape its own per-run budget by scheduling itself.

### T5 — Privilege escalation

*A user grants themselves permissions they do not hold.*

`Role.CreateCustom` and `UpdatePermissions` both take the granter's own permission set and refuse any
permission outside it. Built-in roles are immutable. Role changes require step-up authentication.

### T6 — Audit tampering

*Evidence of an action is altered or removed.*

Per-tenant SHA-256 hash chain; append-only by database permission and by trigger; sequence uniqueness
makes a gap detectable; nightly verification raises a Severity 0 alert on the first discontinuity.

**Honest limit.** An attacker with full database control could recompute the chain. This makes
tampering *detectable*, not impossible — which is the property an auditor actually needs.

### T7 — Secret exposure

Secrets live in Key Vault, reached by workload identity, and their names encode the (tenant,
workspace, environment) triple — so a Development-scoped agent's request for a production credential
resolves to nothing. Logs are redacted by default. Unexpected API errors return only a trace id,
because internal failure text routinely contains connection strings and schema detail.

### T8 — Denial of service

Rate limiting partitioned per principal, not per IP, so several users behind one corporate egress do
not share a bucket and one tenant cannot throttle another. Concurrency bounded per workspace;
mandatory paging on every list endpoint.

---

## 4. Controls that are deliberately absent

Naming these matters as much as naming the controls that exist.

| Not implemented | Why |
|---|---|
| An "emergency bypass" for approvals | A gate with a bypass is not a gate. The emergency action is the kill switch, which stops work rather than permitting more. |
| Agent-to-agent direct invocation | It would make one agent's compromise an arbitrary capability chain. All delegation is orchestrator-mediated. |
| Merge permission for the dev agent | Human review is the control; an agent that can merge its own work has none. |
| Access revocation by the security agent | An agent that can lock out its operators is a liability during the incident it exists to help with. |
| Generic SQL access as an agent tool | A natural-language front end to arbitrary SQL is a data-exfiltration path. |

---

## 5. Secure development requirements

- Dependency, container, SAST, secret and IaC scanning on every pull request; High or Critical fails
  the build. This already caught and blocked a vulnerable OpenTelemetry release during development.
- `TreatWarningsAsErrors` in every project.
- Architecture tests prevent a layering violation from reaching main.
- Security-relevant changes — approval logic, tenant isolation, authorisation — require review by a
  second engineer and a written note in the pull request describing the threat considered.

---

## 6. Incident response

| Severity | Definition | Response |
|---|---|---|
| **SEV0** | Audit chain broken, approval bypass observed, cross-tenant access confirmed | Page immediately; engage the kill switch for affected workspaces; preserve state before remediating |
| **SEV1** | Platform down, provider outage with queue growth, suspected compromise | Page; follow the runbook |
| **SEV2** | Degraded performance, single-tenant impact | Business hours |

The kill switch is the first tool in a suspected-compromise response: it halts every external action
in a workspace within seconds, which stops the bleeding without destroying the evidence.
