<#
.SYNOPSIS  Restores a backup made by Backup.ps1 (database, uploaded files, configuration). DESTRUCTIVE: replaces the current database content and storage files.
.EXAMPLE   .\Restore.ps1 -BackupDir E:\Backups\CyberLMS\20261006_020000 -PgPassword $pw           (database + files)
           .\Restore.ps1 -BackupDir ... -PgPassword $pw -RestoreConfig                             (also restores appsettings.Production.json / web.config)
Order used: site offline -> database -> files -> (config) -> grants -> site online -> health check.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)][string]$BackupDir, [Parameter(Mandatory)][securestring]$PgPassword,
    [string]$DbHost = 'localhost', [int]$DbPort = 5432, [string]$Db = 'cyberlms', [string]$PgUser = 'cyberlms_owner',
    [string]$PgBin = 'C:\Program Files\PostgreSQL\16\bin', [string]$StoragePath = 'D:\AppData\CyberLMS\Uploads', [string]$AppPath = 'C:\inetpub\cyberlms',
    [string]$PoolName = 'CyberLMS', [string]$SiteName = 'CyberLMS', [string]$HostName, [string]$GrantsSql, [switch]$RestoreConfig
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path (Join-Path $BackupDir "$Db.dump"))) { throw "No $Db.dump in $BackupDir" }
# verify integrity first
$bad = Get-Content (Join-Path $BackupDir 'SHA256SUMS.txt') | ForEach-Object { $h, $f = $_ -split '  ', 2; $p = Join-Path $BackupDir $f; if (-not (Test-Path $p) -or (Get-FileHash $p -Algorithm SHA256).Hash -ne $h) { $f } }
if ($bad) { throw "Backup integrity check FAILED for: $($bad -join ', ')" }
if (-not $PSCmdlet.ShouldProcess("$Db on $DbHost and $StoragePath", 'REPLACE with backup content')) { return }

Import-Module WebAdministration
'<html dir="rtl" lang="ar"><meta charset="utf-8"><body style="font-family:Tahoma"><h2>النظام قيد الاستعادة</h2></body></html>' | Set-Content -Encoding UTF8 (Join-Path $AppPath 'app_offline.htm')
Start-Sleep 3
try {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PgPassword); $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    # database: --clean --if-exists drops and recreates the objects inside the existing database (ownership stays with cyberlms_owner)
    & "$PgBin\pg_restore.exe" -h $DbHost -p $DbPort -U $PgUser -d $Db --clean --if-exists --no-owner --exit-on-error (Join-Path $BackupDir "$Db.dump")
    if ($LASTEXITCODE -ne 0) { throw "pg_restore failed ($LASTEXITCODE)" }
    if ($GrantsSql) { & "$PgBin\psql.exe" -h $DbHost -p $DbPort -U $PgUser -d $Db -v ON_ERROR_STOP=1 -f $GrantsSql; if ($LASTEXITCODE -ne 0) { throw 'grants failed' } }
    else { Write-Warning 'Run sql\03-grants.sql as cyberlms_owner now (-GrantsSql) - without it the application role has no access to the restored tables.' }
    # files
    & robocopy (Join-Path $BackupDir 'storage') $StoragePath /MIR /R:2 /W:5 /NFL /NDL /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
    if ($RestoreConfig) { foreach ($f in 'appsettings.Production.json','web.config') { $p = Join-Path $BackupDir $f; if (Test-Path $p) { Copy-Item $p $AppPath -Force } } }
} finally { Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue; Remove-Item (Join-Path $AppPath 'app_offline.htm') -Force -ErrorAction SilentlyContinue }
Restart-WebAppPool -Name $PoolName; Start-Website -Name $SiteName
if ($HostName) { $r = Invoke-WebRequest -Uri "https://$HostName/health" -UseDefaultCredentials -UseBasicParsing; Write-Host "health: $($r.StatusCode) $($r.Content)" }
Write-Host 'RESTORE COMPLETE. Now run the smoke test (docs\HANDOVER.md section 8): content, files, results and acknowledgments must be present.'
