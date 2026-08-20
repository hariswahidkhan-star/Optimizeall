"""Identity: organisations, actors, roles, permissions, sessions.

`Actor` is the union of humans, agents and service accounts. Phase 4 made this one
table deliberately: it is why "agent work credited to a human" is impossible rather
than merely discouraged, and why every activity row can carry a foreign key instead
of a free-text name.
"""

from __future__ import annotations

import enum
import uuid
from datetime import datetime

from sqlalchemy import (
    Boolean,
    CheckConstraint,
    DateTime,
    Enum,
    ForeignKey,
    Integer,
    SmallInteger,
    String,
    Text,
    UniqueConstraint,
)
from sqlalchemy.dialects.postgresql import UUID as PgUUID
from sqlalchemy.orm import Mapped, mapped_column

from pciai.db.base import Base, OrgScopedMixin, TimestampMixin, VersionedMixin
from pciai.ids import uuid7

SCHEMA = "identity"


class ActorType(enum.StrEnum):
    human = "human"
    agent = "agent"
    service = "service"


actor_type_enum = Enum(
    ActorType,
    name="actor_type",
    schema=SCHEMA,
    values_callable=lambda e: [m.value for m in e],
)


class Organization(TimestampMixin, VersionedMixin, Base):
    __tablename__ = "organization"
    __table_args__ = (
        CheckConstraint("week_start_day BETWEEN 1 AND 7", name="week_start_range"),
        CheckConstraint("working_days BETWEEN 1 AND 7", name="working_days_range"),
        {"schema": SCHEMA},
    )

    id: Mapped[uuid.UUID] = mapped_column(PgUUID(as_uuid=True), primary_key=True, default=uuid7)
    name: Mapped[str] = mapped_column(Text, nullable=False)
    slug: Mapped[str] = mapped_column(String(64), nullable=False, unique=True)
    timezone: Mapped[str] = mapped_column(Text, nullable=False, default="Europe/London")
    week_start_day: Mapped[int] = mapped_column(SmallInteger, nullable=False, default=2)
    working_days: Mapped[int] = mapped_column(SmallInteger, nullable=False, default=5)
    programme_start: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    # Reserved now, unused today: the day a data boundary is required this becomes
    # storage routing rather than a migration on populated tables.
    residency_region: Mapped[str] = mapped_column(Text, nullable=False, default="eu-west")


class Actor(TimestampMixin, OrgScopedMixin, Base):
    __tablename__ = "actor"
    __table_args__ = ({"schema": SCHEMA},)

    id: Mapped[uuid.UUID] = mapped_column(PgUUID(as_uuid=True), primary_key=True, default=uuid7)
    actor_type: Mapped[ActorType] = mapped_column(actor_type_enum, nullable=False)
    display_name: Mapped[str] = mapped_column(Text, nullable=False)
    active: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)


class UserAccount(TimestampMixin, Base):
    __tablename__ = "user_account"
    __table_args__ = (
        UniqueConstraint("email", name="email"),
        UniqueConstraint("external_subject", name="external_subject"),
        {"schema": SCHEMA},
    )

    actor_id: Mapped[uuid.UUID] = mapped_column(
        PgUUID(as_uuid=True), ForeignKey(f"{SCHEMA}.actor.id"), primary_key=True
    )
    email: Mapped[str] = mapped_column(Text, nullable=False)
    external_subject: Mapped[str] = mapped_column(Text, nullable=False)
    mfa_enrolled: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    status: Mapped[str] = mapped_column(String(32), nullable=False, default="active")
    last_login_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))


class AgentPrincipal(TimestampMixin, Base):
    """An agent's identity.

    Slice 1 carries `agent_code` as text; the foreign key to `registry.agent_version`
    lands with the agent registry in slice 3. Forward-compatible, and flagged as a
    known simplification rather than left implicit.
    """

    __tablename__ = "agent_principal"
    __table_args__ = ({"schema": SCHEMA},)

    actor_id: Mapped[uuid.UUID] = mapped_column(
        PgUUID(as_uuid=True), ForeignKey(f"{SCHEMA}.actor.id"), primary_key=True
    )
    agent_code: Mapped[str] = mapped_column(String(32), nullable=False)


class Permission(Base):
    """Global, not organisation-scoped: the vocabulary is the same for every tenant."""

    __tablename__ = "permission"
    __table_args__ = ({"schema": SCHEMA},)

    code: Mapped[str] = mapped_column(String(64), primary_key=True)
    category: Mapped[str] = mapped_column(String(32), nullable=False)
    description: Mapped[str] = mapped_column(Text, nullable=False)
    is_write: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)


class Role(TimestampMixin, OrgScopedMixin, Base):
    __tablename__ = "role"
    __table_args__ = (
        UniqueConstraint("org_id", "code", name="org_id"),
        {"schema": SCHEMA},
    )

    id: Mapped[uuid.UUID] = mapped_column(PgUUID(as_uuid=True), primary_key=True, default=uuid7)
    code: Mapped[str] = mapped_column(String(32), nullable=False)
    name: Mapped[str] = mapped_column(Text, nullable=False)
    is_system: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)


class RolePermission(OrgScopedMixin, Base):
    __tablename__ = "role_permission"
    __table_args__ = ({"schema": SCHEMA},)

    role_id: Mapped[uuid.UUID] = mapped_column(
        PgUUID(as_uuid=True), ForeignKey(f"{SCHEMA}.role.id", ondelete="CASCADE"), primary_key=True
    )
    permission_code: Mapped[str] = mapped_column(
        String(64), ForeignKey(f"{SCHEMA}.permission.code"), primary_key=True
    )


class ActorRole(OrgScopedMixin, Base):
    __tablename__ = "actor_role"
    __table_args__ = ({"schema": SCHEMA},)

    actor_id: Mapped[uuid.UUID] = mapped_column(
        PgUUID(as_uuid=True), ForeignKey(f"{SCHEMA}.actor.id", ondelete="CASCADE"), primary_key=True
    )
    role_id: Mapped[uuid.UUID] = mapped_column(
        PgUUID(as_uuid=True), ForeignKey(f"{SCHEMA}.role.id"), primary_key=True
    )
    granted_by: Mapped[uuid.UUID] = mapped_column(
        PgUUID(as_uuid=True), ForeignKey(f"{SCHEMA}.actor.id"), nullable=False
    )
    granted_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), nullable=False, default=datetime.now
    )


class Session(OrgScopedMixin, Base):
    """Sessions are rows so revocation is immediate and enumerable — the joiner/leaver
    control the workbook's Accounts Register already cares about."""

    __tablename__ = "session"
    __table_args__ = ({"schema": SCHEMA},)

    id: Mapped[uuid.UUID] = mapped_column(PgUUID(as_uuid=True), primary_key=True, default=uuid7)
    actor_id: Mapped[uuid.UUID] = mapped_column(
        PgUUID(as_uuid=True), ForeignKey(f"{SCHEMA}.actor.id"), nullable=False
    )
    issued_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    expires_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    revoked_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    ip: Mapped[str | None] = mapped_column(String(64))
    user_agent: Mapped[str | None] = mapped_column(Text)
    token_version: Mapped[int] = mapped_column(Integer, nullable=False, default=1)
