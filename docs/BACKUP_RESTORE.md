# Backup and restore

**Status:** the underlying commands (`pg_dump -Fc`, integrity manifest, `pg_restore --clean --if-exists --no-owner`, `03-grants.sql`, file copy) were executed end-to-end on PostgreSQL 16: a backup was restored into a brand-new empty database and a fresh storage folder, the application started on it as the restricted role, and the admin login, report data and an uploaded PDF (identical SHA-256) were confirmed. The PowerShell wrappers `Backup.ps1` / `Restore.ps1` themselves are **NOT TESTED** on Windows.

## What is backed up (all three are required)
| Item | Where | Contains |
|---|---|---|
| PostgreSQL database | `cyberlms` | users, content metadata, assessments, results, acknowledgments, audit, settings |
| Uploaded files | `Storage:RootPath` (e.g. `D:\AppData\CyberLMS\Uploads`) | attachments, inline images, branding images, `_keys` (data-protection keys; losing them only signs everyone out) |
| Configuration | `C:\inetpub\cyberlms\appsettings.Production.json`, `web.config` | connection string, SMTP password -> **the backup folder must be access-restricted** |
Database and files must come from the same run (`Backup.ps1` does this).

## Backup (server team)
```powershell
$pw = Read-Host -AsSecureString "cyberlms_owner password"
.\Backup.ps1 -Dest E:\Backups\CyberLMS -DbHost dbserver -PgPassword $pw -StoragePath D:\AppData\CyberLMS\Uploads
```
Creates `E:\Backups\CyberLMS\<yyyyMMdd_HHmmss>\` with `cyberlms.dump`, `storage\`, config copies, `SHA256SUMS.txt`, `backup.log`; proves the dump is readable (`pg_restore --list`); restricts the folder to SYSTEM/Administrators; deletes runs older than `-KeepDays` (30).
Schedule: Task Scheduler, daily 02:00, service account with read access to the folders and the DB password (store it with `ConvertFrom-SecureString`, readable only by that account). Keep 30 daily + 12 monthly copies and copy them **off the server**. Alert on a non-zero exit code.

## Restore
Use the same PostgreSQL major version or newer. On a rebuilt server first install prerequisites, create roles/database (`POSTGRESQL.md` steps 1 and 4) and deploy the application (`DEPLOYMENT.md`); then:
```powershell
$pw = Read-Host -AsSecureString "cyberlms_owner password"
.\Restore.ps1 -BackupDir E:\Backups\CyberLMS\20261006_020000 -PgPassword $pw -GrantsSql C:\Deploy\...\sql\03-grants.sql -HostName lms.company.local [-RestoreConfig]
```
The script: verifies the checksums -> asks for confirmation -> shows the maintenance page -> `pg_restore --clean --if-exists --no-owner` into the existing (owner-owned) database -> re-applies grants -> mirrors the file backup into the storage folder -> (optionally) restores configuration -> restarts pool/site -> calls `/health`.
Manual equivalent (database only): `pg_restore -h <host> -U cyberlms_owner -d cyberlms --clean --if-exists --no-owner --exit-on-error cyberlms.dump` then `psql ... -f 03-grants.sql`.

## Restore test (do this quarterly, and once before go-live)
Restore into a scratch server/database, start the application, then confirm: admin login, a known content item and its attachment download, an assessment result, an acknowledgment, `/health` = healthy. Record the date in `PRODUCTION_ACCEPTANCE.md`.

## Recovery targets
RPO = interval between backups (24 h with the nightly job; add `pg_dump` runs or WAL archiving if you need less). RTO = time to rebuild/restore (about the time of steps above).
