<#
.SYNOPSIS  Backs up PostgreSQL, uploaded files and configuration into a time-stamped folder, with checksums and retention.
           Schedule nightly with Task Scheduler (run as a dedicated service account that can read the folders and has the database password).
.EXAMPLE   $pw = Read-Host -AsSecureString; .\Backup.ps1 -Dest E:\Backups\CyberLMS -DbHost dbserver -PgPassword $pw
           (for unattended runs store the password with:  Read-Host -AsSecureString | ConvertFrom-SecureString > pg.secret  and load it with ConvertTo-SecureString - readable only by the service account)
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Dest,
    [Parameter(Mandatory)][securestring]$PgPassword,
    [string]$DbHost = 'localhost', [int]$DbPort = 5432, [string]$Db = 'cyberlms',
    [string]$PgUser = 'cyberlms_owner',                     # the owner role (a read-only backup role also works: GRANT pg_read_all_data)
    [string]$PgBin = 'C:\Program Files\PostgreSQL\16\bin',
    [string]$StoragePath = 'D:\AppData\CyberLMS\Uploads',
    [string]$AppPath = 'C:\inetpub\cyberlms',
    [int]$KeepDays = 30
)
$ErrorActionPreference = 'Stop'
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$target = Join-Path $Dest $stamp
New-Item -ItemType Directory -Force $target | Out-Null
Start-Transcript -Path (Join-Path $target 'backup.log') | Out-Null
try {
    # 1. database (custom format = compressed, restorable with pg_restore, schema + data)
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PgPassword)
    $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    & "$PgBin\pg_dump.exe" -h $DbHost -p $DbPort -U $PgUser -Fc --no-owner -f (Join-Path $target "$Db.dump") $Db
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed ($LASTEXITCODE)" }
    & "$PgBin\pg_restore.exe" --list (Join-Path $target "$Db.dump") | Out-Null      # proves the dump is readable
    if ($LASTEXITCODE -ne 0) { throw 'The dump cannot be read back (pg_restore --list failed)' }
    # 2. uploaded files + data-protection keys (/E keeps everything; /XO not used: this is a full copy per run)
    & robocopy $StoragePath (Join-Path $target 'storage') /E /R:2 /W:5 /NFL /NDL /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
    # 3. configuration (contains secrets: the backup folder must be access-restricted)
    foreach ($f in 'appsettings.Production.json','web.config') { $p = Join-Path $AppPath $f; if (Test-Path $p) { Copy-Item $p $target } }
    # 4. integrity manifest
    Get-ChildItem $target -Recurse -File | Where-Object Name -ne 'SHA256SUMS.txt' | Get-FileHash -Algorithm SHA256 | ForEach-Object { "{0}  {1}" -f $_.Hash, $_.Path.Substring($target.Length + 1) } | Set-Content (Join-Path $target 'SHA256SUMS.txt')
    & icacls $target /inheritance:r /grant 'SYSTEM:(OI)(CI)F' /grant 'BUILTIN\Administrators:(OI)(CI)F' | Out-Null
    # 5. retention
    Get-ChildItem $Dest -Directory | Where-Object { $_.Name -match '^\d{8}_\d{6}$' -and $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } | Remove-Item -Recurse -Force
    Write-Host "BACKUP OK: $target"
} finally { Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue; Stop-Transcript | Out-Null }
