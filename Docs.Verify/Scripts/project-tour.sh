#!/usr/bin/env bash
# Verification script for the Get Started > Project tour page.
#
# The page shows no commands; it describes the structure of a generated
# solution. This script checks those structural facts against the
# MyFirstShiftApp project that install-and-scaffold.sh creates (sample app,
# Internal identity), so the page fails loudly when the template changes shape:
#   - the four projects in the solution and who references whom
#   - the folders each project section of the page names
#   - no E2ETests folder and no Web/Development folder (removed in 2026.10.11.1+)
#   - API ships only appsettings.Development.json
#   - the request-flow example: Product is served by minimal-API endpoints
#     (MapShiftEntitySecureCrud), the old ProductController is [NonController]
#
# REQUIREMENTS: run install-and-scaffold.sh first (from the same folder).

set -uo pipefail

pause_if_interactive() {
    if [ -t 0 ]; then
        read -p "Press Enter to close..."
    fi
}

FAILURES=0
fail() { echo "[verify] FAIL: $*"; FAILURES=$((FAILURES + 1)); }
pass() { echo "[verify] ok: $*"; }
check() { local what="$1"; shift; if "$@"; then pass "$what"; else fail "$what"; fi; }

APP=MyFirstShiftApp
if [ ! -f "$APP/$APP.sln" ]; then
    echo "[verify] $APP/$APP.sln not found — run install-and-scaffold.sh first."
    pause_if_interactive
    exit 1
fi
cd "$APP"

sln_projects() { grep -oE 'Project\([^)]*\) = "[^"]+"' "$APP.sln" | cut -d'"' -f4 | sort | tr '\n' ' '; }
refs() { grep -oE 'ProjectReference Include="[^"]+"' "$APP.$1/$APP.$1.csproj" | grep -oE '[A-Za-z.]+\.csproj' | sed "s/^$APP\.//; s/\.csproj$//" | sort | tr '\n' ' '; }
has_dirs() { local p="$1"; shift; for d in "$@"; do [ -d "$APP.$p/$d" ] || { echo "[verify]   missing $APP.$p/$d"; return 1; }; done; }

# --- Solution layout ---
check "solution has API, Data, Shared, Web" [ "$(sln_projects)" = "$APP.API $APP.Data $APP.Shared $APP.Web " ]
check "API references Data and Web"         [ "$(refs API)" = "Data Web " ]
check "Data references Shared"              [ "$(refs Data)" = "Shared " ]
check "Web references Shared"               [ "$(refs Web)" = "Shared " ]
check "Shared references nothing"           [ -z "$(refs Shared)" ]
check "no E2ETests folder"                  [ ! -e "$APP.E2ETests" ]
check "E2ETests is not in the solution"     bash -c "! grep -q 'E2ETests' '$APP.sln'"

# --- Per-project folders named on the page ---
check "Shared folders"  has_dirs Shared ActionTrees DTOs Enums Localization
check "Data folders"    has_dirs Data DbContext Entities Evaluators Mappers Migrations Projections ReplicationModels Repositories
check "API folders"     has_dirs API Controllers Endpoints Reports Services
check "Web folders"     has_dirs Web Components Pages Shared wwwroot
check "no Web/Development folder"            [ ! -e "$APP.Web/Development" ]
check "Migrations folder starts empty" [ -z "$(find "$APP.Data/Migrations" -name '*.cs')" ]
check "API ships only appsettings.Development.json" \
    [ "$(cd "$APP.API" && ls appsettings*.json | tr '\n' ' ')" = "appsettings.Development.json " ]

# --- Facts in the project sections ---
check "DB inherits ShiftIdentityDbContext (Internal)" grep -q 'class DB : ShiftIdentityDbContext' "$APP.Data/DbContext/DB.cs"
check "ProductRepository is ShiftRepository<DB, Product, ProductListDTO, ProductDTO>" \
    grep -q 'ShiftRepository<DB, Entities.Product, ProductListDTO, ProductDTO>' "$APP.Data/Repositories/ProductRepository.cs"
check "api/country is declared by an attribute on the entity (built-in repository)" \
    grep -q 'ShiftEntitySecureEndpoint<CountryDTO, CountryDTO, .*("api/country"' "$APP.Data/Entities/Country.cs"
check "InvoiceController exists (the controller example)" [ -f "$APP.API/Controllers/InvoiceController.cs" ]
check "Program.cs calls RegisterShiftRepositories" grep -q 'RegisterShiftRepositories' "$APP.API/Program.cs"
check "API serves the Blazor app" grep -q 'UseBlazorFrameworkFiles' "$APP.API/Program.cs"
check "ProductCategory.frx is rendered by ProductCategoryRepository" \
    grep -q 'Reports/ProductCategory.frx' "$APP.Data/Repositories/ProductCategoryRepository.cs"

# --- The flow of a single request ---
check "ProductController is disabled ([NonController])" grep -q '^\[NonController\]' "$APP.API/Controllers/ProductController.cs"
check "Product is served by MapShiftEntitySecureCrud" grep -q 'MapShiftEntitySecureCrud<ProductRepository' "$APP.API/Endpoints/ProductEndpoints.cs"

echo
if [ "$FAILURES" -gt 0 ]; then
    echo "=== Project-tour verification FAILED ($FAILURES) ==="
    pause_if_interactive
    exit 1
fi
echo "=== Project-tour verification completed successfully ==="
pause_if_interactive
