<#
 Nightly backup: PostgreSQL dump + storage folder + production config.
 Usage: .\backup.ps1 -Dest D:\Backups -PgPassword (Read-Host -AsSecureString) 
 Run from Task Scheduler under an account that can read the app folder and the storage folder.
#>
param(
  [string]$Dest = "D:\Backups",
  [string]$PgBin = "C:\Program Files\PostgreSQL\16\bin",
  [string]$Db = "cyberlms",
  [string]$PgUser = "cyberlms_app",
  [string]$PgHost = "localhost",
  [Parameter(Mandatory)][securestring]$PgPassword,
  [string]$Storage = "D:\CyberLMS\Storage",
  [string]$AppDir = "C:\inetpub\cyberlms",
  [int]$KeepDays = 14
)
$ErrorActionPreference = "Stop"
$stamp = Get-Date -Format "yyyyMMdd_HHmm"
$target = Join-Path $Dest $stamp
New-Item -ItemType Directory -Force $target | Out-Null
$env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($PgPassword))
try {
  & "$PgBin\pg_dump.exe" -h $PgHost -U $PgUser -Fc -f "$target\$Db.dump" $Db
  if ($LASTEXITCODE -ne 0) { throw "pg_dump failed" }
} finally { Remove-Item Env:\PGPASSWORD }
robocopy $Storage "$target\storage" /MIR /R:2 /W:5 | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
Copy-Item "$AppDir\appsettings.Production.json" "$target\" -ErrorAction SilentlyContinue
Copy-Item "$AppDir\web.config" "$target\" -ErrorAction SilentlyContinue
Get-ChildItem $Dest -Directory | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } | Remove-Item -Recurse -Force
Write-Host "Backup complete: $target"
