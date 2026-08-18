-- Reverses 002_security_hardening.
--
-- Rolling this back removes tenant isolation at the database layer, so it is
-- intended for a development reset, never for a deployed environment. The roles
-- are left in place: dropping a role that other objects depend on fails, and a
-- rollback that fails halfway is worse than one that leaves an unused role.

DROP TRIGGER IF EXISTS audit_event_no_mutation ON audit_event;
DROP FUNCTION IF EXISTS audit_event_is_append_only();

DROP TRIGGER IF EXISTS agent_definition_immutable ON agent_definition;
DROP FUNCTION IF EXISTS agent_definition_published_is_immutable();

DROP TRIGGER IF EXISTS agent_tool_grant_frozen ON agent_tool_grant;
DROP FUNCTION IF EXISTS agent_tool_grant_is_frozen_when_published();

DROP TRIGGER IF EXISTS task_dependency_acyclic ON task_dependency;
DROP FUNCTION IF EXISTS task_dependency_rejects_cycles();

DROP TRIGGER IF EXISTS approval_decision_no_self_approval ON approval_decision;
DROP FUNCTION IF EXISTS approval_decision_rejects_self_approval();

ALTER TABLE approval_decision DROP CONSTRAINT IF EXISTS approval_decision_rejection_requires_rationale;
ALTER TABLE agent_run DROP CONSTRAINT IF EXISTS agent_run_cost_non_negative;
ALTER TABLE workspace DROP CONSTRAINT IF EXISTS workspace_budget_non_negative;
ALTER TABLE workflow_run DROP CONSTRAINT IF EXISTS workflow_run_cost_non_negative;
ALTER TABLE audit_event DROP CONSTRAINT IF EXISTS audit_event_sequence_positive;

DROP INDEX IF EXISTS ix_knowledge_chunk_embedding_hnsw;

DO $$
DECLARE
    target text;
    all_tables text[] := ARRAY[
        'workspace', 'app_user', 'role', 'role_assignment',
        'agent_definition', 'agent_run', 'agent_memory_entry',
        'workflow_run', 'approval_request', 'approval_policy',
        'audit_event', 'knowledge_document', 'knowledge_chunk',
        'schedule_definition', 'schedule_occurrence',
        'notification', 'outbox_message', 'kpi_snapshot',
        'agent_tool_grant', 'agent_kpi', 'agent_run_step', 'tool_invocation',
        'approval_decision', 'approval_rule', 'work_task', 'task_dependency',
        'notification_delivery'
    ];
BEGIN
    FOREACH target IN ARRAY all_tables
    LOOP
        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I', target);
        EXECUTE format('ALTER TABLE %I NO FORCE ROW LEVEL SECURITY', target);
        EXECUTE format('ALTER TABLE %I DISABLE ROW LEVEL SECURITY', target);
    END LOOP;
END
$$;
