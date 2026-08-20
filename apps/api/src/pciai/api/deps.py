"""Request dependencies: the transaction, the principal, and the permission check."""

from __future__ import annotations

from collections.abc import AsyncIterator, Callable, Coroutine
from typing import Annotated, Any

from fastapi import Depends, Request
from sqlalchemy.ext.asyncio import AsyncSession

from pciai.api.errors import PermissionDenied, Unauthenticated
from pciai.db.session import get_sessionmaker, set_actor_scope, set_org_scope
from pciai.security.principal import Principal, PrincipalError, resolve_principal
from pciai.security.tokens import TokenClaims, TokenError, verify_token


def _bearer(request: Request) -> str:
    header = request.headers.get("Authorization", "")
    scheme, _, token = header.partition(" ")
    if scheme.lower() != "bearer" or not token:
        raise Unauthenticated()
    return token


async def get_claims(request: Request) -> TokenClaims:
    try:
        return verify_token(_bearer(request))
    except TokenError as exc:
        raise Unauthenticated(str(exc)) from exc


async def get_session(
    claims: Annotated[TokenClaims, Depends(get_claims)],
) -> AsyncIterator[AsyncSession]:
    """A transaction already bound to the organisation named in the credential.

    The scope comes from the verified token, never from a request body.
    """
    async with get_sessionmaker()() as session:
        await session.begin()
        await set_org_scope(session, claims.org_id)
        await set_actor_scope(session, claims.actor_id)
        try:
            yield session
            await session.commit()
        except Exception:
            await session.rollback()
            raise


async def get_principal(
    claims: Annotated[TokenClaims, Depends(get_claims)],
    session: Annotated[AsyncSession, Depends(get_session)],
) -> Principal:
    try:
        return await resolve_principal(session, claims)
    except PrincipalError as exc:
        raise Unauthenticated(str(exc)) from exc


CurrentPrincipal = Annotated[Principal, Depends(get_principal)]
DbSession = Annotated[AsyncSession, Depends(get_session)]


def requires(
    permission: str,
) -> Callable[[Principal], Coroutine[Any, Any, Principal]]:
    """Enforce a permission. Absence is a 403 that names what is missing."""

    async def _check(principal: CurrentPrincipal) -> Principal:
        if not principal.has(permission):
            raise PermissionDenied(permission)
        return principal

    return _check
