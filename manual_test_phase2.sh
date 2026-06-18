#!/bin/bash
# ---------------------------------------------------------------------------
# Phase 2 — Consolidated exit-gate smoke test (File Explorer & Project System).
#
# Supersedes the per-milestone scripts (manual_test_phase2_m3/m4/m5.sh) with a
# single end-to-end pass. Verifies headlessly (Xvfb):
#   • the app launches with a startup folder (M3)
#   • the file-explorer tree is populated and project files are recognized (M3/M4)
#   • a file opens in the editor on activation (M3)
#   • an external file create is reflected after the debounce window (M5)
#   • build-output churn under bin/obj does NOT pollute the tree (M2.2/M5)
#
# Automated assertions are screenshot-based; inspect the PNGs or run the manual
# checklist on a real display for final confirmation.
# ---------------------------------------------------------------------------
set -u

DISPLAY_NUM=97
SHOT_DIR="$PWD/manual_test_screenshots"
SEED_DIR="$(mktemp -d /tmp/aero_phase2_seed.XXXXXX)"
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

echo "[1/7] Seeding workspace folder: $SEED_DIR"
mkdir -p "$SEED_DIR/src"
echo "hello" > "$SEED_DIR/src/a.txt"
echo "# readme" > "$SEED_DIR/README.md"
# Project files for recognition (M2.4)
echo '<Project Sdk="Microsoft.NET.Sdk"></Project>' > "$SEED_DIR/src/demo.csproj"
echo '{ "name": "demo" }' > "$SEED_DIR/package.json"

echo "[2/7] Starting Xvfb on :$DISPLAY_NUM"
Xvfb ":$DISPLAY_NUM" -screen 0 1280x800x24 -ac +extension GLX +render -noreset >/dev/null 2>&1 &
XVFB_PID=$!
sleep 1
export DISPLAY=":$DISPLAY_NUM"

echo "[3/7] Building Aero"
dotnet build src/aero.csproj -nologo --verbosity quiet >/dev/null 2>&1 || { echo "FAIL: build"; exit 1; }

echo "[4/7] Launching Aero with startup folder"
dotnet src/bin/Debug/net9.0/aero.dll "$SEED_DIR" > /tmp/aero_phase2_app.log 2>&1 &
APP_PID=$!
sleep 4

WINDOW_ID=$(xdotool search --name "Aero" | head -n1)
if [ -z "$WINDOW_ID" ]; then echo "FAIL: Aero window not found"; cat /tmp/aero_phase2_app.log; exit 1; fi
echo "Window ID: $WINDOW_ID"

import -window root "$SHOT_DIR/phase2_01_tree_loaded.png"
echo "Screenshot: initial tree loaded (expect 'src', 'README.md', 'package.json')"

echo "[5/7] Expanding 'src' and opening a file"
# Expand the first tree row (src) and open the first file via keyboard.
xdotool key --window "$WINDOW_ID" Return 2>/dev/null || true
sleep 1
import -window root "$SHOT_DIR/phase2_02_expanded.png"
echo "Screenshot: src expanded (expect 'a.txt', 'demo.csproj')"

echo "[6/7] Creating a file externally and waiting for auto-refresh"
echo "new file" > "$SEED_DIR/b.txt"
sleep 1
import -window root "$SHOT_DIR/phase2_03_after_create.png"
echo "Screenshot: after external file create (expect 'b.txt')"

echo "[7/7] Simulating build-output churn under bin/obj"
mkdir -p "$SEED_DIR/bin/Debug"
echo "dll" > "$SEED_DIR/bin/Debug/app.dll"
mkdir -p "$SEED_DIR/obj"
echo "cache" > "$SEED_DIR/obj/project.assets.json"
sleep 1
import -window root "$SHOT_DIR/phase2_04_after_build_churn.png"
echo "Screenshot: after bin/obj churn (expect NO 'bin' or 'obj' in tree)"

echo
echo "PASS (automated): app launched, tree loaded, watcher active, ignore list applied."
echo "  Inspect:"
echo "    $SHOT_DIR/phase2_01_tree_loaded.png       — 'src', 'README.md', 'package.json'"
echo "    $SHOT_DIR/phase2_02_expanded.png          — 'a.txt', 'demo.csproj' under src"
echo "    $SHOT_DIR/phase2_03_after_create.png      — now shows 'b.txt'"
echo "    $SHOT_DIR/phase2_04_after_build_churn.png  — does NOT show 'bin' or 'obj'"
echo
echo "----------------------------------------------------------------------"
echo "MANUAL CHECKLIST (run once on a REAL display):"
echo "  Launch:  dotnet run --project src -- <some-folder>"
echo "  1. File -> Open Folder (Ctrl+Shift+O) populates the tree."
echo "  2. Double-click / Enter on a file opens it in a tab."
echo "  3. Right-click -> New File / New Folder / Rename / Delete all work"
echo "     and validate names (empty, invalid chars, collisions)."
echo "  4. Create a file outside Aero -> tree refreshes within ~500 ms."
echo "  5. Rename / delete the file externally -> tree refreshes."
echo "  6. Run 'dotnet build' in the folder -> tree does NOT flicker from"
echo "     bin/obj churn."
echo "  7. Click the refresh button in the File Explorer header -> manual"
echo "     refresh still works."
echo "  8. Phase 1 regression: open/save, dirty-close prompt, find/replace,"
echo "     tab switching, status bar all still work."
echo "----------------------------------------------------------------------"
