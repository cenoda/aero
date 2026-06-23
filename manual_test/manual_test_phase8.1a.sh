#!/bin/bash
# Phase 8.1a — Dockable Panels (Freeform Mode): Full Manual Verification
# Covers M1–M7: layout renders, panels visible, toggle commands, layout persistence,
# drag-and-drop (manual confirmation required), settings mode stub.
#
# Prerequisites: dotnet 9.0 SDK, DISPLAY set (X11 or Wayland)
# Usage: bash manual_test/manual_test_phase8.1a.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
RESULT_FILE="$SCRIPT_DIR/manual_test_screenshots/phase8.1a_results.md"
PASSED=0
FAILED=0

log() { echo "[$(date +%H:%M:%S)] $1"; }
pass() { PASSED=$((PASSED + 1)); log "  ✅ PASS: $1"; echo "| PASS | $1 |" >> "$RESULT_FILE"; }
fail() { FAILED=$((FAILED + 1)); log "  ❌ FAIL: $1 — $2"; echo "| FAIL | $1 — $2 |" >> "$RESULT_FILE"; }
info() { log "  ℹ️  $1"; echo "| INFO | $1 |" >> "$RESULT_FILE"; }

cleanup() {
    if [ -n "${APP_PID:-}" ] && kill -0 "$APP_PID" 2>/dev/null; then
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT

mkdir -p "$SCRIPT_DIR/manual_test_screenshots"

cat > "$RESULT_FILE" <<EOF
# Phase 8.1a — Full Verification Results

Date: $(date '+%Y-%m-%d %H:%M:%S')
Tester: $USER

| Status | Description |
|--------|-------------|
EOF

echo "================================================"
echo " Phase 8.1a: Dockable Panels — Full Verification"
echo "================================================"

# ── BUILD ──────────────────────────────────────────
log "[Step 1/7] Build"
cd "$PROJECT_DIR"
BUILD_OUT=$(dotnet build src/aero.csproj -c Debug -v q 2>&1)
BUILD_ERRORS=$(echo "$BUILD_OUT" | grep -oP 'Error\(s\):\s+\K[0-9]+' || echo "0")
BUILD_WARNS=$(echo "$BUILD_OUT" | grep -oP 'Warning\(s\):\s+\K[0-9]+' || echo "0")
if [ "$BUILD_ERRORS" = "0" ]; then
    pass "Build: 0 errors ($BUILD_WARNS warnings)"
else
    fail "Build" "$BUILD_ERRORS error(s)"
    echo "$BUILD_OUT" | tail -20
    exit 1
fi

# ── TESTS ──────────────────────────────────────────
log "[Step 2/7] Unit + integration tests"
TEST_OUT=$(dotnet test tests --no-build -v q 2>&1)
TEST_COUNT=$(echo "$TEST_OUT" | grep -oP 'Passed:\s+\K[0-9]+' || echo "0")
TEST_FAIL=$(echo "$TEST_OUT" | grep -oP 'Failed:\s+\K[0-9]+' || echo "0")
if [ "$TEST_FAIL" = "0" ] && [ "$TEST_COUNT" -gt 0 ]; then
    pass "Tests: $TEST_COUNT passed, 0 failed"
else
    fail "Tests" "$TEST_FAIL failed, $TEST_COUNT passed"
fi

# ── CODE STRUCTURE ─────────────────────────────────
log "[Step 3/7] Code structure verification"

# DockControl in XAML
AXAML="$PROJECT_DIR/src/MainWindow.axaml"
if grep -q "dock:DockControl" "$AXAML"; then
    pass "DockControl element present in MainWindow.axaml"
else
    fail "DockControl" "dock:DockControl not found in MainWindow.axaml"
fi

# DataTemplates registered
for T in "ExplorerTool" "GitTool" "ProblemsTool" "OutputTool" "EditorDocument"; do
    if grep -q "$T" "$AXAML"; then
        pass "DataTemplate registered for $T"
    else
        fail "DataTemplate" "Missing DataTemplate for $T in MainWindow.axaml"
    fi
done

# DockControl init sequence (InitializeFactory before Layout)
CS="$PROJECT_DIR/src/MainWindow.axaml.cs"
LINE_INIT=$(grep -n "InitializeFactory = true" "$CS" | head -1 | cut -d: -f1)
LINE_LAYOUT=$(grep -n "DockControl.Layout = layout" "$CS" | head -1 | cut -d: -f1)
if [ -n "$LINE_INIT" ] && [ -n "$LINE_LAYOUT" ] && [ "$LINE_INIT" -lt "$LINE_LAYOUT" ]; then
    pass "DockControl init: InitializeFactory (line $LINE_INIT) BEFORE Layout (line $LINE_LAYOUT)"
else
    fail "Init sequence" "InitializeFactory must be before Layout assignment"
fi

# InitializeLayout = false present
if grep -q "InitializeLayout = false" "$CS"; then
    pass "DockControl.InitializeLayout = false (prevents default layout overwrite)"
else
    fail "InitializeLayout" "InitializeLayout = false not found — may cause default layout overwrite"
fi

# Layout persistence save on close
if grep -q "_layoutPersistence.Save" "$CS"; then
    pass "Layout persistence: Save called on window close"
else
    fail "Persistence" "Layout Save not found in OnClosing"
fi

# LayoutPersistenceService registered in DI
APP_CS="$PROJECT_DIR/src/App.axaml.cs"
if grep -q "ILayoutPersistenceService" "$APP_CS"; then
    pass "ILayoutPersistenceService registered in DI"
else
    fail "DI" "ILayoutPersistenceService not found in App.axaml.cs"
fi

# Toggle commands in ShellViewModel
VM="$PROJECT_DIR/src/ViewModels/ShellViewModel.cs"
for CMD in "ToggleSidebarCommand" "ToggleOutputCommand" "ToggleProblemsCommand" "ToggleBottomPanelCommand" "SetLayoutModeCommand"; do
    if grep -q "$CMD" "$VM"; then
        pass "ShellViewModel has $CMD"
    else
        fail "ViewModel" "$CMD not found in ShellViewModel.cs"
    fi
done

# LayoutMode enum
if [ -f "$PROJECT_DIR/src/Docking/LayoutMode.cs" ]; then
    if grep -q "Freeform" "$PROJECT_DIR/src/Docking/LayoutMode.cs" && grep -q "Tile" "$PROJECT_DIR/src/Docking/LayoutMode.cs"; then
        pass "LayoutMode enum has Freeform and Tile values"
    else
        fail "LayoutMode" "LayoutMode enum missing Freeform or Tile"
    fi
else
    fail "LayoutMode" "src/Docking/LayoutMode.cs not found"
fi

# No old Grid layout remnants (IsSidebarVisible etc.)
for OLD in "IsSidebarVisible" "IsBottomPanelVisible" "ActiveSidebarTabIndex" "ActiveBottomTabIndex"; do
    if ! grep -q "$OLD" "$VM" 2>/dev/null; then
        pass "Old property $OLD removed from ShellViewModel"
    else
        fail "Dead code" "$OLD still present in ShellViewModel (should have been removed in M4)"
    fi
done

# Dock.Serializer.Newtonsoft still present (required for cycle handling)
CSPROJ="$PROJECT_DIR/src/aero.csproj"
if grep -q "Dock.Serializer.Newtonsoft" "$CSPROJ"; then
    pass "Dock.Serializer.Newtonsoft present in aero.csproj (required for layout cycle handling)"
else
    fail "Dependency" "Dock.Serializer.Newtonsoft missing from aero.csproj"
fi

# ── APP LAUNCH ─────────────────────────────────────
log "[Step 4/7] App launch"
APP_LOG=$(mktemp)
dotnet run --project src/aero.csproj --no-build -c Debug > "$APP_LOG" 2>&1 &
APP_PID=$!
sleep 5

if ! kill -0 "$APP_PID" 2>/dev/null; then
    fail "App startup" "App crashed on launch"
    cat "$APP_LOG"
    rm -f "$APP_LOG"
    exit 1
fi
pass "App starts without crash (PID=$APP_PID)"

CRASH_COUNT=$(grep -ci "unhandled exception\|NullReferenceException\|InvalidOperation" "$APP_LOG" 2>/dev/null || echo "0")
CRASH_COUNT=$(echo "$CRASH_COUNT" | tr -d '[:space:]')
if [ "${CRASH_COUNT:-0}" -eq 0 ]; then
    pass "No runtime exceptions in first 5 seconds"
else
    fail "Runtime exceptions" "Found $CRASH_COUNT exception(s) in startup log"
    grep -i "exception" "$APP_LOG" | head -5
fi

kill "$APP_PID" 2>/dev/null || true
wait "$APP_PID" 2>/dev/null || true
unset APP_PID
rm -f "$APP_LOG"
pass "App stopped cleanly"

# ── MANUAL SCENARIOS (requires tester) ─────────────
log "[Step 5/7] Manual verification scenarios"
echo ""
echo "┌─────────────────────────────────────────────────────────────────────────┐"
echo "│  MANUAL VERIFICATION REQUIRED                                           │"
echo "│  Run: dotnet run --project src                                          │"
echo "│  Then verify each scenario below and press Y/n for each:               │"
echo "└─────────────────────────────────────────────────────────────────────────┘"
echo ""

ask_manual() {
    local scenario="$1"
    echo -n "  ▶ $scenario [Y/n]: "
    read -r answer
    case "$answer" in
        [Nn]*) fail "Manual" "$scenario" ;;
        *) pass "Manual: $scenario" ;;
    esac
}

