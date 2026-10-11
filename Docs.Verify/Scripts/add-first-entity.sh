#!/usr/bin/env bash
# Anchored verification script for the Get Started > Your first entity page.
#
# Walks the page end-to-end on the scaffolded MyFirstShiftApp:
#   1. Scaffold a Warehouse entity with the shiftentity item template and check
#      the eight files it generates
#   2. Write the page's entity and DTO code (snippets below) into those files and
#      add the page's form fields and list columns to the generated pages
#   3. Build, add the AddWarehouse migration and apply it to a fresh LocalDB database
#   4. Start the API, sign in as SuperUser, create a warehouse through
#      POST api/Warehouse and read it back from the list endpoint
#   5. Stop the API, drop the database, remove the Warehouse files again
#
# Why Warehouse: the sample app already has a Vehicle entity, so the page's
# earlier `-n Vehicle` stopped with "would overwrite existing files".
#
# REQUIREMENTS:
#   - install-and-scaffold.sh and run-sample.sh have run (from the same folder):
#     the project exists and has its InitialCreate migration
#   - LocalDB and sqlcmd (as for run-sample.sh)
#
# The C# and Razor snippets sit inside a quoted heredoc so bash never reads them
# as code; the harness reads them back out of this file and writes them into the
# generated files, so the code the page shows is the code that was built and run.

set -uo pipefail

pause_if_interactive() {
    if [ -t 0 ]; then
        read -p "Press Enter to close..."
    fi
}

docs_only_anchors() {
    cat > /dev/null <<'CODE'
# snippet:WarehouseEntity
using ShiftSoftware.ShiftEntity.Core;
using ShiftSoftware.ShiftEntity.Core.Flags;

namespace MyFirstShiftApp.Data.Entities;

[TemporalShiftEntity]
public class Warehouse : ShiftEntity<Warehouse>
{
    public string Name { get; set; } = default!;
    public string? City { get; set; }
    public int Capacity { get; set; }
}
# endsnippet

# snippet:WarehouseDTO
using ShiftSoftware.ShiftEntity.Model.Dtos;
using System.ComponentModel.DataAnnotations;

namespace MyFirstShiftApp.Shared.DTOs.Warehouse;

public class WarehouseDTO : ShiftEntityViewAndUpsertDTO
{
    public override string? ID { get; set; }

    [Required]
    public string Name { get; set; } = default!;
    public string? City { get; set; }
    public int Capacity { get; set; }
}
# endsnippet

# snippet:WarehouseListDTO
using ShiftSoftware.ShiftEntity.Model.Dtos;

namespace MyFirstShiftApp.Shared.DTOs.Warehouse;

public class WarehouseListDTO : ShiftEntityListDTO
{
    public override string? ID { get; set; }
    public string Name { get; set; } = default!;
    public string? City { get; set; }
}
# endsnippet

# snippet:WarehouseFormFields
    <MudTextFieldExtended Label="Name" @bind-Value="context.Item.Name" For="() => context.Item.Name" />
    <MudTextFieldExtended Label="City" @bind-Value="context.Item.City" For="() => context.Item.City" />
    <MudNumericFieldExtended Label="Capacity" @bind-Value="context.Item.Capacity" For="() => context.Item.Capacity" />
# endsnippet

# snippet:WarehouseListColumns
    <PropertyColumn Title="Name" Property="x => x.Name" />
    <PropertyColumn Title="City" Property="x => x.City" />
# endsnippet
CODE
}

THIS_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"
snippet_body() {
    sed -n "/# snippet:$1\$/,/# endsnippet/p" "$THIS_SCRIPT" | sed '1d;$d'
}

DB_NAME="MyFirstShiftAppEntityVerify_$(date +%s)"
API_PID=""
API_LOG="$(mktemp)"
FAILURES=0
fail() { echo "[verify] FAIL: $*"; FAILURES=$((FAILURES + 1)); }
pass() { echo "[verify] ok: $*"; }

WAREHOUSE_FILES=(
    "MyFirstShiftApp.Shared/DTOs/Warehouse/WarehouseDTO.cs"
    "MyFirstShiftApp.Shared/DTOs/Warehouse/WarehouseListDTO.cs"
    "MyFirstShiftApp.Data/Entities/Warehouse.cs"
    "MyFirstShiftApp.Data/DbContext/Warehouse.cs"
    "MyFirstShiftApp.Data/Repositories/WarehouseRepository.cs"
    "MyFirstShiftApp.API/Controllers/WarehouseController.cs"
    "MyFirstShiftApp.Web/Pages/Warehouse/WarehouseForm.razor"
    "MyFirstShiftApp.Web/Pages/Warehouse/WarehouseList.razor"
)

