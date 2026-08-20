"""Database sessions, and the mechanism that makes tenant isolation structural.

Every request runs inside a transaction that has had `app.org_id` set from the
authenticated principal's claims — never from a request body. A query that forgets
its organisation predicate therefore returns nothing rather than another
organisation's data.
"""

from __future__ import annotations

import uuid
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from sqlalchemy import text
from sqlalchemy.ext.asyncio import (
    AsyncEngine,
    AsyncSession,
    async_sessionmaker,
    create_async_engine,
)

from pciai.settings import get_settings

_engine: AsyncEngine | None = None
_sessionmaker: async_sessionmaker[AsyncSession] | None = None


def get_engine() -> AsyncEngine:
    global _engine
    if _engine is None:
        settings = get_settings()
        _engine = create_async_engine(
            settings.database_url,
            echo=settings.sql_echo,
            pool_pre_ping=True,
        )
    return _engine


def get_sessionmaker() -> async_sessionmaker[AsyncSession]:
    global _sessionmaker
    if _sessionmaker is None:
        _sessionmaker = async_sessionmaker(get_engine(), expire_on_commit=False)
    return _sessionmaker


async def dispose_engine() -> None:
    global _engine, _sessionmaker
    if _engine is not None:
        await _engine.dispose()
    _engine = None
    _sessionmaker = None


async def set_org_scope(session: AsyncSession, org_id: uuid.UUID | None) -> None:
    """Bind the transaction to one organisation for row-level security.

    `SET LOCAL` scopes to the transaction, so a pooled connection cannot leak the
    setting into the next request.
    """
    value = str(org_id) if org_id is not None else ""
    await session.execute(text("SELECT set_config('app.org_id', :v, true)"), {"v": value})


async def set_actor_scope(session: AsyncSession, actor_id: uuid.UUID | None) -> None:
    value = str(actor_id) if actor_id is not None else ""
    await session.execute(text("SELECT set_config('app.actor_id', :v, true)"), {"v": value})


@asynccontextmanager
async def scoped_session(
    org_id: uuid.UUID | None = None, actor_id: uuid.UUID | None = None
) -> AsyncIterator[AsyncSession]:
    """A transaction already bound to an organisation and actor."""
    async with get_sessionmaker()() as session:
        await session.begin()
        await set_org_scope(session, org_id)
        await set_actor_scope(session, actor_id)
        try:
            yield session
            await session.commit()
        except Exception:
            await session.rollback()
            raise
