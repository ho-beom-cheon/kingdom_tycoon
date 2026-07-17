DO $roles$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'tycoon_owner') THEN
        CREATE ROLE tycoon_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'tycoon_migrator') THEN
        CREATE ROLE tycoon_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'tycoon_app') THEN
        CREATE ROLE tycoon_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'tycoon_ops') THEN
        CREATE ROLE tycoon_ops LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'tycoon_readonly') THEN
        CREATE ROLE tycoon_readonly LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
    END IF;
END
$roles$;

DO $owner_membership$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_auth_members membership
          JOIN pg_roles granted_role ON granted_role.oid = membership.roleid
          JOIN pg_roles member_role ON member_role.oid = membership.member
         WHERE granted_role.rolname = 'tycoon_owner'
           AND member_role.rolname = 'tycoon_migrator'
    ) THEN
        GRANT tycoon_owner TO tycoon_migrator;
    END IF;
END
$owner_membership$;

DO $database_grants$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = current_user AND rolsuper)
       OR current_user = (
            SELECT pg_get_userbyid(datdba)
              FROM pg_database
             WHERE datname = current_database()
       ) THEN
        EXECUTE format('GRANT CREATE, CONNECT ON DATABASE %I TO tycoon_owner, tycoon_migrator', current_database());
        EXECUTE format('GRANT CONNECT ON DATABASE %I TO tycoon_app, tycoon_ops, tycoon_readonly', current_database());
    END IF;
END
$database_grants$;

DO $schema_ownership$
DECLARE
    schema_name text;
BEGIN
    FOREACH schema_name IN ARRAY ARRAY['game', 'master', 'ops', 'billing', 'audit']
    LOOP
        IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = schema_name) THEN
            EXECUTE format('ALTER SCHEMA %I OWNER TO tycoon_owner', schema_name);
        END IF;
    END LOOP;
END
$schema_ownership$;

SET ROLE tycoon_owner;

CREATE SCHEMA IF NOT EXISTS game AUTHORIZATION tycoon_owner;
CREATE SCHEMA IF NOT EXISTS master AUTHORIZATION tycoon_owner;
CREATE SCHEMA IF NOT EXISTS ops AUTHORIZATION tycoon_owner;
CREATE SCHEMA IF NOT EXISTS billing AUTHORIZATION tycoon_owner;
CREATE SCHEMA IF NOT EXISTS audit AUTHORIZATION tycoon_owner;

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON SCHEMA game, master, ops, billing, audit FROM PUBLIC;

GRANT USAGE ON SCHEMA game, master, ops, audit TO tycoon_app;
GRANT USAGE ON SCHEMA master, ops, audit TO tycoon_ops;
GRANT USAGE ON SCHEMA game, master, ops, billing, audit TO tycoon_readonly;

ALTER DEFAULT PRIVILEGES FOR ROLE tycoon_owner IN SCHEMA game, master, ops, billing, audit
    REVOKE ALL ON TABLES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES FOR ROLE tycoon_owner IN SCHEMA game, master, ops, billing, audit
    REVOKE ALL ON SEQUENCES FROM PUBLIC;
