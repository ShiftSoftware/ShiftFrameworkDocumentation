#!/usr/bin/env bash
# Anchored verification script for the Get Started > Template parameters page.
#
# Scaffolds every combination the page shows (plus the no-sample + Internal one
# its parameter list describes) into a scratch folder, builds each, and checks
# the facts the page states about the generated files:
#   - --addFunctions / --addTest add MyApp.Functions / MyApp.Test, and only then
#   - the API dev port is a random port in 5000-5300, the Functions port in 7000-7300
#   - the API project gets a fresh UserSecretsId (not the template's own)
#   - --externalShiftIdentityApi / --externalShiftIdentityFrontEnd land in the
#     Web project's wwwroot/appsettings.Development.json
#
# REQUIREMENTS: the shift template installed (run install-and-scaffold.sh first).
#   - the empty skeleton builds with External and with Internal identity
#     (on 2026.10.10.2 and earlier they failed with CS0234 / CS0103; the page
#     keeps a Note for readers on an old template)

set -uo pipefail

pause_if_interactive() {
    if [ -t 0 ]; then
        read -p "Press Enter to close..."
    fi
}

# Snippets shown to readers. Never called: the harness below runs each command
# in its own scratch folder.
docs_only_anchors() {
    # snippet:ScaffoldWithFunctionsAndTests
    dotnet new shift -n MyApp \
        --includeSampleApp true \
        --shiftIdentityHostingType Internal \
        --addFunctions true \
        --addTest true
    # endsnippet

    # snippet:ScaffoldSkeletonExternalIdentity
    dotnet new shift -n MyApp \
        --includeSampleApp false \
        --shiftIdentityHostingType External \
        --externalShiftIdentityApi https://identity.example.com \
        --externalShiftIdentityFrontEnd https://identity.example.com
    # endsnippet
}

WORK="$(mktemp -d)"
FAILURES=0

cleanup() {
    rm -rf "$WORK"
}
trap cleanup EXIT

fail() { echo "[verify] FAIL: $*"; FAILURES=$((FAILURES + 1)); }
pass() { echo "[verify] ok: $*"; }

# scaffold <folder> <dotnet new shift arguments...>
scaffold() {
    local dir="$WORK/$1"; shift
    mkdir -p "$dir"
    (cd "$dir" && dotnet new shift -n MyApp "$@" > scaffold.log 2>&1) \
        || { fail "$(basename "$dir"): dotnet new shift failed"; cat "$dir/scaffold.log"; return 1; }
}

# build <folder> -> 0 when the build has no errors
build() {
    (cd "$WORK/$1" && dotnet build > build.log 2>&1)
}

# build_must_pass <folder>
build_must_pass() {
    if build "$1"; then
        pass "$1 builds"
    else
        fail "$1 does not build:"
        grep -E ': error ' "$WORK/$1/build.log" | sort -u | head -5
    fi
}

port_in_range() { [ "$1" -ge "$2" ] && [ "$1" -le "$3" ]; }

# --- Production-ready starting point: sample + Internal + Functions + Test ---
echo "[verify] scaffolding sample + Internal + --addFunctions + --addTest..."
if scaffold full --includeSampleApp true --shiftIdentityHostingType Internal --addFunctions true --addTest true; then
    D="$WORK/full"
    [ -d "$D/MyApp.Functions" ] && pass "--addFunctions adds MyApp.Functions" || fail "MyApp.Functions missing"
    [ -d "$D/MyApp.Test" ] && pass "--addTest adds MyApp.Test" || fail "MyApp.Test missing"
    grep -q 'localhost\\\\sqlexpress' "$D/MyApp.Test/appsettings.json" \
        && pass "MyApp.Test points at localhost\\sqlexpress" || fail "MyApp.Test connection string changed"

    API_PORT=$(grep -oE 'localhost:[0-9]+' "$D/MyApp.API/Properties/launchSettings.json" | head -1 | cut -d: -f2)
    port_in_range "$API_PORT" 5000 5300 && pass "API port $API_PORT is in 5000-5300" || fail "API port '$API_PORT' outside 5000-5300"
    FN_PORT=$(grep -oE -- '--port [0-9]+' "$D/MyApp.Functions/Properties/launchSettings.json" | head -1 | cut -d' ' -f2)
    port_in_range "$FN_PORT" 7000 7300 && pass "Functions port $FN_PORT is in 7000-7300" || fail "Functions port '$FN_PORT' outside 7000-7300"

    SECRET=$(grep -oE 'UserSecretsId>[0-9A-Fa-f-]{36}' "$D/MyApp.API/MyApp.API.csproj" | cut -d'>' -f2)
    if [ -n "$SECRET" ] && [ "${SECRET,,}" != "9424fe73-f4dd-4d92-a137-deea3d0f1c46" ]; then
        pass "API UserSecretsId is fresh ($SECRET)"
    else
        fail "API UserSecretsId is missing or the template's own"
    fi

    echo "[verify] building..."
    build full && pass "full combination builds" || { fail "full combination does not build"; grep -E ': error ' "$D/build.log" | sort -u | head -5; }
fi

# --- Empty skeleton, external identity ---
echo "[verify] scaffolding no sample + External..."
if scaffold skeleton-external --includeSampleApp false --shiftIdentityHostingType External \
        --externalShiftIdentityApi https://identity.example.com --externalShiftIdentityFrontEnd https://identity.example.com; then
    D="$WORK/skeleton-external"
    [ ! -d "$D/MyApp.Functions" ] && [ ! -d "$D/MyApp.Test" ] \
        && pass "no Functions/Test project without the flags" || fail "unexpected Functions/Test project"
    [ -z "$(find "$D/MyApp.Data/Entities" -name '*.cs' 2>/dev/null)" ] && pass "no sample entities" || fail "sample entities present"
    WEB="$D/MyApp.Web/wwwroot/appsettings.Development.json"
    grep -q '"ShiftIdentityApi": "https://identity.example.com/api/"' "$WEB" \
        && pass "ShiftIdentityApi = https://identity.example.com/api/" || fail "ShiftIdentityApi not set from --externalShiftIdentityApi"
    grep -q '"ShiftIdentityFrontEnd": "https://identity.example.com"' "$WEB" \
        && pass "ShiftIdentityFrontEnd = https://identity.example.com" || fail "ShiftIdentityFrontEnd not set from --externalShiftIdentityFrontEnd"

    echo "[verify] building..."
    build_must_pass skeleton-external
fi

# --- No sample, Internal identity (not a page combination; backs the Note) ---
echo "[verify] scaffolding no sample + Internal..."
if scaffold skeleton-internal --includeSampleApp false --shiftIdentityHostingType Internal; then
    echo "[verify] building..."
    build_must_pass skeleton-internal
fi

echo
if [ "$FAILURES" -gt 0 ]; then
    echo "=== Template-parameters verification FAILED ($FAILURES) ==="
    pause_if_interactive
    exit 1
fi
echo "=== Template-parameters verification completed successfully ==="
pause_if_interactive
