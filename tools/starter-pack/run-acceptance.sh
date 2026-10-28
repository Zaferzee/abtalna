#!/bin/bash
# Starter content pack acceptance on a FRESH Development database (local test machine only).
# Usage: PG="Host=localhost;Username=<role>;Password=<pwd>" PGPASSWORD=<pwd> OUT=<screenshots dir> PORT=5160 tools/starter-pack/run-acceptance.sh
# Requires: a Release build (dotnet build -c Release), psql/createdb/dropdb, node + playwright-core (NODE_PATH or a local node_modules).
# Steps: create an empty database -> install the pack explicitly (console flag) -> start the app -> browser acceptance ->
# run the installer again (must report "already installed" and create nothing) -> drop the database.
set -euo pipefail
: "${PG:?set PG to a PostgreSQL connection string without Database=}"
PORT=${PORT:-5160}; DB=${DB:-starter_pack_accept}; OUT=${OUT:-docs/screenshots/starter-content-pack-v1}
ROOT=$(cd "$(dirname "$0")/../.." && pwd); WEB=$ROOT/src/CyberLms.Web; DLL=$WEB/bin/Release/net10.0/CyberLms.Web.dll
STORE=$(mktemp -d); ANSWERS=$(mktemp)
PGHOST_ARGS=(-h localhost -U "$(sed -n 's/.*Username=\([^;]*\).*/\1/p' <<<"$PG")")
dropdb "${PGHOST_ARGS[@]}" --if-exists "$DB"; createdb "${PGHOST_ARGS[@]}" "$DB"
export ConnectionStrings__Default="$PG;Database=$DB" ASPNETCORE_ENVIRONMENT=Development Storage__RootPath=$STORE \
       Seed__AdminUsername=admin Seed__AdminPassword='Admin#Pass12345' Logging__File__Enabled=false
cd "$WEB"
echo "== install (1st run)"; dotnet "$DLL" --install-starter-pack | tail -1
q() { psql "${PGHOST_ARGS[@]}" "$DB" -Atc "$1"; }
echo "contents=$(q 'select count(*) from "Contents"') questions=$(q 'select count(*) from "Questions"')"
# correct options of the phishing lesson's assessment (the UI answers them to produce a passing result)
q "select coalesce(json_object_agg(q.\"Id\", o.\"Id\"), '{}') from \"Questions\" q join \"QuestionOptions\" o on o.\"QuestionId\"=q.\"Id\" and o.\"IsCorrect\"
   join \"Assessments\" a on a.\"Id\"=q.\"AssessmentId\" join \"Contents\" c on c.\"Id\"=a.\"ContentId\" where c.\"Title\"='التصيد والهندسة الاجتماعية'" > "$ANSWERS"
ASPNETCORE_URLS=http://localhost:$PORT dotnet "$DLL" > "$STORE/app.log" 2>&1 & APP=$!
for i in $(seq 1 60); do curl -s -o /dev/null -w "%{http_code}" "http://localhost:$PORT/Account/Login" | grep -q 200 && break; sleep 1; done
# NODE_RUN_DIR: a folder with node_modules/playwright-core (the script is copied there), default: run in place
RUN=${NODE_RUN_DIR:-$ROOT/tests/ui}; [ "$RUN" != "$ROOT/tests/ui" ] && cp "$ROOT/tests/ui/starter-content-pack-acceptance.mjs" "$RUN/"
case "$OUT" in /*) ;; *) OUT=$ROOT/$OUT;; esac
cd "$RUN"; BASE=http://localhost:$PORT OUT=$OUT ANSWERS=$ANSWERS node starter-content-pack-acceptance.mjs || true
kill $APP; wait $APP 2>/dev/null || true
cd "$WEB"
echo "== install (2nd run)"; dotnet "$DLL" --install-starter-pack | tail -1
echo "contents=$(q 'select count(*) from "Contents"') questions=$(q 'select count(*) from "Questions"') markers=$(q "select count(*) from \"SystemSettings\" where \"Key\"='StarterContentPack.v1.Installed'")"
rm -rf "$STORE" "$ANSWERS"
