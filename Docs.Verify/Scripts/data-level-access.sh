#!/usr/bin/env bash
# Verifies the Data-level access guide against local sources or published packages:
#   1. Compile every C# snippet rendered by the guide.
#   2. Run the SQL Server-backed scoped-token lifecycle proof in StockPlusPlus.
#
# Usage: bash data-level-access.sh [--packages [framework-version]]
# --packages disables local framework references for both checks. Supply a version
# to verify a specific release instead of the shared framework version.
#
# REQUIREMENTS:
#   - Windows with localhost\sqlexpress available to the current identity.
#   - Sibling ShiftEntity, ShiftIdentity, ShiftTemplates, and TestingTools repos.

set -uo pipefail

BUILD_PROPERTIES=()
if [ "$#" -gt 0 ]; then
    if [ "$1" != "--packages" ] || [ "$#" -gt 2 ]; then
        echo "Usage: $0 [--packages [framework-version]]" >&2
        exit 2
    fi
    BUILD_PROPERTIES+=(-p:ImportShiftFrameworkProjects=false)
    # Keep the sample and internal identity features, but exclude development-only
    # tests that reference unpublished framework test fixtures.
    BUILD_PROPERTIES+=(-p:DefineConstants=includeSampleApp%3BinternalShiftIdentityHosting%3BincludeItemTemplateContent)
    if [ "$#" -eq 2 ]; then
        BUILD_PROPERTIES+=("-p:ShiftFrameworkVersion=$2")
    fi
    echo "[verify] using published packages; local framework references are disabled."
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCS_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
SHIFT_ROOT="$(cd "${DOCS_ROOT}/.." && pwd)"

VERIFY_PROJECT="${DOCS_ROOT}/Docs.Verify/DataLevelAccessV2/DataLevelAccessV2.Verify.csproj"
TEST_PROJECT="${SHIFT_ROOT}/ShiftTemplates/content/Framework Project/StockPlusPlus.Test/StockPlusPlus.Test.csproj"
DATA_PROJECT="${SHIFT_ROOT}/ShiftTemplates/content/Framework Project/StockPlusPlus.Data/StockPlusPlus.Data.csproj"
API_PROJECT="${SHIFT_ROOT}/ShiftTemplates/content/Framework Project/StockPlusPlus.API/StockPlusPlus.API.csproj"

DB_NAME="StockPlusPlus_DlaVerify_$(date +%s)_$$"
CONNECTION_STRING="Server=localhost\\sqlexpress;Initial Catalog=${DB_NAME};Persist Security Info=True;Integrated Security=SSPI;TrustServerCertificate=True;"

# The sample factory opts into SHIFT_TEST_ overrides; EF's cleanup host reads the ordinary SQLServer setting.
# Both point only at this run's unique database.
export SHIFT_TEST_ConnectionStrings__SQLServer_Test="${CONNECTION_STRING}"
export ConnectionStrings__SQLServer="${CONNECTION_STRING}"
export ASPNETCORE_ENVIRONMENT=Development

cleanup() {
    echo "[verify] dropping temporary database ${DB_NAME}..."
    dotnet ef database drop --force --context DB \
        --project "${DATA_PROJECT}" \
        --startup-project "${API_PROJECT}" \
        --no-build >/dev/null 2>&1 || true
}

trap cleanup EXIT
trap 'echo "[verify] failed at line ${LINENO}"' ERR

echo "[verify] compiling the guide snippets..."
dotnet build "${VERIFY_PROJECT}" --nologo --verbosity quiet "${BUILD_PROPERTIES[@]}"
if [ $? -ne 0 ]; then exit 1; fi

echo "[verify] running the SQL Server query/row lifecycle proof on ${DB_NAME}..."
dotnet test "${TEST_PROJECT}" --nologo --verbosity quiet \
    --logger "console;verbosity=normal" \
    --filter "FullyQualifiedName~VehicleDataLevelAccessTests.CompanyOr_IsEnforcedEndToEndOnSqlServer" \
    -p:WarningLevel=0 "${BUILD_PROPERTIES[@]}"
if [ $? -ne 0 ]; then exit 1; fi

echo "[verify] data-level access guide verified successfully."
