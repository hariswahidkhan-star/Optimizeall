"""Errors are RFC 9457 problem documents, and the distinctions carry meaning."""

from __future__ import annotations

from httpx import AsyncClient


async def test_problem_document_shape(client: AsyncClient, org_a) -> None:
    response = await client.get("/api/v1/organisation", headers=org_a.auth("analyst"))
    assert response.status_code == 403
    assert response.headers["content-type"].startswith("application/problem+json")
    body = response.json()
    for field in ("type", "title", "status", "detail", "instance", "correlation_id"):
        assert field in body, f"problem document is missing {field}"
    assert body["type"].startswith("https://pciai.org/problems/")
    assert body["status"] == 403
    assert body["instance"] == "/api/v1/organisation"


async def test_correlation_id_in_body_matches_the_header(client: AsyncClient, org_a) -> None:
    """One id links a failure in a response to the audit record of the same request."""
    response = await client.get("/api/v1/organisation", headers=org_a.auth("analyst"))
    assert response.json()["correlation_id"] == response.headers["X-Correlation-Id"]


async def test_unknown_route_is_a_problem_document(client: AsyncClient) -> None:
    response = await client.get("/api/v1/does-not-exist")
    assert response.status_code == 404
    assert response.headers["content-type"].startswith("application/problem+json")


async def test_permission_denied_names_the_missing_permission(client, org_a) -> None:
    """403 says what is restricted and who can grant it — never a bare status code."""
    response = await client.patch(
        "/api/v1/organisation", json={"name": "x"}, headers=org_a.auth("operator")
    )
    assert response.status_code == 403
    assert response.json()["rule"] == "org.manage"
