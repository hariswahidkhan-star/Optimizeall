"""Authorisation: the role matrix, and its three deliberate asymmetries."""

from __future__ import annotations

import pytest

from pciai.security.permissions import PERMISSION_CODES, ROLES


async def test_read_requires_a_permission(client, org_a) -> None:
    response = await client.get("/api/v1/organisation", headers=org_a.auth("analyst"))
    assert response.status_code == 403
    body = response.json()
    assert body["rule"] == "org.read"
    assert "org.read" in body["detail"]


async def test_permitted_read_succeeds(client, org_a) -> None:
    response = await client.get("/api/v1/organisation", headers=org_a.auth("manager"))
    assert response.status_code == 200
    assert response.json()["slug"] == "org-a"
    assert response.headers["ETag"].startswith('W/"')


async def test_write_requires_a_separate_permission(client, org_a) -> None:
    """Manager reads the organisation but does not administer it."""
    response = await client.patch(
        "/api/v1/organisation", json={"timezone": "Asia/Riyadh"}, headers=org_a.auth("manager")
    )
    assert response.status_code == 403
    assert response.json()["rule"] == "org.manage"


async def test_admin_can_administer(client, org_a) -> None:
    response = await client.patch(
        "/api/v1/organisation",
        json={"timezone": "Asia/Riyadh", "week_start_day": 1},
        headers=org_a.auth("admin"),
    )
    assert response.status_code == 200
    body = response.json()
    assert body["timezone"] == "Asia/Riyadh"
    # Week-start 1 is the workbook's Sunday-Thursday setting for Gulf teams.
    assert body["week_start_day"] == 1
    assert body["row_version"] == 2


async def test_validation_failure_is_a_400_with_field_errors(client, org_a) -> None:
    response = await client.patch(
        "/api/v1/organisation", json={"week_start_day": 9}, headers=org_a.auth("admin")
    )
    assert response.status_code == 400
    body = response.json()
    assert body["errors"][0]["field"] == "week_start_day"


@pytest.mark.parametrize(
    ("role", "permission", "expected"),
    [
        # Admin administers the system but does not authorise its outward actions.
        ("Admin", "approval.grant", False),
        ("Admin", "org.manage", True),
        # A safety role that could also authorise is not a safety role.
        ("Compliance", "approval.reject", True),
        ("Compliance", "approval.grant", False),
        # Stopping should be easy; restarting should not.
        ("Manager", "system.emergency_stop", True),
        ("Manager", "system.emergency_stop_release", False),
        ("Admin", "system.emergency_stop_release", False),
        ("Compliance", "system.emergency_stop_release", False),
        ("Owner", "system.emergency_stop_release", True),
        # Restricted personnel data is not a general management read.
        ("Admin", "hr.read", False),
        ("Manager", "hr.read", True),
        ("Analyst", "hr.read", False),
    ],
)
def test_role_matrix_asymmetries(role: str, permission: str, expected: bool) -> None:
    assert (permission in ROLES[role][1]) is expected


def test_every_granted_permission_exists() -> None:
    """A role granting a permission that does not exist would fail at seeding; catch
    it here instead, where the message is useful."""
    for role, (_, granted) in ROLES.items():
        unknown = set(granted) - PERMISSION_CODES
        assert not unknown, f"role {role} grants unknown permissions {sorted(unknown)}"


def test_only_owner_holds_every_permission() -> None:
    holders = [r for r, (_, g) in ROLES.items() if set(g) == PERMISSION_CODES]
    assert holders == ["Owner"]
