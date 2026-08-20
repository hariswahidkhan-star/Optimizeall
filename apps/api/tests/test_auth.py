"""Authentication: a credential that does not verify never reaches the domain."""

from __future__ import annotations

import uuid

import pytest
from httpx import AsyncClient

from pciai.security.tokens import TokenError, issue_dev_token, verify_token
from pciai.settings import Settings


async def test_missing_credential_is_401(client: AsyncClient) -> None:
    response = await client.get("/api/v1/me")
    assert response.status_code == 401
    assert response.headers["WWW-Authenticate"] == "Bearer"


async def test_garbage_token_is_401(client: AsyncClient) -> None:
    response = await client.get("/api/v1/me", headers={"Authorization": "Bearer not-a-token"})
    assert response.status_code == 401


async def test_expired_token_is_401(client: AsyncClient, org_a) -> None:
    token = issue_dev_token(
        actor_id=org_a.actors["owner"],
        org_id=org_a.org_id,
        subject="dev|owner",
        ttl_seconds=-10,
    )
    response = await client.get("/api/v1/me", headers={"Authorization": f"Bearer {token}"})
    assert response.status_code == 401


async def test_valid_token_resolves_the_principal(client: AsyncClient, org_a) -> None:
    response = await client.get("/api/v1/me", headers=org_a.auth("owner"))
    assert response.status_code == 200
    body = response.json()
    assert body["actor_id"] == str(org_a.actors["owner"])
    assert body["org_id"] == str(org_a.org_id)
    assert body["actor_type"] == "human"
    assert "approval.grant" in body["permissions"]


async def test_token_for_an_actor_in_another_organisation_does_not_resolve(
    client: AsyncClient, org_a, org_b
) -> None:
    """The org claim is the RLS scope. An actor outside it is not merely
    unauthorised — under row-level security it does not exist."""
    token = issue_dev_token(
        actor_id=org_b.actors["owner"], org_id=org_a.org_id, subject="dev|owner"
    )
    response = await client.get("/api/v1/me", headers={"Authorization": f"Bearer {token}"})
    assert response.status_code == 401


async def test_dev_tokens_are_refused_outside_local_and_test() -> None:
    """A misconfigured production deployment must fail closed rather than accept
    self-signed tokens."""
    production = Settings(
        environment="production",
        dev_jwt_secret="a-real-secret",
        oidc_issuer="https://issuer.example",
        oidc_audience="pciai",
        oidc_jwks_url="https://issuer.example/jwks",
    )
    with pytest.raises(TokenError):
        issue_dev_token(
            actor_id=uuid.uuid4(),
            org_id=uuid.uuid4(),
            subject="x",
            settings=production,
        )
    with pytest.raises(TokenError):
        verify_token("anything", settings=production)


async def test_production_settings_reject_the_default_secret() -> None:
    with pytest.raises(RuntimeError, match="default value"):
        Settings(
            environment="production",
            oidc_issuer="https://issuer.example",
            oidc_audience="pciai",
            oidc_jwks_url="https://issuer.example/jwks",
        ).verify_startup()


async def test_production_settings_require_oidc_configuration() -> None:
    with pytest.raises(RuntimeError, match="OIDC"):
        Settings(environment="production", dev_jwt_secret="a-real-secret").verify_startup()
