from __future__ import annotations

from fastapi import APIRouter
from sqlalchemy import text

from pciai import __version__
from pciai.api.schemas import Health
from pciai.db.session import get_sessionmaker
from pciai.settings import get_settings

router = APIRouter(tags=["system"])


@router.get("/health", response_model=Health)
async def health() -> Health:
    """Liveness plus a real database round-trip — a health check that cannot reach
    its database is reporting the wrong thing."""
    settings = get_settings()
    database = "up"
    try:
        async with get_sessionmaker()() as session:
            await session.execute(text("SELECT 1"))
    except Exception:
        database = "down"
    return Health(
        status="ok" if database == "up" else "degraded",
        environment=settings.environment,
        version=__version__,
        database=database,
    )
