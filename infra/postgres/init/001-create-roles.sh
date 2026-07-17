#!/usr/bin/env bash
set -Eeuo pipefail

: "${TYCOON_MIGRATOR_PASSWORD:?TYCOON_MIGRATOR_PASSWORD is required}"
: "${TYCOON_APP_PASSWORD:?TYCOON_APP_PASSWORD is required}"
: "${TYCOON_OPS_PASSWORD:?TYCOON_OPS_PASSWORD is required}"
: "${TYCOON_READONLY_PASSWORD:?TYCOON_READONLY_PASSWORD is required}"

psql --set=ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set=migrator_password="$TYCOON_MIGRATOR_PASSWORD" \
  --set=app_password="$TYCOON_APP_PASSWORD" \
  --set=ops_password="$TYCOON_OPS_PASSWORD" \
  --set=readonly_password="$TYCOON_READONLY_PASSWORD" <<'EOSQL'
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

ALTER ROLE tycoon_migrator PASSWORD :'migrator_password';
ALTER ROLE tycoon_app PASSWORD :'app_password';
ALTER ROLE tycoon_ops PASSWORD :'ops_password';
ALTER ROLE tycoon_readonly PASSWORD :'readonly_password';
GRANT tycoon_owner TO tycoon_migrator;
GRANT CREATE, CONNECT ON DATABASE tycoon_local TO tycoon_owner, tycoon_migrator;
GRANT CONNECT ON DATABASE tycoon_local TO tycoon_app, tycoon_ops, tycoon_readonly;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
EOSQL
