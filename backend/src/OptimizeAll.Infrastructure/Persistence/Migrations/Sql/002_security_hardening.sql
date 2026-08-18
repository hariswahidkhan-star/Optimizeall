-- =============================================================================
-- OptimizeAll — security hardening
--
-- Everything in this file exists because application-layer enforcement alone is
-- insufficient. Query filters catch a forgotten WHERE clause; these catch an ORM
-- bypass, a raw SQL mistake, and a compromised application process.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Database roles
-- -----------------------------------------------------------------------------
-- The application role is subject to row-level security. The platform role can
-- bypass it and is reachable only through the audited escalation path, never by
-- the normal request pipeline.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'optimizeall_app') THEN
        CREATE ROLE optimizeall_app NOLOGIN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'optimizeall_platform') THEN
        CREATE ROLE optimizeall_platform NOLOGIN BYPASSRLS;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'optimizeall_readonly') THEN
        CREATE ROLE optimizeall_readonly NOLOGIN;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA public TO optimizeall_app, optimizeall_platform, optimizeall_readonly;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO optimizeall_app;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO optimizeall_readonly, optimizeall_platform;

-- The audit table is append-only. The application role is granted INSERT and
-- SELECT and nothing else, so "audit records cannot be altered" is a permission
-- the database enforces rather than a convention the code follows.
REVOKE UPDATE, DELETE, TRUNCATE ON audit_event FROM optimizeall_app;
GRANT SELECT, INSERT ON audit_event TO optimizeall_app;

-- -----------------------------------------------------------------------------
-- 2. Row-level security
-- -----------------------------------------------------------------------------
-- FORCE matters as much as ENABLE: without it the table owner — which is the
-- migration role — silently bypasses every policy below.

DO $$
DECLARE
    target text;
    tenant_tables text[] := ARRAY[
        'workspace', 'app_user', 'role', 'role_assignment',
        'agent_definition', 'agent_run', 'agent_memory_entry',
        'workflow_run', 'approval_request', 'approval_policy',
        'audit_event', 'knowledge_document', 'knowledge_chunk',
        'schedule_definition', 'schedule_occurrence',
        'notification', 'outbox_message', 'kpi_snapshot'
    ];
BEGIN
    FOREACH target IN ARRAY tenant_tables
    LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', target);
        EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', target);
        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I', target);

        -- Fail closed on a connection that never set the tenant.
        --
        -- current_setting(..., true) yields NULL when the variable was never set,
        -- but an empty string once it has been set and RESET — and ''::uuid raises
        -- rather than returning NULL. nullif() collapses both cases to NULL, and
        -- "tenant_id = NULL" is never true, so an unscoped connection sees zero
        -- rows instead of erroring on every statement.
        EXECUTE format($f$
            CREATE POLICY tenant_isolation ON %I
                USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
        $f$, target);
    END LOOP;
END
$$;

-- Child tables reached only through their parent inherit isolation through the
-- foreign key, but are protected directly as well: a defect that exposed one of
-- them by id would otherwise cross the tenant boundary.

DO $$
DECLARE
    target text;
    child_tables text[] := ARRAY[
        'agent_tool_grant', 'agent_kpi', 'agent_run_step', 'tool_invocation',
        'approval_decision', 'approval_rule', 'work_task', 'task_dependency',
        'notification_delivery'
    ];
BEGIN
    FOREACH target IN ARRAY child_tables
    LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', target);
        EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', target);
    END LOOP;
END
$$;

-- -----------------------------------------------------------------------------
-- 3. Append-only audit trail
-- -----------------------------------------------------------------------------
-- Permissions already prevent the application from updating or deleting audit
-- rows. This trigger additionally stops a superuser session or a future
-- migration from doing it by accident, which is the more realistic risk.

CREATE OR REPLACE FUNCTION audit_event_is_append_only()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'audit_event is append-only; % is not permitted (attempted on sequence %)',
        TG_OP, COALESCE(OLD.sequence, -1)
        USING ERRCODE = 'insufficient_privilege';
END
$$;

