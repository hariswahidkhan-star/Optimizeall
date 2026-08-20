# 16 — Data Model & Schema

**Phase 4 · Status: awaiting approval.** This is the schema *design*: DDL as specification. Versioned migration files are generated from it during the build phase, not here.

## 16.0 Assumptions carried

| Prereq | Assumption | Impact if different |
|---|---|---|
| `P4-02` volumes | **A-07:** at 12 months — ~39k leads, ~500 content items, ~250k tasks, ~600k model executions, ~1.2M audit events, ~150k knowledge chunks | Below this, partitioning is premature but harmless. An order of magnitude above it moves `interaction` and `task` to monthly partitions too, and forces a read replica for analytics |
| `P4-03` external API consumers | **A-08:** internal only; no third party calls our API in v1 | A public contract adds per-consumer rate limits, scoped tokens, a deprecation policy and a published OpenAPI — additive, not a redesign |
| `P4-04` export formats | **A-09:** Excel primary, PDF for reports, CSV for data | Configuration |
| `A-05` residency | Single EU/UK region | **Still the expensive one.** Per-organisation residency routing must land before production data exists, not after |
| `A-03` CMS | REST with canonical control | Determines the shape of `integration.connector.manifest` for the publisher only |

## 16.1 Conventions

| Concern | Decision | Reasoning |
|---|---|---|
| Primary keys | `uuid` (UUIDv7, generated application-side) | Time-ordered gives index locality without exposing row counts, and survives future sharding. Sequences leak volume and break under multi-region |
| Organisation key | `org_id uuid NOT NULL` on **every** business row | Row-level security is meaningless without it. No exceptions, including lookup data |
| Timestamps | `timestamptz`, stored UTC; organisation time zone applied at read | The workbook's own hardest debugging class is time-zone confusion; storing local time repeats it |
| Money | `numeric(18,4)` + `currency char(3)` | Never floating point for money |
| Audit columns | `created_at`, `updated_at`, `created_by`, `updated_by` | `*_by` references `identity.actor`, which unifies humans and agents |
| Soft delete | `deleted_at timestamptz NULL` where records must survive correction | The workbook's rule is that rows are never deleted; corrections are edits |
| Concurrency | `row_version integer NOT NULL DEFAULT 1`, surfaced as an ETag | Optimistic concurrency; lost-update protection on every mutation |
| Naming | `snake_case`, singular table names, `<table>_id` foreign keys | Predictable for generated clients |

### Enumerations — two kinds, deliberately

**Business enumerations become lookup tables in `config`.** Platforms, objectives, brands, activity types, content types, outcomes, lead segments, organisation types, template codes, funnel stages, areas, priorities, frequencies. They are the workbook's `Lists` sheet, and they carry the extra attributes the workbook holds beside each value — the platform's area, priority, value rank, country strength and per-platform deduplication rule.

Foreign keys to these tables give the platform something the spreadsheet never had: **a value that is invisible to reporting cannot be selected**, which is the workbook's own worst structural defect fixed at the schema level.

**State machines become native PostgreSQL enum types.** `task_state`, `approval_state`, `external_action_state`, `content_status`, `outreach_state`, `health_state`, `lifecycle_state`. These are coupled to code — adding a value requires new handling — so the migration friction of a native enum is a feature, not a cost.

### Schemas

Seventeen schemas across the thirteen modules from Phase 2. Two modules split their physical storage:

| Schema | Module | Note |
|---|---|---|
| `identity` | identity | Principals, roles, permissions, sessions |
| `registry` | identity | **Refinement:** agent versions, prompts, tools, grants, evaluations. Same module boundary, separate schema — different lifecycle, different access pattern, and it keeps authentication tables small |
| `config` | config | Settings, enumerations, calendar, flags, kill switch |
| `worksurface` | worksurface | Goals, campaigns, tasks |
| `orchestration` | worksurface | **Refinement:** workflow definitions, versions, runs, schedules, job executions |
| `policy` · `approval` · `content` · `pipeline` · `search` · `insight` · `ledger` · `knowledge` · `integration` · `audit` | as named | |
| `messaging` | cross-cutting | Outbox and consumer idempotency |
| `readmodel` | cross-cutting | Materialised projections for dashboards |

---

## 16.2 Row-level security

Enabled and **forced** on every table carrying `org_id`:

```sql
ALTER TABLE pipeline.lead ENABLE ROW LEVEL SECURITY;
ALTER TABLE pipeline.lead FORCE ROW LEVEL SECURITY;

CREATE POLICY org_isolation ON pipeline.lead
  USING      (org_id = current_setting('app.org_id', true)::uuid)
  WITH CHECK (org_id = current_setting('app.org_id', true)::uuid);
```

`FORCE` matters: without it the table owner bypasses the policy, and the application's own migration role is usually the owner. `app.org_id` is set per connection from the authenticated principal's claims and **never** from a request body. A second setting, `app.actor_id`, supplies audit defaults.

