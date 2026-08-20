from __future__ import annotations

from fastapi import APIRouter

from pciai.api.deps import CurrentPrincipal
from pciai.api.schemas import MeResponse

router = APIRouter(prefix="/api/v1", tags=["identity"])


@router.get("/me", response_model=MeResponse)
async def me(principal: CurrentPrincipal) -> MeResponse:
    return MeResponse(
        actor_id=principal.actor_id,
        org_id=principal.org_id,
        actor_type=principal.actor_type.value,
        display_name=principal.display_name,
        permissions=sorted(principal.permissions),
        task_id=principal.task_id,
    )
