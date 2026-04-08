#!/usr/bin/env bash
# Anchored verification script for the Get Started > Run the generated sample page.
#
# Brings the scaffolded MyFirstShiftApp up end-to-end using LocalDB:
#   1. Override the connection string to point at LocalDB with a fresh database name
#   2. Apply EF Core migrations
#   3. Start the API in the background and wait for it to bind a port
#   4. Curl a known endpoint to confirm it's actually serving requests
#   5. Stop the API, drop the database
#
# REQUIREMENTS:
#   - Windows machine with LocalDB installed (sqllocaldb.exe in PATH)
#   - The MyFirstShiftApp project must already exist (run install-and-scaffold.sh first)
#
# FOR CI: this script is not portable to Linux yet because LocalDB is Windows-only.
# Adding portability requires either Docker (mssql container) or a --databaseProvider
# template parameter that lets us swap to SQLite. Tracked as a follow-up.
#
# DOC SNIPPETS vs VERIFICATION COMMANDS:
# Some snippets (e.g. `dotnet run`) would block forever if executed as bash. To
# keep them as anchored documentation without blocking the verification flow,
# they live inside the `docs_only_anchors` function below — bash parses it but
# never calls it. SnippetGen extracts the regions inside it just fine. The
# verification harness further down runs equivalent operations in a
# background-friendly way.

set -uo pipefail

pause_if_interactive() {
    if [ -t 0 ]; then
        read -p "Press Enter to close..."
    fi
}

# Snippets shown to readers but never actually executed by this script.
# The function is defined but never called. Used for commands that block
# (dotnet run) or commands the user runs by hand (editing config files).
docs_only_anchors() {
    # snippet:CreateInitialMigration
    dotnet ef migrations add InitialCreate --context DB --project MyFirstShiftApp.Data --startup-project MyFirstShiftApp.API
    # endsnippet

    # snippet:ApplyMigrations
    dotnet ef database update --context DB --project MyFirstShiftApp.Data --startup-project MyFirstShiftApp.API
    # endsnippet

    # snippet:RunApi
    dotnet run --project MyFirstShiftApp.API
    # endsnippet
}

trap 'echo; echo "=== Script failed at line $LINENO ==="; cleanup; pause_if_interactive' ERR

DB_NAME="MyFirstShiftAppVerify_$(date +%s)"
CONNSTR="Server=(localdb)\\MSSQLLocalDB;Database=${DB_NAME};Integrated Security=True;TrustServerCertificate=True;"
API_PID=""

cleanup() {
    if [ -n "$API_PID" ] && kill -0 "$API_PID" 2>/dev/null; then
        echo "[verify] stopping API (pid $API_PID)..."
        kill "$API_PID" 2>/dev/null || true
        wait "$API_PID" 2>/dev/null || true
    fi
    echo "[verify] dropping database ${DB_NAME}..."
    sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "IF DB_ID('${DB_NAME}') IS NOT NULL DROP DATABASE [${DB_NAME}];" 2>/dev/null || true
}
trap cleanup EXIT

cd MyFirstShiftApp

# Override the default connection string to point at LocalDB. The template ships
# pointing at localhost\sqlexpress which doesn't exist on most machines.
export ConnectionStrings__SQLServer="${CONNSTR}"
export ConnectionStrings__LiveIdentitySQLServer="${CONNSTR}"

# Force Development environment so appsettings.Development.json (which contains
# the TokenSettings, MappingStrategy, etc.) actually loads. The template ships
# only Development config — no base appsettings.json — so without this the API
# crashes on startup with a null PublicKey.
export ASPNETCORE_ENVIRONMENT=Development

echo "[verify] restoring packages..."
dotnet restore MyFirstShiftApp.API/MyFirstShiftApp.API.csproj

echo "[verify] creating initial migration (template ships an empty Migrations folder)..."
# Idempotent: only add if no migration files exist yet.
if [ -z "$(find MyFirstShiftApp.Data/Migrations -name '*.cs' 2>/dev/null)" ]; then
    dotnet ef migrations add InitialCreate --context DB --project MyFirstShiftApp.Data --startup-project MyFirstShiftApp.API
    if [ $? -ne 0 ]; then echo "[verify] migration creation failed"; exit 1; fi
fi

echo "[verify] applying migrations against LocalDB database '${DB_NAME}'..."
dotnet ef database update --context DB --project MyFirstShiftApp.Data --startup-project MyFirstShiftApp.API
if [ $? -ne 0 ]; then echo "[verify] migration apply failed"; exit 1; fi

echo "[verify] starting API in background on port 5079..."
dotnet run --project MyFirstShiftApp.API --no-launch-profile --urls "http://localhost:5079" > /tmp/api.log 2>&1 &
API_PID=$!

echo "[verify] waiting for API to respond..."
for i in $(seq 1 60); do
    if curl -sf -o /dev/null "http://localhost:5079/" 2>/dev/null \
        || curl -sf -o /dev/null "http://localhost:5079/healthz" 2>/dev/null; then
        echo "[verify] API responded on attempt $i"
        break
    fi
    sleep 1
    if [ "$i" -eq 60 ]; then
        echo "[verify] API failed to respond within 60s — last 30 lines of api.log:"
        tail -30 /tmp/api.log
        exit 1
    fi
done

echo
echo "=== Run-sample verification completed successfully ==="
pause_if_interactive