A forgotten `WHERE org_id = …` therefore returns nothing rather than another organisation's data — the multi-tenant guarantee arriving early, at no extra cost.

---

## 16.3 Identity & registry

```sql
CREATE TYPE identity.actor_type   AS ENUM ('human','agent','service');
CREATE TYPE registry.lifecycle    AS ENUM ('draft','test','staging','production','retired');
CREATE TYPE registry.tool_kind    AS ENUM ('read','write');

CREATE TABLE identity.organization (
  id                uuid PRIMARY KEY,
  name              text        NOT NULL,
  slug              citext      NOT NULL UNIQUE,
  timezone          text        NOT NULL,                    -- IANA, e.g. 'Europe/London'
  week_start_day    smallint    NOT NULL DEFAULT 2 CHECK (week_start_day BETWEEN 1 AND 7),
  working_days      smallint    NOT NULL DEFAULT 5 CHECK (working_days BETWEEN 1 AND 7),
  programme_start   date        NOT NULL,
  residency_region  text        NOT NULL DEFAULT 'eu-west',  -- see §16.11
  created_at        timestamptz NOT NULL DEFAULT now(),
  updated_at        timestamptz NOT NULL DEFAULT now()
);

-- The union of everyone who can act. This table is why "agent work credited to a
-- human" is impossible rather than merely discouraged.
CREATE TABLE identity.actor (
  id           uuid PRIMARY KEY,
  org_id       uuid NOT NULL REFERENCES identity.organization(id),
  actor_type   identity.actor_type NOT NULL,
  display_name text NOT NULL,
  active       boolean NOT NULL DEFAULT true,
  created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE identity.user_account (
  actor_id         uuid PRIMARY KEY REFERENCES identity.actor(id),
  email            citext NOT NULL,
  external_subject text   NOT NULL,          -- OIDC sub
  mfa_enrolled     boolean NOT NULL DEFAULT false,
  status           text   NOT NULL DEFAULT 'active',
  last_login_at    timestamptz,
  UNIQUE (email),
  UNIQUE (external_subject)
);

CREATE TABLE identity.agent_principal (
  actor_id           uuid PRIMARY KEY REFERENCES identity.actor(id),
  agent_id           uuid NOT NULL REFERENCES registry.agent(id),
  current_version_id uuid REFERENCES registry.agent_version(id)
);

CREATE TABLE identity.permission (
  code        text PRIMARY KEY,              -- 'content.publish', 'approval.grant'
  category    text NOT NULL,
  description text NOT NULL,
  is_write    boolean NOT NULL
);

CREATE TABLE identity.role (
  id      uuid PRIMARY KEY,
  org_id  uuid NOT NULL REFERENCES identity.organization(id),
  code    text NOT NULL,                     -- Owner, Admin, Manager, Operator, Closer, Analyst, Compliance
  name    text NOT NULL,
  is_system boolean NOT NULL DEFAULT false,
  UNIQUE (org_id, code)
);

CREATE TABLE identity.role_permission (
  role_id         uuid NOT NULL REFERENCES identity.role(id) ON DELETE CASCADE,
  permission_code text NOT NULL REFERENCES identity.permission(code),
  PRIMARY KEY (role_id, permission_code)
);

CREATE TABLE identity.actor_role (
  actor_id   uuid NOT NULL REFERENCES identity.actor(id) ON DELETE CASCADE,
  role_id    uuid NOT NULL REFERENCES identity.role(id),
  granted_by uuid NOT NULL REFERENCES identity.actor(id),
  granted_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (actor_id, role_id)
);
```

