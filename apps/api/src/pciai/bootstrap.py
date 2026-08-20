"""Seeding: permissions, an organisation, its roles, and the workbook's objectives.

Note what happens here — because row-level security is FORCED, even seeding must bind
a scope before it can insert. That is the isolation guarantee proving itself on the
very first write rather than being taken on trust.

The workbook's own content (objectives, brands) is *configuration*, so it is seeded
through this governed path rather than through a migration: it must be diffable,
approvable and audited, and a migration script is none of those.
"""

from __future__ import annotations

import asyncio
import uuid
from datetime import UTC, datetime

from sqlalchemy import select
from sqlalchemy.dialects.postgresql import insert as pg_insert
from sqlalchemy.ext.asyncio import AsyncSession

from pciai.db.session import dispose_engine, get_sessionmaker, set_org_scope
from pciai.domain.config.models import Brand, Objective
from pciai.domain.identity.models import (
    Actor,
    ActorRole,
    ActorType,
    AgentPrincipal,
    Organization,
    Permission,
    Role,
    RolePermission,
    UserAccount,
)
from pciai.ids import uuid7
from pciai.security.permissions import AGENT_FORBIDDEN, PERMISSIONS, ROLES

# Objective Performance!A4:K14 — the orchestrator's objective function.
WORKBOOK_OBJECTIVES: tuple[tuple[str, str, int, str], ...] = (
    (
        "cert_sales_pcl_ai",
        "Certification Sales - PCL-AI",
        1,
        "Revenue driver — flagship credential",
    ),
    ("cert_sales_pml_ai", "Certification Sales - PML-AI", 2, "Revenue driver — premium tier"),
    ("cert_sales_pfl_ai", "Certification Sales - PFL-AI", 3, "Revenue driver — entry funnel"),
    (
        "honorary_outreach",
        "Honorary Certification Outreach",
        4,
        "Strategic pipeline — fellows legitimise, refer, open doors",
    ),
    ("partnerships_pr", "Partnerships & PR", 5, "Multiplier — one deal moves whole cohorts"),
    (
        "authority_entity",
        "Authority & Entity Building",
        6,
        "Compounding — lifts every other conversion",
    ),
    ("content_seo", "Content & SEO Growth", 7, "Compounding — owned traffic that keeps paying"),
    ("events_webinars", "Events & Webinars", 8, "Pipeline builder — concentrated lead capture"),
    ("certuvo", "Certuvo (Exam Prep)", 9, "Adjacent revenue and feeder into certifications"),
    ("community", "Community Presence", 10, "Trust builder — slow burn, defends reputation"),
    ("brand_awareness", "General Brand Awareness", 11, "Support — spend spare capacity only"),
)

# START HERE §7 — every logged row says which property it serves.
WORKBOOK_BRANDS: tuple[tuple[str, str, bool], ...] = (
    ("institute", "PCI AI - Institute (umbrella)", True),
    ("pcl_ai", "PCL-AI certification", False),
    ("pfl_ai", "PFL-AI certification", False),
    ("pml_ai", "PML-AI certification", False),
    ("pci_world", "PCI World", False),
    ("certuvo", "Certuvo (exam prep)", False),
    ("shared", "All / shared", False),
)


async def seed_permissions(session: AsyncSession) -> None:
    """Global vocabulary; not organisation-scoped, so no RLS applies."""
    for p in PERMISSIONS:
        await session.execute(
            pg_insert(Permission)
            .values(
                code=p.code, category=p.category, description=p.description, is_write=p.is_write
            )
            .on_conflict_do_update(
                index_elements=[Permission.code],
                set_={"category": p.category, "description": p.description, "is_write": p.is_write},
            )
        )


