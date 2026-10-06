# Backup and restore

Back up **three** things: the database, the uploaded files (including `_keys`), and the production configuration.

## 1. PostgreSQL database
Backup (custom format, compressed):
```powershell
$env:PGPASSWORD = "<password>"
& "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe" -h localhost -U cyberlms_app -Fc -f D:\Backups\cyberlms_$(Get-Date -f yyyyMMdd_HHmm).dump cyberlms
```
Restore into an empty database:
```powershell
& "...\psql.exe" -U postgres -c "CREATE DATABASE cyberlms OWNER cyberlms_app ENCODING 'UTF8';"
& "...\pg_restore.exe" -h localhost -U cyberlms_app -d cyberlms --no-owner --clean --if-exists D:\Backups\cyberlms_XXXX.dump
```
`scripts/backup.ps1` automates all three backups (see below).

## 2. Uploaded files
Everything under `Storage:RootPath` (default `D:\CyberLMS\Storage`): content attachments, branding images and `_keys` (data-protection keys; losing them only forces everyone to sign in again).
```powershell
robocopy D:\CyberLMS\Storage D:\Backups\storage /MIR /R:2 /W:5
```
Restore: stop the site, `robocopy D:\Backups\storage D:\CyberLMS\Storage /MIR`, start the site. Keep database and file backups from the same time window - attachment rows reference file paths.

## 3. Production configuration
Copy `appsettings.Production.json` (and any IIS environment variables / `web.config` customizations) to a **protected** location - it contains secrets. Do not store it in source control.

## Schedule & retention
Run `scripts/backup.ps1` nightly from Task Scheduler (e.g. 02:00), keep 14 daily + 12 monthly copies, copy to a second machine, and **test a restore quarterly** on a scratch server.

## Disaster recovery order
1. Install IIS + Hosting Bundle + PostgreSQL. 2. Restore the database. 3. Restore the storage folder. 4. Deploy the release package. 5. Restore `appsettings.Production.json`. 6. Start the site and run the smoke test from DEPLOYMENT.md.
