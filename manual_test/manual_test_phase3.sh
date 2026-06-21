#!/bin/bash
set -e

# Manual smoke test for Phase 3 (Syntax Highlighting & Language Status Bar).
# Runs Aero under Xvfb with a startup folder containing C#, JSON, XML, and Markdown
# sample files, opens each one, and captures screenshots for visual confirmation of
# syntax coloring and the language label in the status bar.

APP_PID=""
XVFB_PID=""
DISPLAY_NUM=98

TEST_DIR=$(mktemp -d /tmp/aero-phase3-XXXXXX)

mkdir -p "$TEST_DIR/src"

cat > "$TEST_DIR/src/Program.cs" <<'CS'
using System;

namespace Demo;

class Program
{
    static void Main(string[] args)
    {
        // A comment
        Console.WriteLine("Hello, World!");
        int x = 42;
        if (x > 0)
        {
            Console.WriteLine("positive");
        }
    }
}
CS

cat > "$TEST_DIR/src/config.json" <<'JSON'
{
    "name": "demo",
    "version": "1.0.0",
    "items": [1, 2, 3]
}
JSON

cat > "$TEST_DIR/src/data.xml" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<Root>
    <Item id="1">Value</Item>
</Root>
XML

cat > "$TEST_DIR/README.md" <<'MD'
# Demo Project

This is a **markdown** sample.

```csharp
Console.WriteLine("code fence");
```
MD

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
dotnet build src/aero.csproj >/dev/null 2>&1
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

# Helper: click the file at the given tree row offset (file explorer is ~180px wide).
open_file_at() {
    local y=$1
    local name=$2
    xdotool mousemove --window "$WINDOW_ID" 90 "$y"
    xdotool click --repeat 2 --delay 250 1
    sleep 0.7
    import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/phase3_${name}.png"
}

echo "[4/6] Screenshot: tree loaded with sample files"
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/phase3_01_tree_loaded.png"

# Expand 'src' so the nested files are visible.
echo "[5/6] Expanding 'src' directory"
xdotool mousemove --window "$WINDOW_ID" 80 62
xdotool click 1
sleep 0.3
xdotool key --window "$WINDOW_ID" Return
sleep 0.7
import -window "$WINDOW_ID" "$PWD/manual_test_screenshots/phase3_02_expanded.png"

# Open each sample file and capture the highlighted editor + status bar.
echo "[6/6] Opening sample files (C#, JSON, XML, Markdown)"
open_file_at 82 "cs"
open_file_at 102 "json"
open_file_at 122 "xml"
open_file_at 142 "md"

echo "Phase 3 manual smoke test completed."
echo "Screenshots:"
ls -l "$PWD/manual_test_screenshots"/phase3_*.png

echo "----------------------------------------------------------------------"
echo "MANUAL CHECKLIST (inspect screenshots or run on a real display):"
echo "  1. phase3_02_expanded.png shows src/ with Program.cs, config.json,"
echo "     data.xml, README.md."
echo "  2. phase3_cs.png shows C# tokens colored and status bar says 'C#'."
echo "  3. phase3_json.png shows JSON keys/strings colored and status bar says 'JSON'."
echo "  4. phase3_xml.png shows XML tags colored and status bar says 'XML'."
echo "  5. phase3_md.png shows Markdown formatting and status bar says 'Markdown'."
echo "----------------------------------------------------------------------"
