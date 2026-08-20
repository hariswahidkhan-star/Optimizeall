"""identity and config schemas, with row-level security

Revision ID: 0001
Revises:
Create Date: 2026-08-18

Slice 1 of the build: organisation, actors, roles, permissions, sessions and the
configuration a manager edits. Row-level security is switched on here rather than
later, because retro-fitting tenant isolation onto populated tables is the migration
nobody wants to run.
"""

from __future__ import annotations

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision = "0001"
down_revision = None
branch_labels = None
depends_on = None

ORG_SCOPED = [
    ("identity", "actor", "org_id"),
    ("identity", "role", "org_id"),
    ("identity", "role_permission", "org_id"),
    ("identity", "actor_role", "org_id"),
    ("identity", "session", "org_id"),
    ("config", "org_setting", "org_id"),
    ("config", "brand", "org_id"),
    ("config", "objective", "org_id"),
]

APP_ROLE = "pciai_app"


def upgrade() -> None:
    op.execute("CREATE SCHEMA IF NOT EXISTS identity")
    op.execute("CREATE SCHEMA IF NOT EXISTS config")

    actor_type = postgresql.ENUM(
        "human", "agent", "service", name="actor_type", schema="identity", create_type=False
    )
    actor_type.create(op.get_bind(), checkfirst=True)

    op.create_table(
        "organization",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("name", sa.Text(), nullable=False),
        sa.Column("slug", sa.String(64), nullable=False, unique=True),
        sa.Column("timezone", sa.Text(), nullable=False, server_default="Europe/London"),
        sa.Column("week_start_day", sa.SmallInteger(), nullable=False, server_default="2"),
        sa.Column("working_days", sa.SmallInteger(), nullable=False, server_default="5"),
        sa.Column("programme_start", sa.DateTime(timezone=True), nullable=False),
        sa.Column("residency_region", sa.Text(), nullable=False, server_default="eu-west"),
        sa.Column("row_version", sa.Integer(), nullable=False, server_default="1"),
        sa.Column(
            "created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.Column(
            "updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.CheckConstraint(
            "week_start_day BETWEEN 1 AND 7", name="ck_organization_week_start_range"
        ),
        sa.CheckConstraint(
            "working_days BETWEEN 1 AND 7", name="ck_organization_working_days_range"
        ),
        schema="identity",
    )

    op.create_table(
        "actor",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("org_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("actor_type", actor_type, nullable=False),
        sa.Column("display_name", sa.Text(), nullable=False),
        sa.Column("active", sa.Boolean(), nullable=False, server_default=sa.true()),
        sa.Column(
            "created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.Column(
            "updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        schema="identity",
    )
    op.create_index("ix_actor_org_id", "actor", ["org_id"], schema="identity")

    op.create_table(
        "user_account",
        sa.Column(
            "actor_id",
            postgresql.UUID(as_uuid=True),
            sa.ForeignKey("identity.actor.id"),
            primary_key=True,
        ),
        sa.Column("email", sa.Text(), nullable=False, unique=True),
        sa.Column("external_subject", sa.Text(), nullable=False, unique=True),
        sa.Column("mfa_enrolled", sa.Boolean(), nullable=False, server_default=sa.false()),
        sa.Column("status", sa.String(32), nullable=False, server_default="active"),
        sa.Column("last_login_at", sa.DateTime(timezone=True)),
        sa.Column(
            "created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.Column(
            "updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        schema="identity",
    )

    op.create_table(
        "agent_principal",
        sa.Column(
            "actor_id",
            postgresql.UUID(as_uuid=True),
            sa.ForeignKey("identity.actor.id"),
            primary_key=True,
        ),
        sa.Column("agent_code", sa.String(32), nullable=False),
        sa.Column(
            "created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.Column(
            "updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        schema="identity",
    )

    op.create_table(
        "permission",
        sa.Column("code", sa.String(64), primary_key=True),
        sa.Column("category", sa.String(32), nullable=False),
        sa.Column("description", sa.Text(), nullable=False),
        sa.Column("is_write", sa.Boolean(), nullable=False, server_default=sa.false()),
        schema="identity",
    )

    op.create_table(
        "role",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("org_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("code", sa.String(32), nullable=False),
        sa.Column("name", sa.Text(), nullable=False),
        sa.Column("is_system", sa.Boolean(), nullable=False, server_default=sa.false()),
        sa.Column(
            "created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.Column(
            "updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.UniqueConstraint("org_id", "code", name="uq_role_org_id"),
        schema="identity",
    )
    op.create_index("ix_role_org_id", "role", ["org_id"], schema="identity")

    op.create_table(
        "role_permission",
        sa.Column(
            "role_id",
            postgresql.UUID(as_uuid=True),
            sa.ForeignKey("identity.role.id", ondelete="CASCADE"),
            primary_key=True,
        ),
        sa.Column(
            "permission_code",
            sa.String(64),
            sa.ForeignKey("identity.permission.code"),
            primary_key=True,
        ),
        sa.Column("org_id", postgresql.UUID(as_uuid=True), nullable=False),
        schema="identity",
    )
    op.create_index("ix_role_permission_org_id", "role_permission", ["org_id"], schema="identity")

    op.create_table(
        "actor_role",
        sa.Column(
            "actor_id",
            postgresql.UUID(as_uuid=True),
            sa.ForeignKey("identity.actor.id", ondelete="CASCADE"),
            primary_key=True,
        ),
        sa.Column(
            "role_id",
            postgresql.UUID(as_uuid=True),
            sa.ForeignKey("identity.role.id"),
            primary_key=True,
        ),
        sa.Column("org_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column(
            "granted_by",
            postgresql.UUID(as_uuid=True),
            sa.ForeignKey("identity.actor.id"),
            nullable=False,
        ),
        sa.Column(
            "granted_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        schema="identity",
    )
    op.create_index("ix_actor_role_org_id", "actor_role", ["org_id"], schema="identity")

    op.create_table(
        "session",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("org_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column(
            "actor_id",
            postgresql.UUID(as_uuid=True),
            sa.ForeignKey("identity.actor.id"),
            nullable=False,
        ),
        sa.Column("issued_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("expires_at", sa.DateTime(timezone=True), nullable=False),
        sa.Column("revoked_at", sa.DateTime(timezone=True)),
        sa.Column("ip", sa.String(64)),
        sa.Column("user_agent", sa.Text()),
        sa.Column("token_version", sa.Integer(), nullable=False, server_default="1"),
        schema="identity",
    )
    op.create_index("ix_session_org_id", "session", ["org_id"], schema="identity")

    op.create_table(
        "org_setting",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("org_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("key", sa.String(64), nullable=False),
        sa.Column("value", postgresql.JSONB(), nullable=False),
        sa.Column("updated_by", postgresql.UUID(as_uuid=True)),
        sa.Column("row_version", sa.Integer(), nullable=False, server_default="1"),
        sa.Column(
            "created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.Column(
            "updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.UniqueConstraint("org_id", "key", name="uq_org_setting_org_id"),
        schema="config",
    )
    op.create_index("ix_org_setting_org_id", "org_setting", ["org_id"], schema="config")

    op.create_table(
        "brand",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("org_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("code", sa.String(32), nullable=False),
        sa.Column("name", sa.Text(), nullable=False),
        sa.Column("is_default", sa.Boolean(), nullable=False, server_default=sa.false()),
        sa.Column(
            "created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.Column(
            "updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.UniqueConstraint("org_id", "code", name="uq_brand_org_id"),
        schema="config",
    )
    op.create_index("ix_brand_org_id", "brand", ["org_id"], schema="config")

    op.create_table(
        "objective",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("org_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("code", sa.String(48), nullable=False),
        sa.Column("name", sa.Text(), nullable=False),
        sa.Column("value_rank", sa.Integer(), nullable=False),
        sa.Column("rationale", sa.Text()),
        sa.Column("active", sa.Boolean(), nullable=False, server_default=sa.true()),
        sa.Column(
            "created_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.Column(
            "updated_at", sa.DateTime(timezone=True), nullable=False, server_default=sa.func.now()
        ),
        sa.UniqueConstraint("org_id", "code", name="uq_objective_org_id"),
        sa.CheckConstraint("value_rank >= 1", name="ck_objective_value_rank_positive"),
        schema="config",
    )
    op.create_index("ix_objective_org_id", "objective", ["org_id"], schema="config")

    # ---- Row-level security -------------------------------------------------
    # NULLIF guards the unset case: an unbound session sees nothing rather than
    # raising a cast error, so a missing scope fails closed.
    op.execute("ALTER TABLE identity.organization ENABLE ROW LEVEL SECURITY")
    op.execute("ALTER TABLE identity.organization FORCE ROW LEVEL SECURITY")
    op.execute(
        """
        CREATE POLICY org_isolation ON identity.organization
          USING      (id = NULLIF(current_setting('app.org_id', true), '')::uuid)
          WITH CHECK (id = NULLIF(current_setting('app.org_id', true), '')::uuid)
        """
    )

    for schema, table, column in ORG_SCOPED:
        op.execute(f"ALTER TABLE {schema}.{table} ENABLE ROW LEVEL SECURITY")
        op.execute(f"ALTER TABLE {schema}.{table} FORCE ROW LEVEL SECURITY")
        op.execute(
            f"""
            CREATE POLICY org_isolation ON {schema}.{table}
              USING      ({column} = NULLIF(current_setting('app.org_id', true), '')::uuid)
              WITH CHECK ({column} = NULLIF(current_setting('app.org_id', true), '')::uuid)
            """
        )

    # ---- Grants -------------------------------------------------------------
    # The application role owns nothing, so row-level security applies to it.
    for schema in ("identity", "config"):
        op.execute(f"GRANT USAGE ON SCHEMA {schema} TO {APP_ROLE}")
        op.execute(
            f"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {schema} TO {APP_ROLE}"
        )
        op.execute(
            f"ALTER DEFAULT PRIVILEGES IN SCHEMA {schema} "
            f"GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {APP_ROLE}"
        )


def downgrade() -> None:
    for schema, table, _ in ORG_SCOPED:
        op.execute(f"DROP POLICY IF EXISTS org_isolation ON {schema}.{table}")
    op.execute("DROP POLICY IF EXISTS org_isolation ON identity.organization")
    for table in ("objective", "brand", "org_setting"):
        op.drop_table(table, schema="config")
    for table in (
        "session",
        "actor_role",
        "role_permission",
        "role",
        "permission",
        "agent_principal",
        "user_account",
        "actor",
        "organization",
    ):
        op.drop_table(table, schema="identity")
    op.execute("DROP TYPE IF EXISTS identity.actor_type")
    op.execute("DROP SCHEMA IF EXISTS config CASCADE")
    op.execute("DROP SCHEMA IF EXISTS identity CASCADE")
