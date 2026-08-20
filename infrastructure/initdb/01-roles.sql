-- Cluster-level bootstrap. Roles and database live outside the migration history
-- because they are infrastructure, not schema.
--
-- Two roles, deliberately: migrations own the tables, the application does not.
-- A non-owning application role is what makes row-level security apply to it.

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pciai_owner') THEN
    CREATE ROLE pciai_owner LOGIN PASSWORD 'pciai_owner';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pciai_app') THEN
    CREATE ROLE pciai_app LOGIN PASSWORD 'pciai_app';
  END IF;
END
$$;

-- The migration/operations role may bypass row-level security; the application role
-- may not, and never gets this attribute. Without a bypass somewhere, no operator
-- could enumerate organisations at all — the isolation policy is deliberately
-- absolute, so the escape hatch has to be explicit and auditable rather than
-- discovered later by someone widening the app role.
ALTER ROLE pciai_owner BYPASSRLS;
ALTER ROLE pciai_app NOBYPASSRLS;
