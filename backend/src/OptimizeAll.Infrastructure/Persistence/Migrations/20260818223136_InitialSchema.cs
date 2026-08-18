using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace OptimizeAll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "agent_definition",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    definition_version = table.Column<int>(type: "integer", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mission = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    system_prompt = table.Column<string>(type: "text", maxLength: 2147483647, nullable: false),
                    model_policy = table.Column<string>(type: "jsonb", nullable: false),
                    memory_policy = table.Column<string>(type: "jsonb", nullable: false),
                    budget_policy = table.Column<string>(type: "jsonb", nullable: false),
                    max_risk_class = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    approval_policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_definition", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_memory_entry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agent_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "text", maxLength: 2147483647, nullable: false),
                    importance = table.Column<float>(type: "real", nullable: false),
                    outcome_signal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_memory_entry", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_run",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agent_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    trigger_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    triggered_by = table.Column<Guid>(type: "uuid", nullable: true),
                    input = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false),
                    output = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: true),
                    error = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: true),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    prompt_tokens = table.Column<long>(type: "bigint", nullable: false),
                    completion_tokens = table.Column<long>(type: "bigint", nullable: false),
                    cost_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    cost_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    tool_call_count = table.Column<int>(type: "integer", nullable: false),
                    iteration_count = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_holder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    blocking_approval_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_dry_run = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_run", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "citext", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "approval_policy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_policy", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "approval_request",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agent_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tool_invocation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    risk_class = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    payload = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false),
                    payload_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    estimated_cost_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    estimated_cost_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    requested_by_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    requested_by_principal = table.Column<Guid>(type: "uuid", nullable: false),
                    required_approver_count = table.Column<int>(type: "integer", nullable: false),
                    required_role_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_request", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    before_state = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: true),
                    after_state = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    previous_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    entry_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_uri = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    content_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_document", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kpi_snapshot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kpi_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    granularity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dimensions = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kpi_snapshot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    link_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_builtin = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    permissions = table.Column<HashSet<string>>(type: "text[]", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_assignment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignment", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule_definition",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cron_expression = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false),
                    miss_policy = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    next_occurrence_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_occurrence_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_definition", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule_occurrence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dispatched_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_occurrence", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "citext", maxLength: 63, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    plan_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    suspension_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    purge_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_run",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    objective_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    objective_payload = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estimated_cost_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    estimated_cost_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    actual_cost_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    actual_cost_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_run", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "citext", maxLength: 63, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    budget_cap_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    budget_cap_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    kill_switch_engaged = table.Column<bool>(type: "boolean", nullable: false),
                    kill_switch_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    kill_switch_engaged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    kill_switch_engaged_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_kpi",
                columns: table => new
                {
                    AgentDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_kpi", x => new { x.AgentDefinitionId, x.Id });
                    table.ForeignKey(
                        name: "FK_agent_kpi_agent_definition_AgentDefinitionId",
                        column: x => x.AgentDefinitionId,
                        principalTable: "agent_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_tool_grant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tool_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    risk_class = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    constraints = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_tool_grant", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_tool_grant_agent_definition_agent_definition_id",
                        column: x => x.agent_definition_id,
                        principalTable: "agent_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_run_step",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    step_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    content = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false),
                    tokens = table.Column<long>(type: "bigint", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_run_step", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_run_step_agent_run_agent_run_id",
                        column: x => x.agent_run_id,
                        principalTable: "agent_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tool_invocation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tool_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    risk_class = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    arguments = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false),
                    result = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    denial_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    approval_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_invocation", x => x.id);
                    table.ForeignKey(
                        name: "FK_tool_invocation_agent_run_agent_run_id",
                        column: x => x.agent_run_id,
                        principalTable: "agent_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_rule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_class = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tool_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    threshold_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    threshold_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    required_role_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    required_approver_count = table.Column<int>(type: "integer", nullable: false),
                    expiry = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_rule", x => x.id);
                    table.ForeignKey(
                        name: "FK_approval_rule_approval_policy_policy_id",
                        column: x => x.policy_id,
                        principalTable: "approval_policy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_decision",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    approval_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approver_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rationale = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    step_up_verified = table.Column<bool>(type: "boolean", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_decision", x => x.id);
                    table.ForeignKey(
                        name: "FK_approval_decision_approval_request_approval_request_id",
                        column: x => x.approval_request_id,
                        principalTable: "approval_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", maxLength: 2147483647, nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunk", x => x.id);
                    table.ForeignKey(
                        name: "FK_knowledge_chunk_knowledge_document_document_id",
                        column: x => x.document_id,
                        principalTable: "knowledge_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_delivered = table.Column<bool>(type: "boolean", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_delivery", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_delivery_notification_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "notification",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_task",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    assigned_agent_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    input = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: false),
                    output = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: true),
                    agent_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    heartbeat_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    blocked_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_task", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_task_workflow_run_workflow_run_id",
                        column: x => x.workflow_run_id,
                        principalTable: "workflow_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_dependency",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    depends_on_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependency_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_dependency", x => new { x.task_id, x.Id });
                    table.ForeignKey(
                        name: "FK_task_dependency_work_task_task_id",
                        column: x => x.task_id,
                        principalTable: "work_task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_definition_workspace_id_agent_key_definition_version",
                table: "agent_definition",
                columns: new[] { "workspace_id", "agent_key", "definition_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_definition_workspace_id_agent_key_status",
                table: "agent_definition",
                columns: new[] { "workspace_id", "agent_key", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_memory_entry_workspace_id_agent_key_environment_tier",
                table: "agent_memory_entry",
                columns: new[] { "workspace_id", "agent_key", "environment", "tier" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_run_correlation_id",
                table: "agent_run",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_run_workspace_id_created_at",
                table: "agent_run",
                columns: new[] { "workspace_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_agent_run_workspace_id_status_lease_expires_at",
                table: "agent_run",
                columns: new[] { "workspace_id", "status", "lease_expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_run_step_agent_run_id_sequence",
                table: "agent_run_step",
                columns: new[] { "agent_run_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_tool_grant_agent_definition_id_tool_key",
                table: "agent_tool_grant",
                columns: new[] { "agent_definition_id", "tool_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_user_tenant_id_email",
                table: "app_user",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_user_tenant_id_external_subject",
                table: "app_user",
                columns: new[] { "tenant_id", "external_subject" },
                unique: true,
                filter: "external_subject IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_approval_decision_approval_request_id_approver_user_id",
                table: "approval_decision",
                columns: new[] { "approval_request_id", "approver_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_policy_workspace_id_key",
                table: "approval_policy",
                columns: new[] { "workspace_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_request_agent_run_id",
                table: "approval_request",
                column: "agent_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_approval_request_workspace_id_environment_status_expires_at",
                table: "approval_request",
                columns: new[] { "workspace_id", "environment", "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_rule_policy_id",
                table: "approval_rule",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_event_correlation_id",
                table: "audit_event",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_event_tenant_id_action_occurred_at",
                table: "audit_event",
                columns: new[] { "tenant_id", "action", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_event_tenant_id_occurred_at",
                table: "audit_event",
                columns: new[] { "tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_event_tenant_id_sequence",
                table: "audit_event",
                columns: new[] { "tenant_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_document_id",
                table: "knowledge_chunk",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_workspace_id_environment",
                table: "knowledge_chunk",
                columns: new[] { "workspace_id", "environment" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_document_workspace_id_environment_content_hash",
                table: "knowledge_document",
                columns: new[] { "workspace_id", "environment", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kpi_snapshot_workspace_id_environment_kpi_key_period_start_~",
                table: "kpi_snapshot",
                columns: new[] { "workspace_id", "environment", "kpi_key", "period_start", "granularity" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_recipient_user_id_read_at_created_at",
                table: "notification",
                columns: new[] { "recipient_user_id", "read_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_delivery_NotificationId",
                table: "notification_delivery",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_status_next_attempt_at",
                table: "outbox_message",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_tenant_id_message_type_idempotency_key",
                table: "outbox_message",
                columns: new[] { "tenant_id", "message_type", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_tenant_id_key",
                table: "role",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_assignment_principal_id_role_id_workspace_id_environme~",
                table: "role_assignment",
                columns: new[] { "principal_id", "role_id", "workspace_id", "environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_assignment_tenant_id_principal_id",
                table: "role_assignment",
                columns: new[] { "tenant_id", "principal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_definition_is_enabled_next_occurrence_utc",
                table: "schedule_definition",
                columns: new[] { "is_enabled", "next_occurrence_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_definition_workspace_id_environment_key",
                table: "schedule_definition",
                columns: new[] { "workspace_id", "environment", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_occurrence_schedule_id_occurrence_utc",
                table: "schedule_occurrence",
                columns: new[] { "schedule_id", "occurrence_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_dependency_task_id_depends_on_task_id",
                table: "task_dependency",
                columns: new[] { "task_id", "depends_on_task_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_slug",
                table: "tenant",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tool_invocation_agent_run_id",
                table: "tool_invocation",
                column: "agent_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_task_workflow_run_id_status",
                table: "work_task",
                columns: new[] { "workflow_run_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_run_correlation_id",
                table: "workflow_run",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_run_workspace_id_status",
                table: "workflow_run",
                columns: new[] { "workspace_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_tenant_id",
                table: "workspace",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_tenant_id_slug",
                table: "workspace",
                columns: new[] { "tenant_id", "slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_kpi");

            migrationBuilder.DropTable(
                name: "agent_memory_entry");

            migrationBuilder.DropTable(
                name: "agent_run_step");

            migrationBuilder.DropTable(
                name: "agent_tool_grant");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "approval_decision");

            migrationBuilder.DropTable(
                name: "approval_rule");

            migrationBuilder.DropTable(
                name: "audit_event");

            migrationBuilder.DropTable(
                name: "knowledge_chunk");

            migrationBuilder.DropTable(
                name: "kpi_snapshot");

            migrationBuilder.DropTable(
                name: "notification_delivery");

            migrationBuilder.DropTable(
                name: "outbox_message");

            migrationBuilder.DropTable(
                name: "role");

            migrationBuilder.DropTable(
                name: "role_assignment");

            migrationBuilder.DropTable(
                name: "schedule_definition");

            migrationBuilder.DropTable(
                name: "schedule_occurrence");

            migrationBuilder.DropTable(
                name: "task_dependency");

            migrationBuilder.DropTable(
                name: "tenant");

            migrationBuilder.DropTable(
                name: "tool_invocation");

            migrationBuilder.DropTable(
                name: "workspace");

            migrationBuilder.DropTable(
                name: "agent_definition");

            migrationBuilder.DropTable(
                name: "approval_request");

            migrationBuilder.DropTable(
                name: "approval_policy");

            migrationBuilder.DropTable(
                name: "knowledge_document");

            migrationBuilder.DropTable(
                name: "notification");

            migrationBuilder.DropTable(
                name: "work_task");

            migrationBuilder.DropTable(
                name: "agent_run");

            migrationBuilder.DropTable(
                name: "workflow_run");
        }
    }
}