async def create_organization(
    session: AsyncSession,
    *,
    name: str,
    slug: str,
    timezone: str = "Europe/London",
    week_start_day: int = 2,
    programme_start: datetime | None = None,
) -> Organization:
    org_id = uuid7()
    # Bind the scope first: the WITH CHECK clause refuses an insert whose org_id
    # does not match the bound organisation.
    await set_org_scope(session, org_id)
    org = Organization(
        id=org_id,
        name=name,
        slug=slug,
        timezone=timezone,
        week_start_day=week_start_day,
        programme_start=programme_start or datetime.now(UTC),
    )
    session.add(org)
    await session.flush()

    for code, (display, permissions) in ROLES.items():
        role = Role(id=uuid7(), org_id=org_id, code=code, name=display, is_system=True)
        session.add(role)
        await session.flush()
        for permission_code in sorted(permissions):
            session.add(
                RolePermission(role_id=role.id, permission_code=permission_code, org_id=org_id)
            )

    for code, display, rank, rationale in WORKBOOK_OBJECTIVES:
        session.add(
            Objective(
                id=uuid7(),
                org_id=org_id,
                code=code,
                name=display,
                value_rank=rank,
                rationale=rationale,
            )
        )
    for code, display, is_default in WORKBOOK_BRANDS:
        session.add(
            Brand(id=uuid7(), org_id=org_id, code=code, name=display, is_default=is_default)
        )

    await session.flush()
    return org


async def _role_id(session: AsyncSession, org_id: uuid.UUID, code: str) -> uuid.UUID:
    row = (
        await session.execute(select(Role.id).where(Role.org_id == org_id, Role.code == code))
    ).scalar_one()
    return row


async def create_human(
    session: AsyncSession,
    *,
    org_id: uuid.UUID,
    display_name: str,
    email: str,
    external_subject: str,
    role_codes: tuple[str, ...],
    granted_by: uuid.UUID | None = None,
) -> Actor:
    actor = Actor(id=uuid7(), org_id=org_id, actor_type=ActorType.human, display_name=display_name)
    session.add(actor)
    await session.flush()
    session.add(UserAccount(actor_id=actor.id, email=email, external_subject=external_subject))
    for code in role_codes:
        session.add(
            ActorRole(
                actor_id=actor.id,
                role_id=await _role_id(session, org_id, code),
                org_id=org_id,
                granted_by=granted_by or actor.id,
            )
        )
    await session.flush()
    return actor


async def create_agent(
    session: AsyncSession,
    *,
    org_id: uuid.UUID,
    agent_code: str,
    display_name: str,
    role_codes: tuple[str, ...] = (),
    granted_by: uuid.UUID,
) -> Actor:
    """Create an agent principal.

    Agents hold **no** role-matrix permissions. Capability comes from tool grants,
    which arrive with the agent registry in slice 3 — and no tool maps to approving
    anything, which is why an agent can publish content and can never authorise it.

    `role_codes` therefore defaults to empty, and any role carrying a permission on
    the agent-forbidden list is refused here rather than filtered later: a
    misconfiguration must not become a capability that merely happens to be masked
    at resolution time.
    """
    for code in role_codes:
        granted = set(ROLES[code][1])
        overlap = granted & AGENT_FORBIDDEN
        if overlap:
            raise ValueError(
                f"role '{code}' grants {sorted(overlap)}, which no agent principal may hold"
            )
    actor = Actor(id=uuid7(), org_id=org_id, actor_type=ActorType.agent, display_name=display_name)
    session.add(actor)
    await session.flush()
    session.add(AgentPrincipal(actor_id=actor.id, agent_code=agent_code))
    for code in role_codes:
        session.add(
            ActorRole(
                actor_id=actor.id,
                role_id=await _role_id(session, org_id, code),
                org_id=org_id,
                granted_by=granted_by,
            )
        )
    await session.flush()
    return actor


async def seed_local_development() -> None:
    """Create a usable local organisation. Never run against production."""
    async with get_sessionmaker()() as session:
        async with session.begin():
            await seed_permissions(session)
            org = await create_organization(
                session, name="PCI AI — Project Controls Institute", slug="pci-ai"
            )
            owner = await create_human(
                session,
                org_id=org.id,
                display_name="Owner",
                email="owner@pciai.org",
                external_subject="dev|owner",
                role_codes=("Owner",),
            )
            await create_human(
                session,
                org_id=org.id,
                display_name="Manager",
                email="manager@pciai.org",
                external_subject="dev|manager",
                role_codes=("Manager",),
                granted_by=owner.id,
            )
            await create_agent(
                session,
                org_id=org.id,
                agent_code="CWRITE",
                display_name="Content Writer",
                granted_by=owner.id,
            )
        print(f"seeded organisation {org.id} ({org.slug})")
    await dispose_engine()


if __name__ == "__main__":
    asyncio.run(seed_local_development())