remove_warehouse() {
    rm -rf MyFirstShiftApp.Shared/DTOs/Warehouse MyFirstShiftApp.Web/Pages/Warehouse \
           MyFirstShiftApp.Data/Entities/Warehouse.cs MyFirstShiftApp.Data/DbContext/Warehouse.cs \
           MyFirstShiftApp.Data/Repositories/WarehouseRepository.cs MyFirstShiftApp.API/Controllers/WarehouseController.cs
    rm -f MyFirstShiftApp.Data/Migrations/*_AddWarehouse.cs MyFirstShiftApp.Data/Migrations/*_AddWarehouse.Designer.cs
    # Put the model snapshot back the way InitialCreate left it.
    [ -f "$SNAPSHOT_BACKUP" ] && cp "$SNAPSHOT_BACKUP" MyFirstShiftApp.Data/Migrations/DBModelSnapshot.cs
}

cleanup() {
    if [ -n "$API_PID" ] && kill -0 "$API_PID" 2>/dev/null; then
        echo "[verify] stopping API (pid $API_PID)..."
        kill "$API_PID" 2>/dev/null || true
        wait "$API_PID" 2>/dev/null || true
    fi
    echo "[verify] dropping database ${DB_NAME}..."
    sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "IF DB_ID('${DB_NAME}') IS NOT NULL BEGIN ALTER DATABASE [${DB_NAME}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${DB_NAME}]; END" 2>/dev/null || true
    echo "[verify] removing the Warehouse files..."
    remove_warehouse
    rm -f "$API_LOG" "$SNAPSHOT_BACKUP"
}
trap 'echo; echo "=== Script failed at line $LINENO ==="; pause_if_interactive' ERR

cd MyFirstShiftApp

if [ -z "$(find MyFirstShiftApp.Data/Migrations -name '*_InitialCreate.cs' 2>/dev/null)" ]; then
    echo "[verify] no InitialCreate migration — run run-sample.sh first."; exit 1
fi
SNAPSHOT_BACKUP="$(mktemp)"
cp MyFirstShiftApp.Data/Migrations/DBModelSnapshot.cs "$SNAPSHOT_BACKUP"
remove_warehouse   # leftovers from an interrupted run
trap cleanup EXIT

export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__SQLServer="Server=(localdb)\\MSSQLLocalDB;Database=${DB_NAME};Integrated Security=True;TrustServerCertificate=True;"
export CosmosDb__Enabled=false

# The reader arrives from Run the generated sample with a migrated database;
# create that state first (before the model changes, or EF refuses to update).
echo "[verify] creating the database from InitialCreate (the state Run the sample leaves)..."
if ! dotnet ef database update --context DB --project MyFirstShiftApp.Data --startup-project MyFirstShiftApp.API > ef.log 2>&1; then
    echo "[verify] InitialCreate failed:"; tail -20 ef.log; exit 1
fi
rm -f ef.log

# --- Step 1: scaffold ---
echo "[verify] scaffolding the Warehouse entity..."
# snippet:ScaffoldEntity
dotnet new shiftentity -n Warehouse --solution MyFirstShiftApp
# endsnippet
if [ $? -ne 0 ]; then echo "[verify] entity scaffold failed"; exit 1; fi

missing=0
for f in "${WAREHOUSE_FILES[@]}"; do [ -f "$f" ] || { echo "[verify]   missing $f"; missing=1; }; done
[ "$missing" = 0 ] && pass "the eight files were generated" || fail "generated files missing"
[ ! -e MyFirstShiftApp.Data/Mappers/WarehouseMapper.cs ] && pass "no mapper file (maps are automatic)" || fail "a mapper file was generated"
grep -q 'base(null)' MyFirstShiftApp.API/Controllers/WarehouseController.cs \
    && pass "the controller passes no TypeAuth action (base(null))" || fail "controller TypeAuth changed"
grep -q 'TypeAuthAction="null"' MyFirstShiftApp.Web/Pages/Warehouse/WarehouseForm.razor \
    && pass "the form has TypeAuthAction=\"null\"" || fail "form TypeAuth changed"
grep -q 'DbSet<Warehouse>' MyFirstShiftApp.Data/DbContext/Warehouse.cs \
    && pass "DbContext partial adds DbSet<Warehouse>" || fail "DbContext partial changed"

# --- Step 2/3: the page's code ---
echo "[verify] writing the page's entity, DTO, form and list code..."
snippet_body WarehouseEntity   > MyFirstShiftApp.Data/Entities/Warehouse.cs
snippet_body WarehouseDTO      > MyFirstShiftApp.Shared/DTOs/Warehouse/WarehouseDTO.cs
snippet_body WarehouseListDTO  > MyFirstShiftApp.Shared/DTOs/Warehouse/WarehouseListDTO.cs
insert_before() {  # insert_before <file> <closing tag> <snippet>
    local body; body="$(snippet_body "$3")"
    awk -v tag="$2" -v body="$body" 'index($0, tag) == 1 { print body } { print }' "$1" > "$1.tmp" && mv "$1.tmp" "$1"
}
insert_before MyFirstShiftApp.Web/Pages/Warehouse/WarehouseForm.razor '</ShiftEntityForm>' WarehouseFormFields
insert_before MyFirstShiftApp.Web/Pages/Warehouse/WarehouseList.razor '</ShiftList>' WarehouseListColumns
grep -q 'context.Item.Capacity' MyFirstShiftApp.Web/Pages/Warehouse/WarehouseForm.razor && grep -q 'x => x.City' MyFirstShiftApp.Web/Pages/Warehouse/WarehouseList.razor \
    || { echo "[verify] could not add the form fields / list columns"; exit 1; }

echo "[verify] building..."
if dotnet build MyFirstShiftApp.API/MyFirstShiftApp.API.csproj > build.log 2>&1; then
    pass "the project builds with the page's code"
else
    fail "build failed"; grep -E ': error ' build.log | sort -u | head -10; exit 1
fi
rm -f build.log

# --- Step 4: migration ---
echo "[verify] adding and applying the AddWarehouse migration..."
# snippet:AddWarehouseMigration
dotnet ef migrations add AddWarehouse --context DB --project MyFirstShiftApp.Data --startup-project MyFirstShiftApp.API
# endsnippet
if [ $? -ne 0 ]; then echo "[verify] migration add failed"; exit 1; fi
# snippet:ApplyWarehouseMigration
dotnet ef database update --context DB --project MyFirstShiftApp.Data --startup-project MyFirstShiftApp.API
# endsnippet
if [ $? -ne 0 ]; then echo "[verify] migration apply failed"; exit 1; fi
TABLE=$(sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d "$DB_NAME" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Warehouse' AND COLUMN_NAME IN ('Name','City','Capacity')" | tr -d '[:space:]')
[ "$TABLE" = "3" ] && pass "Warehouse table has Name, City, Capacity" || fail "Warehouse table/columns missing ($TABLE)"

# --- Step 5: run, save, list ---
echo "[verify] starting API on port 5079..."
dotnet run --project MyFirstShiftApp.API --no-launch-profile --urls "http://localhost:5079" > "$API_LOG" 2>&1 &
API_PID=$!
for i in $(seq 1 60); do
    curl -sf -m 5 -o /dev/null "http://localhost:5079/" && break
    sleep 1
    [ "$i" -eq 60 ] && { echo "[verify] API did not start:"; tail -30 "$API_LOG"; exit 1; }
done

CHALLENGE=$(head -c 32 /dev/urandom | openssl dgst -sha256 -binary | base64 | tr '+/' '-_' | tr -d '=')
TOKEN=$(curl -s -m 20 -X POST "http://localhost:5079/api/identity/v2/login" -H 'Content-Type: application/json' \
    -d "{\"username\":\"SuperUser\",\"password\":\"OneTwo\",\"codeChallenge\":\"$CHALLENGE\"}" | grep -oE '"Token":"[^"]+"' | cut -d'"' -f4)
[ -n "$TOKEN" ] || { echo "[verify] could not sign in as SuperUser"; exit 1; }

CREATED=$(curl -s -m 20 -X POST "http://localhost:5079/api/Warehouse" -H "Authorization: Bearer $TOKEN" \
    -H 'Content-Type: application/json' -d '{"name":"Main Warehouse","city":"Erbil","capacity":500}' -w '\n%{http_code}')
CODE=$(echo "$CREATED" | tail -1)
[ "$CODE" = "200" ] || [ "$CODE" = "201" ] && pass "POST api/Warehouse saved a warehouse ($CODE)" \
    || { fail "POST api/Warehouse answered $CODE"; echo "$CREATED" | head -c 400; echo; }

LIST=$(curl -s -m 20 "http://localhost:5079/api/Warehouse" -H "Authorization: Bearer $TOKEN")
echo "$LIST" | grep -q '"Name":"Main Warehouse"' && echo "$LIST" | grep -q '"City":"Erbil"' \
    && pass "the list endpoint returns it" || { fail "the warehouse is not in the list"; echo "$LIST" | head -c 400; echo; }

echo
if [ "$FAILURES" -gt 0 ]; then
    echo "=== Add-first-entity verification FAILED ($FAILURES) ==="
    pause_if_interactive
    exit 1
fi
echo "=== Add-first-entity verification completed successfully ==="
pause_if_interactive
