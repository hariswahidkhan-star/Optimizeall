# 13 — Sequence & Workflow Diagrams

Seven sequences and three workflow state machines. Each shows a mechanism the prose cannot convey compactly: where the boundary is crossed, what state a request moves through, and what happens when it goes wrong.

## 13.1 Content production — the full governed loop, including a QA rejection

```mermaid
sequenceDiagram
    autonumber
    participant T as Temporal (WF-020)
    participant CS as CSTRAT
    participant CW as CWRITE
    participant CQ as CQA
    participant CP as COMP
    participant G as Execution gate
    participant H as Human approver
    participant CMS as CMS connector
    T->>CS: select brief (P1, Easy first)
    CS->>G: check_similarity(topic, keywords)
    G-->>CS: no near-duplicate
    CS-->>T: strategy: pillar, audience, funnel, channel, CTA
    T->>CW: draft from brief + prompt
    CW-->>T: draft + citations + confidence
    T->>CQ: brand & editorial QA
    CQ-->>T: REJECTED — unsupported claim, weak CTA
    T->>CW: revise with structured feedback
    CW-->>T: draft v2
    T->>CQ: QA again
    CQ-->>T: PASS
    T->>CP: compliance & claim verification
    CP-->>T: PASS — claims evidenced, canonical rule satisfied
    T->>G: create_approval_request(content_id)
    G->>H: approval item (preview, evidence, risk, benefit)
    H-->>G: APPROVED
    G->>G: kill switch · rate limit · idempotency reserve
    G->>CMS: publish(content_id)
    CMS-->>G: published URL + provider ref
    G-->>T: ExternalAction = Succeeded
    T->>T: schedule analytics capture at +1d, +7d, +30d
```

The QA rejection is drawn deliberately: it is `AC-02`, and a loop that has never been exercised is a loop that does not work.

## 13.2 Outreach preparation — where the platform stops

```mermaid
sequenceDiagram
    autonumber
    participant T as Temporal (WF-012)
    participant LE as LEAD
    participant OU as OUT
    participant P as policy
    participant G as Execution gate
    participant H as Human approver
    participant S as Human send queue
    participant O as Operator
    T->>LE: qualify + personalise
    LE->>P: check_suppression(contact)
    P-->>LE: clear
    LE-->>T: ICP 4, intent 3, evidence[3], score 72 → band B
    T->>OU: compose from template M2
    OU-->>T: text (287 chars) + personal line + citation
    T->>P: length · banned phrases · frequency cap · consent
    P-->>T: PASS
    T->>G: create_approval_request(outreach_item)
    G->>H: preview + target + risk + supporting research
    H-->>G: APPROVED (expires in 48h)
    G->>S: HumanWorkItem — copy-ready text, profile link, char count
    Note over S,O: The platform stops here. No automated send exists.
    O->>S: confirm sent
    S-->>T: actual send time, exact text, sender
    T->>T: follow-up timers at +4d (M4) and +10d (M5), max two
```

Step 15 is the architectural point: the confirmation, not the approval, starts the timers — anchoring them to the event rather than the record (`WF-021`).

## 13.3 The execution gate rejecting an unauthorised tool

```mermaid
sequenceDiagram
    autonumber
    participant A as Agent (CWRITE)
    participant R as agent-runtime
    participant G as Execution gate
    participant AU as audit
    participant N as Notifications
    A->>R: tool request: send_approved_email(...)
    R->>G: ToolRequest (principal = CWRITE v7, task 4821)
    G->>G: 1 schema OK
    G->>G: 2 tool grant — CWRITE has no email grant
    G->>AU: DENIED — rule tool_grant, correlation 9f2c…
    G-->>R: denied (terminal, not retryable)
    R-->>A: tool unavailable
    G->>N: security alert (3rd denial for this version today)
    Note over G,N: A denial is a defect in the agent version, not a policy success.
```

## 13.4 Publish timeout — why a retry cannot duplicate

```mermaid
sequenceDiagram
    autonumber
    participant W as worker
    participant EA as ExternalAction ledger
    participant C as CMS connector
    participant CMS as CMS
    W->>EA: reserve(key = hash(org, publish, content 812, sha256(body)))
    EA-->>W: Reserved
    W->>C: publish
    C->>CMS: POST /posts
    CMS--xC: timeout (write actually succeeded)
    C-->>W: timeout
    W->>EA: state = Unknown
    Note over W,EA: Retry from Unknown is never a re-send.
    W->>C: reconciliation query
    C->>CMS: GET /posts?client_ref=812
    CMS-->>C: found, id 5573
    C-->>W: already exists
    W->>EA: state = Succeeded, provider_ref = 5573
```

Connectors that cannot support a reconciliation query declare it in the manifest; their `Unknown` actions require human confirmation rather than a blind retry.

