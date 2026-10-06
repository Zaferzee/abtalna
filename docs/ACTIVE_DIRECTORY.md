# Windows Authentication / Active Directory

**Status:** the application-side mapping is verified by automated tests with a stand-in for IIS authentication (`WindowsAuthTests`); IIS negotiation (Kerberos/NTLM) and the optional LDAP look-up are **NOT TESTED** until run on your domain.

## How it works
1. IIS authenticates the browser (Kerberos via `Negotiate`, NTLM fallback). **Anonymous Authentication is disabled** for the site, so no request reaches the application without a domain identity.
2. The application sees the identity as `DOMAIN\username` and (on first request, at `/Account/WindowsLogin`) maps it to a row in its own `Users` table (case-insensitive key `domain\user`), then issues its own HttpOnly/Secure cookie. In `Windows` mode the login page forwards silently to this step - the employee just opens the site.
3. **First valid domain sign-in creates the profile automatically** (`Authentication:Windows:AutoProvision=true`) with the **Employee (`User`) role only**. Display name and e-mail are read from AD when possible (below). Set it to `false` to allow only pre-created/imported users.
4. `Authentication:Windows:AllowedDomains` (e.g. `["CORP"]`) rejects identities from any other domain or from local machine accounts, with an Arabic message and a log warning.
5. **No password of any kind is stored for Windows accounts** (`PasswordHash` is null). AD passwords never reach the application.
6. Disabling a user in *Admin -> Users* blocks them immediately (re-checked every minute), independent of AD.

## Authorization is independent of AD
Being a domain user never makes someone an administrator. Roles live only in the application database (`UserRoles`). Administrators are made in exactly two ways:
* **First administrator (bootstrap), from the server console** - requires access to the server and its production configuration, so it cannot be done through the website:
  ```powershell
  cd C:\inetpub\cyberlms
  .\Bootstrap-Admin.ps1 -Identity 'CORP\first.admin'
  # (equivalent: dotnet .\CyberLms.Web.dll --bootstrap-admin "CORP\first.admin")
  ```
  Creates the account if needed (Windows sign-in, no password) and grants Administrator. Idempotent, written to the audit log (`ADMIN_BOOTSTRAPPED`) and the application log. No source edit, no permanent hard-coded user, no "all domain users are admin". Refuses to touch an existing *local* account.
* **Afterwards, inside the application:** an administrator opens *Admin -> Users*, creates/edits a user and ticks "Administrator" (a Windows user may be pre-created as `DOMAIN\sam` with sign-in type "Windows").
Break-glass: run `Bootstrap-Admin.ps1` again from the server for any account.

## What is read from AD (optional, no AD sync)
On the first sign-in the application tries a read-only LDAP query (`sAMAccountName`) **as the application-pool identity** (no stored credentials) to fill **display name** and **e-mail**. If it fails (permissions, firewall, non-domain host) a warning is logged and the profile is created with the user name only - sign-in is never blocked. Disable with `Authentication:Windows:LookupDirectory=false`; set `Authentication:Windows:LdapPath` (e.g. `LDAP://DC=corp,DC=local`) for multi-domain forests. Not built (future): scheduled sync, departments/managers, AD groups.

## IIS / AD checklist (infrastructure team)
1. Server joined to the domain; site name `lms.company.local` resolves in DNS.
2. Windows Authentication enabled with providers `Negotiate`, `NTLM`; Anonymous disabled (done by `02-Deploy-Site.ps1`, verified by `03-Verify-Deployment.ps1`).
3. Kerberos: with `ApplicationPoolIdentity` the machine account is the service identity. If users browse to a **CNAME/alias** rather than the server's own name, register the SPN once: `setspn -S HTTP/lms.company.local <SERVERNAME>$` (and `HTTP/lms` for the short name). Without a valid SPN the browser silently falls back to NTLM (works, less secure).
4. Group Policy: add `https://lms.company.local` to **Local intranet** zone ("Automatic logon only in Intranet zone") so browsers send credentials without prompting.
5. Firewall: 443 from client networks to the web server; LDAP/Kerberos from the web server to domain controllers (standard domain-member traffic).
6. Decide the NetBIOS domain name(s) for `AllowedDomains` (`echo %USERDOMAIN%` on a domain PC).

## Test (on the real domain)
| # | Test | Expected |
|---|---|---|
| 1 | Domain employee opens `https://lms.company.local` | no password prompt; Arabic dashboard; header shows the AD display name |
| 2 | Same user, different case / second browser | same profile (no duplicate in *Admin -> Users*) |
| 3 | User from another domain / local server account | Arabic "domain not permitted" message |
| 4 | Employee opens `/Admin/Dashboard` | access denied page (not an admin) |
| 5 | `Bootstrap-Admin.ps1` then that user reopens the site | Admin menu appears; audit log shows `ADMIN_BOOTSTRAPPED` |
| 6 | Disable the user in the application | next request is signed out/denied within ~1 minute |
