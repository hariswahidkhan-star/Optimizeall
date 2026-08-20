from __future__ import annotations

from typing import Annotated

from fastapi import APIRouter, Depends
from sqlalchemy import select

from pciai.api.deps import DbSession, requires
from pciai.api.schemas import ActorSummary
from pciai.domain.identity.models import Actor, ActorRole, Role
from pciai.security.principal import Principal

router = APIRouter(prefix="/api/v1", tags=["identity"])


@router.get("/actors", response_model=list[ActorSummary])
async def list_actors(
    session: DbSession,
    _: Annotated[Principal, Depends(requires("actor.read"))],
) -> list[ActorSummary]:
    """Humans and agents in one list, each labelled by type.

    Agent work is attributed to the agent. The workbook's scorecards are built on
    who did what, and mixing the two populations would corrupt them.
    """
    actors = list((await session.execute(select(Actor).order_by(Actor.display_name))).scalars())
    role_rows = await session.execute(
        select(ActorRole.actor_id, Role.code).join(Role, Role.id == ActorRole.role_id)
    )
    roles: dict[str, list[str]] = {}
    for actor_id, code in role_rows.all():
        roles.setdefault(str(actor_id), []).append(code)
    return [
        ActorSummary(
            id=a.id,
            actor_type=a.actor_type.value,
            display_name=a.display_name,
            active=a.active,
            roles=sorted(roles.get(str(a.id), [])),
        )
        for a in actors
    ]
