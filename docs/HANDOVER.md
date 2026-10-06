# Deployment handover - CyberLMS (Arabic MVP) on Windows Server / IIS / PostgreSQL / Active Directory

Package: build with `deploy/build-package.sh` from the approved commit (baseline `2a0de40`, deployment work in PR #1). Detailed procedures: `DEPLOYMENT.md`, `POSTGRESQL.md`, `ACTIVE_DIRECTORY.md`, `BACKUP_RESTORE.md`, `PRODUCTION_ACCEPTANCE.md`.

## 1. What must be installed on the server
1. Windows Server 2019/2022, domain-joined. 2. IIS with Windows Authentication (`01-Install-Prerequisites.ps1`). 3. **.NET 10 Hosting Bundle** (`dotnet-hosting-10.0.x-win.exe`). 4. PostgreSQL 16+ (server or separate host) and its client tools on the web server. 5. A TLS certificate for the site name in `LocalMachine\My`. (No .NET SDK, no Node, no Visual Studio.)

## 2. Values to obtain from the infrastructure / e-mail teams
| # | Value | From | Example |
|---|---|---|---|
| 1 | Site host name (DNS record -> web server) | Network/DNS | `lms.company.local` |
| 2 | TLS certificate (SAN = host name) from the internal CA + its **thumbprint**; CA root trusted on clients | PKI | `0123...ABCD` |
| 3 | AD **NetBIOS domain name(s)** allowed to sign in | AD team | `CORP` |
| 4 | **First administrator's AD account** | Cybersecurity owner | `CORP\first.admin` |
| 5 | SPN registered if users use an alias (CNAME) | AD team | `setspn -S HTTP/lms.company.local WEBSERVER$` |
| 6 | GPO: site in *Local intranet* zone (silent Windows sign-in) | AD team | `https://lms.company.local` |
| 7 | PostgreSQL host, port, TLS policy; permission to create 2 roles + 1 database | DBA | `dbserver:5432`, `SSL Mode=Require` |
| 8 | Two strong passwords (owner, app) - kept in your vault | DBA | - |
| 9 | Drive paths: uploads, logs, backups, backup destination off-server | Infra | `D:\AppData\CyberLMS\Uploads`, `D:\AppData\CyberLMS\Logs`, `E:\Backups\CyberLMS` |
| 10 | Firewall: clients -> web 443 (and 80 for redirect); web -> DB 5432; web -> domain controllers (LDAP/Kerberos) | Network | - |
| 11 | **SMTP/Exchange relay**: host, port, security (None/STARTTLS/SSL), is authentication required (username + password), sender address/name allowed to relay from the web server's IP, internal CA root if TLS | E-mail team | `mail.company.local:25`, sender `lms@company.local` |
| 12 | Service account for the backup task | Infra | `CORP\svc-lms-backup` |

## 3. Deployment order
1. Copy + unzip the package, verify `SHA256SUMS.txt`. 2. `01-Install-Prerequisites.ps1`. 3. PostgreSQL scripts (section 7). 4. Import certificate. 5. Create data folders. 6. `02-Deploy-Site.ps1`. 7. Edit `appsettings.Production.json` (section 4). 8. `Bootstrap-Admin.ps1`. 9. Recycle pool, `03-Verify-Deployment.ps1`. 10. Smoke test (section 8). 11. Schedule `Backup.ps1`; do one restore test. 12. Record results in `PRODUCTION_ACCEPTANCE.md`.

## 4. Configuration values you must provide (`C:\inetpub\cyberlms\appsettings.Production.json`)
```json
{
  "AllowedHosts": "lms.company.local",
  "ConnectionStrings": { "Default": "Host=dbserver;Port=5432;Database=cyberlms;Username=cyberlms_app;Password=<APP PASSWORD>;SSL Mode=Require;Maximum Pool Size=50" },
  "Database": { "AutoMigrate": false },
  "Authentication": { "Mode": "Windows", "Windows": { "AutoProvision": true, "AllowedDomains": [ "CORP" ], "LookupDirectory": true } },
  "Storage": { "RootPath": "D:\\AppData\\CyberLMS\\Uploads" },
  "Logging": { "File": { "Enabled": true, "Path": "D:\\AppData\\CyberLMS\\Logs", "RetentionDays": 30 } },
  "App": { "DisplayTimeZone": "Arab Standard Time", "BaseUrl": "https://lms.company.local" },
  "Smtp": { "Host": "<relay>", "Port": 25, "Sender": "lms@company.local", "SenderName": "منصة التوعية بالأمن السيبراني", "Username": "", "Password": "", "Security": "Auto" }
}
```
Do **not** set `Seed:*` in production (no local admin password exists); the first admin comes from `Bootstrap-Admin.ps1`. The application refuses to start without a connection string or an absolute `Storage:RootPath`.

## 5. IIS configuration (applied by `02-Deploy-Site.ps1`)
Pool `CyberLMS`: No Managed Code, 64-bit, ApplicationPoolIdentity, Load User Profile, idle time-out 0, AlwaysRunning. Site `CyberLMS` -> `C:\inetpub\cyberlms`; bindings https:443 `lms.company.local` + certificate (SNI), http:80 (redirects). **Windows Authentication on (Negotiate, NTLM); Anonymous off.** In-process hosting; 600 MB request limit; directory browsing off; server headers removed. Permissions: app folder Read/Execute, `appsettings.Production.json` Read (app pool) + Administrators, storage **Modify**, logs **Modify** - all for `IIS AppPool\CyberLMS`; Administrators/SYSTEM Full; no domain-user access.

## 6. Active Directory configuration
Server domain-joined; SPN if alias; GPO intranet zone; `AllowedDomains=["CORP"]`; first admin via `Bootstrap-Admin.ps1 -Identity 'CORP\first.admin'`. A domain user becomes an **employee** automatically on first sign-in and **never an admin** automatically; admins are then managed in *Admin -> Users*. No AD passwords are stored; display name/e-mail are read from AD when permitted (otherwise left blank). Details and the 6-step AD test: `ACTIVE_DIRECTORY.md`.

## 7. PostgreSQL preparation
```
psql -U postgres -h dbserver -d postgres -v owner_pw="<owner pw>" -v app_pw="<app pw>" -f sql\01-create-roles-and-database.sql
psql -U cyberlms_owner -h dbserver -d cyberlms -v ON_ERROR_STOP=1 -f sql\02-migrate.sql
psql -U cyberlms_owner -h dbserver -d cyberlms -v ON_ERROR_STOP=1 -f sql\03-grants.sql
psql -U cyberlms_owner -h dbserver -d cyberlms -f sql\04-verify.sql        # roles not superuser; app role INSERT,SELECT only on AuditLogs
```
Runtime identity = `cyberlms_app` (never `postgres`, never the owner).

## 8. Smoke test
The 25 steps are in `PRODUCTION_ACCEPTANCE.md` section 6 (with the sandbox result next to each). Execute them in order from a **domain PC** with a domain employee account and the first administrator account, with the Windows Server's SMTP relay. Also run failure tests F1-F9 (section 3 there) and `03-Verify-Deployment.ps1`. Attach the output and screenshots.

## 9. Current PASS / FAIL / NOT TESTED
| Area | Sandbox (Linux, Release package) | Real server |
|---|---|---|
| Release build, 22 automated tests, package contents, config hygiene | PASS | - |
| IIS | NOT TESTED | NOT TESTED |
| HTTPS (app behaviour) / IIS certificate binding | PASS / NOT TESTED | NOT TESTED |
| Windows Authentication (IIS negotiation) | NOT TESTED | NOT TESTED |
| AD mapping logic (stand-in) / real AD + LDAP lookup | PASS / NOT TESTED | NOT TESTED |
| PostgreSQL least privilege, migrations, runtime as restricted role | PASS | NOT TESTED |
| File storage (persistence, redeploy, traversal) | PASS | NOT TESTED |
| Arabic / RTL | PASS | NOT TESTED (client check) |
| Assessments, acknowledgments, reports, authorization | PASS | NOT TESTED |
| Failure tests F1-F9 | PASS | NOT TESTED |
| Backup + restore (commands) / PowerShell wrappers | PASS / NOT TESTED | NOT TESTED |
| SMTP Arabic RTL, STARTTLS + AUTH (local relay) | PASS | NOT TESTED |
| PowerShell scripts (`deploy\*.ps1`) | NOT TESTED | NOT TESTED |
| **Overall** | | **B - Ready with Minor Issues** |

## 10. Blockers for Production "A"
1. Nothing has been executed on the real Windows Server yet: IIS, HTTPS binding, Windows Authentication, AD mapping, PostgreSQL scripts, file storage permissions, backup/restore, SMTP. These are the **only** blockers; no known application defect remains open.
2. The PowerShell scripts were written but could not be executed here - expect to correct small environment-specific details during the first run and record them.
3. Items from section 2 (certificate, SPN/GPO, DB access, SMTP relay) must be supplied by the infrastructure/e-mail teams.
