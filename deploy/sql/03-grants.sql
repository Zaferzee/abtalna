-- Run as cyberlms_owner (or postgres) AFTER 02-migrate.sql, and again after every future migration:
--   psql -U cyberlms_owner -h <host> -d cyberlms -f 03-grants.sql
-- Gives the website only what it needs: read/write rows, no DDL, no ownership.
GRANT USAGE ON SCHEMA public TO cyberlms_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO cyberlms_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO cyberlms_app;

-- The audit trail is append-only for the application.
REVOKE UPDATE, DELETE, TRUNCATE ON "AuditLogs" FROM cyberlms_app;
-- Migration history is read-only for the application (it only checks that the schema is current).
REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON "__EFMigrationsHistory" FROM cyberlms_app;

-- Tables/sequences created by future migrations (run by cyberlms_owner) are accessible too;
-- re-run the two REVOKE statements above after such a migration if the new table needs the same restriction.
ALTER DEFAULT PRIVILEGES FOR ROLE cyberlms_owner IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO cyberlms_app;
ALTER DEFAULT PRIVILEGES FOR ROLE cyberlms_owner IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO cyberlms_app;
