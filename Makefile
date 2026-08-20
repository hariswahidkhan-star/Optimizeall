# PCI AI Autonomous Growth OS
API := apps/api
export PYTHONPATH := $(API)/src

.DEFAULT_GOAL := help
.PHONY: help install db-roles db-create migrate seed run test lint format typecheck check clean

help:  ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN{FS=":.*?## "};{printf "  \033[36m%-12s\033[0m %s\n",$$1,$$2}'

install:  ## Install runtime and development dependencies
	pip install -e "$(API)[dev]"

db-roles:  ## Create the owner and application roles (idempotent)
	psql -U postgres -v ON_ERROR_STOP=1 -f $(API)/scripts/bootstrap_db.sql

db-create:  ## Create the development and test databases (idempotent)
	createdb -U postgres -O pciai_owner pciai      2>/dev/null || true
	createdb -U postgres -O pciai_owner pciai_test 2>/dev/null || true

migrate:  ## Apply migrations to the development database
	cd $(API) && python -m alembic upgrade head

seed:  ## Seed a local organisation, roles and the workbook's objectives
	cd $(API) && python -m pciai.bootstrap

token:  ## Mint a local development token (ACTOR=Owner)
	@cd $(API) && python -m pciai.devtoken --actor $${ACTOR:-Owner}

run:  ## Serve the API on :8080 with reload
	cd $(API) && python -m uvicorn pciai.main:app --reload --port 8080

test:  ## Run the test suite against a real PostgreSQL
	cd $(API) && PCIAI_ENVIRONMENT=test python -m pytest -q

lint:  ## Lint and check formatting
	cd $(API) && ruff check . && ruff format --check .

format:  ## Apply formatting and safe fixes
	cd $(API) && ruff check --fix . && ruff format .

typecheck:  ## Strict type checking
	cd $(API) && python -m mypy

check: lint typecheck test  ## Everything CI runs

clean:
	find . -name __pycache__ -type d -prune -exec rm -rf {} + 2>/dev/null || true
