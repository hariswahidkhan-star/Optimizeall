"""Mint a development token for a seeded actor.

Uses the operations role, which may bypass row-level security — the application
role cannot, so there is no way to reach this from the running service. Local and
test only; the issuer refuses to run anywhere else.
"""

from __future__ import annotations

import argparse
import asyncio

from sqlalchemy import select
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine

from pciai.domain.identity.models import Actor, Organization
from pciai.security.tokens import issue_dev_token
from pciai.settings import get_settings


async def mint(slug: str, display_name: str) -> str:
    settings = get_settings()
    engine = create_async_engine(settings.migration_database_url)
    try:
        async with async_sessionmaker(engine)() as session:
            org_id = (
                await session.execute(select(Organization.id).where(Organization.slug == slug))
            ).scalar_one()
            actor_id = (
                await session.execute(
                    select(Actor.id).where(
                        Actor.org_id == org_id, Actor.display_name == display_name
                    )
                )
            ).scalar_one()
            return issue_dev_token(
                actor_id=actor_id, org_id=org_id, subject=f"dev|{display_name.lower()}"
            )
    finally:
        await engine.dispose()


def main() -> None:
    parser = argparse.ArgumentParser(description="Mint a local development token")
    parser.add_argument("--slug", default="pci-ai")
    parser.add_argument("--actor", default="Owner")
    args = parser.parse_args()
    print(asyncio.run(mint(args.slug, args.actor)))


if __name__ == "__main__":
    main()
