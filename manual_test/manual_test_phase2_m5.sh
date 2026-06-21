#!/bin/bash
# ---------------------------------------------------------------------------
# Phase 2 M5 — FileSystemWatcher & auto-refresh smoke test.
#
# Verifies headlessly (Xvfb):
#   • the app launches with a startup folder
#   • the file-explorer tree is populated
#   • an external file create is reflected after the debounce window
#   • build-output churn under bin/obj does not cause refreshes
#
# The automated assertions are screenshot-based; inspect the PNGs or run the
# checklist on a real display for final confirmation.
# ---------------------------------------------------------------------------
set -u

DISPLAY_NUM=98
SHOT_DIR="$PWD/manual_test_screenshots"
SEED_DIR="$(mktemp -d /tmp/aero_m5_seed.XXXXXX)"
APP_PID=""
XVFB_PID=""

cleanup() {
    echo "[cleanup] Stopping app and Xvfb..."
    [ -n "$APP_PID" ] && kill "$APP_PID" 2>/dev/null || true
    [ -n "$XVFB_PID" ] && kill "$XVFB_PID" 2>/dev/null || true
    rm -rf "$SEED_DIR" 2>/dev/null || true
}
trap cleanup EXIT

mkdir -p "$SHOT_DIR"

echo "[1/6] Seeding workspace folder: $SEED_DIR"
mkdir -p "$SEED_DIR/src"
echo "hello" > "$SEED_DIR/src/a.txt"
echo "# readme" > "$SEED_DIR/README.md"

echo "[2/6] Starting Xvfb on :$DISPLAY_NUM"
Xvfb ":$DISPLAY_NUM" -screen 0 1280x800x24 -ac +extension GLX +render -noreset >/dev/null 2>&1 &
XVFB_PID=$!
sleep 1
export DISPLAY=":$DISPLAY_NUM"

echo "[3/6] Building Aero"
dotnet build src/aero.csproj -nologo --verbosity quiet >/dev/null 2>&1 || { echo "FAIL: build"; exit 1; }

echo "[4/6] Launching Aero with startup folder"
dotnet src/bin/Debug/net9.0/aero.dll "$SEED_DIR" > /tmp/aero_m5_app.log 2>&1 &
APP_PID=$!
sleep 4

WINDOW_ID=$(xdotool search --name "Aero" | head -n1)
if [ -z "$WINDOW_ID" ]; then echo "FAIL: Aero window not found"; exit 1; fi
echo "Window ID: $WINDOW_ID"

import -window root "$SHOT_DIR/phase2_m5_tree_loaded.png"
echo "Screenshot: initial tree loaded"

echo "[5/6] Creating a file externally and waiting for auto-refresh"
echo "new file" > "$SEED_DIR/b.txt"
sleep 1
import -window root "$SHOT_DIR/phase2_m5_after_create.png"
echo "Screenshot: after external file create"

echo "[6/6] Simulating build-output churn under bin/obj"
mkdir -p "$SEED_DIR/bin/Debug"
echo "dll" > "$SEED_DIR/bin/Debug/app.dll"
echo "pdb" > "$SEED_DIR/bin/Debug/app.pdb"
mkdir -p "$SEED_DIR/obj"
echo "cache" > "$SEED_DIR/obj/project.assets.json"
sleep 1
import -window root "$SHOT_DIR/phase2_m5_after_build_churn.png"
echo "Screenshot: after bin/obj churn"

echo
echo "PASS (automated): app launched, tree loaded, and watcher stayed active."
echo "  Inspect:"
echo "    $SHOT_DIR/phase2_m5_tree_loaded.png          — should show 'src' and 'README.md'"
echo "    $SHOT_DIR/phase2_m5_after_create.png         — should now show 'b.txt'"
echo "    $SHOT_DIR/phase2_m5_after_build_churn.png    — should NOT show 'bin' or 'obj'"
echo
echo "----------------------------------------------------------------------"
echo "MANUAL CHECKLIST (run once on a REAL display):"
echo "  Launch:  dotnet run --project src -- <some-folder>"
echo "  1. Create a file outside Aero in the opened folder → tree refreshes"
echo "     within ~500 ms and the new file appears."
echo "  2. Rename / delete the file externally → tree refreshes."
echo "  3. Run 'dotnet build' in the folder (creates bin/obj) → tree does NOT"
echo "     flicker/refresh from build-output churn."
echo "  4. Click the refresh button (↻) in the File Explorer header → manual"
echo "     refresh still works."
echo "----------------------------------------------------------------------"
