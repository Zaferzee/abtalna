<#
.SYNOPSIS  Installs the Windows features and the .NET 10 Hosting Bundle required by CyberLMS. Run once, as Administrator, on the web server.
.PARAMETER HostingBundleInstaller  Path to dotnet-hosting-10.0.x-win.exe already downloaded from https://dotnet.microsoft.com/download/dotnet/10.0 (the server may not have internet access).
#>
[CmdletBinding()]
param([string]$HostingBundleInstaller)
$ErrorActionPreference = 'Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this script as Administrator.' }

$features = 'Web-Server','Web-WebServer','Web-Common-Http','Web-Static-Content','Web-Default-Doc','Web-Http-Errors','Web-Health','Web-Http-Logging',
            'Web-Security','Web-Filtering','Web-Windows-Auth','Web-Performance','Web-Stat-Compression','Web-Mgmt-Tools','Web-Mgmt-Console'
Write-Host 'Installing IIS features...'
Install-WindowsFeature -Name $features | Out-Null
# Features that must NOT be installed (reduce attack surface): directory browsing, WebDAV, FTP, CGI/ISAPI, ASP/ASP.NET 4, Basic/Digest auth, server-side includes
$unwanted = 'Web-Dir-Browsing','Web-DAV-Publishing','Web-Ftp-Server','Web-CGI','Web-ISAPI-Ext','Web-ISAPI-Filter','Web-ASP','Web-Asp-Net45','Web-Basic-Auth','Web-Digest-Auth','Web-Includes'
foreach ($f in $unwanted) { if ((Get-WindowsFeature $f).Installed) { Write-Warning "Removing unnecessary IIS feature $f"; Uninstall-WindowsFeature $f | Out-Null } }

$hasBundle = $false
try { $hasBundle = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Updates\.NET Core' -ErrorAction SilentlyContinue) -ne $null -and (& dotnet --list-runtimes 2>$null | Select-String 'Microsoft.AspNetCore.App 10\.') } catch {}
if ($hasBundle) { Write-Host 'ASP.NET Core 10 runtime already installed.' }
elseif ($HostingBundleInstaller) {
    Write-Host 'Installing the .NET 10 Hosting Bundle...'
    Start-Process -FilePath $HostingBundleInstaller -ArgumentList '/install','/quiet','/norestart' -Wait
    Write-Host 'Restarting IIS...'; & iisreset | Out-Null
} else { Write-Warning 'The ASP.NET Core 10 runtime / Hosting Bundle is NOT installed. Download dotnet-hosting-10.0.x-win.exe and re-run with -HostingBundleInstaller <path>.' }
Write-Host 'Done. Verify with:  dotnet --list-runtimes   (expect Microsoft.AspNetCore.App 10.x)  and  Get-WebGlobalModule AspNetCoreModuleV2'