DROP TRIGGER IF EXISTS audit_event_no_mutation ON audit_event;
CREATE TRIGGER audit_event_no_mutation
    BEFORE UPDATE OR DELETE ON audit_event
    FOR EACH ROW EXECUTE FUNCTION audit_event_is_append_only();

-- -----------------------------------------------------------------------------
-- 4. Published agent definitions are immutable
-- -----------------------------------------------------------------------------
-- The aggregate refuses to modify a published definition. This makes the same
-- guarantee true for any path that reaches the table without going through it,
-- while still allowing the one legitimate transition: Published -> Deprecated.

CREATE OR REPLACE FUNCTION agent_definition_published_is_immutable()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.status = 'Published' THEN
        IF NEW.status = 'Deprecated'
           AND NEW.system_prompt IS NOT DISTINCT FROM OLD.system_prompt
           AND NEW.model_policy  IS NOT DISTINCT FROM OLD.model_policy
           AND NEW.budget_policy IS NOT DISTINCT FROM OLD.budget_policy
           AND NEW.memory_policy IS NOT DISTINCT FROM OLD.memory_policy
           AND NEW.max_risk_class IS NOT DISTINCT FROM OLD.max_risk_class THEN
            RETURN NEW;
        END IF;

        IF NEW.system_prompt   IS DISTINCT FROM OLD.system_prompt
           OR NEW.model_policy  IS DISTINCT FROM OLD.model_policy
           OR NEW.budget_policy IS DISTINCT FROM OLD.budget_policy
           OR NEW.memory_policy IS DISTINCT FROM OLD.memory_policy
           OR NEW.max_risk_class IS DISTINCT FROM OLD.max_risk_class
           OR NEW.agent_key      IS DISTINCT FROM OLD.agent_key
           OR NEW.definition_version IS DISTINCT FROM OLD.definition_version THEN
            RAISE EXCEPTION
                'agent definition %/% is published and immutable; create a new version instead',
                OLD.agent_key, OLD.definition_version
                USING ERRCODE = 'integrity_constraint_violation';
        END IF;
    END IF;

    RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS agent_definition_immutable ON agent_definition;
CREATE TRIGGER agent_definition_immutable
    BEFORE UPDATE ON agent_definition
    FOR EACH ROW EXECUTE FUNCTION agent_definition_published_is_immutable();

-- -----------------------------------------------------------------------------
-- 5. Tool grants on a published definition are frozen
-- -----------------------------------------------------------------------------
-- Freezing the definition row while leaving its grants editable would let an
-- agent's capabilities change after review, which is the thing version
-- immutability exists to prevent.

CREATE OR REPLACE FUNCTION agent_tool_grant_is_frozen_when_published()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    definition_status text;
    definition_id uuid := COALESCE(NEW.agent_definition_id, OLD.agent_definition_id);
BEGIN
    SELECT status INTO definition_status
    FROM agent_definition
    WHERE id = definition_id;

    IF definition_status = 'Published' THEN
        RAISE EXCEPTION
            'tool grants cannot be changed on a published agent definition (%)', definition_id
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;

    RETURN COALESCE(NEW, OLD);
END
$$;

DROP TRIGGER IF EXISTS agent_tool_grant_frozen ON agent_tool_grant;
CREATE TRIGGER agent_tool_grant_frozen
    BEFORE INSERT OR UPDATE OR DELETE ON agent_tool_grant
    FOR EACH ROW EXECUTE FUNCTION agent_tool_grant_is_frozen_when_published();

-- -----------------------------------------------------------------------------
-- 6. The task graph must stay acyclic
-- -----------------------------------------------------------------------------
-- The aggregate rejects a cycle before it is persisted. This is the backstop for
-- a path that writes an edge directly: a cyclic graph deadlocks the orchestrator
-- permanently, and no timeout can distinguish it from work that is merely slow.

