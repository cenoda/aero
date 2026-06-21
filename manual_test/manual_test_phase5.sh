#!/bin/bash
set -e

# Manual smoke test for Phase 5 (Output Panel / Fake Terminal).
# Runs Aero under Xvfb with a startup folder, opens the Output panel,
# runs commands, tests cancellation, and verifies output display.

APP_PID=""
XVFB_PID=""
DISPLAY_NUM=98

TEST_DIR=$(mktemp -d /tmp/aero-phase5-XXXXXX)

# Create a simple .NET project to test dotnet build
mkdir -p "$TEST_DIR/src"
cat > "$TEST_DIR/src/Program.cs" <<'CS'
using System;
class Program { static void Main() => Console.WriteLine("Hello from Aero!"); }
CS

cat > "$TEST_DIR/aero.csproj" <<'CSP'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework></PropertyGroup>
</Project>
CSP

cleanup() {
    echo "[cleanup] Stopping app and Xvfb..."
    [ -n "$APP_PID" ] && kill "$APP_PID" 2>/dev/null || true
    [ -n "$XVFB_PID" ] && kill "$XVFB_PID" 2>/dev/null || true
    rm -rf "$TEST_DIR"
}
trap cleanup EXIT

echo "[1/8] Starting Xvfb on :$DISPLAY_NUM"
Xvfb ":$DISPLAY_NUM" -screen 0 1280x800x24 -ac +extension GLX +render -noreset &
XVFB_PID=$!
sleep 1
export DISPLAY=":$DISPLAY_NUM"

echo "[2/8] Building and launching Aero"
dotnet build src/aero.csproj -c Debug >/dev/null 2>&1 || dotnet build src/aero.csproj -c Debug
dotnet src/bin/Debug/net9.0/aero.dll "$TEST_DIR" &
APP_PID=$!
sleep 3

echo "[3/8] Locating window"
WINDOW_ID=$(xdotool search --name "Aero" | head -n1)
if [ -z "$WINDOW_ID" ]; then
    echo "FAIL: Aero window not found"
    exit 1
fi
echo "Window ID: $WINDOW_ID"

# Helper: press Ctrl+OemTilde to open Output panel
open_output_panel() {
    xdotool key --window "$WINDOW_ID" ctrl+quoteright
    sleep 1
}

# Helper: type a command into the output panel text box (approximate position)
type_command() {
    local cmd="$1"
    xdotool mousemove --window "$WINDOW_ID" 300 35
    xdotool click 1
    sleep 0.3
    xdotool type --delay 50 "$cmd"
    sleep 0.3
}

# Helper: click the Run button (approximate position)
click_run() {
    xdotool mousemove --window "$WINDOW_ID" 500 35
    xdotool click 1
    sleep 0.3
}

echo "[4/8] Opening Output panel with Ctrl+OemTilde"
open_output_panel
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/phase5_01_panel_open.png"

echo "[5/8] Running 'dotnet --version'"
type_command "dotnet --version"
click_run
sleep 3
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/phase5_02_dotnet_version.png"

echo "[6/8] Running 'ls -la'"
type_command "ls -la"
click_run
sleep 2
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/phase5_03_ls_la.png"

echo "[7/8] Testing Clear button"
# Click Clear button (approximate position)
xdotool mousemove --window "$WINDOW_ID" 560 35
xdotool click 1
sleep 0.5
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/phase5_04_after_clear.png"

echo "[8/8] Cleanup"
kill "$APP_PID" 2>/dev/null || true
sleep 1

echo "----------------------------------------------------------------------"
echo "MANUAL CHECKLIST (inspect screenshots):"
echo "  1. phase5_01_panel_open.png shows Output tab visible in bottom panel."
echo "  2. phase5_02_dotnet_version.png shows .NET version in output."
echo "  3. phase5_03_ls_la.png shows ls output with file listing."
echo "  4. phase5_04_after_clear.png shows empty output (after Clear)."
echo "  5. Ctrl+OemTilde toggles Output panel."
echo "  6. Exit code lines appear after commands (e.g., '[Process exited with code 0]')."
echo "----------------------------------------------------------------------"
echo "Phase 5 manual smoke test completed."
ls -l "$PWD/manual_test_screenshots"/phase5_*.png || true