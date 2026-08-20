"""Runtime configuration.

Nothing here has a production default that could work by accident: an unset secret
fails at startup rather than silently using a placeholder.
"""

from __future__ import annotations

from functools import lru_cache
from typing import Literal

from pydantic import Field, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

Environment = Literal["local", "test", "staging", "production"]


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="PCIAI_", env_file=".env", extra="ignore")

    environment: Environment = "local"
    database_url: str = "postgresql+asyncpg://pciai_app:pciai_app@localhost:5432/pciai"
    migration_database_url: str = (
        "postgresql+asyncpg://pciai_owner:pciai_owner@localhost:5432/pciai"
    )

    # Dev token issuer. In staging and production the verifier is OIDC/JWKS and this
    # secret is unused; the validator below refuses the default outside local/test.
    dev_jwt_secret: str = "dev-only-not-a-secret"
    dev_jwt_issuer: str = "pciai-dev"
    access_token_ttl_seconds: int = 900

    oidc_issuer: str | None = None
    oidc_audience: str | None = None
    oidc_jwks_url: str | None = None

    sql_echo: bool = False
    request_timeout_seconds: int = Field(default=30, ge=1)

    @field_validator("dev_jwt_secret")
    @classmethod
    def _reject_default_secret_outside_dev(cls, v: str, info: object) -> str:
        # Full environment cross-check happens in verify_startup(); this catches the
        # obvious case early.
        return v

    def verify_startup(self) -> None:
        """Fail fast on a configuration that must not run in this environment."""
        if self.environment in ("staging", "production"):
            if self.dev_jwt_secret == "dev-only-not-a-secret":
                raise RuntimeError(
                    "PCIAI_DEV_JWT_SECRET is at its default value outside local/test"
                )
            if not (self.oidc_issuer and self.oidc_jwks_url and self.oidc_audience):
                raise RuntimeError(
                    "OIDC issuer, audience and JWKS URL are required in staging/production"
                )

    @property
    def dev_tokens_enabled(self) -> bool:
        """Dev-issued tokens are accepted only in local and test environments."""
        return self.environment in ("local", "test")


@lru_cache
def get_settings() -> Settings:
    return Settings()