```sql
CREATE TABLE registry.agent (
  id       uuid PRIMARY KEY,
  org_id   uuid NOT NULL,
  code     text NOT NULL,                    -- ORCH, LEAD, CWRITE …
  name     text NOT NULL,
  division text NOT NULL,
  mission  text NOT NULL,
  active   boolean NOT NULL DEFAULT true,
  UNIQUE (org_id, code)
);

CREATE TABLE registry.prompt_version (
  id           uuid PRIMARY KEY,
  org_id       uuid NOT NULL,
  prompt_id    uuid NOT NULL REFERENCES registry.prompt(id),
  version_no   integer NOT NULL,
  template     text    NOT NULL,
  variables    jsonb   NOT NULL DEFAULT '[]',
  lifecycle    registry.lifecycle NOT NULL DEFAULT 'draft',
  created_by   uuid NOT NULL REFERENCES identity.actor(id),
  approved_by  uuid REFERENCES identity.actor(id),
  approved_at  timestamptz,
  UNIQUE (prompt_id, version_no),
  CHECK (lifecycle <> 'production' OR approved_by IS NOT NULL)   -- unapproved cannot be live
);

CREATE TABLE registry.agent_version (
  id                uuid PRIMARY KEY,
  org_id            uuid NOT NULL,
  agent_id          uuid NOT NULL REFERENCES registry.agent(id),
  version_no        integer NOT NULL,
  lifecycle         registry.lifecycle NOT NULL DEFAULT 'draft',
  prompt_version_id uuid NOT NULL REFERENCES registry.prompt_version(id),
  model_policy      jsonb NOT NULL,     -- task class → model class, fallback chain
  knowledge_scope   jsonb NOT NULL,
  memory_scope      jsonb NOT NULL,
  output_schema     jsonb NOT NULL,     -- the JSON Schema the platform validates against
  policies          jsonb NOT NULL,     -- approval requirements, confidence threshold
  ceilings          jsonb NOT NULL,     -- steps, tool calls, wall time, tokens, cost
  created_by        uuid NOT NULL REFERENCES identity.actor(id),
  approved_by       uuid REFERENCES identity.actor(id),
  approved_at       timestamptz,
  created_at        timestamptz NOT NULL DEFAULT now(),
  UNIQUE (agent_id, version_no),
  CHECK (lifecycle <> 'production' OR approved_by IS NOT NULL)
);

CREATE TABLE registry.tool (
  key                 text PRIMARY KEY,       -- 'publish_content', 'search_leads'
  name                text NOT NULL,
  module              text NOT NULL,
  kind                registry.tool_kind NOT NULL,
  input_schema        jsonb NOT NULL,
  output_schema       jsonb NOT NULL,
  required_permission text NOT NULL REFERENCES identity.permission(code),
  risk                text NOT NULL,
  side_effects        text NOT NULL,
  timeout_ms          integer NOT NULL,
  retry_policy        jsonb NOT NULL
);

-- Deny by default: absence of a row is absence of the capability.
CREATE TABLE registry.agent_tool_grant (
  agent_version_id uuid NOT NULL REFERENCES registry.agent_version(id) ON DELETE CASCADE,
  tool_key         text NOT NULL REFERENCES registry.tool(key),
  PRIMARY KEY (agent_version_id, tool_key)
);

CREATE TABLE registry.evaluation_run (
  id               uuid PRIMARY KEY,
  org_id           uuid NOT NULL,
  agent_version_id uuid NOT NULL REFERENCES registry.agent_version(id),
  evaluation_set_id uuid NOT NULL REFERENCES registry.evaluation_set(id),
  run_at           timestamptz NOT NULL DEFAULT now(),
  scores           jsonb NOT NULL,
  compared_to      uuid REFERENCES registry.agent_version(id),
  regression       boolean NOT NULL,
  UNIQUE (agent_version_id, evaluation_set_id, run_at)
);
```

The two `CHECK (lifecycle <> 'production' OR approved_by IS NOT NULL)` constraints are worth noting: *an unapproved prompt or agent version cannot be in production*, enforced by the database rather than by the promotion workflow remembering to check.

---

## 16.4 Pipeline — where the workbook's arithmetic becomes a constraint

The most important design decision in this phase is that derived values are **generated columns**. `FR-022` says the lead score is computed, never written. A generated column makes that literally true: there is no `UPDATE` that can set it.

