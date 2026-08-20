"""Token issue and verification.

Two verifiers behind one interface. The dev issuer exists so the platform is
runnable without an identity provider; it is refused outside local and test, so a
misconfigured production deployment fails closed rather than accepting self-signed
tokens.
"""

from __future__ import annotations

import uuid
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from typing import Any

import jwt

from pciai.settings import Settings, get_settings


class TokenError(Exception):
    """Raised when a credential is absent, malformed, expired or untrusted."""


@dataclass(frozen=True)
class TokenClaims:
    subject: str
    org_id: uuid.UUID
    actor_id: uuid.UUID
    session_id: uuid.UUID | None
    task_id: uuid.UUID | None
    agent_version_id: uuid.UUID | None
    expires_at: datetime


def issue_dev_token(
    *,
    actor_id: uuid.UUID,
    org_id: uuid.UUID,
    subject: str,
    session_id: uuid.UUID | None = None,
    task_id: uuid.UUID | None = None,
    agent_version_id: uuid.UUID | None = None,
    ttl_seconds: int | None = None,
    settings: Settings | None = None,
) -> str:
    s = settings or get_settings()
    if not s.dev_tokens_enabled:
        raise TokenError("dev tokens are not available in this environment")
    now = datetime.now(UTC)
    ttl = ttl_seconds if ttl_seconds is not None else s.access_token_ttl_seconds
    payload: dict[str, Any] = {
        "iss": s.dev_jwt_issuer,
        "sub": subject,
        "org_id": str(org_id),
        "actor_id": str(actor_id),
        "iat": int(now.timestamp()),
        "exp": int((now + timedelta(seconds=ttl)).timestamp()),
    }
    if session_id:
        payload["sid"] = str(session_id)
    # An agent token is bound to one task. That binding is what lets the execution
    # gate check scope without trusting anything the model asserted.
    if task_id:
        payload["task_id"] = str(task_id)
    if agent_version_id:
        payload["agent_version_id"] = str(agent_version_id)
    return jwt.encode(payload, s.dev_jwt_secret, algorithm="HS256")


def verify_token(token: str, settings: Settings | None = None) -> TokenClaims:
    s = settings or get_settings()
    if not s.dev_tokens_enabled:
        # Slice 1 ships the dev verifier only; the OIDC/JWKS verifier lands with the
        # identity-provider integration. Failing loudly beats accepting a token the
        # platform cannot actually validate.
        raise TokenError("OIDC verification is not configured in this build")
    try:
        payload = jwt.decode(
            token,
            s.dev_jwt_secret,
            algorithms=["HS256"],
            issuer=s.dev_jwt_issuer,
            options={"require": ["exp", "iat", "sub", "iss"]},
        )
    except jwt.ExpiredSignatureError as exc:
        raise TokenError("token expired") from exc
    except jwt.InvalidTokenError as exc:
        raise TokenError("token invalid") from exc

    try:
        return TokenClaims(
            subject=str(payload["sub"]),
            org_id=uuid.UUID(payload["org_id"]),
            actor_id=uuid.UUID(payload["actor_id"]),
            session_id=uuid.UUID(payload["sid"]) if payload.get("sid") else None,
            task_id=uuid.UUID(payload["task_id"]) if payload.get("task_id") else None,
            agent_version_id=(
                uuid.UUID(payload["agent_version_id"]) if payload.get("agent_version_id") else None
            ),
            expires_at=datetime.fromtimestamp(payload["exp"], tz=UTC),
        )
    except (KeyError, ValueError) as exc:
        raise TokenError("token missing required claims") from exc
