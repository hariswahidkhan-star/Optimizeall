"""Correlation identity.

One id per business workflow flows request → workflow → activity → agent execution →
model call → tool request → connector call → audit event. It is what turns "trace one
business workflow across services" from an inference into a query.
"""

from __future__ import annotations

import uuid
from collections.abc import Awaitable, Callable

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import Response

HEADER = "X-Correlation-Id"


class CorrelationIdMiddleware(BaseHTTPMiddleware):
    async def dispatch(
        self, request: Request, call_next: Callable[[Request], Awaitable[Response]]
    ) -> Response:
        incoming = request.headers.get(HEADER)
        try:
            correlation_id = str(uuid.UUID(incoming)) if incoming else str(uuid.uuid4())
        except ValueError:
            correlation_id = str(uuid.uuid4())
        request.state.correlation_id = correlation_id
        response = await call_next(request)
        response.headers[HEADER] = correlation_id
        return response