```sql
CREATE TYPE pipeline.outreach_state AS ENUM
  ('qualified','composed','policy_blocked','pending_approval','queued_for_human',
   'sent','awaiting_reply','follow_up_1','follow_up_2','no_response','replied',
   'declined','suppressed','expired','rejected');

CREATE TABLE pipeline.contact (
  id             uuid PRIMARY KEY,
  org_id         uuid NOT NULL,
  account_id     uuid REFERENCES pipeline.account(id),
  full_name      text NOT NULL,
  role_title     text,
  seniority      text,
  country        text,
  profile_url    text,
  email_hash     bytea,                        -- hashed; raw email lives encrypted
  years_experience smallint CHECK (years_experience >= 0),
  classification text NOT NULL DEFAULT 'confidential',
  created_at     timestamptz NOT NULL DEFAULT now(),
  updated_at     timestamptz NOT NULL DEFAULT now(),
  deleted_at     timestamptz,
  row_version    integer NOT NULL DEFAULT 1
);

-- Identity resolution: the workbook's duplicate defect, fixed at the schema level.
CREATE UNIQUE INDEX contact_profile_uk ON pipeline.contact (org_id, lower(profile_url))
  WHERE profile_url IS NOT NULL AND deleted_at IS NULL;
CREATE UNIQUE INDEX contact_email_uk   ON pipeline.contact (org_id, email_hash)
  WHERE email_hash IS NOT NULL AND deleted_at IS NULL;

CREATE TABLE pipeline.lead (
  id            uuid PRIMARY KEY,
  org_id        uuid NOT NULL,
  contact_id    uuid NOT NULL REFERENCES pipeline.contact(id),
  account_id    uuid REFERENCES pipeline.account(id),
  segment_code  text REFERENCES config.lead_segment(code),
  objective_id  uuid NOT NULL REFERENCES config.objective(id),
  brand_id      uuid NOT NULL REFERENCES config.brand(id),
  source        text NOT NULL,
  owner_actor_id uuid REFERENCES identity.actor(id),

  qualified     boolean NOT NULL DEFAULT false,
  disqualification_reason text
    CHECK (disqualification_reason IN
      ('years_experience','no_current_employer','seniority','inactive_account')),

  icp_fit       smallint CHECK (icp_fit  BETWEEN 1 AND 5),
  intent        smallint CHECK (intent   BETWEEN 1 AND 5),

  -- FR-022: the workbook's formula, unwritable by any code path.
  score integer GENERATED ALWAYS AS
    (COALESCE(icp_fit,0) * 12 + COALESCE(intent,0) * 8) STORED,

  band  text GENERATED ALWAYS AS (
    CASE WHEN icp_fit IS NULL AND intent IS NULL THEN NULL
         WHEN COALESCE(icp_fit,0)*12 + COALESCE(intent,0)*8 >= 80 THEN 'A'
         WHEN COALESCE(icp_fit,0)*12 + COALESCE(intent,0)*8 >= 60 THEN 'B'
         WHEN COALESCE(icp_fit,0)*12 + COALESCE(intent,0)*8 >= 40 THEN 'C'
         ELSE 'D' END) STORED,

  -- Outreach facts denormalised onto the lead so funnel stage can also be generated.
  connection_sent boolean NOT NULL DEFAULT false,
  accepted        boolean NOT NULL DEFAULT false,
  message_sent    boolean NOT NULL DEFAULT false,
  outcome_code    text REFERENCES config.outcome(code),

  funnel_stage_code text GENERATED ALWAYS AS (
    CASE
      WHEN outcome_code = 'Converted'           THEN '8_won'
      WHEN outcome_code = 'Application Started' THEN '7_opportunity'
      WHEN outcome_code = 'Meeting Booked'      THEN '6_meeting'
      WHEN outcome_code IN ('Interested','Info Requested') THEN '5_interested'
      WHEN outcome_code IN ('Declined','Not Relevant','Do Not Contact / Unsubscribed')
                                                THEN '9_closed_lost'
      WHEN message_sent    THEN '4_contacted'
      WHEN accepted        THEN '2_engaged'
      WHEN connection_sent THEN '1_awareness'
      WHEN COALESCE(icp_fit,0)*12 + COALESCE(intent,0)*8 >= 60 THEN '3_qualified'
      ELSE '1_awareness'
    END) STORED,

  next_action     text,
  next_action_due date,
  declined_at     timestamptz,
  converted_at    timestamptz,
  pci_order_ref   text,

  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  row_version integer NOT NULL DEFAULT 1,

  -- FR-028: revenue that cannot be reconciled is not revenue.
  CONSTRAINT converted_needs_order_ref
    CHECK (converted_at IS NULL OR pci_order_ref IS NOT NULL),
  -- Data-health check: acceptance without a logged request is impossible.
  CONSTRAINT accepted_needs_request
    CHECK (NOT accepted OR connection_sent)
);

-- AI-023: a rating without evidence is not a valid output.
CREATE TABLE pipeline.lead_evidence (
  id           uuid PRIMARY KEY,
  org_id       uuid NOT NULL,
  lead_id      uuid NOT NULL REFERENCES pipeline.lead(id) ON DELETE CASCADE,
  kind         text NOT NULL CHECK (kind IN ('icp','intent','personalisation')),
  claim        text NOT NULL,
  source_url   text,
  retrieved_at timestamptz NOT NULL,
  published_at timestamptz,
  confidence   numeric(4,3) CHECK (confidence BETWEEN 0 AND 1),
  model_execution_id uuid REFERENCES ledger.model_execution(id)
);

CREATE TABLE pipeline.outreach_item (
  id             uuid PRIMARY KEY,
  org_id         uuid NOT NULL,
  lead_id        uuid NOT NULL REFERENCES pipeline.lead(id),
  template_code  text NOT NULL REFERENCES content.message_template(code),
  channel        text NOT NULL,
  composed_text  text NOT NULL,
  char_count     integer GENERATED ALWAYS AS (length(composed_text)) STORED,
  char_limit     integer NOT NULL,
  personal_line  text NOT NULL,
  personal_line_evidence_id uuid NOT NULL REFERENCES pipeline.lead_evidence(id),
  state          pipeline.outreach_state NOT NULL DEFAULT 'composed',
  approval_id    uuid REFERENCES approval.approval_item(id),
  external_action_id uuid REFERENCES integration.external_action(id),

  approved_text  text,
  sent_text      text,
  sent_at        timestamptz,
  sent_by_actor_id uuid REFERENCES identity.actor(id),
  divergence     boolean GENERATED ALWAYS AS
                   (sent_text IS NOT NULL AND sent_text IS DISTINCT FROM approved_text) STORED,

  follow_up_number smallint NOT NULL DEFAULT 0,
  parent_outreach_id uuid REFERENCES pipeline.outreach_item(id),

  created_at timestamptz NOT NULL DEFAULT now(),
  row_version integer NOT NULL DEFAULT 1,

  CONSTRAINT within_char_limit  CHECK (char_count <= char_limit),
  CONSTRAINT personal_line_present CHECK (length(btrim(personal_line)) > 0),
  CONSTRAINT max_two_follow_ups CHECK (follow_up_number <= 2),
  CONSTRAINT sent_needs_approval CHECK (sent_at IS NULL OR approval_id IS NOT NULL)
);
```

