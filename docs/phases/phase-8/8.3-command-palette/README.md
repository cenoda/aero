# 8.3 — Command Palette

**Goal:** Ctrl+Shift+P opens a fuzzy-search overlay over all registered IDE commands.

**Scope:**
- DialogHost overlay with TextBox + filtered list
- Register existing ReactiveCommands from ShellViewModel + file open as entries
- FuzzySharp match on display name; Enter to execute, Esc to dismiss
- Show keyboard shortcut hints next to each command
- Use the same `CommandRegistry` data source as **8.8 Keybinding Display** (shared metadata)

**Dependencies:**
- **8.9 Design System** — custom overlay styling (corner radius, shadow, spacing)
- **8.2 Theme Engine** — color tokens for overlay background, text, selection
- **Shared CommandRegistry** — define an `ICommandRegistry` service that both 8.3 and 8.8 use. This centralizes command metadata: `{ Name, KeyGesture, Category, ReactiveCommand }`. Currently commands are scattered across ShellViewModel and MainWindow.axaml KeyBindings.

**Implementation Note: Custom Overlay (no DialogHost)**
`DialogHost.Avalonia` requires Avalonia >= 12.0.0 and is incompatible with our Avalonia 11.3 target. The command palette will use a **custom overlay** instead:
- An overlay `Border` with semi-transparent background (rgba) covering the editor area
- A centered `StackPanel` containing a `TextBox` (search input) + `ListBox` (filtered results)
- `Esc` key dismisses the overlay; `Enter` executes the selected command
- `KeyboardNavigation` with arrow keys for item selection
- This is simple enough (< 100 lines of XAML + code-behind) that a library dependency is unnecessary

**Exit condition:** All IDE commands are searchable and executable from the palette. Keyboard shortcuts shown in the list.

**Tests:**
- Unit: CommandRegistry returns all registered commands with correct metadata
- Unit: FuzzySharp filtering returns correct matches (e.g. "mwin" → "Open MainWindow")
- Integration: Custom overlay opens/closes on Ctrl+Shift+P / Esc
- Integration: Selected command executes and closes palette
- Manual: Arrow key navigation works, Esc dismisses, Enter executes

