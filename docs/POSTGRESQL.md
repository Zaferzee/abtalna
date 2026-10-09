# PostgreSQL - production preparation

**Version:** PostgreSQL **16 or newer** (tested on 16). **Encoding** UTF8. Two non-superuser roles; the application never runs as `postgres` and never as the owner.

| Role | Purpose | Privileges |
|---|---|---|
| `cyberlms_owner` | owns the database; applies migrations (DBA only) | owner of `cyberlms` and its tables |
| `cyberlms_app` | runtime identity of the website | `CONNECT`; `SELECT/INSERT/UPDATE/DELETE` on tables; `USAGE/SELECT` on sequences. **No DDL**, no ownership. **Audit log is append-only** (`INSERT, SELECT` only); migration history is read-only |

Files (in the package `sql\`): `01-create-roles-and-database.sql`, `02-migrate.sql` (idempotent: safe to re-run), `03-grants.sql`, `04-verify.sql`.

## Procedure
1. Choose two strong passwords (store in your password vault). On the DB server, as a PostgreSQL administrator:
   ```
   psql -U postgres -h <host> -d postgres -v owner_pw="<owner password>" -v app_pw="<app password>" -f 01-create-roles-and-database.sql
   ```
2. Apply the schema **as the owner**:
   ```
   psql -U cyberlms_owner -h <host> -d cyberlms -v ON_ERROR_STOP=1 -f 02-migrate.sql
   psql -U cyberlms_owner -h <host> -d cyberlms -v ON_ERROR_STOP=1 -f 03-grants.sql
   ```
3. Verify: `psql -U cyberlms_owner -h <host> -d cyberlms -f 04-verify.sql` - expect: both roles `rolsuper = f`; owner `cyberlms_owner`; migrations `20261006104958_InitialCreate`, `20261006173411_AddContentAcknowledgmentText` and `20261009014228_AddContentCompletion` listed; the application role holds only `INSERT, SELECT` on `AuditLogs`; no table owned by another role.
4. Connection string (in `appsettings.Production.json` or `ConnectionStrings__Default`):
   `Host=<host>;Port=5432;Database=cyberlms;Username=cyberlms_app;Password=<app password>;SSL Mode=Require;Maximum Pool Size=50`
   (`SSL Mode=Require` if the server has TLS enabled - recommended when the database is on another host; `Prefer`/`Disable` for a local server.)
5. Network: allow only the web server's address to the PostgreSQL port in `pg_hba.conf` (`hostssl cyberlms cyberlms_app <web-server-ip>/32 scram-sha-256`) and the Windows firewall.
6. Start the site; `GET /health` must return `healthy`. With `Database:AutoMigrate=false` the application only **checks** that the schema is current and logs CRITICAL if not.

## Future releases
Apply the new `02-migrate.sql` and re-run `03-grants.sql` as `cyberlms_owner` **before** switching the site back on (take a backup first).

## What was verified (Linux, PostgreSQL 16)
Roles/grants applied exactly as above; the application role was refused `CREATE TABLE`, `DROP TABLE`, `DELETE FROM "AuditLogs"` and `UPDATE "__EFMigrationsHistory"`; the full UI journey (content, uploads, assessments, acknowledgments, reports, branding) ran **as `cyberlms_app` with `AutoMigrate=false`**; `02-migrate.sql` is idempotent. Not verified: Windows PostgreSQL installer specifics, `pg_hba.conf` of your server.
