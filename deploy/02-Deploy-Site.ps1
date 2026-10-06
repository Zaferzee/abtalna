<#
.SYNOPSIS  Creates (or updates) the CyberLMS IIS site, application pool, folders, permissions, HTTPS binding and Windows Authentication.
.EXAMPLE   .\02-Deploy-Site.ps1 -PackageDir C:\Deploy\cyberlms-20261006-1200-abc1234 -HostName lms.company.local -CertThumbprint 0123...ABCD `
                               -StoragePath D:\AppData\CyberLMS\Uploads -LogPath D:\AppData\CyberLMS\Logs
.EXAMPLE   .\02-Deploy-Site.ps1 -PackageDir C:\Deploy\cyberlms-NEW -HostName lms.company.local -CertThumbprint ... -StoragePath ... -LogPath ... -Update
           (update: puts the site offline, replaces application files, KEEPS appsettings.Production.json, storage and logs, brings the site back)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDir,          # unzipped package folder (contains app\, sql\, ...)
    [Parameter(Mandatory)][string]$HostName,             # e.g. lms.company.local (must match the certificate)
    [Parameter(Mandatory)][string]$CertThumbprint,       # certificate in LocalMachine\My
    [Parameter(Mandatory)][string]$StoragePath,          # uploads + data-protection keys (dedicated folder, outside the app folder)
    [Parameter(Mandatory)][string]$LogPath,              # application logs
    [string]$AppPath  = 'C:\inetpub\cyberlms',
    [string]$SiteName = 'CyberLMS',
    [string]$PoolName = 'CyberLMS',
    [switch]$Update
)
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration
if (-not (Test-Path (Join-Path $PackageDir 'app\CyberLms.Web.dll'))) { throw "Package not found: $PackageDir\app\CyberLms.Web.dll" }
if ($StoragePath.StartsWith($AppPath, [StringComparison]::OrdinalIgnoreCase) -or $LogPath.StartsWith($AppPath, [StringComparison]::OrdinalIgnoreCase)) { throw 'StoragePath and LogPath must be OUTSIDE the application folder (so updates never touch them).' }
$cert = Get-Item "Cert:\LocalMachine\My\$CertThumbprint" -ErrorAction Stop
$identity = "IIS AppPool\$PoolName"

# 1. folders
foreach ($d in $AppPath,$StoragePath,$LogPath) { New-Item -ItemType Directory -Force -Path $d | Out-Null }

# 2. application pool: No Managed Code, 64-bit, ApplicationPoolIdentity
if (-not (Test-Path "IIS:\AppPools\$PoolName")) { New-WebAppPool -Name $PoolName | Out-Null }
Set-ItemProperty "IIS:\AppPools\$PoolName" -Name managedRuntimeVersion -Value ''           # No Managed Code (ASP.NET Core runs in-process)
Set-ItemProperty "IIS:\AppPools\$PoolName" -Name enable32BitAppOnWin64 -Value $false      # 64-bit
Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.identityType -Value ApplicationPoolIdentity
Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.loadUserProfile -Value $true
Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)   # never idle out (keeps caches/sessions warm)
Set-ItemProperty "IIS:\AppPools\$PoolName" -Name startMode -Value AlwaysRunning
Set-ItemProperty "IIS:\AppPools\$PoolName" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)

# 3. take the site offline during an update
$offline = Join-Path $AppPath 'app_offline.htm'
if ($Update -and (Test-Path (Join-Path $AppPath 'CyberLms.Web.dll'))) {
    '<html dir="rtl" lang="ar"><meta charset="utf-8"><body style="font-family:Tahoma"><h2>النظام قيد التحديث</h2><p>يرجى المحاولة بعد دقائق.</p></body></html>' | Set-Content -Encoding UTF8 $offline
    Start-Sleep -Seconds 3
}

# 4. copy application files. NEVER overwrite machine configuration, never touch storage/logs.
$keep = Join-Path $AppPath 'appsettings.Production.json'
& robocopy (Join-Path $PackageDir 'app') $AppPath /E /XF appsettings.Production.json app_offline.htm /XD logs | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
if (-not (Test-Path $keep)) {
    Copy-Item (Join-Path $AppPath 'appsettings.Production.sample.json') $keep
    Write-Warning "Created $keep from the sample. EDIT IT NOW (connection string, storage, SMTP...) before the first start."
}

# 5. NTFS permissions (least privilege)
function Grant($path, $who, $rights) { & icacls $path /grant "${who}:$rights" /T /C | Out-Null }
& icacls $AppPath     /inheritance:r /grant 'SYSTEM:(OI)(CI)F' /grant 'BUILTIN\Administrators:(OI)(CI)F' | Out-Null
Grant $AppPath     $identity '(OI)(CI)RX'                 # application folder: read & execute only
& icacls $keep /inheritance:r /grant 'SYSTEM:F' /grant 'BUILTIN\Administrators:F' /grant "${identity}:R" | Out-Null   # secrets file: admins + app pool (read) only
& icacls $StoragePath /inheritance:r /grant 'SYSTEM:(OI)(CI)F' /grant 'BUILTIN\Administrators:(OI)(CI)F' | Out-Null
Grant $StoragePath $identity '(OI)(CI)M'                 # uploads / keys: modify
& icacls $LogPath     /inheritance:r /grant 'SYSTEM:(OI)(CI)F' /grant 'BUILTIN\Administrators:(OI)(CI)F' | Out-Null
Grant $LogPath     $identity '(OI)(CI)M'                 # logs: modify

# 6. site + HTTPS binding (certificate bound with SNI)
if (-not (Get-Website -Name $SiteName -ErrorAction SilentlyContinue)) {
    New-Website -Name $SiteName -PhysicalPath $AppPath -ApplicationPool $PoolName -HostHeader $HostName -Port 443 -Ssl -SslFlags 1 | Out-Null
} else { Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $AppPath; Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $PoolName }
$binding = Get-WebBinding -Name $SiteName -Protocol https | Where-Object { $_.bindingInformation -like "*:443:$HostName" }
if (-not $binding) { New-WebBinding -Name $SiteName -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1; $binding = Get-WebBinding -Name $SiteName -Protocol https | Where-Object { $_.bindingInformation -like "*:443:$HostName" } }
$binding.AddSslCertificate($CertThumbprint, 'My')
# Optional plain-HTTP binding so that http://host redirects to https (the application issues the redirect + HSTS)
if (-not (Get-WebBinding -Name $SiteName -Protocol http -ErrorAction SilentlyContinue)) { New-WebBinding -Name $SiteName -Protocol http -Port 80 -HostHeader $HostName }

# 7. authentication: Windows Authentication ON (Negotiate first, then NTLM), Anonymous OFF, for this site only
$loc = $SiteName
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $loc -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -Value $false
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $loc -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $loc -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name useKernelMode -Value $true
Clear-WebConfiguration -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $loc -Filter 'system.webServer/security/authentication/windowsAuthentication/providers'
Add-WebConfiguration  -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $loc -Filter 'system.webServer/security/authentication/windowsAuthentication/providers' -Value @{ value = 'Negotiate' }
Add-WebConfiguration  -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $loc -Filter 'system.webServer/security/authentication/windowsAuthentication/providers' -Value @{ value = 'NTLM' }
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $loc -Filter 'system.webServer/directoryBrowse' -Name enabled -Value $false

# 8. bring the site back
if (Test-Path $offline) { Remove-Item $offline -Force }
Restart-WebAppPool -Name $PoolName
Start-Website -Name $SiteName
Write-Host "Deployed. Next: edit $keep if this is a new install, apply the database scripts (docs\POSTGRESQL.md), then run 03-Verify-Deployment.ps1."
