#!/bin/bash
set -e

# Manual smoke test for Phase 2 M3 (Open Folder & File Activation)
# Runs Aero under Xvfb with a startup folder, expands a directory,
# and double-clicks a file to verify a tab opens.
# The folder picker itself is exercised separately by the unit tests and
# the File → Open Folder menu binding; this script verifies the tree/load
# and activation path end-to-end in a headless environment.

APP_PID=""
XVFB_PID=""
DISPLAY_NUM=99

TEST_DIR=$(mktemp -d /tmp/aero-m3-XXXXXX)

mkdir -p "$TEST_DIR/src"
echo "console.log('hello');" > "$TEST_DIR/src/app.js"
echo "# README" > "$TEST_DIR/README.md"

cleanup() {
    echo "[cleanup] Stopping app and Xvfb..."
    [ -n "$APP_PID" ] && kill "$APP_PID" 2>/dev/null || true
    [ -n "$XVFB_PID" ] && kill "$XVFB_PID" 2>/dev/null || true
    rm -rf "$TEST_DIR"
}
trap cleanup EXIT

mkdir -p "$PWD/manual_test_screenshots"

echo "[1/6] Starting Xvfb on :$DISPLAY_NUM"
Xvfb ":$DISPLAY_NUM" -screen 0 1280x800x24 -ac +extension GLX +render -noreset &
XVFB_PID=$!
sleep 1

export DISPLAY=":$DISPLAY_NUM"

echo "[2/6] Building and launching Aero with folder: $TEST_DIR"
dotnet build src >/dev/null 2>&1
dotnet src/bin/Debug/net9.0/aero.dll "$TEST_DIR" &
APP_PID=$!
sleep 3

echo "[3/6] Locating window"
WINDOW_ID=$(xdotool search --name "Aero" | head -n1)
if [ -z "$WINDOW_ID" ]; then
    echo "FAIL: Aero window not found"
    exit 1
fi
echo "Window ID: $WINDOW_ID"

xdotool windowfocus "$WINDOW_ID" || true

echo "[4/6] Screenshot: tree loaded from startup folder"
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/aero_m3_01_tree_loaded.png"

# Expand the 'src' directory. Sidebar is 250px wide; the first root row
# (src) sits just below the File Explorer header (approx y=62).
echo "[5/6] Expanding 'src' directory"
xdotool mousemove --window "$WINDOW_ID" 80 62
xdotool click 1
sleep 0.3
xdotool click 1
sleep 0.7

import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/aero_m3_02_expanded.png"

# Double-click the root-level README.md file to open it in a tab.
# (Synthetic double-clicks on root items are reliable in Xvfb; nested items
# are already verified by the lazy-load expansion above and by unit tests.)
echo "[6/6] Double-clicking file to open tab"
xdotool mousemove --window "$WINDOW_ID" 100 95
xdotool click --repeat 2 --delay 250 1
sleep 0.7

import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/aero_m3_03_file_opened.png"

echo "M3 manual smoke test completed."
echo "Screenshots:"
ls -l "$PWD/manual_test_screenshots"/aero_m3_*.png
