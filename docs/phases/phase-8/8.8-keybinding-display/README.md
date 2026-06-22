# 8.8 — Keybinding Display (Read-Only)

**Goal:** Show a read-only reference of all keyboard shortcuts.

**Scope:**
- Categorized list of shortcuts by area (Editor, File, Navigation, Git, Build, etc.)
- Display as a dialog or overlay
- **Custom keybinding editing is deferred to Phase 9**
- Use the same `CommandRegistry` data source as **8.3 Command Palette** (shared metadata with `{ Name, KeyGesture, Category }`)
- If `CommandRegistry` is not yet implemented, define it as part of this sub-phase and share with 8.3

**Dependencies:**
- **Shared CommandRegistry** — must define `ICommandRegistry` with command metadata. This sub-phase and 8.3 both consume it. The registry collects commands from:
  - `ShellViewModel` (19 ReactiveCommands)
  - `MainWindow.axaml` / `Window.KeyBindings` (keyboard shortcuts)
  - Editor-specific commands (completion, format)
  - Git commands
  - Build commands
- **8.9 Design System** — dialog/overlay styling
- **8.2 Theme Engine** — color tokens

**Exit condition:** User can open a reference page showing all current keyboard shortcuts, organized by category.

**Tests:**
- Unit: CommandRegistry contains all expected commands with correct metadata
- Unit: Categories are non-empty and correctly grouped
- Integration: Keybinding reference opens/closes correctly
- Manual: All shortcuts shown match actual IDE shortcuts

