#!/bin/bash
set -e

# Manual smoke test for Phase 1 editor features
# Runs Aero under Xvfb and exercises basic UI interactions.
# Note: Synthetic key events (xdotool) are not reliably delivered to Avalonia's
# TextEditor in a bare Xvfb session, so direct text typing cannot be verified here.
# Text-editing logic is covered by the unit test suite (89 passing tests).

APP_PID=""
XVFB_PID=""
DISPLAY_NUM=99

cleanup() {
    echo "[cleanup] Stopping app and Xvfb..."
    [ -n "$APP_PID" ] && kill "$APP_PID" 2>/dev/null || true
    [ -n "$XVFB_PID" ] && kill "$XVFB_PID" 2>/dev/null || true
    rm -f /tmp/aero_test_*.png /tmp/aero_test.txt
}
trap cleanup EXIT

mkdir -p "$PWD/manual_test_screenshots"

echo "[1/8] Starting Xvfb on :$DISPLAY_NUM"
Xvfb ":$DISPLAY_NUM" -screen 0 1280x800x24 -ac +extension GLX +render -noreset &
XVFB_PID=$!
sleep 1

export DISPLAY=":$DISPLAY_NUM"

echo "[2/8] Building and launching Aero"
dotnet build src >/dev/null 2>&1
dotnet src/bin/Debug/net9.0/aero.dll &
APP_PID=$!
sleep 3

echo "[3/8] Locating window"
WINDOW_ID=$(xdotool search --name "Aero" | head -n1)
if [ -z "$WINDOW_ID" ]; then
    echo "FAIL: Aero window not found"
    exit 1
fi
echo "Window ID: $WINDOW_ID"

echo "[4/8] Screenshot: initial state"
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/aero_test_01_initial.png"

echo "[5/8] Testing new file (Ctrl+N)"
xdotool windowfocus "$WINDOW_ID" || true
xdotool key --window "$WINDOW_ID" Ctrl+n
sleep 0.7
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/aero_test_02_newfile.png"

echo "[6/8] Testing multiple tabs + tab switching (Ctrl+Tab / Ctrl+Shift+Tab)"
xdotool key --window "$WINDOW_ID" Ctrl+n
sleep 0.5
xdotool key --window "$WINDOW_ID" Ctrl+n
sleep 0.5
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/aero_test_03_multitabs.png"
xdotool key --window "$WINDOW_ID" Ctrl+Tab
sleep 0.3
xdotool key --window "$WINDOW_ID" Ctrl+Shift+Tab
sleep 0.3

echo "[7/8] Testing find overlay (Ctrl+F)"
xdotool key --window "$WINDOW_ID" Ctrl+f
sleep 0.7
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/aero_test_04_find.png"

echo "[8/8] Testing status bar + final state"
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/aero_test_05_status.png"

echo "Manual smoke test completed successfully."
echo "Screenshots:"
ls -l "$PWD/manual_test_screenshots"/aero_test_*.png