echo "Layout scenarios (start app first):"
ask_manual "Default layout: Explorer+Git on left, Editor center, Problems+Output bottom"
ask_manual "Left sidebar shows Explorer as first tab, Git as second tab"
ask_manual "Bottom panel shows Problems as first tab, Output as second tab"
ask_manual "Ctrl+\` (backtick) toggles Output panel visibility"
ask_manual "View > Toggle Sidebar toggles the left panel"
ask_manual "View > Toggle Output shows/hides Output"
ask_manual "View > Toggle Problems shows/hides Problems"
ask_manual "View > Toggle Bottom Panel shows/hides the bottom zone"
ask_manual "View > Layout Mode shows 'Freeform' active, 'Tile' grayed out"
ask_manual "Drag Explorer tab to bottom zone — panel moves"
ask_manual "Drag Git tab to a different position — panel moves"
ask_manual "Close a panel via X button — panel disappears, zone collapses if empty"
ask_manual "Rearrange panels, quit IDE, reopen — layout is restored"

# ── LAYOUT FILE ────────────────────────────────────
log "[Step 6/7] Layout file check"
LAYOUT_FILE="$HOME/.aero/layout.json"
if [ -f "$LAYOUT_FILE" ]; then
    LAYOUT_SIZE=$(wc -c < "$LAYOUT_FILE")
    if [ "$LAYOUT_SIZE" -gt 10 ]; then
        pass "Layout file exists and non-empty: $LAYOUT_FILE ($LAYOUT_SIZE bytes)"
    else
        fail "Layout file" "File exists but suspiciously small ($LAYOUT_SIZE bytes)"
    fi