Five constraints on that last table encode five separate workbook rules — character limit, personal line, the two-follow-up maximum, no send without approval, and automatic detection when the text sent diverges from the text approved. None of them can be bypassed by a bug in application code.

```sql
CREATE TABLE pipeline.partnership (
  id             uuid PRIMARY KEY,
  org_id         uuid NOT NULL,
  account_id     uuid NOT NULL REFERENCES pipeline.account(id),
  org_type_code  text NOT NULL REFERENCES config.org_type(code),
  contact_id     uuid REFERENCES pipeline.contact(id),
  why_them       text NOT NULL,
  icp_fit        smallint CHECK (icp_fit BETWEEN 1 AND 5),
  intent         smallint CHECK (intent  BETWEEN 1 AND 5),
  score integer GENERATED ALWAYS AS
    (COALESCE(icp_fit,0)*12 + COALESCE(intent,0)*8) STORED,
  stage_code     text NOT NULL REFERENCES config.partnership_stage(code),
  potential_value numeric(18,4),
  deal_value      numeric(18,4),
  contract_signed_at date,
  next_step      text,
  next_step_due  date,
  objective_id   uuid NOT NULL REFERENCES config.objective(id),
  brand_id       uuid NOT NULL REFERENCES config.brand(id),
  -- Data-health check: a signed deal worth nothing sits in neither pipeline nor revenue.
  CONSTRAINT signed_needs_value CHECK (contract_signed_at IS NULL OR deal_value IS NOT NULL)
);
```

---

## 16.5 Content

```sql
CREATE TYPE content.status AS ENUM
  ('idea','briefed','drafting','qa','compliance','pending_approval',
   'scheduled','held','publishing','published','repurposed','publish_failed','expired');

CREATE TABLE content.content_item (
  id            uuid PRIMARY KEY,
  org_id        uuid NOT NULL,
  brief_id      uuid REFERENCES content.article_brief(id),
  campaign_id   uuid REFERENCES worksurface.campaign(id),
  objective_id  uuid NOT NULL REFERENCES config.objective(id),
  brand_id      uuid NOT NULL REFERENCES config.brand(id),
  platform_id   uuid NOT NULL REFERENCES config.platform(id),      -- NOT NULL: data-health check
  content_type_code text NOT NULL REFERENCES config.content_type(code),
  title         text NOT NULL,
  pillar        text, cluster text, funnel_stage text, cta text,
  status        content.status NOT NULL DEFAULT 'idea',
  scheduled_at  timestamptz,
  published_at  timestamptz,
  published_url text,
  canonical_url text,
  original_content_id uuid REFERENCES content.content_item(id),    -- repurposed → original
  author_actor_id uuid REFERENCES identity.actor(id),
  approval_id   uuid REFERENCES approval.approval_item(id),
  embedding     vector(1536),                                       -- near-duplicate detection
  created_at timestamptz NOT NULL DEFAULT now(),
  row_version integer NOT NULL DEFAULT 1,

  -- The two mirror-image data-health checks, as one biconditional.
  CONSTRAINT published_status_and_date_agree
    CHECK ((status IN ('published','repurposed')) = (published_at IS NOT NULL)),
  CONSTRAINT published_needs_url
    CHECK (status NOT IN ('published','repurposed') OR published_url IS NOT NULL),
  CONSTRAINT repurposed_needs_original
    CHECK (status <> 'repurposed' OR original_content_id IS NOT NULL)
);

CREATE TABLE content.message_template (
  id          uuid PRIMARY KEY,
  org_id      uuid NOT NULL,
  code        text NOT NULL,                    -- M1 … M15
  use_case    text NOT NULL,
  body        text NOT NULL,
  char_limit  integer NOT NULL,
  char_count  integer GENERATED ALWAYS AS (length(body)) STORED,
  rules       text,
  status      text NOT NULL DEFAULT 'draft' CHECK (status IN ('draft','approved','retired')),
  approved_by uuid REFERENCES identity.actor(id),
  approved_at timestamptz,
  version_no  integer NOT NULL DEFAULT 1,
  UNIQUE (org_id, code, version_no),
  CONSTRAINT template_within_limit CHECK (char_count <= char_limit),
  CONSTRAINT approved_needs_approver
    CHECK (status <> 'approved' OR (approved_by IS NOT NULL AND approved_at IS NOT NULL))
);
```

