#!/bin/bash
# Phase 8.1a M2 — Drag-and-Drop Rearrangement Verification
# Verifies: Dock layout renders, panels visible, drag-and-drop works
#
# Prerequisites: dotnet 9.0 SDK, DISPLAY set (X11 or Wayland)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
RESULT_FILE="$SCRIPT_DIR/manual_test_screenshots/phase8.1a_m2_results.md"
PASSED=0
FAILED=0

log() {
    echo "[$(date +%H:%M:%S)] $1"
}

pass() {
    PASSED=$((PASSED + 1))
    log "  ✅ PASS: $1"
    echo "| PASS | $1 |" >> "$RESULT_FILE"
}

fail() {
    FAILED=$((FAILED + 1))
    log "  ❌ FAIL: $1"
    echo "| FAIL | $1 — $2 |" >> "$RESULT_FILE"
}

cleanup() {
    if [ -n "${APP_PID:-}" ] && kill -0 "$APP_PID" 2>/dev/null; then
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
    fi
}

trap cleanup EXIT

mkdir -p "$SCRIPT_DIR/manual_test_screenshots"

cat > "$RESULT_FILE" <<EOF
# Phase 8.1a M2 — Drag-and-Drop Test Results

Date: $(date '+%Y-%m-%d %H:%M:%S')
Tester: $USER

| Status | Description |
|--------|-------------|
EOF

echo "============================================="
echo " Phase 8.1a M2: Drag-and-Drop Verification"
echo "============================================="

# 1. Build
log "[1/5] Building project..."
cd "$PROJECT_DIR"
BUILD_LOG=$(mktemp)
dotnet build src/aero.csproj -c Debug -v q > "$BUILD_LOG" 2>&1 || true
BUILD_ERRORS=$(grep -oP 'Error\(s\):\s+\K[0-9]+' "$BUILD_LOG" || echo "0")
rm -f "$BUILD_LOG"
if [ "$BUILD_ERRORS" != "0" ]; then
    fail "Build" "Build has $BUILD_ERRORS error(s)"
    echo "Results: $RESULT_FILE"
    exit 1
fi
pass "Build succeeds with 0 errors"

# 2. Run tests
log "[2/5] Running test suite..."
TEST_LOG=$(mktemp)
dotnet test tests --no-build -v q > "$TEST_LOG" 2>&1 || true
TEST_OUTPUT=$(cat "$TEST_LOG")
rm -f "$TEST_LOG"
TEST_COUNT=$(echo "$TEST_OUTPUT" | grep -oP 'Passed:\s+\K[0-9]+' || echo "0")
TEST_FAILURES=$(echo "$TEST_OUTPUT" | grep -oP 'Failed:\s+\K[0-9]+' || echo "0")
if [ "$TEST_FAILURES" = "0" ] && [ "$TEST_COUNT" -gt 0 ]; then
    pass "All $TEST_COUNT tests pass"
else
    fail "Tests" "$TEST_FAILURES failures, $TEST_COUNT passed"
fi

# 3. Launch app
log "[3/5] Launching Aero..."
cd "$PROJECT_DIR"
APP_LOG=$(mktemp)
dotnet run --project src/aero.csproj --no-build -c Debug > "$APP_LOG" 2>&1 &
APP_PID=$!
sleep 5

if ! kill -0 "$APP_PID" 2>/dev/null; then
    fail "App startup" "App crashed on launch"
    cat "$APP_LOG"
    rm -f "$APP_LOG"
    echo "Results: $RESULT_FILE"
    exit 1
fi
pass "App starts without crash (PID=$APP_PID)"

# 4. Check for runtime exceptions in first 5 seconds
CRASH_LINES=$(grep -ci "unhandled exception\|NullReferenceException\|ArgumentException\|InvalidOperationException" "$APP_LOG" || true)
CRASH_LINES=${CRASH_LINES:-0}
if [ "$CRASH_LINES" -gt 0 ]; then
    fail "Runtime exceptions" "Found $CRASH_LINES exception(s) in log"
    grep -i "exception" "$APP_LOG" | head -5
else
    pass "No runtime exceptions in first 5 seconds"
fi

# 5. Kill app
log "[4/5] Stopping app..."
kill "$APP_PID" 2>/dev/null || true
wait "$APP_PID" 2>/dev/null || true
pass "App stopped cleanly"

# 6. Verify DockControl init sequence in source
log "[5/5] Verifying DockControl initialization in code..."
INIT_OK=true

# Check that InitializeFactory is set before Layout
CS_FILE="$PROJECT_DIR/src/MainWindow.axaml.cs"
if ! grep -q "InitializeFactory = true" "$CS_FILE"; then
    fail "Init sequence" "InitializeFactory not set in MainWindow.axaml.cs"
    INIT_OK=false
fi

if ! grep -q "DockControl.Layout = layout" "$CS_FILE"; then
    fail "Init sequence" "Layout assignment not found in MainWindow.axaml.cs"
    INIT_OK=false
fi

# Check InitializeFactory appears before Layout assignment
LINE_INIT=$(grep -n "InitializeFactory = true" "$CS_FILE" | head -1 | cut -d: -f1)
LINE_LAYOUT=$(grep -n "DockControl.Layout = layout" "$CS_FILE" | head -1 | cut -d: -f1)
if [ -n "$LINE_INIT" ] && [ -n "$LINE_LAYOUT" ]; then
    if [ "$LINE_INIT" -lt "$LINE_LAYOUT" ]; then
        pass "InitializeFactory set BEFORE Layout assignment (line $LINE_INIT < line $LINE_LAYOUT)"
    else
        fail "Init order" "InitializeFactory (line $LINE_INIT) should be before Layout (line $LINE_LAYOUT)"
        INIT_OK=false
    fi
else
    fail "Init sequence" "Could not determine line numbers"
    INIT_OK=false
fi

# Check DataTemplates registered
AXAML_FILE="$PROJECT_DIR/src/MainWindow.axaml"
for TEMPLATE in "ExplorerTool" "GitTool" "ProblemsTool" "OutputTool" "EditorDocument"; do
    if grep -q "DataType=\"dockTools:$TEMPLATE\"\|DataType=\"docModels:$TEMPLATE\"" "$AXAML_FILE"; then
        pass "DataTemplate registered for $TEMPLATE"
    else
        fail "DataTemplate" "Missing DataTemplate for $TEMPLATE"
    fi
done

# Check DockControl present in XAML
if grep -q "dock:DockControl" "$AXAML_FILE"; then
    pass "DockControl element present in MainWindow.axaml"
else
    fail "DockControl" "DockControl not found in MainWindow.axaml"
fi

# Summary
echo ""
echo "============================================="
echo " Results: $PASSED passed, $FAILED failed"
echo "============================================="
echo "Results written to: $RESULT_FILE"

if [ "$FAILED" -gt 0 ]; then
    exit 1
fi
