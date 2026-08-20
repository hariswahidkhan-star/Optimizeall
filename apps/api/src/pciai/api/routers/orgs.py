from __future__ import annotations

from typing import Annotated

from fastapi import APIRouter, Depends, Response
from sqlalchemy import select

from pciai.api.deps import DbSession, requires
from pciai.api.errors import NotFound
from pciai.api.schemas import ObjectiveResponse, OrganizationResponse, OrganizationUpdate
from pciai.domain.config.models import Objective
from pciai.domain.identity.models import Organization
from pciai.security.principal import Principal

router = APIRouter(prefix="/api/v1", tags=["organisation"])


@router.get("/organisation", response_model=OrganizationResponse)
async def read_organisation(
    session: DbSession,
    response: Response,
    principal: Annotated[Principal, Depends(requires("org.read"))],
) -> Organization:
    org = (
        await session.execute(select(Organization).where(Organization.id == principal.org_id))
    ).scalar_one_or_none()
    if org is None:
        raise NotFound("Organisation")
    response.headers["ETag"] = f'W/"{org.row_version}"'
    return org


@router.get("/objectives", response_model=list[ObjectiveResponse])
async def list_objectives(
    session: DbSession,
    _: Annotated[Principal, Depends(requires("org.read"))],
) -> list[Objective]:
    """The workbook's value-ranked campaigns, in rank order — the orchestrator's
    objective function, exposed as data a manager can edit."""
    rows = await session.execute(
        select(Objective).where(Objective.active).order_by(Objective.value_rank)
    )
    return list(rows.scalars().all())


@router.patch("/organisation", response_model=OrganizationResponse)
async def update_organisation(
    payload: OrganizationUpdate,
    session: DbSession,
    response: Response,
    principal: Annotated[Principal, Depends(requires("org.manage"))],
) -> Organization:
    org = (
        await session.execute(select(Organization).where(Organization.id == principal.org_id))
    ).scalar_one_or_none()
    if org is None:
        raise NotFound("Organisation")
    for field, value in payload.model_dump(exclude_none=True).items():
        setattr(org, field, value)
    org.row_version += 1
    await session.flush()
    response.headers["ETag"] = f'W/"{org.row_version}"'
    return org
