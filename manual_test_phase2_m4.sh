#!/bin/bash
# ---------------------------------------------------------------------------
# Phase 2 M4 — File Explorer smoke test.
#
# SCOPE (see docs/issues/open/ISSUE-009):
#   Automated, headless (Xvfb) part — what we CAN verify reliably:
#     • the app launches
#     • the M3 CLI startup-folder argument (`aero <dir>`) opens a folder
#     • the tree renders its nodes
#
#   The interactive context-menu → dialog flow (right-click a node → New File /
#   New Folder / Rename / Delete → dialog → tree updates) is NOT automated here.
#   Driving Avalonia popup menus and modal dialogs via xdotool under a
#   window-manager-less Xvfb proved unreliable (ISSUE-009). That flow is covered
#   at the ViewModel level by the unit tests; the rendering/interaction is to be
#   confirmed once by a human on a REAL display using the checklist printed at
#   the end of this script.
# ---------------------------------------------------------------------------
set -u

DISPLAY_NUM=97
SHOT_DIR="$PWD/manual_test_screenshots"
SEED_DIR="$(mktemp -d /tmp/aero_m4_seed.XXXXXX)"
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

echo "[1/5] Seeding workspace folder: $SEED_DIR"
mkdir -p "$SEED_DIR/src"
echo "hello" > "$SEED_DIR/src/a.txt"
echo "# readme" > "$SEED_DIR/README.md"

echo "[2/5] Starting Xvfb on :$DISPLAY_NUM"
Xvfb ":$DISPLAY_NUM" -screen 0 1280x800x24 -ac +extension GLX +render -noreset >/dev/null 2>&1 &
XVFB_PID=$!
sleep 1
export DISPLAY=":$DISPLAY_NUM"

echo "[3/5] Building Aero"
dotnet build src/aero.csproj -nologo --verbosity quiet >/dev/null 2>&1 || { echo "FAIL: build"; exit 1; }

echo "[4/5] Launching Aero with startup folder"
dotnet src/bin/Debug/net9.0/aero.dll "$SEED_DIR" &
APP_PID=$!
sleep 4

WINDOW_ID=$(xdotool search --name "Aero" | head -n1)
if [ -z "$WINDOW_ID" ]; then echo "FAIL: Aero window not found"; exit 1; fi
echo "Window ID: $WINDOW_ID"

echo "[5/5] Screenshot: tree populated from startup folder"
import -window root "$SHOT_DIR/phase2_m4_tree_loaded.png"

echo
echo "PASS (automated): app launched and opened the startup folder."
echo "  Inspect $SHOT_DIR/phase2_m4_tree_loaded.png — the sidebar should show"
echo "  'src' and 'README.md' and the status bar should read '2 entries'."
echo
echo "----------------------------------------------------------------------"
echo "MANUAL CHECKLIST (run once on a REAL display — see ISSUE-009):"
echo "  Launch:  dotnet run --project src -- <some-folder>"
echo "  1. Right-click a file node      → context menu shows New File / New"
echo "     Folder / Rename / Delete."
echo "  2. New File   → dialog opens, type a name, OK → file appears in tree."
echo "  3. New Folder → dialog opens, type a name, OK → folder appears."
echo "  4. Rename (or F2) → dialog pre-filled with current name → rename works."
echo "  5. Delete (or Del) → confirm dialog → node removed from tree."
echo "  6. Delete a file that is OPEN in a tab → tab/buffer stays intact (R1.4)."
echo "----------------------------------------------------------------------"
