#!/usr/bin/env bash
# Builds the production package (framework-dependent, for IIS + ASP.NET Core Hosting Bundle).
# Usage: deploy/build-package.sh [output-dir]      (needs the .NET 10 SDK; runs on Linux/macOS/Windows Git-Bash)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$ROOT/artifacts}"
STAMP="$(date -u +%Y%m%d-%H%M)"
COMMIT="$(git -C "$ROOT" rev-parse --short HEAD 2>/dev/null || echo unknown)"
PKG="cyberlms-$STAMP-$COMMIT"
rm -rf "$OUT/$PKG" "$OUT/$PKG.zip"; mkdir -p "$OUT/$PKG/app"

dotnet publish "$ROOT/src/CyberLms.Web" -c Release -o "$OUT/$PKG/app" --nologo -p:DebugType=none -p:DebugSymbols=false

# Never ship secrets or machine-specific settings
rm -f "$OUT/$PKG/app/appsettings.Production.json" "$OUT/$PKG/app/appsettings.Development.json" "$OUT/$PKG/app/appsettings.Local.json"
rm -rf "$OUT/$PKG/app/logs" "$OUT/$PKG/app/storage-dev"

cp -r "$ROOT/deploy/sql" "$OUT/$PKG/sql"
cp "$ROOT/deploy/"*.ps1 "$OUT/$PKG/" 2>/dev/null || true
cp "$ROOT/docs/DEPLOYMENT.md" "$ROOT/docs/POSTGRESQL.md" "$ROOT/docs/ACTIVE_DIRECTORY.md" "$ROOT/docs/BACKUP_RESTORE.md" "$ROOT/docs/HANDOVER.md" "$OUT/$PKG/" 2>/dev/null || true
printf 'CyberLMS package %s\ncommit %s\nbuilt (UTC) %s\n' "$PKG" "$COMMIT" "$STAMP" > "$OUT/$PKG/VERSION.txt"
( cd "$OUT/$PKG" && find . -type f ! -name SHA256SUMS.txt -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS.txt )
( cd "$OUT" && zip -qr "$PKG.zip" "$PKG" )
echo "Package: $OUT/$PKG.zip"
echo "Files in app/: $(find "$OUT/$PKG/app" -type f | wc -l)"
