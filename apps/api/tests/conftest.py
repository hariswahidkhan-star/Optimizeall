"""Test harness.

Tests run against a real PostgreSQL instance, not a stand-in. The isolation
guarantee is a database feature — a test that mocks the database proves nothing
about it.
"""

from __future__ import annotations

import os
import uuid
from collections.abc import AsyncIterator, Iterator
from pathlib import Path

import pytest

API_ROOT = Path(__file__).resolve().parents[1]

os.environ.setdefault("PCIAI_ENVIRONMENT", "test")
os.environ.setdefault(
    "PCIAI_DATABASE_URL", "postgresql+asyncpg://pciai_app:pciai_app@127.0.0.1:5432/pciai_test"
)
os.environ.setdefault(
    "PCIAI_MIGRATION_DATABASE_URL",
    "postgresql+asyncpg://pciai_owner:pciai_owner@127.0.0.1:5432/pciai_test",
)

from alembic import command  # noqa: E402
from alembic.config import Config  # noqa: E402
from httpx import ASGITransport, AsyncClient  # noqa: E402
from sqlalchemy import text  # noqa: E402
from sqlalchemy.ext.asyncio import create_async_engine  # noqa: E402

from pciai.bootstrap import (  # noqa: E402
    create_agent,
    create_human,
    create_organization,
    seed_permissions,
)
from pciai.db.session import dispose_engine, get_sessionmaker, set_org_scope  # noqa: E402
from pciai.main import create_app  # noqa: E402
from pciai.security.tokens import issue_dev_token  # noqa: E402
from pciai.settings import get_settings  # noqa: E402

TRUNCATE = """
TRUNCATE identity.actor_role, identity.role_permission, identity.session,
         identity.agent_principal, identity.user_account, identity.role,
         identity.actor, identity.organization,
         config.objective, config.brand, config.org_setting
RESTART IDENTITY CASCADE;
"""


@pytest.fixture(scope="session", autouse=True)
def migrated() -> Iterator[None]:
    cfg = Config(str(API_ROOT / "alembic.ini"))
    cfg.set_main_option("script_location", str(API_ROOT / "alembic"))
    command.upgrade(cfg, "head")
    yield


@pytest.fixture(autouse=True)
async def clean_database(migrated: None) -> AsyncIterator[None]:
    owner = create_async_engine(get_settings().migration_database_url)
    async with owner.begin() as conn:
        await conn.execute(text(TRUNCATE))
    await owner.dispose()
    yield
    await dispose_engine()


class Fixture:
    """One organisation, its roles, and a token factory for its actors."""

    def __init__(self, org_id: uuid.UUID) -> None:
        self.org_id = org_id
        self.actors: dict[str, uuid.UUID] = {}

    def token(self, who: str, **claims: object) -> str:
        return issue_dev_token(
            actor_id=self.actors[who],
            org_id=self.org_id,
            subject=f"dev|{who}",
            **claims,  # type: ignore[arg-type]
        )

    def auth(self, who: str, **claims: object) -> dict[str, str]:
        return {"Authorization": f"Bearer {self.token(who, **claims)}"}


async def _build_org(name: str, slug: str, *, with_agent: bool = False) -> Fixture:
    async with get_sessionmaker()() as session, session.begin():
        await seed_permissions(session)
        org = await create_organization(session, name=name, slug=slug)
        fixture = Fixture(org.id)
        owner = await create_human(
            session,
            org_id=org.id,
            display_name=f"{slug} owner",
            email=f"owner@{slug}.test",
            external_subject=f"dev|{slug}|owner",
            role_codes=("Owner",),
        )
        fixture.actors["owner"] = owner.id
        for role in ("Admin", "Manager", "Operator", "Analyst", "Compliance"):
            actor = await create_human(
                session,
                org_id=org.id,
                display_name=f"{slug} {role.lower()}",
                email=f"{role.lower()}@{slug}.test",
                external_subject=f"dev|{slug}|{role.lower()}",
                role_codes=(role,),
                granted_by=owner.id,
            )
            fixture.actors[role.lower()] = actor.id
        if with_agent:
            agent = await create_agent(
                session,
                org_id=org.id,
                agent_code="CWRITE",
                display_name="Content Writer",
                granted_by=owner.id,
            )
            fixture.actors["agent"] = agent.id
    return fixture


@pytest.fixture
async def org_a() -> Fixture:
    return await _build_org("Org A", "org-a", with_agent=True)


@pytest.fixture
async def org_b() -> Fixture:
    return await _build_org("Org B", "org-b")


@pytest.fixture
async def client() -> AsyncIterator[AsyncClient]:
    app = create_app()
    transport = ASGITransport(app=app)
    async with AsyncClient(transport=transport, base_url="http://test") as c:
        yield c


@pytest.fixture
async def scoped():
    """Open a transaction bound to an organisation, as the application role."""
    from contextlib import asynccontextmanager

    @asynccontextmanager
    async def _open(org_id: uuid.UUID | None):
        async with get_sessionmaker()() as session:
            await session.begin()
            await set_org_scope(session, org_id)
            try:
                yield session
            finally:
                await session.rollback()

    return _open