`approved_needs_approver` closes the workbook's own open item: templates are currently marked Approved with no approver recorded. Here that state cannot exist.

---

## 16.6 Approval, policy, integration

```sql
CREATE TYPE approval.state AS ENUM
  ('generated','reviewed','pending_human','approved','rejected','edited',
   'expired','executing','held','executed','execution_failed');

CREATE TABLE approval.approval_item (
  id                    uuid PRIMARY KEY,
  org_id                uuid NOT NULL,
  action_type           text NOT NULL,
  subject_type          text NOT NULL,
  subject_id            uuid NOT NULL,
  generating_actor_id   uuid NOT NULL REFERENCES identity.actor(id),
  reviewing_actor_id    uuid REFERENCES identity.actor(id),
  risk_level            text NOT NULL CHECK (risk_level IN ('low','medium','high')),
  reason                text NOT NULL,
  preview               jsonb NOT NULL,
  target                jsonb NOT NULL,        -- who receives this, on what channel
  expected_benefit      text,
  evidence              jsonb NOT NULL DEFAULT '[]',
  state                 approval.state NOT NULL DEFAULT 'generated',
  expires_at            timestamptz,
  decided_by            uuid REFERENCES identity.actor(id),
  decided_at            timestamptz,
  feedback_code         text,
  decision_note         text,
  correlation_id        uuid NOT NULL,
  created_at            timestamptz NOT NULL DEFAULT now(),
  row_version           integer NOT NULL DEFAULT 1,

  -- APR-018 / SEC-036 at the schema level: four eyes, enforced.
  CONSTRAINT four_eyes CHECK (
    decided_by IS NULL
    OR (decided_by <> generating_actor_id
        AND (reviewing_actor_id IS NULL OR decided_by <> reviewing_actor_id))
  ),
  CONSTRAINT decided_has_timestamp
    CHECK ((decided_by IS NULL) = (decided_at IS NULL))
);

-- A trigger additionally rejects any decided_by whose actor_type <> 'human'.
```

```sql
-- Append-only. Removal is an administrative act with its own audit trail.
CREATE TABLE policy.suppression_entry (
  id           uuid PRIMARY KEY,
  org_id       uuid NOT NULL,
  subject_kind text NOT NULL CHECK (subject_kind IN ('contact','email','domain','account')),
  subject_hash bytea NOT NULL,
  reason       text NOT NULL,
  source       text NOT NULL,
  created_by   uuid NOT NULL REFERENCES identity.actor(id),
  created_at   timestamptz NOT NULL DEFAULT now(),
  UNIQUE (org_id, subject_kind, subject_hash)
);
REVOKE UPDATE, DELETE ON policy.suppression_entry FROM app_role;
```

```sql
CREATE TYPE integration.action_state AS ENUM
  ('reserved','in_flight','succeeded','failed','unknown');

CREATE TABLE integration.external_action (
  id              uuid PRIMARY KEY,
  org_id          uuid NOT NULL,
  idempotency_key text NOT NULL,               -- hash(org, action_type, entity_id, content_hash)
  action_type     text NOT NULL,
  connector_id    uuid NOT NULL REFERENCES integration.connector(id),
  subject_type    text NOT NULL,
  subject_id      uuid NOT NULL,
  state           integration.action_state NOT NULL DEFAULT 'reserved',
  provider_ref    text,
  content_hash    bytea NOT NULL,
  attempts        smallint NOT NULL DEFAULT 0,
  last_error      text,
  reconciliation_state text,
  approval_id     uuid REFERENCES approval.approval_item(id),
  requested_by_actor_id uuid NOT NULL REFERENCES identity.actor(id),
  correlation_id  uuid NOT NULL,
  created_at      timestamptz NOT NULL DEFAULT now(),
  completed_at    timestamptz,
  UNIQUE (org_id, idempotency_key)             -- one key, one effect
);

CREATE TABLE integration.human_work_item (
  id                 uuid PRIMARY KEY,
  org_id             uuid NOT NULL,
  external_action_id uuid NOT NULL UNIQUE REFERENCES integration.external_action(id),
  assigned_actor_id  uuid REFERENCES identity.actor(id),
  channel            text NOT NULL,
  payload            jsonb NOT NULL,
  state              text NOT NULL DEFAULT 'queued',
  confirmed_at       timestamptz,
  confirmed_by       uuid REFERENCES identity.actor(id),
  confirmed_payload  jsonb,
  CONSTRAINT confirmed_by_human_only
    CHECK (confirmed_by IS NULL OR confirmed_at IS NOT NULL)
);
```

