# OptimizeAll — Rollout Plan

- **Document ID:** OA-ROL-001
- **Version:** 1.0

---

## 1. Where this actually stands

Being precise about this matters more than a confident timeline.

**Built and verified:**
- Domain model with the governance invariants, 126 passing tests.
- Application layer, agent runtime and tool authorisation chokepoint, 14 passing tests.
- PostgreSQL schema with row-level security, verified by execution against a live database.
- Modular provider layer (Anthropic, OpenAI, Gemini) with failover, compile-verified.
- REST API and background workers, building and layering-enforced.
- React frontend for approvals, runs and the executive view; builds, 12 passing tests.
- Containers, Azure infrastructure as code, CI/CD with security scanning.
- Seeded 29-agent workforce, validated against the domain's own rules.

**Not yet done, and blocking GA:**
- Automated integration suite (the database controls are verified, but by hand).
- End-to-end approval journey test.
- Load test against the stated NFR targets.
- Provider contract tests against recorded interactions.
- Restore drill.
- Independent security review.

The SRS acceptance criteria (§7) list all of these. Nothing below assumes they are complete.

---

## 2. Phases

### Phase 1 — Harden (4 weeks)

Close the testing gaps above. Nothing new is built.

**Exit:** every SRS acceptance criterion met; load test sustains targets for 60 minutes; restore
drill completes within RTO; security review returns no open High or Critical finding.

### Phase 2 — Internal use (4 weeks)

The team runs its own marketing, content and reporting on the platform. One tenant, real work, real
consequences.

This phase exists because a governance product cannot be evaluated from the outside. Whether the
approval queue is tolerable at twenty items a day, whether the run trace actually answers "why did it
do that", whether the kill switch is reachable when someone is worried — none of that is visible in a
test suite.

**Exit:** 30 consecutive days with no SEV0 or SEV1; approval median decision time under 4 hours;
every agent used in anger at least once; the team prefers using it to not using it.

### Phase 3 — Design partners (8 weeks)

Three to five customers, hand-picked for willingness to give hard feedback rather than for logo
value. Each gets a named engineer.

Onboard one at a time. The second customer should benefit from what the first exposed, and that only
happens if there is a gap between them.

**Exit:** 99.9% availability across the period; no cross-tenant incident; every partner would
recommend it; support load per tenant trending down rather than up.

### Phase 4 — General availability

Self-service onboarding, published SLA, on-call rotation staffed to a sustainable ratio.

---

## 3. Sequencing rules

**One tenant at a time, early.** Multi-tenancy defects are discovered by having a second tenant, not
by testing with one. The second onboarding is the important one.

**Production last, always.** Every agent runs in Development, then Staging, then Production, in that
order, for at least a week each. The environment tiers exist precisely so that a new agent's failure
mode is discovered somewhere harmless.

**Dry run before live.** Every new agent's first Production execution is a dry run: fully recorded,
nothing applied. Reviewing what it *would* have done costs an hour and has repeatedly been the
cheapest way to find a badly-scoped agent.

**Gates tighten before they loosen.** A tenant may start with two approvers on everything and relax
to one where the evidence supports it. Starting loose and tightening after an incident is a much
worse trade.

---

## 4. Rollback triggers

Stop the rollout and reassess if any of these occurs:

- Any cross-tenant data exposure, however brief.
- Any approval bypass, including one caught by the payload-mismatch check.
- Audit chain verification failure not explained by a known operational cause.
- Two SEV1 incidents in one week.
- Approval median decision time above 24 hours — a queue nobody works is a gate in name only.

The first three are absolute: they mean a load-bearing guarantee stopped holding, and no amount of
customer pressure changes that.

---

## 5. What could go wrong, and what we would do

| Risk | Likelihood | Response |
|---|---|---|
| Approval fatigue — reviewers rubber-stamp | High | Measure decision time and rejection rate per approver. A reviewer who never rejects is not reviewing. Batch low-risk items; keep high-risk ones individual. |
| Agent output quality below expectation | Medium | Every run records its model, prompt version and approval outcome, so this is measurable rather than anecdotal. Adjust model policy per agent on evidence. |
| Provider price or terms change | Medium | Prices are configuration; providers are interchangeable. This is the risk the abstraction was built for. |
| A tenant demands a gating exception | High | Refuse. A gate with an exception is not a gate, and the first exception establishes the precedent. Offer stricter policies, faster approvals, or a narrower agent instead. |
| Cost per tenant exceeds the plan | Medium | Budget caps already stop the bleeding. The commercial question is separate from the technical one. |
| Onboarding is slower than sales expects | High | Sequence one tenant at a time regardless. A rushed second onboarding is how multi-tenancy defects reach two customers instead of one. |

---

## 6. Communication

Design partners get a named engineer, a weekly call, and an honest changelog — including a plain
account of anything that went wrong on their tenant. A governance product that is evasive about its
own failures is asking customers to trust something it will not model itself.
