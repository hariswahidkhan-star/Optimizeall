"""Application entry point."""

from __future__ import annotations

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from fastapi import FastAPI

from pciai import __version__
from pciai.api.errors import install_error_handlers
from pciai.api.middleware import CorrelationIdMiddleware
from pciai.api.routers import actors, health, me, orgs
from pciai.db.session import dispose_engine
from pciai.settings import get_settings


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    get_settings().verify_startup()
    yield
    await dispose_engine()


def create_app() -> FastAPI:
    app = FastAPI(
        title="PCI AI Autonomous Growth OS",
        version=__version__,
        lifespan=lifespan,
        openapi_url="/api/v1/openapi.json",
        docs_url="/api/v1/docs",
    )
    app.add_middleware(CorrelationIdMiddleware)
    install_error_handlers(app)
    app.include_router(health.router)
    app.include_router(me.router)
    app.include_router(orgs.router)
    app.include_router(actors.router)
    return app


app = create_app()
