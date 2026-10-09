# Cybersecurity Awareness & Compliance LMS (MVP)

Internal, on-premises LMS for publishing cybersecurity policies/controls/training, running short assessments, collecting policy acknowledgments and reporting - sized for ~5,000 users.

**Stack:** ASP.NET Core 10 (MVC) - PostgreSQL - EF Core - Bootstrap 5 (bundled locally, no CDN, RTL ready) - IIS/Windows Server.

## What it does
| Admin | Employee |
|---|---|
| Create/edit/publish content (rich text, images, PDF/Office, video, links) | Dashboard with pending acknowledgments, assessments, results |
| Require acknowledgment, see who did / did not acknowledge, reset | Read content, watch video, open attachments |
| Build assessments (single / true-false / multiple choice), pass mark, attempts | Take assessments, see Passed/Failed immediately |
| Reports (assessment results, attempts, acknowledgments) + CSV/Excel export, email reminders | Acknowledge policies, view own history |
| Users (create, import CSV, roles, reset password), audit log | **Arabic-first, fully RTL** interface (English kept for future use) |
| **Settings -> Branding & Appearance studio** (multi-line names, logos and icons, login page text and image controls, formal theme presets, colors, live preview, readability checks; see [docs/BRANDING.md](docs/BRANDING.md)), SMTP | |

## Quick start (development)
```bash
# PostgreSQL running locally; create DB and role first (see docs/DEPLOYMENT.md section 2)
export ConnectionStrings__Default="Host=localhost;Database=cyberlms;Username=cyberlms;Password=<pw>"
export Seed__AdminUsername=admin Seed__AdminPassword='<choose a strong password>'
dotnet run --project src/CyberLms.Web          # http://localhost:5000 (see console), Development environment
export TEST_PG="Host=localhost;Username=<role that can create databases>;Password=<...>"
dotnet test                                    # integration tests use throw-away PostgreSQL databases
```
Sign in as `admin` (you must change the password), then create users under *Admin -> Users*.

## Layout
```
src/CyberLms.Web/
  Domain/            entities
  Data/              DbContext, migrations, seeder
  Services/          storage, settings/branding, auth, scoring, reports, email, audit
  Controllers/       employee-facing (Home, Content, Assessments, MyResults, Account, Files)
  Areas/Admin/       admin controllers, models, views
  Views/, wwwroot/   UI and local static libraries
tests/CyberLms.Tests unit + localization-coverage + end-to-end HTTP tests (PostgreSQL)
tests/ui/            Playwright walk-through that screenshots every Arabic screen and lists untranslated words
docs/                HANDOVER, DEPLOYMENT, POSTGRESQL, ACTIVE_DIRECTORY, BACKUP_RESTORE, DATABASE, LOCALIZATION, PRODUCTION_ACCEPTANCE, FUTURE_ROADMAP
deploy/              package builder, IIS/PowerShell scripts (install, deploy, verify, bootstrap admin, backup, restore) and PostgreSQL scripts
```

## Security notes
No secrets in the repository (connection string, SMTP password, seed admin password come from environment/secret config). CSRF tokens on every POST, server-side role authorization, HTML sanitization of rich text, strict upload validation (extension + size + file signature, generated storage names, files outside the web root, served only via authenticated endpoints), lockout after repeated failures, secure cookies/HSTS/CSP, no stack traces in production.

## Documentation
[Deployment handover](docs/HANDOVER.md) - [Production acceptance](docs/PRODUCTION_ACCEPTANCE.md) - [Localization](docs/LOCALIZATION.md) - [Deployment](docs/DEPLOYMENT.md) - [Database & ERD](docs/DATABASE.md) - [Active Directory](docs/ACTIVE_DIRECTORY.md) - [Backup/restore](docs/BACKUP_RESTORE.md) - [Roadmap & limitations](docs/FUTURE_ROADMAP.md)
