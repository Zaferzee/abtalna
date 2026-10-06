# Roadmap

## CURRENT MVP (implemented and tested)
* ASP.NET Core 10 MVC modular monolith, PostgreSQL + EF Core, single deployable (IIS in-process).
* Local authentication (PBKDF2 hashing, lockout, forced first-login password change) **and** a Windows/AD sign-in path (documented, to be verified on the real domain). Roles `Admin` / `User` stored in tables.
* Content management (6 types, sanitized rich text, image/PDF/Office/video uploads, internal links, publish/unpublish, delete rules).
* Policy acknowledgment (unique per user/content/version, admin reset, reports).
* Assessments (single / true-false / multiple choice, passing %, attempt limit, automatic scoring, immutable attempt history, duplicate-to-edit).
* Employee dashboard, admin dashboard with drill-down, reports with filters, CSV and Excel export.
* **Branding & Appearance** from the Admin UI (org/system name, logo, favicon, colors, header/sidebar, login page, welcome text, footer) applied live without rebuild.
* SMTP email (manual/event-based notifications and reminders), basic audit log, Arabic/English UI with RTL, CSV user import.
* Security: CSRF, XSS sanitization + CSP, upload validation (extension, size, magic bytes, generated file names), secure cookies, HSTS, no secrets in source.

## FUTURE TARGET STATE (not implemented - do not assume it exists)
| Area | Target |
|---|---|
| Active Directory | Scheduled LDAP sync of users, departments, managers, enabled/disabled state; nested groups |
| AD group targeting | Assign content/assessments to AD groups, departments, OUs |
| SSO | Kerberos/SAML/OIDC single sign-on and optionally Keycloak/Entra |
| Policy/content versioning | Immutable versions, diff, approval workflow; `Contents.Version` and `UserAcknowledgments.ContentVersion` already exist for this |
| Re-acknowledgment | Periodic or on-new-version re-acknowledgment, grace periods, escalation |
| Campaigns | Training campaigns with audience, start/due dates, progress tracking |
| Recurring training | Annual/quarterly awareness cycles, onboarding tracks |
| Question bank | Tagged bank, difficulty, categories, import/export |
| Randomized exams | Random draws per attempt, shuffled options, timed exams |
| Manager dashboards | Department/team views for managers |
| Advanced reporting | Trends, benchmarking, scheduled/PDF reports, data warehouse export |
| Notifications | Scheduler for automatic reminders/escalations, templates, per-user preferences, Teams/SMS |
| Compliance evidence | Immutable/hash-chained audit, evidence packs, retention policies, SIEM (syslog/CEF) forwarding |
| Certificates | Completion certificates |
| Multilingual content | Per-item Arabic/English content, full translation resources (the MVP localizes the UI chrome and employee pages only; admin screens are English) |
| RBAC | Granular permissions, custom roles, content owners, delegated admins |
| Enterprise compliance | Mapping to NCA-ECC / ISO 27001 / NIST controls with coverage reports |
| Platform | Background job runner, caching, horizontal scale (only if measured need) |

## Known MVP limitations
* Windows Authentication and IIS hosting steps could not be executed in the build environment; verify with DEPLOYMENT.md section 10.
* Admin screens are English-only (employee UI is Arabic/English).
* "Passed/Failed/Not attempted" dashboard figures count user x published-assessment pairs.
* Question edits are locked once an assessment has attempts (duplicate it instead) to keep history trustworthy.
* Uploaded videos are served as-is (no transcoding); use MP4 (H.264) or WebM.
