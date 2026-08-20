"""Resolving a verified token into an authorised principal.

The token's `org_id` claim becomes the row-level-security scope. An actor that does
not belong to that organisation is therefore not merely unauthorised — under RLS it
does not exist, and resolution returns nothing.
"""

from __future__ import annotations

import uuid
from dataclasses import dataclass
from datetime import UTC, datetime

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from pciai.domain.identity.models import (
    Actor,
    ActorRole,
    ActorType,
    RolePermission,
)
from pciai.domain.identity.models import (
    Session as SessionRow,
)
from pciai.security.permissions import AGENT_FORBIDDEN
from pciai.security.tokens import TokenClaims


@dataclass(frozen=True)
class Principal:
    actor_id: uuid.UUID
    org_id: uuid.UUID
    actor_type: ActorType
    display_name: str
    permissions: frozenset[str]
    task_id: uuid.UUID | None = None
    agent_version_id: uuid.UUID | None = None

    @property
    def is_human(self) -> bool:
        return self.actor_type is ActorType.human

    def has(self, permission: str) -> bool:
        return permission in self.permissions


class PrincipalError(Exception):
    """The credential verified but does not resolve to an active, authorised actor."""


async def resolve_principal(session: AsyncSession, claims: TokenClaims) -> Principal:
    actor = (
        await session.execute(select(Actor).where(Actor.id == claims.actor_id))
    ).scalar_one_or_none()
    if actor is None:
        # Either no such actor, or the actor belongs to another organisation and RLS
        # filtered it out. The two are indistinguishable on purpose.
        raise PrincipalError("actor not found in this organisation")
    if not actor.active:
        raise PrincipalError("actor is not active")

    if claims.session_id is not None:
        row = (
            await session.execute(select(SessionRow).where(SessionRow.id == claims.session_id))
        ).scalar_one_or_none()
        if row is None:
            raise PrincipalError("session not found")
        if row.revoked_at is not None:
            raise PrincipalError("session revoked")
        if row.expires_at <= datetime.now(UTC):
            raise PrincipalError("session expired")

    permissions = set(
        (
            await session.execute(
                select(RolePermission.permission_code)
                .join(ActorRole, ActorRole.role_id == RolePermission.role_id)
                .where(ActorRole.actor_id == actor.id)
            )
        )
        .scalars()
        .all()
    )

    if actor.actor_type is ActorType.agent:
        # Belt and braces. An agent should never be granted these by role, and a
        # misconfiguration must not become a capability.
        permissions -= AGENT_FORBIDDEN

    return Principal(
        actor_id=actor.id,
        org_id=actor.org_id,
        actor_type=actor.actor_type,
        display_name=actor.display_name,
        permissions=frozenset(permissions),
        task_id=claims.task_id,
        agent_version_id=claims.agent_version_id,
    )
