<#
.SYNOPSIS  Read-only checks of the IIS deployment. Prints PASS / FAIL for each item. Run on the web server as Administrator, as a domain user (for the Windows-auth request).
.EXAMPLE   .\03-Verify-Deployment.ps1 -HostName lms.company.local -StoragePath D:\AppData\CyberLMS\Uploads -LogPath D:\AppData\CyberLMS\Logs
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$HostName, [Parameter(Mandatory)][string]$StoragePath, [Parameter(Mandatory)][string]$LogPath,
      [string]$SiteName = 'CyberLMS', [string]$PoolName = 'CyberLMS', [string]$AppPath = 'C:\inetpub\cyberlms')
Import-Module WebAdministration
$results = New-Object System.Collections.Generic.List[object]
function Check($name, [scriptblock]$test) {
    try { $r = & $test; $ok = [bool]$r } catch { $ok = $false; $r = $_.Exception.Message }
    $results.Add([pscustomobject]@{ Check = $name; Result = $(if ($ok) { 'PASS' } else { 'FAIL' }); Detail = $(if ($ok) { '' } else { "$r" }) })
}
$pool = "IIS:\AppPools\$PoolName"; $acct = "IIS AppPool\$PoolName"
Check 'ASP.NET Core Module V2 installed'            { Get-WebGlobalModule | Where-Object Name -eq 'AspNetCoreModuleV2' }
Check 'Pool: No Managed Code'                        { (Get-ItemProperty $pool).managedRuntimeVersion -eq '' }
Check 'Pool: 64-bit'                                 { (Get-ItemProperty $pool).enable32BitAppOnWin64 -eq $false }
Check 'Pool: ApplicationPoolIdentity'                { (Get-ItemProperty $pool).processModel.identityType -eq 'ApplicationPoolIdentity' }
Check 'Pool started'                                 { (Get-WebAppPoolState $PoolName).Value -eq 'Started' }
Check 'Site started'                                 { (Get-WebsiteState $SiteName).Value -eq 'Started' }
Check 'HTTPS binding with certificate'               { Get-WebBinding -Name $SiteName -Protocol https | Where-Object { $_.certificateHash } }
Check 'Windows Authentication enabled'               { (Get-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $SiteName -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name enabled).Value }
Check 'Anonymous Authentication disabled'            { -not (Get-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $SiteName -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled).Value }
Check 'Directory browsing disabled'                  { -not (Get-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $SiteName -Filter 'system.webServer/directoryBrowse' -Name enabled).Value }
Check 'Storage path outside app folder'              { -not $StoragePath.StartsWith($AppPath, [StringComparison]::OrdinalIgnoreCase) }
Check 'App pool can modify storage'                  { (& icacls $StoragePath) -match [regex]::Escape($acct) -and ((& icacls $StoragePath) -join ' ') -match '\(M\)|\(F\)' }
Check 'App pool can modify logs'                     { (& icacls $LogPath) -match [regex]::Escape($acct) }
Check 'App pool cannot write to app folder'          { -not (((& icacls $AppPath) | Select-String ([regex]::Escape($acct))) -match '\((M|F|W)\)') }
Check 'appsettings.Production.json present'         { Test-Path (Join-Path $AppPath 'appsettings.Production.json') }
Check 'No Development settings deployed'            { -not (Test-Path (Join-Path $AppPath 'appsettings.Development.json')) }
Check 'web.config present'                           { Test-Path (Join-Path $AppPath 'web.config') }

# live requests (use the current Windows identity)
$base = "https://$HostName"
try {
    $h = Invoke-WebRequest -Uri "$base/health" -UseDefaultCredentials -UseBasicParsing
    Check '/health returns 200 healthy (database reachable, schema current)' { $h.StatusCode -eq 200 -and $h.Content -match 'healthy' }
    $r = Invoke-WebRequest -Uri "$base/Account/Login" -UseDefaultCredentials -UseBasicParsing -MaximumRedirection 5
    Check 'Application page served over HTTPS'           { $r.StatusCode -eq 200 }
    Check 'Security header: X-Content-Type-Options'      { $r.Headers['X-Content-Type-Options'] -eq 'nosniff' }
    Check 'Security header: Content-Security-Policy'     { [bool]$r.Headers['Content-Security-Policy'] }
    Check 'Security header: Strict-Transport-Security'   { [bool]$r.Headers['Strict-Transport-Security'] }
    Check 'Server / X-Powered-By headers hidden'         { -not $r.Headers['Server'] -and -not $r.Headers['X-Powered-By'] }
    Check 'Arabic RTL page'                              { $r.Content -match 'dir="rtl"' -and $r.Content -match 'lang="ar"' }
} catch { Check 'HTTPS requests to the site' { throw $_ } }
try { $a = Invoke-WebRequest -Uri "https://$HostName/appsettings.Production.json" -UseDefaultCredentials -UseBasicParsing -ErrorAction Stop; Check 'Configuration file is NOT downloadable' { $false } }
catch { Check 'Configuration file is NOT downloadable' { $_.Exception.Response.StatusCode.value__ -in 404,403 } }
try { $c = Invoke-WebRequest -Uri ("http://$HostName/Account/Login") -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop; Check 'HTTP redirects to HTTPS' { $false } }
catch { Check 'HTTP redirects to HTTPS' { $_.Exception.Response.StatusCode.value__ -in 301,302,307,308 } }

$results | Format-Table -AutoSize
$fail = ($results | Where-Object Result -eq 'FAIL').Count
Write-Host ("{0} checks, {1} FAILED" -f $results.Count, $fail) -ForegroundColor $(if ($fail) { 'Red' } else { 'Green' })
exit $fail
