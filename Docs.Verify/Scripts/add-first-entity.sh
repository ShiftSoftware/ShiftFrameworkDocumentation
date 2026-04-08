#!/usr/bin/env bash
# Anchored verification script for the Get Started > Your first entity page.
#
# Adds a new entity to the scaffolded MyFirstShiftApp using the shiftentity
# item template, then builds the project to confirm everything compiles.
#
# REQUIREMENTS:
#   - The MyFirstShiftApp project must already exist (run install-and-scaffold.sh first)
#   - The shift template must be installed (also via install-and-scaffold.sh)

set -uo pipefail

pause_if_interactive() {
    if [ -t 0 ]; then
        read -p "Press Enter to close..."
    fi
}
trap 'echo; echo "=== Script failed at line $LINENO ==="; pause_if_interactive' ERR

# Snippets shown to readers but never actually executed by this script.
docs_only_anchors() {
    # snippet:ScaffoldEntity
    dotnet new shiftentity -n Vehicle --solution MyFirstShiftApp
    # endsnippet
}

cd MyFirstShiftApp

# Clean up any leftover Vehicle files from a previous verification run so the
# scaffold doesn't fail with "destination not empty".
rm -rf MyFirstShiftApp.Shared/DTOs/Vehicle \
       MyFirstShiftApp.Data/Entities/Vehicle.cs \
       MyFirstShiftApp.Data/DbContext/Vehicle.cs \
       MyFirstShiftApp.Data/Repositories/VehicleRepository.cs \
       MyFirstShiftApp.API/Controllers/VehicleController.cs \
       MyFirstShiftApp.Web/Pages/Vehicle

echo "[verify] scaffolding Vehicle entity..."
dotnet new shiftentity -n Vehicle --solution MyFirstShiftApp
if [ $? -ne 0 ]; then echo "[verify] entity scaffold failed"; exit 1; fi

echo "[verify] confirming the eight expected files were generated..."
expected_files=(
    "MyFirstShiftApp.Shared/DTOs/Vehicle/VehicleDTO.cs"
    "MyFirstShiftApp.Shared/DTOs/Vehicle/VehicleListDTO.cs"
    "MyFirstShiftApp.Data/Entities/Vehicle.cs"
    "MyFirstShiftApp.Data/Repositories/VehicleRepository.cs"
    "MyFirstShiftApp.Data/DbContext/Vehicle.cs"
    "MyFirstShiftApp.API/Controllers/VehicleController.cs"
    "MyFirstShiftApp.Web/Pages/Vehicle/VehicleForm.razor"
    "MyFirstShiftApp.Web/Pages/Vehicle/VehicleList.razor"
)
for f in "${expected_files[@]}"; do
    if [ ! -f "$f" ]; then
        echo "[verify] expected file missing: $f"
        exit 1
    fi
done
echo "[verify] all 8 files present"

echo "[verify] building the API project to confirm everything compiles..."
dotnet build MyFirstShiftApp.API/MyFirstShiftApp.API.csproj 2>&1 | tail -5
if [ $? -ne 0 ]; then echo "[verify] build failed"; exit 1; fi

echo
echo "=== Add-first-entity verification completed successfully ==="
pause_if_interactive
