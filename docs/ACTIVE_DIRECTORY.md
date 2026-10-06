# Active Directory / Windows Authentication

## Design
Authentication is separated from the application user record:

* `Authentication:Mode` = `Local` (password), `Windows` (AD), or `Both`.
* Both paths end in the same place: an application `User` row + a secure cookie. **Roles and permissions always come from the application database**, never from the sign-in method.
* AD passwords are never seen or stored by the application. Windows sign-in uses Kerberos/NTLM negotiated by IIS (`Negotiate`).
* A Windows user is mapped by `DOMAIN\sam` to `Users.Username` (`AuthSource = Windows`). With `Authentication:Windows:AutoProvision=true` (default) the first successful Windows sign-in creates an employee account; set it to `false` to allow only pre-created/imported users (*Admin -> Users -> New user / Import CSV* with sign-in type "Windows").
* Promote people to administrators from *Admin -> Users* (the first admin is seeded locally).

## IIS setup (documented, not testable in the build sandbox)
1. Server Manager -> add role service **IIS -> Security -> Windows Authentication**.
2. IIS Manager -> Site -> *Authentication*: enable **Windows Authentication** **and keep Anonymous Authentication enabled** (the app challenges only on `/Account/WindowsLogin`; other pages use the cookie).
3. Windows Authentication -> *Providers*: `Negotiate` first, then `NTLM`.
4. Join the server to the domain; for Kerberos, register an SPN if the site runs under a domain service account: `setspn -S HTTP/lms.company.local DOMAIN\svc-lms`. With ApplicationPoolIdentity on a domain-joined server no SPN work is needed.
5. Set `Authentication:Mode` to `Both` (or `Windows`), recycle the app pool. The login page then shows **Sign in with Windows account**.
6. Make the site part of the *Local intranet* zone (GPO) so browsers send credentials silently.

Under Kestrel (development / non-IIS) the same endpoint uses `Microsoft.AspNetCore.Authentication.Negotiate`.

## Not in the MVP (see FUTURE_ROADMAP.md)
LDAP user/department/group synchronization, AD-group-based targeting, SSO (SAML/OIDC). The `Users.ExternalId`/`AuthSource` columns and the role tables exist so these can be added without a rewrite.

## Troubleshooting
* Browser prompts for credentials: site not in Intranet zone, or Windows Auth disabled.
* 401 loops: Anonymous Authentication disabled, or provider order wrong.
* "Your account has not been enabled": `AutoProvision=false` and the user is not in the Users list (use exact `DOMAIN\sam`).
