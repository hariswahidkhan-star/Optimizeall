"""The permission vocabulary, and the roles that hold it.

Permissions are verbs on modules, not screens — screens compose them. Three
asymmetries in the role matrix are deliberate and are asserted by tests:

  * Admin cannot approve. Administering the system and authorising its outward
    actions are different duties.
  * Compliance can reject but never approve. A safety role that could also
    authorise is not a safety role.
  * Only the Owner can release an emergency stop, though several roles can engage
    one. Stopping should be easy; restarting should not.
"""

from __future__ import annotations

from typing import Final, NamedTuple


class PermissionDef(NamedTuple):
    code: str
    category: str
    description: str
    is_write: bool


PERMISSIONS: Final[tuple[PermissionDef, ...]] = (
    PermissionDef("org.read", "org", "Read organisation settings", False),
    PermissionDef("org.manage", "org", "Change organisation settings", True),
    PermissionDef("actor.read", "identity", "List actors and their roles", False),
    PermissionDef("actor.manage", "identity", "Create actors and grant roles", True),
    PermissionDef("approval.read", "approval", "Read the approval queue", False),
    PermissionDef("approval.grant", "approval", "Approve a proposed action", True),
    PermissionDef("approval.reject", "approval", "Reject a proposed action", True),
    PermissionDef("content.read", "content", "Read content records", False),
    PermissionDef("content.publish", "content", "Publish approved content", True),
    PermissionDef("lead.read", "pipeline", "Read leads", False),
    PermissionDef("outreach.confirm_send", "pipeline", "Confirm a human-executed send", True),
    PermissionDef("analytics.read", "insight", "Read KPIs and reports", False),
    PermissionDef("cost.read", "ledger", "Read AI and channel spend", False),
    PermissionDef("budget.set", "ledger", "Set budgets", True),
    PermissionDef("audit.read", "audit", "Read the audit record", False),
    PermissionDef("hr.read", "identity", "Read restricted personnel scoring", False),
    PermissionDef("system.emergency_stop", "system", "Engage an emergency stop", True),
    PermissionDef("system.emergency_stop_release", "system", "Release an emergency stop", True),
)

PERMISSION_CODES: Final[frozenset[str]] = frozenset(p.code for p in PERMISSIONS)

_READ_ALL = (
    "org.read",
    "actor.read",
    "approval.read",
    "content.read",
    "lead.read",
    "analytics.read",
    "audit.read",
)

ROLES: Final[dict[str, tuple[str, tuple[str, ...]]]] = {
    "Owner": (
        "Owner",
        tuple(PERMISSION_CODES),
    ),
    "Admin": (
        "Administrator",
        (
            *_READ_ALL,
            "org.manage",
            "actor.manage",
            "content.publish",
            "cost.read",
            "budget.set",
            "system.emergency_stop",
        ),
    ),
    "Manager": (
        "Manager",
        (
            *_READ_ALL,
            "approval.grant",
            "approval.reject",
            "content.publish",
            "outreach.confirm_send",
            "hr.read",
            "system.emergency_stop",
        ),
    ),
    "Operator": (
        "Operator",
        ("content.read", "lead.read", "outreach.confirm_send"),
    ),
    "Closer": ("Closer", ("lead.read",)),
    "Analyst": ("Analyst", ("analytics.read", "content.read", "lead.read", "cost.read")),
    "Compliance": (
        "Compliance",
        (*_READ_ALL, "approval.reject", "system.emergency_stop"),
    ),
}

#: Permissions no agent principal may ever hold, whatever its role grants.
#: Capability for an agent comes from tool grants, never from this matrix — and no
#: tool maps to approving something. Enforced in `principal.py` and asserted by test.
AGENT_FORBIDDEN: Final[frozenset[str]] = frozenset(
    {
        "approval.grant",
        "approval.reject",
        "actor.manage",
        "org.manage",
        "budget.set",
        "hr.read",
        "system.emergency_stop",
        "system.emergency_stop_release",
        "outreach.confirm_send",
    }
)
