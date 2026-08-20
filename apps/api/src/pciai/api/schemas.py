"""Response contracts. Every field is explicit; nothing is serialised by accident."""

from __future__ import annotations

import uuid
from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field


class Health(BaseModel):
    status: str
    environment: str
    version: str
    database: str


class MeResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    actor_id: uuid.UUID
    org_id: uuid.UUID
    actor_type: str
    display_name: str
    permissions: list[str]
    task_id: uuid.UUID | None = None


class OrganizationResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    name: str
    slug: str
    timezone: str
    week_start_day: int
    working_days: int
    programme_start: datetime
    residency_region: str
    row_version: int


class OrganizationUpdate(BaseModel):
    name: str | None = None
    timezone: str | None = None
    week_start_day: int | None = Field(default=None, ge=1, le=7)
    working_days: int | None = Field(default=None, ge=1, le=7)


class ActorSummary(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    actor_type: str
    display_name: str
    active: bool
    roles: list[str] = []


class ObjectiveResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    code: str
    name: str
    value_rank: int
    rationale: str | None
    active: bool