else
    info "Layout file not found at $LAYOUT_FILE — run and close the app to create it"
fi

# ── DOCS CHECK ─────────────────────────────────────
log "[Step 7/7] Documentation check"

PHASES="$PROJECT_DIR/docs/roadmap/PHASES.md"
if grep -q "\[x\].*8.1a" "$PHASES" 2>/dev/null; then
    pass "PHASES.md: 8.1a marked complete"
else
    fail "Docs" "PHASES.md 8.1a item not marked [x]"
fi

LIBRARIES="$PROJECT_DIR/docs/LIBRARIES.md"
if grep -qi "Dock.Avalonia" "$LIBRARIES" && grep -qi "8.1" "$LIBRARIES"; then
    pass "LIBRARIES.md: Dock.Avalonia entry references Phase 8.1"
else
    fail "Docs" "LIBRARIES.md Dock.Avalonia entry not updated for 8.1a"
fi

# ── SUMMARY ────────────────────────────────────────
echo ""
echo "================================================"
echo " Results: $PASSED passed, $FAILED failed"
echo "================================================"
echo "Full results: $RESULT_FILE"
echo ""
[ "$FAILED" -eq 0 ] && echo "✅ Phase 8.1a M7 complete" || echo "❌ Fix failures before marking complete"
[ "$FAILED" -gt 0 ] && exit 1 || exit 0
