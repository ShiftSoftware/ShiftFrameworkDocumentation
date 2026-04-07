#!/usr/bin/env bash
# Anchored verification script for the Get Started > Installation page.
#
# Every command in this file is manually verified against the framework version
# noted in the doc page's TestedAgainst metadata. Doc pages reference named
# regions below via <DocSnippet Name="..." Language="bash" />.
#
# IMPORTANT: do not edit a region in isolation. If you change a command here,
# walk through the resulting flow on a clean machine and bump TestedAgainst on
# every doc page that consumes the affected snippet.

set -uo pipefail

# Print a clear error and pause if anything fails, so the window doesn't close
# silently when this script is double-clicked from Explorer. Pauses are skipped
# in non-interactive runs (CI, parent shells without a TTY).
pause_if_interactive() {
    if [ -t 0 ]; then
        read -p "Press Enter to close..."
    fi
}
trap 'echo; echo "=== Script failed at line $LINENO ==="; pause_if_interactive' ERR

# snippet:InstallTemplate
dotnet new install ShiftSoftware.ShiftTemplates
# endsnippet

# snippet:VerifyTemplateInstalled
dotnet new list shift
# endsnippet

# snippet:UpgradeTemplate
dotnet new install ShiftSoftware.ShiftTemplates@*
# endsnippet

# snippet:ScaffoldFirstProject
mkdir -p MyFirstShiftApp
cd MyFirstShiftApp
dotnet new shift -n MyFirstShiftApp \
    --includeSampleApp true \
    --shiftIdentityHostingType Internal
# endsnippet

# snippet:BuildProject
dotnet build
# endsnippet

# snippet:VerifyDotnetVersion
dotnet --version
# endsnippet

echo
echo "=== All steps completed successfully ==="
pause_if_interactive
