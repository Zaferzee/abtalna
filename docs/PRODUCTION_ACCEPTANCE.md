# Production acceptance

Legend: **PASS** = executed and passed - **FAIL** = executed and failed - **NOT TESTED** = not executed (cannot be executed in the build environment, or not yet run on the target).
Two evidence columns are used everywhere:
* **Sandbox** = the Release package run on Linux (Kestrel, PostgreSQL 16, real HTTPS with a self-signed certificate, a local test SMTP server, real Chromium). It proves the application; it does **not** prove IIS, Windows or Active Directory.
* **Real server** = executed on the production Windows Server. **Nothing has been executed there yet, so every entry is NOT TESTED.**

## 0. Classification

| | |
|---|---|
| Baseline | `2a0de40` (tag `mvp-baseline`); deployment work on branch `claude/sweet-bell-tzevx1` / PR #1 |
| **Current classification** | **B - Ready with Minor Issues** (not A) |
| Rule | **A** requires every row of the gate below to be PASS **on the real server** (SMTP may be accepted by the owner as non-blocking). Arabic/RTL is already PASS; the real-server environment checks are open. |

## 1. Gate for "A - Ready for Production"

| Requirement | Sandbox | Real server | Evidence / what is missing |
|---|---|---|---|
| IIS | NOT TESTED | **NOT TESTED** | Scripts written (`deploy/02-Deploy-Site.ps1`, `03-Verify-Deployment.ps1`); no IIS available here. Run them and record the output. |
| HTTPS | PASS (Kestrel TLS: HTTP->HTTPS redirect, Secure/HttpOnly/SameSite cookies, HSTS on a non-localhost host, security headers, no `Server` header) | **NOT TESTED** | Certificate binding in IIS and client trust not tested. |
| Windows Authentication | NOT TESTED (IIS negotiation) | **NOT TESTED** | Application side tested with an IIS stand-in (see next row). |
| AD mapping | PASS (stand-in identity: auto-provision as Employee only, case-insensitive match, no password stored, other domain rejected, disabled user rejected, directory data used, lookup failure tolerated, admin only via bootstrap) | **NOT TESTED** | Real `DOMAIN\user`, SPN/Kerberos and the optional LDAP look-up not tested. |
| PostgreSQL | PASS (PostgreSQL 16: non-superuser owner + restricted app role, idempotent migration script, app ran with `AutoMigrate=false`, DDL/audit tampering refused) | **NOT TESTED** | Run `deploy/sql` on the real server; verify with `04-verify.sql`. |
| File storage | PASS (uploads outside the app folder, survive restart and redeploy, traversal/probe requests refused, authenticated serving only) | **NOT TESTED** | NTFS permissions for `IIS AppPool\CyberLMS` not tested. |
| Arabic / RTL | PASS (section 7) | NOT TESTED (client fonts/browsers) | Check on a client PC through the real site. |
| Assessments | PASS (automated E2E + UI journey as the restricted DB role over HTTPS) | **NOT TESTED** | Smoke test steps 11-17. |
| Acknowledgments | PASS | **NOT TESTED** | Smoke test steps 18-20. |
| Reports | PASS (on-screen + CSV/Excel, Arabic headers) | **NOT TESTED** | Smoke test step 21. |
| Authorization | PASS (employee blocked from admin, CSRF, domain user never admin) | **NOT TESTED** | Smoke test / failure test F1. |
| Backup | PASS (commands run end-to-end: dump, checksum, restore into an empty database + empty storage, app started, data and files identical) | **NOT TESTED** | `Backup.ps1` / `Restore.ps1` are untested PowerShell wrappers; run both on the server. |
| Production SMTP | PASS against a local relay (STARTTLS + AUTH, Arabic subject/RTL body/sender) | **NOT TESTED** | Needs your relay details (`HANDOVER.md` section 2). May be accepted by the owner as non-blocking. |

## 2. Release / package checks

