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
| Users (create, import CSV, roles, reset password), audit log | Arabic / English UI with RTL |
| **Settings -> Branding & Appearance** (names, logo, favicon, colors, login page, welcome, footer), SMTP | |

## Quick start (development)
```bash
# PostgreSQL running locally; create DB and role first (see docs/DEPLOYMENT.md section 2)
export ConnectionStrings__Default="Host=localhost;Database=cyberlms;Username=cyberlms;Password=<pw>"
export Seed__AdminUsername=admin Seed__AdminPassword='Change#Me12345'
dotnet run --project src/CyberLms.Web          # http://localhost:5000 (see console), Development environment
dotnet test                                    # integration tests use a throw-away PostgreSQL database
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
tests/CyberLms.Tests unit tests (scoring, CSV) + end-to-end HTTP tests against PostgreSQL
docs/                DEPLOYMENT, DATABASE, ACTIVE_DIRECTORY, BACKUP_RESTORE, FUTURE_ROADMAP
scripts/backup.ps1   nightly backup script
```

## Security notes
No secrets in the repository (connection string, SMTP password, seed admin password come from environment/secret config). CSRF tokens on every POST, server-side role authorization, HTML sanitization of rich text, strict upload validation (extension + size + file signature, generated storage names, files outside the web root, served only via authenticated endpoints), lockout after repeated failures, secure cookies/HSTS/CSP, no stack traces in production.

## Documentation
[Deployment](docs/DEPLOYMENT.md) - [Database & ERD](docs/DATABASE.md) - [Active Directory](docs/ACTIVE_DIRECTORY.md) - [Backup/restore](docs/BACKUP_RESTORE.md) - [Roadmap & limitations](docs/FUTURE_ROADMAP.md)
