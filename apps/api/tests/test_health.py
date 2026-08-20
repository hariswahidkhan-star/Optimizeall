"""The health check reaches the database, because a health check that cannot is
reporting the wrong thing."""

from __future__ import annotations

from httpx import AsyncClient


async def test_health_reports_database_reachable(client: AsyncClient) -> None:
    response = await client.get("/health")
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "ok"
    assert body["database"] == "up"
    assert body["environment"] == "test"


async def test_every_response_carries_a_correlation_id(client: AsyncClient) -> None:
    response = await client.get("/health")
    assert response.headers["X-Correlation-Id"]


async def test_supplied_correlation_id_is_echoed(client: AsyncClient) -> None:
    supplied = "018f2c1a-0000-7000-8000-000000000001"
    response = await client.get("/health", headers={"X-Correlation-Id": supplied})
    assert response.headers["X-Correlation-Id"] == supplied
