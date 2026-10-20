#!/usr/bin/env bash
# Verifies the Data-level access guide against local sources or published packages:
#   1. Compile every C# snippet rendered by the guide.
#   2. Run the SQL Server-backed scoped-token lifecycle proof (VehicleDataLevelAccessTests).
#
# Usage: bash data-level-access.sh [--packages [framework-version]]
#
# Default: both checks build against the sibling framework repos (StockPlusPlus in ShiftTemplates).
# --packages: both checks use published NuGet packages only. The snippets compile against the
# packages, and the lifecycle proof runs inside a fresh project generated from the published
# ShiftTemplates of that version (installed into a private template hive, removed afterwards),
# which is exactly what a reader gets. Supply a version to verify a specific release instead of
# the current one (ShiftFrameworkVersion in ShiftTemplates/ShiftFrameworkGlobalSettings.props).
#
# REQUIREMENTS:
#   - Windows with localhost\sqlexpress available to the current identity.
#   - The dotnet-ef tool.
#   - A sibling ShiftTemplates repo; for the default mode also sibling ShiftEntity, ShiftIdentity,
#     TypeAuth and ShiftFrameworkTestingTools repos.

set -uo pipefail

PACKAGES=false
VERSION=""
if [ "$#" -gt 0 ]; then
    if [ "$1" != "--packages" ] || [ "$#" -gt 2 ]; then
        echo "Usage: $0 [--packages [framework-version]]" >&2
        exit 2
    fi
    PACKAGES=true
    if [ "$#" -eq 2 ]; then VERSION="$2"; fi
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCS_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
SHIFT_ROOT="$(cd "${DOCS_ROOT}/.." && pwd)"

VERIFY_PROJECT="${DOCS_ROOT}/Docs.Verify/DataLevelAccessV2/DataLevelAccessV2.Verify.csproj"
SAMPLE_ROOT="${SHIFT_ROOT}/ShiftTemplates/content/Framework Project"
TEST_PROJECT="${SAMPLE_ROOT}/StockPlusPlus.Test/StockPlusPlus.Test.csproj"
DATA_PROJECT="${SAMPLE_ROOT}/StockPlusPlus.Data/StockPlusPlus.Data.csproj"
API_PROJECT="${SAMPLE_ROOT}/StockPlusPlus.API/StockPlusPlus.API.csproj"

BUILD_PROPERTIES=()
WORK_DIR=""
if [ "${PACKAGES}" = true ]; then
    if [ -z "${VERSION}" ]; then
        VERSION="$(sed -n 's:.*<ShiftFrameworkVersion>\(.*\)</ShiftFrameworkVersion>.*:\1:p' \
            "${SHIFT_ROOT}/ShiftTemplates/ShiftFrameworkGlobalSettings.props" | head -1)"
    fi
    if [ -z "${VERSION}" ]; then
        echo "[verify] could not read ShiftFrameworkVersion; pass the version explicitly." >&2
        exit 2
    fi
    BUILD_PROPERTIES+=(-p:ImportShiftFrameworkProjects=false "-p:ShiftFrameworkVersion=${VERSION}")
    echo "[verify] using published packages ${VERSION}; local framework references are disabled."
fi

DB_NAME="StockPlusPlus_DlaVerify_$(date +%s)_$$"
CONNECTION_STRING="Server=localhost\\sqlexpress;Initial Catalog=${DB_NAME};Persist Security Info=True;Integrated Security=SSPI;TrustServerCertificate=True;"

# The test factory opts into SHIFT_TEST_ overrides; EF's cleanup host reads the ordinary SQLServer setting.
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
    if [ -n "${WORK_DIR}" ]; then
        echo "[verify] removing the generated project and template hive..."
        rm -rf "${WORK_DIR}"
    fi
}

trap cleanup EXIT
trap 'echo "[verify] failed at line ${LINENO}"' ERR

echo "[verify] compiling the guide snippets..."
dotnet build "${VERIFY_PROJECT}" --nologo --verbosity quiet "${BUILD_PROPERTIES[@]}"
if [ $? -ne 0 ]; then exit 1; fi

if [ "${PACKAGES}" = true ]; then
    WORK_DIR="$(mktemp -d)"
    HIVE="${WORK_DIR}/template-hive"
    echo "[verify] installing ShiftSoftware.ShiftTemplates ${VERSION} into a private template hive..."
    dotnet new install "ShiftSoftware.ShiftTemplates::${VERSION}" --debug:custom-hive "${HIVE}" >/dev/null
    if [ $? -ne 0 ]; then echo "[verify] template ${VERSION} could not be installed"; exit 1; fi

    echo "[verify] generating a project with the sample app and tests..."
    (cd "${WORK_DIR}" && dotnet new shift -n DlaVerify \
        --includeSampleApp true --shiftIdentityHostingType Internal --addTest true \
        --debug:custom-hive "${HIVE}" >/dev/null)
    if [ $? -ne 0 ]; then echo "[verify] project generation failed"; exit 1; fi

    GENERATED_TEST="$(find "${WORK_DIR}" -name 'DlaVerify.Test.csproj' -not -path '*/template-hive/*' | head -1)"
    if [ -z "${GENERATED_TEST}" ]; then echo "[verify] generated project has no test project"; exit 1; fi
    GENERATED_ROOT="$(dirname "$(dirname "${GENERATED_TEST}")")"
    TEST_PROJECT="${GENERATED_TEST}"
    DATA_PROJECT="${GENERATED_ROOT}/DlaVerify.Data/DlaVerify.Data.csproj"
    API_PROJECT="${GENERATED_ROOT}/DlaVerify.API/DlaVerify.API.csproj"
    # The generated project pins its own package versions and has no local framework references.
    BUILD_PROPERTIES=()
fi

echo "[verify] running the SQL Server query/row lifecycle proof on ${DB_NAME}..."
dotnet test "${TEST_PROJECT}" --nologo --verbosity quiet \
    --logger "console;verbosity=normal" \
    --filter "FullyQualifiedName~VehicleDataLevelAccessTests.CompanyOr_IsEnforcedEndToEndOnSqlServer" \
    -p:WarningLevel=0 "${BUILD_PROPERTIES[@]}"
if [ $? -ne 0 ]; then exit 1; fi

echo "[verify] data-level access guide verified successfully."