---

## 16.7 Insight, ledger, audit — the partitioned tables

```sql
CREATE TABLE insight.kpi_computation (
  id             uuid PRIMARY KEY,
  org_id         uuid NOT NULL,
  kpi_id         uuid NOT NULL REFERENCES insight.kpi_definition(id),
  formula_version integer NOT NULL,
  window_start   timestamptz NOT NULL,
  window_end     timestamptz NOT NULL,
  computed_at    timestamptz NOT NULL DEFAULT now(),
  source_freshness jsonb NOT NULL          -- {"gsc":"2026-08-18T04:00Z","ga4":"…"}
);

CREATE TABLE insight.kpi_input_ref (
  computation_id uuid NOT NULL REFERENCES insight.kpi_computation(id) ON DELETE CASCADE,
  module         text NOT NULL,
  aggregate      text NOT NULL,
  record_id      uuid NOT NULL,
  PRIMARY KEY (computation_id, module, aggregate, record_id)
);

CREATE TABLE insight.kpi_observation (
  id             uuid NOT NULL,
  org_id         uuid NOT NULL,
  kpi_id         uuid NOT NULL,
  computation_id uuid NOT NULL REFERENCES insight.kpi_computation(id),
  period_start   timestamptz NOT NULL,
  period_end     timestamptz NOT NULL,
  value          numeric(18,4) NOT NULL,
  status         text NOT NULL,               -- ok | warning | critical
  PRIMARY KEY (id, period_start)
) PARTITION BY RANGE (period_start);
```

`kpi_input_ref` is `FR-091` made physical: every displayed number resolves to the exact rows it was computed from, and to the formula version used. Without this table, lineage is a promise.

```sql
CREATE TABLE ledger.model_execution (
  id                uuid NOT NULL,
  org_id            uuid NOT NULL,
  task_id           uuid,
  agent_version_id  uuid REFERENCES registry.agent_version(id),
  prompt_version_id uuid REFERENCES registry.prompt_version(id),
  provider          text NOT NULL,
  model             text NOT NULL,
  task_class        text NOT NULL,
  input_tokens      integer NOT NULL CHECK (input_tokens  >= 0),
  output_tokens     integer NOT NULL CHECK (output_tokens >= 0),
  cached_tokens     integer NOT NULL DEFAULT 0 CHECK (cached_tokens >= 0),
  cost_amount       numeric(18,6) NOT NULL CHECK (cost_amount >= 0),
  currency          char(3) NOT NULL,
  latency_ms        integer NOT NULL CHECK (latency_ms >= 0),
  outcome           text NOT NULL,
  correlation_id    uuid NOT NULL,
  created_at        timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

-- Append-only, hash-chained per organisation.
CREATE TABLE audit.audit_event (
  id                uuid NOT NULL,
  org_id            uuid NOT NULL,
  seq               bigint NOT NULL,
  prev_hash         bytea NOT NULL,
  payload_hash      bytea NOT NULL,
  occurred_at       timestamptz NOT NULL DEFAULT now(),
  actor_id          uuid NOT NULL,
  actor_type        identity.actor_type NOT NULL,
  agent_version_id  uuid,
  prompt_version_id uuid,
  action            text NOT NULL,
  entity_type       text NOT NULL,
  entity_id         uuid,
  correlation_id    uuid NOT NULL,
  risk_level        text,
  result            text NOT NULL,
  cost_amount       numeric(18,6),
  payload           jsonb NOT NULL,
  PRIMARY KEY (id, occurred_at),
  UNIQUE (org_id, seq, occurred_at)
) PARTITION BY RANGE (occurred_at);

REVOKE UPDATE, DELETE ON audit.audit_event FROM app_role;
```

Partitioning is monthly on all three, with the next three months pre-created by a scheduled job. At A-07 volumes this keeps the working set small and makes retention a `DROP PARTITION` rather than a mass delete.

---

## 16.8 Messaging

```sql
CREATE TABLE messaging.outbox (
  id             uuid PRIMARY KEY,
  org_id         uuid NOT NULL,
  event_type     text NOT NULL,
  event_version  smallint NOT NULL DEFAULT 1,
  payload        jsonb NOT NULL,
  correlation_id uuid NOT NULL,
  causation_id   uuid,
  occurred_at    timestamptz NOT NULL DEFAULT now(),
  published_at   timestamptz
);
CREATE INDEX outbox_unpublished ON messaging.outbox (occurred_at)
  WHERE published_at IS NULL;                  -- the relay's only query

CREATE TABLE messaging.processed (
  consumer     text NOT NULL,
  event_id     uuid NOT NULL,
  processed_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (consumer, event_id)
);
```

---

## 16.9 The ten data-health checks as constraints

The workbook counts these after the fact. Nine become impossible; the tenth cannot be a constraint because the truth lives in another system.