| Check | Status | Evidence |
|---|---|---|
| Release compilation | PASS | `dotnet build -c Release`: 0 warnings, 0 errors |
| Automated tests | PASS | 22 tests (unit, localization coverage, E2E on PostgreSQL, Windows-mapping stand-in, failure handling), Debug and Release |
| No development-only dependency | PASS | package = published output only; `Microsoft.EntityFrameworkCore.Design` is build-time (`PrivateAssets`); no SDK needed on the server |
| No local-machine path hard-coded | PASS | scan of package config: only `<placeholders>`; storage/log paths are configuration |
| No development database assumed | PASS | start-up refuses to run without `ConnectionStrings:Default`, without `Storage:RootPath`, and with a relative storage path in Production (tested) |
| No secrets committed | PASS | `git grep` for passwords/keys: none in `src/`, `deploy/`, package; sample files contain placeholders |
| No test credentials remain | PASS | test credentials exist only in `tests/` and are absent from the package; `TEST_PG` must be supplied to run tests |
| No test SMTP server required | PASS | SMTP is optional; unset SMTP is handled (notifications disabled with an Arabic message) |
| Developer exception page disabled in Production | PASS | verified with a forced database outage: Arabic error page, no stack trace, no connection details |
| Production logging enabled | PASS | daily log files; verified content (start-up, failed sign-in, SMTP failure, DB outage, missing file); no passwords in logs (grep) |
| Clean IIS package | PASS | `deploy/build-package.sh` -> zip with `app\`, `sql\`, scripts, docs, `SHA256SUMS.txt`; contents listed in `DEPLOYMENT.md` section 3 |

## 3. Failure tests

| # | Test | Sandbox | Real server | Result observed |
|---|---|---|---|---|
| F1 | Employee opens an Admin URL | PASS | NOT TESTED | redirect to Arabic "access denied" page; admin pages never rendered |
| F2 | Invalid upload extension / fake content | PASS | NOT TESTED | rejected with Arabic message, nothing stored |
| F3 | Oversized upload | PASS | NOT TESTED | per-file limit: Arabic message on the form; request above the hard limit: HTTP 413 Arabic page. (IIS rejects >600 MB itself with its own 404.13 page - outside the application) |
| F4 | Missing file on disk | PASS | NOT TESTED | Arabic 404 page, no path leaked, warning in the log |
| F5 | Invalid assessment submission | PASS | NOT TESTED | foreign attempt -> 404; forged option ids/unknown fields score 0; double submit ignored |
| F6 | Expired/invalid antiforgery token | PASS | NOT TESTED | Arabic "invalid request / session expired" page (400) |
| F7 | Database unavailable | PASS | NOT TESTED | Arabic 503 page, `/health` 503, errors logged; recovers by itself when the database returns (no restart) |
| F8 | SMTP unavailable | PASS | NOT TESTED | admin sees an Arabic failure message with the technical reason; queued mail failures logged (host, port, security, count); site unaffected |
| F9 | SMTP certificate not trusted | PASS | NOT TESTED | clear error; install the CA root in the Windows trust store |

## 4. Security checks

| Check | Sandbox | Real server | Evidence |
|---|---|---|---|
| HTTPS | PASS | NOT TESTED | redirect 307 to https; HSTS 1 year (not sent for `localhost` by design) |
| Secure cookies / HttpOnly / SameSite | PASS | NOT TESTED | `CyberLms.Auth`: Secure, HttpOnly, SameSite=Lax; `CyberLms.Csrf`: Secure, HttpOnly, SameSite=Strict; culture cookie Secure on HTTPS |
| Antiforgery | PASS | NOT TESTED | every POST validated (tests F6) |
| Authorization on admin endpoints | PASS | NOT TESTED | policy `Admin` on the whole area |
| Upload restrictions | PASS | NOT TESTED | extension + size + file signature + generated names |
| Production error handling | PASS | NOT TESTED | no stack traces; reference id only |
| Secrets outside repository | PASS | NOT TESTED | env/appsettings.Production.json (ACL-restricted by the deploy script) |
| No directory browsing / file exposure | PASS (app) | NOT TESTED (IIS) | 15 probe URLs (`/appsettings*.json`, `/web.config`, `*.dll`, `/logs/`, `/_keys/`, `..%2f`, `/storage/`) -> 404; `web.config` disables directory browsing and denies `.json/.resx/.pdb` |
| Unnecessary IIS features | NOT TESTED | NOT TESTED | `01-Install-Prerequisites.ps1` installs the minimum and removes unwanted features |
| Security headers | PASS | NOT TESTED | CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, HSTS; `Server`/`X-Powered-By` absent |
| Logging without sensitive data | PASS | NOT TESTED | grep of logs for passwords/connection secrets: none |
| Least-privilege database identity | PASS | NOT TESTED | see gate |

## 5. File storage checks

| Check | Sandbox | Real server |
|---|---|---|
| Uploads work (image, PDF, inline image, branding) | PASS | NOT TESTED |
| Files survive application restart | PASS | NOT TESTED (IIS restart) |
| Files survive redeployment (files replaced, config kept) | PASS (same hash after redeploy; old session cookie still valid) | NOT TESTED |
| Application update does not overwrite uploads | PASS (storage outside app folder; script excludes it) | NOT TESTED |
| Users cannot browse arbitrary files | PASS | NOT TESTED |
| Path traversal prevented | PASS | NOT TESTED |
| Only authorized content served | PASS (login required; drafts hidden from employees) | NOT TESTED |

## 6. The 25-step production smoke test (to be executed on the real server)
Fill the **Real server** column. The Sandbox column shows the equivalent already executed against the Release package.

| # | Step | Sandbox | Real server |
|---|---|---|---|
| 1 | Domain employee opens the site | PASS (stand-in identity) | NOT TESTED |
| 2 | Windows Authentication identifies the employee | NOT TESTED | NOT TESTED |
| 3 | Employee profile resolved/created | PASS (stand-in identity) | NOT TESTED |
| 4 | Admin logs in | PASS | NOT TESTED |
| 5 | Admin creates Arabic cybersecurity content | PASS | NOT TESTED |
| 6 | Admin uploads a PDF | PASS | NOT TESTED |
| 7 | Admin uploads an image | PASS | NOT TESTED |
| 8 | Admin publishes the content | PASS | NOT TESTED |
| 9 | Employee sees the published content | PASS | NOT TESTED |
| 10 | Employee opens the attached file | PASS | NOT TESTED |
| 11 | Admin creates an Arabic assessment | PASS | NOT TESTED |
| 12 | Admin creates questions and answers | PASS | NOT TESTED |
| 13 | Employee completes the assessment | PASS | NOT TESTED |
| 14 | Score calculated correctly | PASS | NOT TESTED |
| 15 | Pass/fail shown in Arabic | PASS | NOT TESTED |
| 16 | Admin sees the employee result | PASS | NOT TESTED |
| 17 | Admin identifies employees who did not attempt | PASS | NOT TESTED |
| 18 | Admin publishes content requiring acknowledgment | PASS | NOT TESTED |
| 19 | Employee acknowledges it | PASS | NOT TESTED |
| 20 | Admin sees the acknowledgment | PASS | NOT TESTED |
| 21 | Report export works | PASS (CSV + Excel) | NOT TESTED |
| 22 | SMTP notification sent and received in Arabic | PASS (local STARTTLS relay) | NOT TESTED |
| 23 | IIS is restarted | NOT TESTED (application restart + redeploy: PASS) | NOT TESTED |
| 24 | Application returns successfully | PASS (`/health`) | NOT TESTED |
| 25 | Existing content, files, results, acknowledgments intact | PASS (after restart, redeploy and restore) | NOT TESTED |

## 7. Arabic / RTL Acceptance (mandatory)

Evidence: automated tests (`tests/CyberLms.Tests`, 22 passing), the complete UI walk-through `tests/ui/arabic-rtl-journey.mjs` (43 screens captured in a real Chromium running with an **English** browser locale, so any text that depended on the browser language would have shown; 0 native browser dialogs, 0 JavaScript errors) and a scan of every visible word. Screens were inspected visually, not only by `dir="rtl"`.

| # | Requirement | Result | Evidence / notes |
|---|---|---|---|
| 1 | User portal fully Arabic | **PASS** | Login, dashboard, content list/details, policies, acknowledgment, assessments (take/result), My Results, change password, 403/404/400/500 pages. |
| 2 | Admin portal fully Arabic | **PASS** | Dashboard, content management + form, assessments, questions, users, reports (3 tabs + attempt details), settings (branding / general / SMTP), audit log, sidebar and header. |
| 3 | RTL verified | **PASS** | `lang="ar" dir="rtl"` + Bootstrap RTL build on all 43 scanned pages; sidebar on the right, breadcrumbs, icons, dropdowns, cards and stat tiles mirrored; numbers/dates/emails/usernames kept LTR with `<bdi>`/`dir="ltr"`. |
| 4 | Forms verified | **PASS** | Labels, hints, selects, switches, color pickers, input groups; browser-native validation turned off so errors come from the server in Arabic; native "Choose file / No file chosen" replaced by an Arabic widget. |
| 5 | Tables verified | **PASS** | Arabic headers, right-aligned, action columns, empty-state rows, status badges. |
| 6 | Assessments verified | **PASS** | Arabic questions/answers/instructions, RTL choices, pass/fail wording (مجتاز / غير مجتاز), Arabic confirm dialog before submit, per-question correct/incorrect. |
| 7 | Reports verified | **PASS** | Arabic filters, labels and status; CSV/Excel headers are Arabic (اسم المستخدم، البريد الإلكتروني، الاختبار، الحالة، تاريخ آخر محاولة ...) with a UTF-8 BOM so Excel shows Arabic correctly; Arabic export file names. |
| 8 | Branding page verified | **PASS** | All branding fields, upload widgets, preview and buttons in Arabic; live-apply verified. |
| 9 | Validation messages verified | **PASS** | DataAnnotations and model-binding messages localized from resources (e.g. «حقل اسم المستخدم مطلوب.»), plus business-rule messages (password policy, duplicate username, question rules). |
| 10 | Error messages verified | **PASS** | Wrong credentials, upload rejections, CSRF/expired session (400), not found (404), access denied, unexpected error - all Arabic with a reference id. |
| 11 | Email templates verified | **PASS** | Subject and body in Arabic, `dir="rtl"`, Arabic salutation and footer; sent through a real SMTP exchange and decoded (see `LocalizationTests` and the manual SMTP run). |

Other Arabic-first requirements:

| Requirement | Result | Notes |
|---|---|---|
| Resource-based localization, no Arabic hard-coded in controllers/services | **PASS** | `Resources/SharedResource.ar.resx`; code/views use English keys (`L["..."]`). A test fails the build if any key used in code/views has no Arabic text, or an Arabic value is missing a placeholder. The only Arabic outside the resx files is the language switch's own label «العربية» (a language's name). |
| Default culture ar-SA | **PASS** | `RequestLocalization` default; English kept as future compatibility (cookie / Settings -> Default language). |
| Dates / numbers | **PASS** | UTC in the database; shown as `dd/MM/yyyy HH:mm`, **Gregorian** (ar-SA's default Hijri calendar is deliberately not used) with Western digits. |
| Rich-text editor | **PASS** | RTL by default, Arabic toolbar tooltips, headings, bold/italic, numbered/bulleted lists, quotes, links, **inline image upload**, RTL toggle; output stored as sanitized semantic HTML. |
| Confirmation dialogs | **PASS** | Arabic modal (تأكيد / إلغاء) instead of the browser's `confirm()`. |

### Minor Arabic/RTL observations (do not block, tracked)
1. In the editor only, numbered-list markers sit slightly differently from the published output (published content renders correctly).
2. Technical tokens intentionally remain in Latin: SMTP, STARTTLS, SSL/TLS, CSV, Excel, PDF, file extensions, `DOMAIN\user`, request-reference ids, user names and e-mail addresses.
3. The language switch shows the other language's own name ("English").
4. The `filename=` fallback of exported files is ASCII-sanitised; browsers use the UTF-8 `filename*` (Arabic) value.
5. Arabic wording should be reviewed once by your cybersecurity/compliance team (terminology list is in `Resources/SharedResource.ar.resx`).


## 8. Open items / blockers for "A"
1. **Run the deployment on the real Windows Server** (`DEPLOYMENT.md`), then `03-Verify-Deployment.ps1`, then section 6 above.
2. **Windows Authentication + AD** on the real domain (`ACTIVE_DIRECTORY.md` test table) - critical.
3. **PostgreSQL** scripts on the real database host (`POSTGRESQL.md`).
4. **Backup and restore** on the server (`BACKUP_RESTORE.md`), including one restore test.
5. **SMTP relay** details and one real Arabic mail received (or owner's written acceptance as non-blocking).
6. Obtain: TLS certificate, SPN (if alias), GPO intranet-zone setting, firewall rules (all listed in `HANDOVER.md` section 2).
7. Known minor limitation: IIS-level rejection of requests above 600 MB shows the IIS error page (not Arabic). The application's own limits (default 500 MB video) are lower and give Arabic messages.

When items 1-5 are PASS on the real server (and 6 resolved), change the classification to **A** and record the date, the person and the evidence (output of `03-Verify-Deployment.ps1`, screenshots of the smoke test).

## 9. Re-running the Arabic UI verification
```bash
# fresh database + app running with Seed__AdminUsername=admin and a Seed__AdminPassword of your choice
npm i playwright-core
OUT=./ui-out BASE=https://localhost:5443 CHROME=/path/to/chrome node tests/ui/arabic-rtl-journey.mjs   # the script has the first-login seed password in a constant near the top: set it to your Seed__AdminPassword
```