CREATE OR REPLACE FUNCTION task_dependency_rejects_cycles()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    creates_cycle boolean;
BEGIN
    IF NEW.task_id = NEW.depends_on_task_id THEN
        RAISE EXCEPTION 'a task cannot depend on itself (%)', NEW.task_id
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;

    -- The proposed edge closes a loop exactly when task_id is already reachable
    -- from depends_on_task_id by following existing edges.
    WITH RECURSIVE reachable(node) AS (
        SELECT NEW.depends_on_task_id
        UNION
        SELECT d.depends_on_task_id
        FROM task_dependency d
        JOIN reachable r ON d.task_id = r.node
    )
    SELECT EXISTS (SELECT 1 FROM reachable WHERE node = NEW.task_id)
    INTO creates_cycle;

    IF creates_cycle THEN
        RAISE EXCEPTION
            'dependency % -> % would create a cycle in the task graph',
            NEW.task_id, NEW.depends_on_task_id
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;

    RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS task_dependency_acyclic ON task_dependency;
CREATE TRIGGER task_dependency_acyclic
    BEFORE INSERT OR UPDATE ON task_dependency
    FOR EACH ROW EXECUTE FUNCTION task_dependency_rejects_cycles();

-- -----------------------------------------------------------------------------
-- 7. An approver cannot decide their own request
-- -----------------------------------------------------------------------------
-- Enforced in the aggregate as well. Duplicated here because self-approval is
-- the single control most likely to be bypassed by a future "just this once"
-- code path, and the database has no such pressures.

CREATE OR REPLACE FUNCTION approval_decision_rejects_self_approval()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    requester uuid;
    requester_type text;
BEGIN
    SELECT requested_by_principal, requested_by_type
    INTO requester, requester_type
    FROM approval_request
    WHERE id = NEW.approval_request_id;

    IF requester = NEW.approver_user_id THEN
        RAISE EXCEPTION
            'the principal that requested an action cannot decide it (request %)',
            NEW.approval_request_id
            USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS approval_decision_no_self_approval ON approval_decision;
CREATE TRIGGER approval_decision_no_self_approval
    BEFORE INSERT ON approval_decision
    FOR EACH ROW EXECUTE FUNCTION approval_decision_rejects_self_approval();

-- A rejection without a stated reason is not a decision anyone can act on later.
ALTER TABLE approval_decision
    DROP CONSTRAINT IF EXISTS approval_decision_rejection_requires_rationale;

ALTER TABLE approval_decision
    ADD CONSTRAINT approval_decision_rejection_requires_rationale
    CHECK (decision <> 'Reject' OR (rationale IS NOT NULL AND length(btrim(rationale)) > 0));

-- -----------------------------------------------------------------------------
-- 8. Vector index
-- -----------------------------------------------------------------------------
-- HNSW rather than IVFFlat: it needs no training pass, so a workspace that has
-- just ingested its first documents gets usable recall immediately instead of
-- after a rebuild.

CREATE INDEX IF NOT EXISTS ix_knowledge_chunk_embedding_hnsw
    ON knowledge_chunk USING hnsw (embedding vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);

-- -----------------------------------------------------------------------------
-- 9. Monetary sanity constraints
-- -----------------------------------------------------------------------------

ALTER TABLE agent_run DROP CONSTRAINT IF EXISTS agent_run_cost_non_negative;
ALTER TABLE agent_run ADD CONSTRAINT agent_run_cost_non_negative CHECK (cost_amount >= 0);

ALTER TABLE workspace DROP CONSTRAINT IF EXISTS workspace_budget_non_negative;
ALTER TABLE workspace ADD CONSTRAINT workspace_budget_non_negative CHECK (budget_cap_amount >= 0);

ALTER TABLE workflow_run DROP CONSTRAINT IF EXISTS workflow_run_cost_non_negative;
ALTER TABLE workflow_run ADD CONSTRAINT workflow_run_cost_non_negative CHECK (actual_cost_amount >= 0);

-- Audit sequences start at 1 and are gapless per tenant; the unique index on
-- (tenant_id, sequence) already exists, and this makes a nonsensical value fail
-- at write time rather than at verification time.
ALTER TABLE audit_event DROP CONSTRAINT IF EXISTS audit_event_sequence_positive;
ALTER TABLE audit_event ADD CONSTRAINT audit_event_sequence_positive CHECK (sequence >= 1);
