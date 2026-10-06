-- Verification (run as cyberlms_owner or postgres):
--   psql -U cyberlms_owner -h <host> -d cyberlms -f 04-verify.sql
\echo '--- roles (expect rolsuper = f for both) ---'
SELECT rolname, rolsuper, rolcreatedb, rolcreaterole FROM pg_roles WHERE rolname IN ('cyberlms_owner','cyberlms_app') ORDER BY 1;
\echo '--- database owner (expect cyberlms_owner) ---'
SELECT datname, pg_get_userbyid(datdba) AS owner FROM pg_database WHERE datname = 'cyberlms';
\echo '--- migrations applied ---'
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;
\echo '--- application role privileges on the audit table (expect only INSERT, SELECT) ---'
SELECT privilege_type FROM information_schema.role_table_grants WHERE grantee = 'cyberlms_app' AND table_name = 'AuditLogs' ORDER BY 1;
\echo '--- tables owned by something other than cyberlms_owner (expect none) ---'
SELECT tablename, tableowner FROM pg_tables WHERE schemaname = 'public' AND tableowner <> 'cyberlms_owner';