| Workbook check | Mechanism |
|---|---|
| Unreadable (text) date | `timestamptz` / `date` columns; parsed at the API boundary |
| Future-dated rows | `CHECK (occurred_at <= now() + interval '1 day')` on event-dated columns |
| Accepted without a logged request | `CHECK (NOT accepted OR connection_sent)` on `pipeline.lead` |
| Minutes under a name not on the roster | `actor_id` foreign key to `identity.actor` |
| Published with no published date | `CHECK ((status IN ('published','repurposed')) = (published_at IS NOT NULL))` |
| Published date but not marked published | the same biconditional — one constraint closes both directions |
| Signed deal with no value | `CHECK (contract_signed_at IS NULL OR deal_value IS NOT NULL)` |
| Duplicate leads | partial unique indexes on profile URL and email hash |
| Published content with no platform | `platform_id NOT NULL` |
| Negative values | `CHECK (… >= 0)` on every quantity, minute and money column |
| *Reconciliation against systems of record* | **not a constraint** — a continuous job comparing conversions to PCI order references |

---

## 16.10 Indexes

Beyond primary keys and the uniqueness constraints above:

```sql
-- Every hot query leads with org_id; RLS makes that predicate universal anyway.
CREATE INDEX lead_stage_idx      ON pipeline.lead (org_id, funnel_stage_code, updated_at DESC);
CREATE INDEX lead_band_score_idx ON pipeline.lead (org_id, band, score DESC)
  WHERE qualified AND declined_at IS NULL;
CREATE INDEX outreach_state_idx  ON pipeline.outreach_item (org_id, state, created_at);
CREATE INDEX outreach_followup_idx ON pipeline.outreach_item (org_id, sent_at)
  WHERE state IN ('awaiting_reply','follow_up_1');
CREATE INDEX approval_queue_idx  ON approval.approval_item (org_id, state, expires_at)
  WHERE state = 'pending_human';
CREATE INDEX content_pub_idx     ON content.content_item (org_id, status, published_at DESC);
CREATE INDEX content_fts_idx     ON content.content_item
  USING gin (to_tsvector('english', title));
CREATE INDEX task_queue_idx      ON worksurface.task (org_id, state, priority, scheduled_at);
CREATE INDEX audit_corr_idx      ON audit.audit_event (org_id, correlation_id);
CREATE INDEX model_exec_cost_idx ON ledger.model_execution (org_id, created_at DESC, agent_version_id);
CREATE INDEX chunk_ann_idx       ON knowledge.chunk
  USING hnsw (embedding vector_cosine_ops) WHERE superseded_at IS NULL;
```

Two deliberate choices: the approval-queue index is **partial** on `pending_human`, because that is the only state anyone queries in anger; and the vector index excludes superseded chunks, so retrieval never has to filter them out after the fact.

---

## 16.11 Migration strategy

**Principle: no release contains a change that requires the previous release to be down.** Every structural change is expand → migrate → contract, spanning at least two releases.

| Phase | What ships | Rule |
|---|---|---|
| **Expand** | New column nullable, new table, new index `CONCURRENTLY` | Old code still works unchanged |
| **Migrate** | Idempotent, batched, resumable backfill run as a job — never inside the migration | Batches of ~5,000 rows with a bounded lock time |
| **Contract** | `SET NOT NULL` via `NOT VALID` then `VALIDATE CONSTRAINT`; drop the old column | Only after the release that stopped reading it has been live |

Specific rules that matter in PostgreSQL:

- Adding a `NOT NULL` column with a default is safe on modern PostgreSQL; adding a `CHECK` is not — add `NOT VALID`, then `VALIDATE`, which takes a weaker lock.
- Indexes are always built `CONCURRENTLY`; a failed concurrent build leaves an invalid index that the next migration must drop.
- Enum values can be added but not removed; removing one is a new type plus a rewrite, and is therefore a planned two-release operation.
- Partitions for the next three months are created by a scheduled job, not by migrations, so a deployment gap never causes an insert failure.

**Seed data is not migration data.** The workbook's content — 133 platforms, 11 objectives, 7 brands, 5 domains, 15 templates, 15 compliance checks, 63 tasks, 7 pillars, 76 keywords, 5,683 briefs, the KPI catalogue — is loaded through the **governed import path**, not through migration scripts. It is business configuration: it must be diffable, approvable and audited, and a migration script is none of those. Only the enumerations that code depends on structurally ship as migrations.

**Rollback.** Every migration declares a down path or is explicitly marked irreversible with a reason; irreversible migrations require approval before merge. In practice forward-fix is preferred, and the expand/contract discipline is what makes forward-fix possible without downtime.

**Residency.** Under A-05 residency is a deployment property. The `residency_region` column on `identity.organization` exists now, unused, so that the day a Gulf boundary is required the change is storage routing plus provider filtering — not a schema migration on populated tables.
