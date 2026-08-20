"""Configuration: settings, brands, objectives.

Phase 4 rule — business enumerations are lookup tables, not native enums, so a value
carries the attributes the workbook keeps beside it and referential integrity makes
an unreportable value unselectable.
"""

from __future__ import annotations

import uuid
from typing import Any

from sqlalchemy import Boolean, CheckConstraint, Integer, String, Text, UniqueConstraint
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.dialects.postgresql import UUID as PgUUID
from sqlalchemy.orm import Mapped, mapped_column

from pciai.db.base import Base, OrgScopedMixin, TimestampMixin, VersionedMixin
from pciai.ids import uuid7

SCHEMA = "config"


class OrgSetting(TimestampMixin, OrgScopedMixin, VersionedMixin, Base):
    __tablename__ = "org_setting"
    __table_args__ = (
        UniqueConstraint("org_id", "key", name="org_id"),
        {"schema": SCHEMA},
    )

    id: Mapped[uuid.UUID] = mapped_column(PgUUID(as_uuid=True), primary_key=True, default=uuid7)
    key: Mapped[str] = mapped_column(String(64), nullable=False)
    value: Mapped[dict[str, Any]] = mapped_column(JSONB, nullable=False)
    updated_by: Mapped[uuid.UUID | None] = mapped_column(PgUUID(as_uuid=True))


class Brand(TimestampMixin, OrgScopedMixin, Base):
    __tablename__ = "brand"
    __table_args__ = (
        UniqueConstraint("org_id", "code", name="org_id"),
        {"schema": SCHEMA},
    )

    id: Mapped[uuid.UUID] = mapped_column(PgUUID(as_uuid=True), primary_key=True, default=uuid7)
    code: Mapped[str] = mapped_column(String(32), nullable=False)
    name: Mapped[str] = mapped_column(Text, nullable=False)
    is_default: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)


class Objective(TimestampMixin, OrgScopedMixin, Base):
    """The workbook's eleven value-ranked campaigns. The rank is the orchestrator's
    objective function, so it is configuration a manager edits — not a constant."""

    __tablename__ = "objective"
    __table_args__ = (
        UniqueConstraint("org_id", "code", name="org_id"),
        CheckConstraint("value_rank >= 1", name="value_rank_positive"),
        {"schema": SCHEMA},
    )

    id: Mapped[uuid.UUID] = mapped_column(PgUUID(as_uuid=True), primary_key=True, default=uuid7)
    code: Mapped[str] = mapped_column(String(48), nullable=False)
    name: Mapped[str] = mapped_column(Text, nullable=False)
    value_rank: Mapped[int] = mapped_column(Integer, nullable=False)
    rationale: Mapped[str | None] = mapped_column(Text)
    active: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)
