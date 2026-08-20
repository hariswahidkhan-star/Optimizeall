"""Acceptance criterion AC-08: an attempt to read another organisation's data is
blocked at the data layer.

These tests exercise real PostgreSQL row-level security. They are the reason the
harness refuses to use a stand-in database: the guarantee being tested is a database
feature, and a mock would prove only that the mock behaves.
"""

from __future__ import annotations

from sqlalchemy import func, select, text

from pciai.domain.identity.models import Actor, Organization


async def test_scope_returns_only_its_own_actors(scoped, org_a, org_b) -> None:
    async with scoped(org_a.org_id) as session:
        rows = (await session.execute(select(Actor.id, Actor.org_id))).all()
    assert rows, "org A should see its own actors"
    assert {org for _, org in rows} == {org_a.org_id}
    assert org_b.actors["owner"] not in {actor for actor, _ in rows}


async def test_explicitly_selecting_another_organisation_returns_nothing(
    scoped, org_a, org_b
) -> None:
    """Even naming the other organisation's key returns nothing. Isolation is not a
    forgotten-predicate safety net; it is a filter that cannot be argued with."""
    async with scoped(org_a.org_id) as session:
        rows = (await session.execute(select(Actor.id).where(Actor.org_id == org_b.org_id))).all()
    assert rows == []


async def test_unbound_session_sees_nothing(scoped, org_a) -> None:
    """A missing scope fails closed."""
    async with scoped(None) as session:
        count = (await session.execute(select(func.count()).select_from(Actor))).scalar_one()
    assert count == 0


async def test_organisation_row_itself_is_scoped(scoped, org_a, org_b) -> None:
    async with scoped(org_a.org_id) as session:
        ids = list((await session.execute(select(Organization.id))).scalars())
    assert ids == [org_a.org_id]


async def test_insert_into_another_organisation_is_rejected(scoped, org_a, org_b) -> None:
    """The WITH CHECK clause refuses a write that would place a row outside the
    bound scope — isolation covers writes, not only reads."""
    async with scoped(org_a.org_id) as session:
        try:
            await session.execute(
                text(
                    "INSERT INTO config.brand (id, org_id, code, name, is_default) "
                    "VALUES (gen_random_uuid(), :other, 'smuggled', 'Smuggled', false)"
                ),
                {"other": org_b.org_id},
            )
            await session.flush()
        except Exception as exc:  # the policy raises; the type is driver-specific
            assert "policy" in str(exc).lower() or "row-level security" in str(exc).lower()
        else:  # pragma: no cover - a pass here is a failure of the guarantee
            raise AssertionError("cross-organisation insert was permitted")


async def test_api_list_is_scoped_to_the_credential(client, org_a, org_b) -> None:
    a = await client.get("/api/v1/actors", headers=org_a.auth("owner"))
    b = await client.get("/api/v1/actors", headers=org_b.auth("owner"))
    assert a.status_code == b.status_code == 200
    a_ids = {row["id"] for row in a.json()}
    b_ids = {row["id"] for row in b.json()}
    assert a_ids and b_ids
    assert a_ids.isdisjoint(b_ids)


async def test_objectives_are_scoped(client, org_a, org_b) -> None:
    response = await client.get("/api/v1/objectives", headers=org_a.auth("owner"))
    assert response.status_code == 200
    payload = response.json()
    assert len(payload) == 11, "the workbook's eleven objectives"
    assert [o["value_rank"] for o in payload] == list(range(1, 12))
    assert payload[0]["code"] == "cert_sales_pcl_ai"

    other = await client.get("/api/v1/objectives", headers=org_b.auth("owner"))
    assert {o["id"] for o in payload}.isdisjoint({o["id"] for o in other.json()})
