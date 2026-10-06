<#
.SYNOPSIS  One-time: makes the given Active Directory account the first application administrator (does NOT make any other domain user an admin).
           Needs server access + the production configuration (appsettings.Production.json), so it cannot be triggered from the website.
.EXAMPLE   .\Bootstrap-Admin.ps1 -Identity 'CORP\itsec.manager'
After that, administrators are managed inside the application (Admin > Users).
#>
[CmdletBinding()] param([Parameter(Mandatory)][string]$Identity, [string]$AppPath = 'C:\inetpub\cyberlms')
$ErrorActionPreference = 'Stop'
if ($Identity -notmatch '^[A-Za-z0-9._-]+\\[A-Za-z0-9._$-]+$') { throw "Use the form DOMAIN\username" }
Push-Location $AppPath
try { $env:ASPNETCORE_ENVIRONMENT = 'Production'; & dotnet .\CyberLms.Web.dll --bootstrap-admin $Identity; if ($LASTEXITCODE -ne 0) { throw "Bootstrap failed (exit $LASTEXITCODE). See the message above and the application log." } }
finally { Pop-Location }
