# Deployment on Windows Server + IIS + PostgreSQL

Target: internal Windows Server, IIS (in-process ASP.NET Core), PostgreSQL 16+, HTTPS.
**Status of verification:** the application was built, migrated, published (`Release`) and exercised end-to-end on Linux/Kestrel against real PostgreSQL. The IIS and Windows-Authentication steps below follow Microsoft's documented procedure but were **not executed in this environment** - run the smoke test in section 10 on the real server.

## 1. Prerequisites on the server
1. Windows Server 2019/2022 with the **Web Server (IIS)** role (include *Windows Authentication* if you will use AD sign-in).
2. **.NET 10 Hosting Bundle** (installs the ASP.NET Core Module V2). Restart IIS afterwards (`iisreset`).
3. **PostgreSQL 16+** (same server or another internal host).
4. A TLS certificate for the site name (internal CA or enterprise PKI).

## 2. PostgreSQL setup
Run in `psql` as `postgres` (choose your own strong password):
```sql
CREATE ROLE cyberlms_app LOGIN PASSWORD '<strong-password>';
CREATE DATABASE cyberlms OWNER cyberlms_app ENCODING 'UTF8';
```
The application role needs to own the database so migrations can run. If your DBA prefers a restricted runtime account, set `Database:AutoMigrate=false` and apply migrations with an owner account (section 6).

## 3. Build the release package (on a build machine with the .NET 10 SDK)
```powershell
dotnet publish src/CyberLms.Web -c Release -o C:\inetpub\cyberlms
```
Copy the output folder to the server (e.g. `C:\inetpub\cyberlms`). It contains `web.config` (in-process hosting, 600 MB upload limit) and a sample production settings file.

## 4. Storage folder
```powershell
New-Item -ItemType Directory D:\CyberLMS\Storage
icacls D:\CyberLMS\Storage /grant "IIS AppPool\CyberLMS:(OI)(CI)M"
```
The path is configured with `Storage:RootPath` (never hard-coded). It is **not** inside the web root and is never served directly - files are streamed through authenticated controllers. Data-protection keys (auth cookies) are kept in `<Storage>\_keys` unless `Security:DataProtectionKeysPath` is set; back them up with the storage folder.

## 5. IIS site
1. Application pool `CyberLMS`: **.NET CLR version = No Managed Code**, identity = ApplicationPoolIdentity (or a service account), *Load User Profile* = True.
2. Site -> physical path `C:\inetpub\cyberlms`, binding **https** (port 443) with your certificate (and optionally http 80 - the app redirects to HTTPS and sends HSTS in production).
3. Grant `IIS AppPool\CyberLMS` *Read & execute* on `C:\inetpub\cyberlms` and *Modify* on `C:\inetpub\cyberlms\logs` (create it if you enable stdout logging).

## 6. Configuration (no secrets in source control)
Create `C:\inetpub\cyberlms\appsettings.Production.json` (start from `appsettings.Production.sample.json`) **or** use machine-level environment variables (`ConnectionStrings__Default`, `Smtp__Password`, `Seed__AdminPassword`; IIS: *Configuration Editor -> system.webServer/aspNetCore/environmentVariables*). Keep the file ACL'd to Administrators + the app-pool identity.

| Setting | Meaning |
|---|---|
| `ConnectionStrings:Default` | `Host=...;Database=cyberlms;Username=cyberlms_app;Password=...` |
| `Authentication:Mode` | `Local`, `Windows` or `Both` (see ACTIVE_DIRECTORY.md) |
| `Storage:RootPath`, `Storage:MaxVideoMB` ... | upload root and per-type size limits (keep `web.config` `maxAllowedContentLength` >= the largest) |
| `Smtp:*` | server, port, sender, username, security; **`Smtp:Password` only here/env, never in the DB**. Host/port/sender can also be edited in *Admin -> Settings -> Email*. |
| `Seed:AdminUsername` / `Seed:AdminPassword` | creates the first administrator **only if no admin exists**; password must change at first login. Remove `Seed:AdminPassword` after the first start. |
| `Security:RequireHttpsCookies`, `Security:RedirectToHttps` | `true` in production (defaults) |
| `App:DisplayTimeZone` | e.g. `Arab Standard Time` (timestamps are stored in UTC) |
| `Database:AutoMigrate` | `true` applies pending migrations at startup |

## 7. Migrations
* Automatic: start the app (default).
* Manual: on a machine with the SDK, `dotnet ef database update --project src/CyberLms.Web --connection "<connection string>"`, or generate a script: `dotnet ef migrations script --idempotent -o migrate.sql` and run it with `psql`.

## 8. HTTPS
Bind the certificate in IIS (*Bindings -> Add -> https*). Test `https://<host>/`. In production the app sets `Secure` cookies, HSTS, and a CSP / security headers; stack traces are never shown (`/Home/Error` shows only a request id). Application logs go to the Windows/IIS logs (enable `stdoutLogEnabled` temporarily in `web.config` for troubleshooting).

## 9. SMTP
Configure `Smtp:*` (and the password via secret) then *Admin -> Settings -> Email -> Send test email*. Notifications are sent from a background queue; failures are logged, never block the UI.

## 10. Smoke test after deployment
1. Browse to `https://<host>/` -> login page appears (HTTP redirects to HTTPS).
2. Sign in as the seeded admin, change the password.
3. *Settings*: set organization name, logo, colors -> visible immediately.
4. Create content with a PDF/image/video, publish; sign in as an employee and open it.
5. Create an assessment + questions; take it as the employee; check *Reports* and export CSV/Excel.
6. Upload a >30 MB video to confirm the upload limit configuration.

## 10b. Arabic / RTL check
After the smoke test, run section 1 of `PRODUCTION_ACCEPTANCE.md` on the server URL (login page, an admin screen, an assessment, a report export, a test e-mail) and record the results. Client PCs need an Arabic-capable font (Segoe UI/Tahoma are standard on Windows).

## 11. Updating
Stop the site (or drop `app_offline.htm` into the folder), copy the new publish output over the old one **keeping `appsettings.Production.json`**, start again. Migrations run automatically.

## Local development
```bash
export ConnectionStrings__Default="Host=localhost;Database=cyberlms;Username=cyberlms;Password=..."
export Seed__AdminUsername=admin Seed__AdminPassword='Change#Me12345'
dotnet run --project src/CyberLms.Web        # Development environment: HTTP, files in ./storage-dev
dotnet test                                   # needs PostgreSQL (TEST_PG env var overrides the server connection)
```
