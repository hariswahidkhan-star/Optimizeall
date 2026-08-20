"""RFC 9457 problem details.

The line between 400 and 422 carries weight: a client can retry a 400 after fixing
the payload, but a 422 means the platform's rules rejected the intent, and the
correct response is usually to change the plan rather than the JSON.
"""

from __future__ import annotations

from typing import Any

from fastapi import FastAPI, Request, status
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

PROBLEM_BASE = "https://pciai.org/problems"
CONTENT_TYPE = "application/problem+json"


class ProblemError(Exception):
    """An error that renders as a problem document."""

    def __init__(
        self,
        *,
        status_code: int,
        title: str,
        problem_type: str,
        detail: str,
        rule: str | None = None,
        errors: list[dict[str, Any]] | None = None,
    ) -> None:
        super().__init__(detail)
        self.status_code = status_code
        self.title = title
        self.problem_type = problem_type
        self.detail = detail
        self.rule = rule
        self.errors = errors or []


class Unauthenticated(ProblemError):
    def __init__(self, detail: str = "A valid credential is required.") -> None:
        super().__init__(
            status_code=status.HTTP_401_UNAUTHORIZED,
            title="Not authenticated",
            problem_type=f"{PROBLEM_BASE}/unauthenticated",
            detail=detail,
        )


class PermissionDenied(ProblemError):
    """403 always names the missing permission. It never hides a record's existence —
    that is what 404 is for."""

    def __init__(self, permission: str) -> None:
        super().__init__(
            status_code=status.HTTP_403_FORBIDDEN,
            title="Permission denied",
            problem_type=f"{PROBLEM_BASE}/permission-denied",
            detail=f"This action requires the '{permission}' permission.",
            rule=permission,
        )


class NotFound(ProblemError):
    def __init__(self, what: str = "Resource") -> None:
        super().__init__(
            status_code=status.HTTP_404_NOT_FOUND,
            title="Not found",
            problem_type=f"{PROBLEM_BASE}/not-found",
            detail=f"{what} does not exist, or is not visible to your organisation.",
        )


class PolicyViolation(ProblemError):
    def __init__(self, rule: str, detail: str) -> None:
        super().__init__(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            title="Policy denied this action",
            problem_type=f"{PROBLEM_BASE}/policy-violation",
            detail=detail,
            rule=rule,
        )


def _document(exc: ProblemError, request: Request) -> JSONResponse:
    body: dict[str, Any] = {
        "type": exc.problem_type,
        "title": exc.title,
        "status": exc.status_code,
        "detail": exc.detail,
        "instance": request.url.path,
        "correlation_id": getattr(request.state, "correlation_id", None),
    }
    if exc.rule:
        body["rule"] = exc.rule
    if exc.errors:
        body["errors"] = exc.errors
    headers = {}
    if exc.status_code == status.HTTP_401_UNAUTHORIZED:
        headers["WWW-Authenticate"] = "Bearer"
    return JSONResponse(body, status_code=exc.status_code, media_type=CONTENT_TYPE, headers=headers)


def install_error_handlers(app: FastAPI) -> None:
    @app.exception_handler(ProblemError)
    async def _problem(request: Request, exc: ProblemError) -> JSONResponse:
        return _document(exc, request)

    @app.exception_handler(RequestValidationError)
    async def _validation(request: Request, exc: RequestValidationError) -> JSONResponse:
        errors = [
            {
                "field": ".".join(str(p) for p in err.get("loc", ())[1:]),
                "code": err.get("type", "invalid"),
                "message": err.get("msg", "Invalid value"),
            }
            for err in exc.errors()
        ]
        problem = ProblemError(
            status_code=status.HTTP_400_BAD_REQUEST,
            title="Malformed request",
            problem_type=f"{PROBLEM_BASE}/malformed-request",
            detail="The request body or parameters could not be parsed.",
            errors=errors,
        )
        return _document(problem, request)

    @app.exception_handler(StarletteHTTPException)
    async def _http(request: Request, exc: StarletteHTTPException) -> JSONResponse:
        problem = ProblemError(
            status_code=exc.status_code,
            title=str(exc.detail),
            problem_type=f"{PROBLEM_BASE}/http-error",
            detail=str(exc.detail),
        )
        return _document(problem, request)
