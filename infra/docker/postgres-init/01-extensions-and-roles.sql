-- Runs once, on first container start, before the application connects.
--
-- Creates the extensions the schema depends on and the login role the application uses. The
-- application role is deliberately not the database owner and not a superuser: both bypass
-- row-level security unconditionally, so developing against either would mean tenant isolation is
-- never actually exercised until production.

CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS citext;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'optimizeall_app') THEN
        CREATE ROLE optimizeall_app LOGIN PASSWORD 'devpassword';
    ELSE
        ALTER ROLE optimizeall_app LOGIN PASSWORD 'devpassword';
    END IF;
END
$$;

GRANT CONNECT ON DATABASE optimizeall TO optimizeall_app;
GRANT USAGE, CREATE ON SCHEMA public TO optimizeall_app;

-- Migrations run as the owner; these defaults make sure the application role can use whatever the
-- migrations create without a manual grant after every schema change.
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO optimizeall_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO optimizeall_app;
