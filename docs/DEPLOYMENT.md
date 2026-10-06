# Deployment guide - Windows Server + IIS + PostgreSQL + Active Directory

Target: one internal Windows Server (IIS, in-process ASP.NET Core), PostgreSQL 16+, Active Directory domain, HTTPS, Windows Authentication.
Everything below is scripted in `deploy/` (PowerShell, run as Administrator). **Status:** the application, package, database scripts, backup/restore commands, logging, HTTPS behaviour and failure handling were verified in a Linux/Kestrel + PostgreSQL environment; the IIS, Windows Authentication and Active Directory steps are **NOT TESTED** until you run them (see `PRODUCTION_ACCEPTANCE.md`).

## 1. What must be installed on the server
| Item | Version / source |
|---|---|
| Windows Server | 2019 or 2022, domain-joined, fully patched |
| IIS + features | installed by `deploy\01-Install-Prerequisites.ps1` (Web-Server, Static Content, Default Doc, Http Errors, Http Logging, Request Filtering, **Windows Authentication**, Compression, Mgmt Console). Removes/does not install: Directory Browsing, WebDAV, FTP, CGI/ISAPI, ASP, ASP.NET 4.x, Basic/Digest auth, SSI |
| **.NET 10 Hosting Bundle** (includes ASP.NET Core Runtime + ASP.NET Core Module V2) | download `dotnet-hosting-10.0.x-win.exe` from https://dotnet.microsoft.com/download/dotnet/10.0 and copy it to the server |
| PostgreSQL | **16 or newer** (same server or a dedicated DB host) + client tools (`psql`, `pg_dump`, `pg_restore`) on the web server for backup scripts |
| TLS certificate | issued by your internal CA for the site name (SAN = `lms.<domain>`), imported into `LocalMachine\My` |

The .NET SDK is **not** needed on the server (framework-dependent package).

## 2. Values to obtain from the infrastructure team
See the checklist in `HANDOVER.md` section 2. Minimum: site host name, certificate thumbprint, AD NetBIOS domain name, first administrator's AD account, PostgreSQL host/port/TLS policy, storage/log/backup drive paths, SMTP relay details.

## 3. Build the package (on the build machine)
```bash
git checkout <approved commit> && deploy/build-package.sh        # needs the .NET 10 SDK; produces artifacts/cyberlms-<date>-<commit>.zip
dotnet test                                                       # (optional) needs TEST_PG, see README
```
The package contains **only**:
```
app\         the published application (CyberLms.Web.dll + libraries + web.config + wwwroot + ar\ resources + appsettings.json + appsettings.Production.sample.json)
sql\         01-create-roles-and-database.sql, 02-migrate.sql (idempotent), 03-grants.sql, 04-verify.sql
*.ps1        01-Install-Prerequisites, 02-Deploy-Site, 03-Verify-Deployment, Bootstrap-Admin, Backup, Restore
*.md         this guide, POSTGRESQL, ACTIVE_DIRECTORY, BACKUP_RESTORE, HANDOVER
VERSION.txt, SHA256SUMS.txt
```
It does **not** contain: `appsettings.Development.json`, `appsettings.Production.json`, any password, certificate, storage folder, log, `.pdb`, source code or test data. Verify with `SHA256SUMS.txt` after copying to the server.