## 13.5 Model provider fallback under data classification

```mermaid
sequenceDiagram
    autonumber
    participant A as Agent
    participant GW as Model gateway
    participant P1 as Primary provider
    participant P2 as Fallback provider
    participant L as ledger
    A->>GW: task class reason.deep · classification Confidential
    GW->>L: pre-flight cost estimate vs budget
    L-->>GW: within budget
    GW->>P1: request
    P1--xGW: 503
    GW->>GW: fallback chain → P2 approved for Confidential? YES
    GW->>P2: request
    P2-->>GW: response
    GW->>L: record provider, model, tokens, cost, latency
    GW-->>A: result (provider recorded in the trace)
```

If P2 were **not** approved for the payload's classification it is skipped, not used, and the task queues for the primary's recovery — alerting if deadline-critical (`AI-062`).

## 13.6 Emergency stop catching an approved-but-unexecuted item

```mermaid
sequenceDiagram
    autonumber
    participant O as Owner
    participant CF as config
    participant G as Execution gate
    participant T as Temporal
    participant Q as Pending queue
    O->>CF: EMERGENCY STOP (all external writes)
    CF->>CF: kill switch = ON
    CF->>T: signal running workflows → park
    CF->>Q: hold approved-but-unexecuted items
    Note over Q: The case that matters — approval granted before the problem was found.
    G->>G: step 6 blocks every new request
    O->>CF: release (Owner only)
    CF->>Q: resume, re-checking approval expiry first
```

## 13.7 Approval expiry on a news-linked post

```mermaid
sequenceDiagram
    autonumber
    participant AP as approval
    participant T as Temporal timer
    participant CS as CSTRAT
    AP->>T: item created, expires in 24h (news-linked)
    T-->>AP: 24h elapsed, no decision
    AP->>AP: state = Expired
    AP->>CS: return for regeneration — relevance window closed
    Note over AP,CS: A post approved two days late is not the same post.
```

---

## 13.8 Workflow state machines

### Content item

```mermaid
stateDiagram-v2
    [*] --> Briefed
    Briefed --> Drafting
    Drafting --> QA
    QA --> Drafting: rejected (structured feedback)
    QA --> Compliance: passed
    Compliance --> Drafting: claim unsupported
    Compliance --> PendingApproval
    PendingApproval --> Drafting: rejected
    PendingApproval --> Expired: timer
    Expired --> Drafting
    PendingApproval --> Scheduled: approved
    Scheduled --> Publishing
    Publishing --> Published
    Publishing --> PublishFailed: permanent error
    PublishFailed --> Scheduled: fixed
    Published --> Measuring
    Measuring --> [*]
    Scheduled --> Held: emergency lock
    Held --> Scheduled: released
```

### Outreach item

```mermaid
stateDiagram-v2
    [*] --> Qualified
    Qualified --> Suppressed: on suppression hit
    Qualified --> Composed
    Composed --> PolicyBlocked: length · phrase · cap · consent
    PolicyBlocked --> Composed
    Composed --> PendingApproval
    PendingApproval --> Rejected
    PendingApproval --> Expired: 48h
    Expired --> Composed
    PendingApproval --> QueuedForHuman: approved
    QueuedForHuman --> Sent: operator confirms
    Sent --> AwaitingReply
    AwaitingReply --> FollowUp1: +4d no reply
    FollowUp1 --> FollowUp2: +10d no reply
    FollowUp2 --> NoResponse
    AwaitingReply --> Replied
    Replied --> Declined: outcome
    Declined --> Suppressed
    Replied --> MeetingBooked
    MeetingBooked --> HandedOver
    Suppressed --> [*]
    NoResponse --> [*]
    HandedOver --> [*]
```

Two transitions carry the workbook's rules directly: `FollowUp2 → NoResponse` enforces the hard maximum of two follow-ups, and `Declined → Suppressed` is permanent and irreversible without an audited administrative act.

### Approval item

```mermaid
stateDiagram-v2
    [*] --> Generated
    Generated --> Reviewed: second agent (four-eyes)
    Reviewed --> PendingHumanApproval
    PendingHumanApproval --> Approved
    PendingHumanApproval --> Rejected: with structured feedback
    PendingHumanApproval --> Edited: human amends
    Edited --> Approved
    PendingHumanApproval --> Expired
    Approved --> Executing
    Approved --> Held: kill switch
    Held --> Executing: released, expiry re-checked
    Executing --> Executed
    Executing --> ExecutionFailed
    Expired --> [*]
    Rejected --> [*]
    Executed --> [*]
```

`Approved → Held` is the state that makes `FR-112` real: without it, emergency stop would catch only work that had not yet been approved, which is the wrong half.
