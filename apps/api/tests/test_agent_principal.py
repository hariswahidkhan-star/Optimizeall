"""Agent principals: capability comes from tool grants, never from the role matrix.

No tool maps to approving anything, which is why an agent can publish content and
can never authorise it.
"""

from __future__ import annotations

import pytest

from pciai.bootstrap import create_agent
from pciai.db.session import get_sessionmaker
from pciai.security.permissions import AGENT_FORBIDDEN


async def test_agent_resolves_as_an_agent(client, org_a) -> None:
    response = await client.get("/api/v1/me", headers=org_a.auth("agent"))
    assert response.status_code == 200
    assert response.json()["actor_type"] == "agent"


async def test_agent_holds_no_matrix_permission_at_all(client, org_a) -> None:
    """Capability for an agent comes from tool grants, never from the role matrix."""
    response = await client.get("/api/v1/me", headers=org_a.auth("agent"))
    granted = set(response.json()["permissions"])
    assert granted == set()
    assert granted.isdisjoint(AGENT_FORBIDDEN)


@pytest.mark.parametrize(
    ("role", "forbidden"),
    [
        ("Manager", "approval.grant"),
        # Found by this guard rather than by review: Operator carries the
        # confirm-send permission, and only a human confirms a human send.
        ("Operator", "outreach.confirm_send"),
        ("Admin", "org.manage"),
        ("Compliance", "approval.reject"),
    ],
)
async def test_granting_an_agent_a_privileged_role_is_refused(org_a, role, forbidden) -> None:
    """Refused at creation, not filtered afterwards."""
    async with get_sessionmaker()() as session, session.begin():
        from pciai.db.session import set_org_scope

        await set_org_scope(session, org_a.org_id)
        with pytest.raises(ValueError, match="no agent principal may hold"):
            await create_agent(
                session,
                org_id=org_a.org_id,
                agent_code="ROGUE",
                display_name="Rogue",
                role_codes=(role,),
                granted_by=org_a.actors["owner"],
            )


async def test_agent_token_carries_its_task_binding(client, org_a) -> None:
    """An agent token is minted per task. That binding is what lets the execution
    gate check scope without trusting anything the model asserted."""
    import uuid

    task_id = uuid.uuid4()
    response = await client.get("/api/v1/me", headers=org_a.auth("agent", task_id=task_id))
    assert response.status_code == 200
    assert response.json()["task_id"] == str(task_id)


async def test_agents_appear_in_the_actor_list_labelled_as_agents(client, org_a) -> None:
    """Agent work is attributed to the agent. The workbook's scorecards are built on
    who did what, and mixing the populations would corrupt them."""
    response = await client.get("/api/v1/actors", headers=org_a.auth("owner"))
    kinds = {row["display_name"]: row["actor_type"] for row in response.json()}
    assert kinds["Content Writer"] == "agent"
    assert any(v == "human" for v in kinds.values())