## 4. Deployment order (new installation)
1. Copy the zip to the server (e.g. `C:\Deploy\`), unzip, check `SHA256SUMS.txt`.
2. `.\01-Install-Prerequisites.ps1 -HostingBundleInstaller C:\Deploy\dotnet-hosting-10.0.x-win.exe`
3. Prepare PostgreSQL (`POSTGRESQL.md`): roles, database, schema, grants, verification.
4. Import the TLS certificate into `LocalMachine\My`; note the thumbprint.
5. Create folders on a data drive **outside** the application folder, e.g. `D:\AppData\CyberLMS\Uploads`, `D:\AppData\CyberLMS\Logs`, `E:\Backups\CyberLMS` (the paths are yours to choose; nothing is hard-coded).
6. `.\02-Deploy-Site.ps1 -PackageDir C:\Deploy\cyberlms-... -HostName lms.company.local -CertThumbprint <thumb> -StoragePath D:\AppData\CyberLMS\Uploads -LogPath D:\AppData\CyberLMS\Logs`
   It creates the pool/site/binding, sets permissions, enables Windows Authentication and disables Anonymous. On first run it creates `C:\inetpub\cyberlms\appsettings.Production.json` from the sample and warns you to edit it.
7. Edit `C:\inetpub\cyberlms\appsettings.Production.json` (section 6). Never commit it.
8. `.\Bootstrap-Admin.ps1 -Identity 'CORP\first.admin'` (see `ACTIVE_DIRECTORY.md`).
9. Recycle the pool (`Restart-WebAppPool CyberLMS`) and run `.\03-Verify-Deployment.ps1 -HostName lms.company.local -StoragePath ... -LogPath ...`.
10. Run the smoke test (`HANDOVER.md` section 8) and record results in `PRODUCTION_ACCEPTANCE.md`.
11. Schedule `Backup.ps1` (`BACKUP_RESTORE.md`) and run one restore test on a scratch server.

## 5. IIS configuration (what `02-Deploy-Site.ps1` sets)
| Setting | Value |
|---|---|
| Application pool `CyberLMS` | .NET CLR version **No Managed Code**; **64-bit** (Enable 32-bit Applications = False); Integrated pipeline; identity **ApplicationPoolIdentity** (`IIS AppPool\CyberLMS`); Load User Profile = True; Idle time-out = 0; Start mode = AlwaysRunning; periodic recycle off (recycle on deployment) |
| Site `CyberLMS` | physical path `C:\inetpub\cyberlms`; bindings: **https :443 host `lms.company.local` + certificate (SNI)**, http :80 (the application redirects to HTTPS and sends HSTS) |
| Authentication (site level) | **Windows Authentication = Enabled** (providers `Negotiate`, then `NTLM`), kernel mode on; **Anonymous Authentication = Disabled**; everything else disabled |
| Hosting model | in-process (`web.config`), `ASPNETCORE_ENVIRONMENT=Production` |
| Request limits | `maxAllowedContentLength` 629145600 (600 MB) in `web.config` - keep it above `Storage:MaxVideoMB` |
| Hardening in `web.config` | directory browsing off, `Server` header removed, `X-Powered-By` removed, `.json/.resx/.pdb` requests denied, `logs` segment hidden |
| HTTPS | certificate bound in IIS; application sets Secure/HttpOnly/SameSite cookies, HSTS (1 year) and security headers in Production |

### Which Windows identity needs which folder (set by the script)
| Folder | Identity | Rights | Why |
|---|---|---|---|
| `C:\inetpub\cyberlms` (application) | `IIS AppPool\CyberLMS` | Read & Execute | runs the code; **cannot modify** it |
| `C:\inetpub\cyberlms\appsettings.Production.json` | `IIS AppPool\CyberLMS` (Read), `SYSTEM`, `Administrators` | Read / Full | secrets; inheritance removed so ordinary users cannot read it |
| Storage `D:\AppData\CyberLMS\Uploads` | `IIS AppPool\CyberLMS` | **Modify** | uploaded files, branding images, data-protection keys (`_keys`) |
| Logs `D:\AppData\CyberLMS\Logs` | `IIS AppPool\CyberLMS` | **Modify** | daily log files |
| Backups `E:\Backups\CyberLMS` | backup service account | Modify | written by `Backup.ps1`; **not** readable by the app pool or ordinary users |
| All of the above | `SYSTEM`, `BUILTIN\Administrators` | Full | administration |
Domain users have **no** file-system access to any of these folders; they only reach the application through IIS.

## 6. Production configuration (`appsettings.Production.json`)
Template: `app\appsettings.Production.sample.json`. Values you must provide:
| Key | Meaning / example |
|---|---|
| `AllowedHosts` | `lms.company.local` |
| `ConnectionStrings:Default` | `Host=dbserver;Port=5432;Database=cyberlms;Username=cyberlms_app;Password=<app role password>;SSL Mode=Require;Maximum Pool Size=50` (**never** a superuser/owner account) |
| `Database:AutoMigrate` | `false` (schema is applied by the DBA with the owner role) |
| `Authentication:Mode` | `Windows` (production); `Both` only if you want local break-glass accounts |
| `Authentication:Windows:AllowedDomains` | `[ "CORP" ]` - NetBIOS domain name(s) allowed to sign in |
| `Authentication:Windows:AutoProvision` | `true`: first valid domain sign-in creates an **employee** profile |
| `Storage:RootPath` | `D:\AppData\CyberLMS\Uploads` (required, absolute, outside the app folder; the application refuses to start without it) |
| `Logging:File:Path` | `D:\AppData\CyberLMS\Logs` |
| `App:BaseUrl` | `https://lms.company.local` (links in e-mails) |
| `Smtp:*` | host, port, sender, sender name, username, security; **password via `Smtp:Password` here or the machine environment variable `Smtp__Password`** - never stored in the database or logged |
| `App:DisplayTimeZone` | `Arab Standard Time` |
Secrets can alternatively be set as machine environment variables (`ConnectionStrings__Default`, `Smtp__Password`) - restart the pool afterwards.

## 7. Updating an existing installation
```powershell
.\02-Deploy-Site.ps1 -PackageDir C:\Deploy\cyberlms-NEW ... -Update
```
The script puts up `app_offline.htm` (Arabic maintenance page), replaces application files, **keeps `appsettings.Production.json`, storage and logs**, and starts the site. If the release contains a new migration: run the new `sql\02-migrate.sql` and `sql\03-grants.sql` as `cyberlms_owner` before step "bring the site back" (the application logs a CRITICAL message and `/health` returns 503 until the schema is current). Take a backup first.

> Releases from the content-authoring update onwards contain the migration `20261006173411_AddContentAcknowledgmentText` (one nullable column, no data change). Apply the new `sql\02-migrate.sql` (it skips what is already applied) and re-run `sql\03-grants.sql`; `04-verify.sql` then lists both migrations.

## 8. Logs and monitoring
* Application log: `Logging:File:Path\cyberlms-YYYYMMDD.log` (30 days). Contains errors, warnings (failed sign-ins with user name + IP, rejected domains, SMTP failures, missing files) and start-up messages. It never contains passwords, connection strings, request bodies or SMTP credentials.
* IIS logs: `%SystemDrive%\inetpub\logs\LogFiles`. ASP.NET Core Module startup failures: Windows Event Log (Application) and, if you set `stdoutLogEnabled="true"` temporarily, `C:\inetpub\cyberlms\logs\stdout*`.
* Monitoring endpoint: `GET https://lms.company.local/health` -> `200 {"status":"healthy"}` or `503` (`database-unreachable`, `migrations-pending`). Requires Windows credentials like every other URL.

## 9. Local development
```bash
export TEST_PG="Host=localhost;Username=<role that can create databases>;Password=<...>"   # tests create/drop throw-away databases
export ConnectionStrings__Default="Host=localhost;Database=cyberlms;Username=...;Password=..."
export Seed__AdminUsername=admin Seed__AdminPassword='<choose a strong password>'
dotnet run --project src/CyberLms.Web     # Development environment: HTTP, files in ./storage-dev
dotnet test
```
