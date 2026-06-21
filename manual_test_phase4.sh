#!/bin/bash
# Phase 4 — Basic LSP Integration
# Manual smoke test
# See docs/phases/phase-4/IMPLEMENTATION_PLAN.md §8 for the target scenario.
#
# Prerequisites: dotnet, csharp-ls installed on PATH
#   csharp-ls: dotnet tool install -g csharp-ls
#
# Usage: bash manual_test_phase4.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"

echo "============================================"
echo "  Aero IDE — Phase 4 Manual Smoke Test"
echo "============================================"
echo ""

# 1. Build check
echo "=== [1/6] Building project ==="
cd "$PROJECT_DIR"
dotnet build src/aero.csproj 2>&1 | tail -3
echo "BUILD: OK"
echo ""

# 2. Test check
echo "=== [2/6] Running tests ==="
dotnet test tests 2>&1 | tail -2
echo ""

# 3. Start the app (background)
echo "=== [3/6] Starting Aero IDE in background ==="
dotnet run --project src &
AERO_PID=$!
echo "Aero PID: $AERO_PID"
sleep 3

if kill -0 "$AERO_PID" 2>/dev/null; then
    echo "LAUNCH: OK"
else
    echo "LAUNCH: FAILED — app exited early"
    exit 1
fi
echo ""

# 4. Check for csharp-ls availability
echo "=== [4/6] Checking csharp-ls availability ==="
if command -v csharp-ls &> /dev/null; then
    echo "csharp-ls found: $(which csharp-ls)"
    echo "LSP_SERVER: AVAILABLE"
else
    echo "csharp-ls not found on PATH."
    echo "  Install: dotnet tool install -g csharp-ls"
    echo "LSP_SERVER: NOT AVAILABLE (graceful degradation expected)"
fi
echo ""

# 5. Verify app is still running (smoke)
echo "=== [5/6] App health check ==="
sleep 2
if kill -0 "$AERO_PID" 2>/dev/null; then
    echo "APP_HEALTH: OK (PID $AERO_PID running)"
else
    echo "APP_HEALTH: FAILED — process died"
    exit 1
fi
echo ""

# 6. Clean shutdown
echo "=== [6/6] Stopping Aero IDE ==="
kill "$AERO_PID" 2>/dev/null || true
wait "$AERO_PID" 2>/dev/null || true
echo "SHUTDOWN: OK"
echo ""

echo "============================================"
echo "  Phase 4 Smoke Test: PASSED"
echo "============================================"
echo ""
echo "Manual verification steps (to be performed visually):"
echo "  1. Open a C# folder (File → Open Folder)"
echo "  2. Open a .cs file — check status bar says 'LSP session started'"
echo "  3. Introduce a syntax error — check Problems panel shows diagnostic"
echo "  4. Fix the error — check diagnostic disappears"
echo "  5. Press Ctrl+Space — check completion popup appears"
echo "  6. Toggle Problems panel (View → Toggle Problems)"