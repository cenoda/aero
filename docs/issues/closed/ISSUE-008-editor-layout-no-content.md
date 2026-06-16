# ISSUE-008: Editor content area is blank — missing AvaloniaEdit theme

- **Label:** BUG
- **Priority:** critical
- **Status:** closed
- **Opened:** 2026-06-17
- **Closed:** 2026-06-17

## Description

After creating a new file, the tab header appeared but the editor content area below
it was completely blank. No line numbers, caret, or text were visible, and typing
seemed to have no effect.

The root cause was **not** layout — the `TabControl` and `TextEditor` were sized
correctly. The `AvaloniaEdit.TextEditor` control simply had **no theme/style loaded**,
so its template was never applied and it rendered nothing.

## Expected Behavior

- Opening or creating a document shows the `TextEditor` with line numbers.
- The editor fills the remaining space below the tab header.
- Typing inserts text into the active document.

## Actual Behavior

- Tab header rendered, but the editor surface was blank/white.
- Typing did not appear in the document (before the fix).

## Debug Log

### Attempt 1
- **Hypothesis:** Text rendering fails in the virtual X11 environment.
- **Action:** Ran the app with `AVALONIA_DISABLE_GPU=true` to force software rendering.
- **Result:** Editor content area was still blank.

### Attempt 2
- **Hypothesis:** The `DockPanel` layout is not giving the `TabControl` enough space.
- **Action:** Replaced `DockPanel` with `Grid` in `EditorView.axaml`.
- **Result:** Still blank; a `TextBlock` placed in the same `ContentTemplate` rendered
  and filled the space, proving the layout was correct and the issue was specific to
  `AvaloniaEdit.TextEditor`.

### Attempt 3
- **Hypothesis:** The `TextEditor` document binding or property initialization is failing.
- **Action:** Replaced the `Document` binding with a hard-coded `Text="direct text test"`
  and `Background="LightYellow"`.
- **Result:** Still blank — the control itself was not rendering regardless of content.

### Attempt 4
- **Hypothesis:** The AvaloniaEdit control theme is missing from `App.axaml`.
- **Action:** Added `<StyleInclude Source="avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml" />`.
- **Result:** Line numbers, caret, and typed text appeared correctly.

## Resolution

Added the AvaloniaEdit Simple theme to `src/App.axaml`:

```xml
<StyleInclude Source="avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml" />
```

Also kept the `EditorView.axaml` cleanup that replaces the `DockPanel` with a `Grid`
so the `TabControl` and welcome message share the same cell explicitly. This is not
required for the fix but makes the layout more robust.

## Files Changed

- `src/App.axaml` — include AvaloniaEdit theme
- `src/Views/EditorView.axaml` — replace `DockPanel` with `Grid`; keep element-name
  binding for the tab close button (ISSUE-007)

## Verification

- `dotnet build` — clean (0 warnings, 0 errors)
- `dotnet test` — 89/89 passing
- Manual GUI test in `Xvfb`:
  - New file (`Ctrl+N`) creates a tab
  - `TextEditor` renders with line number "1"
  - Typing inserts text
  - Dirty indicator (`*`) appears in the tab
  - Find/replace overlay opens with `Ctrl+F`
  - Dirty-close confirmation dialog appears when closing a modified tab

## Related

- ISSUE-007: tab close button binding crash found during the same manual test
