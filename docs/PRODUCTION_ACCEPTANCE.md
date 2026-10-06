# Production acceptance

Readiness classification used in this document:

* **A. Ready for Production** - every mandatory item below is PASS, including the Arabic / RTL section, and the deployment checks were executed on the real target.
* **B. Ready with Minor Issues** - all mandatory Arabic/RTL items PASS; remaining issues are minor or environment checks still to be executed on the real server.
* **C. Not Ready** - any mandatory item FAILS, or any major user/administrator screen is still English.

> **Arabic is a core requirement.** If any major administrative or user screen remains English the application must not be classified A.

## Current classification: **B. Ready with Minor Issues**

Why not A: IIS hosting and Windows/Active Directory authentication could not be executed in the build environment (see "Environment checks still open"). Why not C: every mandatory Arabic/RTL item below is PASS and the functional/security checks pass on PostgreSQL.

## 1. Arabic / RTL Acceptance (mandatory)

Evidence: automated tests (`tests/CyberLms.Tests`, 15 passing), the complete UI walk-through `tests/ui/arabic-rtl-journey.mjs` (43 screens captured in a real Chromium running with an **English** browser locale, so any text that depended on the browser language would have shown; 0 native browser dialogs, 0 JavaScript errors) and a scan of every visible word. Screens were inspected visually, not only by `dir="rtl"`.

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

## 2. Functional acceptance - PASS (automated, PostgreSQL)
Admin login -> content (+ files, links, inline images) -> publish -> employee reads -> assessment (3 question types) -> automatic scoring -> pass/fail -> acknowledgment (no duplicates) -> reports (passed/failed/not attempted, acknowledged/not) -> CSV/Excel export -> audit log -> live branding. See `EndToEndTests`.

## 3. Security acceptance - PASS (automated)
CSRF on every POST, server-side authorization, lockout, XSS sanitization + CSP, upload validation (extension, size, signature, generated names), no secrets in source, production error handling. See `EndToEndTests.Security_controls`.

## 4. Environment checks still open (must be executed on the real server)
| Check | Status |
|---|---|
| IIS in-process hosting, 600 MB upload limit, HTTPS binding (DEPLOYMENT.md section 10 smoke test) | **NOT VERIFIED** |
| Windows Authentication / Active Directory sign-in (ACTIVE_DIRECTORY.md) | **NOT VERIFIED** |
| Real SMTP relay with your server and certificate | **NOT VERIFIED** (verified against a local test SMTP server) |
| Arabic fonts on client PCs (Segoe UI / Tahoma ship with Windows) | Expected OK - confirm on a client |
| Backup/restore on the production host (commands verified on Linux PostgreSQL) | **NOT VERIFIED** on Windows |

When these are executed and recorded as PASS, and the minor observations are accepted, the classification can move to **A. Ready for Production**.

## 5. Re-running the Arabic UI verification
```bash
# fresh DB + app running on http://localhost:5080 with Seed__AdminPassword='Admin#Pass12345'
npm i playwright-core
OUT=./ui-out CHROME=/path/to/chrome node tests/ui/arabic-rtl-journey.mjs
# inspect ./ui-out/shots/*.png and ./ui-out/scan.json (visible Latin words per page)
```
