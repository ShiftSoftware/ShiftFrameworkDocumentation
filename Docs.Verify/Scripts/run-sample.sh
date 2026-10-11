#!/usr/bin/env bash
# Anchored verification script for the Get Started > Run the generated sample page.
#
# Brings the scaffolded MyFirstShiftApp up end-to-end using LocalDB:
#   1. Take the connection string the page shows (LocalDbConnectionString below),
#      with a fresh database name, and the page's Cosmos setting (DisableCosmos)
#   2. Create the initial migration and apply it
#   3. Start the API in the background and wait for it to answer
#   4. Check what the page promises after the first run:
#        - the seed data: SuperUser, country IQ, region KRG, company SFT
#        - SuperUser / OneTwo signs in (the first step of the login the app's
#          login page uses), and a wrong password is refused
#        - with Cosmos disabled, the API log has no "fail:" lines
#   5. Stop the API, drop the database
#
# REQUIREMENTS:
#   - Windows machine with LocalDB installed (sqllocaldb.exe in PATH) and sqlcmd
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
#
# The two JSON snippets sit inside a quoted heredoc so bash never reads them as
# code. The harness reads the connection string and the Cosmos flag back out of
# this file, so the values the page shows are the values that were tested.

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
    cat > /dev/null <<'JSON'
# snippet:LocalDbConnectionString
"ConnectionStrings": {
  "SQLServer": "Server=(localdb)\\MSSQLLocalDB;Database=MyFirstShiftApp;Integrated Security=True;TrustServerCertificate=True;"
}
# endsnippet

# snippet:DisableCosmos
"CosmosDb": {
  "Enabled": false
}
# endsnippet
JSON

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

THIS_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"

# Read a snippet's body back out of this file.
snippet_body() {
    sed -n "/# snippet:$1\$/,/# endsnippet/p" "$THIS_SCRIPT" | sed '1d;$d'
}

DB_NAME="MyFirstShiftAppVerify_$(date +%s)"
API_PID=""
API_LOG="$(mktemp)"
FAILURES=0
fail() { echo "[verify] FAIL: $*"; FAILURES=$((FAILURES + 1)); }
pass() { echo "[verify] ok: $*"; }

cleanup() {
    if [ -n "$API_PID" ] && kill -0 "$API_PID" 2>/dev/null; then
        echo "[verify] stopping API (pid $API_PID)..."
        kill "$API_PID" 2>/dev/null || true
        wait "$API_PID" 2>/dev/null || true
    fi
    echo "[verify] dropping database ${DB_NAME}..."
    sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "IF DB_ID('${DB_NAME}') IS NOT NULL BEGIN ALTER DATABASE [${DB_NAME}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${DB_NAME}]; END" 2>/dev/null || true
    rm -f "$API_LOG"
}
trap 'echo; echo "=== Script failed at line $LINENO ==="; pause_if_interactive' ERR
trap cleanup EXIT

# The page's LocalDB connection string, with this run's database name. The JSON
# escapes the backslash (\\MSSQLLocalDB); the real value has one.
PAGE_CONNSTR=$(snippet_body LocalDbConnectionString | grep -oE '"SQLServer": "[^"]+"' | cut -d'"' -f4 | sed 's/\\\\/\\/g')
CONNSTR="${PAGE_CONNSTR/Database=MyFirstShiftApp;/Database=${DB_NAME};}"
if [ -z "$PAGE_CONNSTR" ] || [ "$CONNSTR" = "$PAGE_CONNSTR" ]; then
    echo "[verify] could not read the LocalDbConnectionString snippet"; exit 1
fi
COSMOS_ENABLED=$(snippet_body DisableCosmos | grep -oE '"Enabled": (true|false)' | awk '{print $2}')
if [ "$COSMOS_ENABLED" != "false" ]; then
    echo "[verify] could not read the DisableCosmos snippet"; exit 1
fi

cd MyFirstShiftApp

# Apply the two snippets as environment variables rather than editing the
# scaffolded appsettings.Development.json — same keys, same values.
export ConnectionStrings__SQLServer="${CONNSTR}"
export CosmosDb__Enabled="${COSMOS_ENABLED}"

# Force Development environment so appsettings.Development.json (which contains
# the TokenSettings, etc.) actually loads. The template ships only Development
# config — no base appsettings.json — so without this the API crashes on startup
# in AddShiftIdentity with "Value cannot be null. (Parameter 's')".
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
dotnet run --project MyFirstShiftApp.API --no-launch-profile --urls "http://localhost:5079" > "$API_LOG" 2>&1 &
API_PID=$!

echo "[verify] waiting for API to respond..."
for i in $(seq 1 60); do
    if curl -sf -m 5 -o /dev/null "http://localhost:5079/" 2>/dev/null; then
        echo "[verify] API responded on attempt $i"
        break
    fi
    sleep 1
    if [ "$i" -eq 60 ]; then
        echo "[verify] API failed to respond within 60s — last 30 lines of the API log:"
        tail -30 "$API_LOG"
        exit 1
    fi
done

# --- The seed data the page lists ---
seeded() {
    sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d "$DB_NAME" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM ShiftIdentity.$1 WHERE $2 = '$3'" 2>/dev/null | tr -d '[:space:]'
}
[ "$(seeded Users Username SuperUser)" = "1" ] && pass "seeded user SuperUser" || fail "no SuperUser"
[ "$(seeded Countries ShortCode IQ)" = "1" ]    && pass "seeded country IQ"    || fail "no country IQ"
[ "$(seeded Regions ShortCode KRG)" = "1" ]     && pass "seeded region KRG"    || fail "no region KRG"
[ "$(seeded Companies ShortCode SFT)" = "1" ]   && pass "seeded company SFT"   || fail "no company SFT"

# --- SuperUser / OneTwo signs in ---
# The app's login page posts username, password and a PKCE code challenge
# (43 characters, base64url) to api/identity/v2/login; a correct password
# answers with a session.
CHALLENGE=$(head -c 32 /dev/urandom | openssl dgst -sha256 -binary | base64 | tr '+/' '-_' | tr -d '=')
login() {
    curl -s -m 20 -X POST "http://localhost:5079/api/identity/v2/login" -H 'Content-Type: application/json' \
        -d "{\"username\":\"SuperUser\",\"password\":\"$1\",\"codeChallenge\":\"$CHALLENGE\"}"
}
login OneTwo | grep -q '"kind":"session"' && pass "SuperUser / OneTwo signs in" || fail "SuperUser / OneTwo did not sign in"
login not-the-password | grep -q '"kind":"refused"' && pass "a wrong password is refused" || fail "a wrong password was not refused"

# --- No failures in the log with Cosmos disabled ---
FAIL_LINES=$(grep -c '^fail' "$API_LOG" || true)
[ "$FAIL_LINES" = "0" ] && pass "no 'fail:' lines in the API log" || { fail "$FAIL_LINES 'fail:' lines in the API log"; grep -A2 '^fail' "$API_LOG" | head -12; }

echo
if [ "$FAILURES" -gt 0 ]; then
    echo "=== Run-sample verification FAILED ($FAILURES) ==="
    pause_if_interactive
    exit 1
fi
echo "=== Run-sample verification completed successfully ==="
pause_if_interactive
