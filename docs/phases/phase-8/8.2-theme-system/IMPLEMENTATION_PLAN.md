# 8.2 — Theme Engine Implementation Plan

**Status:** Draft — pre-implementation (2026-06-22)

---

## M0: Entry Gates

- [ ] `dotnet build src/aero.csproj` succeeds (0 errors)
- [ ] `dotnet test tests` passes (baseline: 495 passed)
- [ ] `docs/phases/phase-8/TOFIX.md` has no open blocker items for 8.2
- [ ] Phase 8.9 Design System is complete (verified ✅)

---

## Source Verification (per plan-rules.md §1)

Every claim below is verified against current `src/` at commit time.

| Claim | Verified In |
|-------|-------------|
| `SettingsModel.Theme` is `string`, defaults to `"Light"` | `src/Models/Settings/SettingsModel.cs` line 7 |
| `ISettingsService.LoadSettingsAsync()` / `SaveSettingsAsync()` exist | `src/Services/ISettingsService.cs` lines 22-23 |
| `SettingsService` persists to `~/.aero/settings.json` | `src/Services/SettingsService.cs` line 37 |
| `App.axaml` merges 7 ResourceDictionaries (Spacing..Icons) | `src/App.axaml` lines 10-16 |
| `App.axaml` loads `SimpleTheme.xaml` as base style | `src/App.axaml` line 19 |
| `App.axaml` loads `ControlThemes.axaml` after SimpleTheme | `src/App.axaml` line 22 |
| `Avalonia.Themes.Simple` 11.3.17 installed | `dotnet list` — confirmed |
| `Application.RequestedThemeVariantProperty` exists in Avalonia 11.3 | `Avalonia.Base.xml` line 34798 |
| `ThemeVariant.Light` / `ThemeVariant.Dark` static properties exist | `Avalonia.Base.xml` lines 34828, 34833 |
| `ActualThemeVariantChanged` event on `StyledElement` | `Avalonia.Base.xml` line 33247 |
| `ShellViewModel` has `_settingsService` field | `src/ViewModels/ShellViewModel.cs` line 40 |
| `ShellViewModel` has no theme-related properties yet | `grep "Theme\|theme" ShellViewModel.cs` — no matches |
| `MainWindow.axaml` has 4 hardcoded hex colors | `#CCCCCC` ×2, `#F0F0F0` |
| `FileExplorerView.axaml` has 7 hardcoded hex colors | `#FAFAFA`, `#CCCCCC` ×3, `#F0F0F0` ×2, `#0078D4`, `#4CAF50` |
| `OutputView.axaml` has 4 hardcoded hex colors | `#1E1E1E`, `#3C3C3C`, `#2D2D30`, `#D4D4D4` |
| `ProblemsView.axaml` has 4 hardcoded hex colors | `#FAFAFA`, `#CCCCCC` ×2, `#F0F0F0` |
| `FindReplaceOverlay.axaml` has 2 hardcoded hex colors | `#E8E8E8`, `#CCCCCC` |
| `EditorView.axaml` has 1 hardcoded hex color | `#4CAF50` |
| `GitGraphView.axaml` has 2 hardcoded hex colors | `#333`, `White` |
| `GitDiffViewModel.cs` has 3 hardcoded `SolidColorBrush` colors | `0xFF008000`, `0xFF800000`, `0xFF0000FF` |
| `EditorView.axaml.cs` hardcodes `ThemeName.DarkPlus` | line — `new RegistryOptions(ThemeName.DarkPlus)` |
| 8.9 `Spacing.axaml` — 6 scale + 8 aliases | Verified: lines 4-23 |
| 8.9 `CornerRadius.axaml` — 7 radius tokens | Verified: lines 4-11 |
| 8.9 `Borders.axaml` — 2 border tokens | Verified: lines 4-6 |
| 8.9 `Shadows.axaml` — 3 shadow levels | Verified: lines 4-6 |
| 8.9 `Typography.axaml` — 5 sizes + 3 weights | Verified: lines 4-12 |
| 8.9 `Transitions.axaml` — 1 duration (200ms) | Verified: line 10 |
| 8.9 `ControlThemes.axaml` — 5 control selectors | Verified: lines 16-49 |
| Token inventory in `TOKENS.md` — 103 tokens (not 115 as README says) | Counted: Global 6 + Window 5 + Editor 14 + Tab 7 + Panel 12 + StatusBar 4 + Menu 5 + Button 5 + Input 5 + Scrollbar 4 + Dialog 6 + FindReplace 3 + Diff 10 + Graph 5 + Git 5 + Debug 5 + Syntax 8 + Notification 3 + Badge 3 = **103** |

---

## Scope

### In scope

1. **Create `src/Styles/ThemeLight.axaml`** — Light preset `ResourceDictionary` with all 103 color tokens as `SolidColorBrush` resources
2. **Create `src/Styles/ThemeDark.axaml`** — Dark preset `ResourceDictionary` with all 103 color tokens as `SolidColorBrush` resources
3. **Create `src/Services/ThemeService.cs`** — manages active theme, runtime switching, JSON override loading
4. **Modify `src/App.axaml`** — load `ThemeLight.axaml` by default, merge into ResourceDictionaries
5. **Modify `src/App.axaml.cs`** — resolve `ThemeService` at startup, apply persisted theme + JSON override
6. **Modify `src/ViewModels/ShellViewModel.cs`** — add `ThemeVariant` property + `ToggleThemeCommand` (Ctrl+Shift+T)
7. **Modify `src/Models/Settings/SettingsModel.cs`** — no change needed (already has `Theme` property)
8. **Replace all hardcoded hex colors** in Views with `{DynamicResource tokenName}` bindings (23 occurrences across 7 files + 3 in GitDiffViewModel)
9. **Modify `src/Views/EditorView.axaml.cs`** — replace hardcoded `ThemeName.DarkPlus` with dynamic TextMate theme selection based on active theme
10. **Update `docs/roadmap/PHASES.md`** — mark 8.2 items `[x]`

### Out of scope

- No custom theme editor UI (deferred to future phase)
- No theme plugin system
- No per-workspace theme (global only)
- No `RequestedThemeVariant` sync with OS dark mode (deferred to Phase 9)
- No syntax token recoloring (tokens 102-109 are informational only, TextMate drives actual highlighting)
- No status bar theme indicator (deferred to 8.6 Settings Page)

---

## Architecture

### How Avalonia 11.3 Theme Switching Works

```
Application.RequestedThemeVariant = ThemeVariant.Light (or .Dark)
         │
         ▼
ResourceDictionary lookup order:
  1. Application.Resources (ThemeLight.axaml or ThemeDark.axaml)
  2. MergedDictionaries (Spacing, CornerRadius, ... — theme-agnostic)
  3. ControlThemes.axaml (style overrides)
  4. SimpleTheme.xaml (Avalonia base)
```

- `DynamicResource` bindings re-resolve when `RequestedThemeVariant` changes — no restart needed.
- Light and Dark presets define the **same 103 resource keys** with different values.
- JSON override is applied at startup by modifying the active ResourceDictionary before controls render.

### ThemeService Design

```csharp
public sealed class ThemeService
{
    // Load Light or Dark preset ResourceDictionary
    // Apply JSON override (~/.aero/theme-override.json) on top
    // Switch at runtime via Application.Current.RequestedThemeVariant
}
```

- Resolves `SettingsModel.Theme` → `ThemeVariant.Light` or `ThemeVariant.Dark`
- Loads corresponding preset ResourceDictionary
- Applies JSON override (if exists) by parsing `~/.aero/theme-override.json` and setting matching keys on the merged dictionary
- Runtime switch: sets `Application.Current.RequestedThemeVariant`, saves to settings

### JSON Override Design

File: `~/.aero/theme-override.json`

```json
{
  "editor.background": "#1a1a2e",
  "editor.foreground": "#e0e0e0",
  "global.accent": "#e94560"
}
```

- Created on first user edit (not at startup)
- Any token key from TOKENS.md is overridable
- Values must be valid hex color strings (`#RRGGBB` or `#RRGGBBAA`)
- Unknown keys are silently ignored
- Empty file = no override (full preset)

---

## Implementation

### Step 1: Create Light Preset — `src/Styles/ThemeLight.axaml`

`ResourceDictionary` with 103 `SolidColorBrush` entries using light-appropriate colors.

Token naming follows TOKENS.md: `{area}.{property}` → resource key (e.g., `editor.background`).

**Color palette (Light preset — Rider-inspired):**

| Area | Primary BG | Primary FG | Notes |
|------|-----------|-----------|-------|
| Window | `#FFFFFF` | `#1E1E1E` | White window background |
| Editor | `#FFFFFF` | `#1E1E1E` | White editor canvas |
| Panel (sidebar) | `#F5F5F5` | `#3C3C3C` | Slightly warm gray |
| Panel (bottom) | `#F5F5F5` | `#3C3C3C` | Same as sidebar |
| Status Bar | `#0078D4` | `#FFFFFF` | Blue (VS/Rider style) |
| Tab (active) | `#FFFFFF` | `#1E1E1E` | Matches editor bg |
| Tab (inactive) | `#ECECEC` | `#6E6E6E` | Slightly darker |
| Accent | `#0078D4` | — | Blue primary accent |
| Error | `#E81123` | — | Red destructive |
| Warning | `#F7630C` | — | Orange warning |

**Notation:** The actual file will be ~120 lines. Each token maps to a `SolidColorBrush`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui">
    <!-- Phase 8.2 Theme Engine: Light Preset — 103 color tokens -->
    <!-- Attribution: Colors inspired by JetBrains Rider / VS Code Light+ -->
    <SolidColorBrush x:Key="global.background" Color="#FFFFFF"/>
    <SolidColorBrush x:Key="global.foreground" Color="#1E1E1E"/>
    <SolidColorBrush x:Key="global.accent" Color="#0078D4"/>
    <!-- ... remaining 100 tokens ... -->
</ResourceDictionary>
```

### Step 2: Create Dark Preset — `src/Styles/ThemeDark.axaml`

Same 103 keys, dark-appropriate values:

| Area | Primary BG | Primary FG | Notes |
|------|-----------|-----------|-------|
| Window | `#1E1E1E` | `#D4D4D4` | VS Code Dark+ background |
| Editor | `#1E1E1E` | `#D4D4D4` | Same as window |
| Panel (sidebar) | `#252526` | `#CCCCCC` | Slightly lighter than editor |
| Panel (bottom) | `#252526` | `#CCCCCC` | Same as sidebar |
| Status Bar | `#007ACC` | `#FFFFFF` | Brighter blue for dark mode |
| Tab (active) | `#1E1E1E` | `#D4D4D4` | Matches editor bg |
| Tab (inactive) | `#2D2D2D` | `#858585` | Slightly lighter |
| Accent | `#007ACC` | — | Brighter blue for dark bg |
| Error | `#F44747` | — | Lighter red for dark bg |
| Warning | `#CCA700` | — | Muted yellow for dark bg |

### Step 3: Create `src/Services/ThemeService.cs`

```csharp
namespace Aero.Services;

public sealed class ThemeService
{
    private readonly ISettingsService _settings;
    private readonly string _overridePath; // ~/.aero/theme-override.json

    // Apply persisted theme + JSON override at startup
    public async Task ApplyThemeAsync() { ... }

    // Toggle Light ↔ Dark at runtime
    public async Task ToggleThemeAsync() { ... }

    // Apply JSON override to active ResourceDictionary
    private void ApplyOverride(Dictionary<string, string> overrides) { ... }

    // Parse override file, return empty dict if missing/invalid
    private async Task<Dictionary<string, string>> LoadOverrideAsync() { ... }
}
```

**Runtime switch flow:**

```
ToggleThemeAsync()
  1. Read current SettingsModel.Theme ("Light" or "Dark")
  2. Flip to opposite ("Dark" or "Light")
  3. Save to settings.json via ISettingsService
  4. Swap ResourceDictionary (remove old, add new)
  5. Apply JSON override on top
  6. Set Application.Current.RequestedThemeVariant
```

### Step 4: Modify `src/App.axaml`

Add theme ResourceDictionary after the existing MergedDictionaries:

```xml
<ResourceDictionary.MergedDictionaries>
    <!-- ...existing 7 includes (Spacing..Icons)... -->
    <!-- Phase 8.2: Theme preset (default: Light) -->
    <ResourceInclude Source="avares://aero/Styles/ThemeLight.axaml" />
</ResourceDictionary.MergedDictionaries>
```

### Step 5: Modify `src/App.axaml.cs`

After DI setup, resolve ThemeService and apply:

```csharp
// In OnFrameworkInitializationCompleted, after BuildServices():
var themeService = _services.GetRequiredService<ThemeService>();
await themeService.ApplyThemeAsync();
```

Register ThemeService in DI:

```csharp
services.AddSingleton<ThemeService>();
```

### Step 6: Modify `src/ViewModels/ShellViewModel.cs`

Add theme toggle:

```csharp
// New property:
[Reactive] public bool IsDarkTheme { get; set; }

// New command:
public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }

// In constructor:
ToggleThemeCommand = ReactiveCommand.CreateFromTask(ToggleThemeAsync);

private async Task ToggleThemeAsync()
{
    var themeService = ...; // injected via DI
    await themeService.ToggleThemeAsync();
    IsDarkTheme = _settingsService.LoadSettingsAsync().Result.Theme == "Dark";
}
```

Wire keyboard shortcut `Ctrl+Shift+T` in `MainWindow.axaml.cs` or keymap.

### Step 7: Replace Hardcoded Colors in Views

Replace every hardcoded `#hex` with `{DynamicResource tokenName}`:

| File | Line | Old | New |
|------|------|-----|-----|
| `MainWindow.axaml` | 96 | `Background="#CCCCCC"` | `Background="{DynamicResource panel.border}"` |
| `MainWindow.axaml` | 115 | `Background="#CCCCCC"` | `Background="{DynamicResource panel.border}"` |
| `MainWindow.axaml` | 138-139 | `Background="#F0F0F0" BorderBrush="#CCCCCC"` | `Background="{DynamicResource panel.sectionBackground}" BorderBrush="{DynamicResource panel.border}"` |
| `FileExplorerView.axaml` | 9-10 | `Background="#FAFAFA" BorderBrush="#CCCCCC"` | `Background="{DynamicResource panel.background}" BorderBrush="{DynamicResource panel.border}"` |
| `FileExplorerView.axaml` | 16-17 | `Background="#F0F0F0" BorderBrush="#CCCCCC"` | `Background="{DynamicResource panel.sectionBackground}" BorderBrush="{DynamicResource panel.border}"` |
| `FileExplorerView.axaml` | 38-39 | `Background="#F0F0F0" BorderBrush="#CCCCCC"` | `Background="{DynamicResource panel.sectionBackground}" BorderBrush="{DynamicResource panel.border}"` |
| `FileExplorerView.axaml` | 62 | `Foreground="#0078D4"` | `Foreground="{DynamicResource global.accent}"` |
| `FileExplorerView.axaml` | 107 | `Foreground="#4CAF50"` | `Foreground="{DynamicResource git.stagedForeground}"` |
| `OutputView.axaml` | 9-10 | `Background="#1E1E1E" BorderBrush="#3C3C3C"` | `Background="{DynamicResource panel.background}" BorderBrush="{DynamicResource panel.border}"` |
| `OutputView.axaml` | 15 | `Background="#2D2D30"` | `Background="{DynamicResource editor.background}"` |
| `OutputView.axaml` | 58 | `Foreground="#D4D4D4"` | `Foreground="{DynamicResource editor.foreground}"` |
| `ProblemsView.axaml` | 10-11 | `Background="#FAFAFA" BorderBrush="#CCCCCC"` | `Background="{DynamicResource panel.background}" BorderBrush="{DynamicResource panel.border}"` |
| `ProblemsView.axaml` | 16-17 | `Background="#F0F0F0" BorderBrush="#CCCCCC"` | `Background="{DynamicResource panel.sectionBackground}" BorderBrush="{DynamicResource panel.border}"` |
| `FindReplaceOverlay.axaml` | 15-16 | `Background="#E8E8E8" BorderBrush="#CCCCCC"` | `Background="{DynamicResource findReplace.background}" BorderBrush="{DynamicResource findReplace.border}"` |
| `EditorView.axaml` | 26 | `Foreground="#4CAF50"` | `Foreground="{DynamicResource git.stagedForeground}"` |
| `GitGraphView.axaml` | 48-49 | `Background="#333"` / `Foreground="White"` | `Background="{DynamicResource badge.background}"` / `Foreground="{DynamicResource badge.foreground}"` |

**GitDiffViewModel.cs** — replace hardcoded `SolidColorBrush` with resource lookups (requires Avalonia `Application.Current?.TryFindResource()` pattern, same as `GlyphGeometry` in Phase 8.5):

```csharp
// Before:
new SolidColorBrush(0xFF008000)  // Addition
new SolidColorBrush(0xFF800000)  // Deletion
new SolidColorBrush(0xFF0000FF)  // Header

// After:
Application.Current?.TryFindResource("diff.insertedText") as SolidColorBrush
Application.Current?.TryFindResource("diff.removedText") as SolidColorBrush
Application.Current?.TryFindResource("diff.headerGutter") as SolidColorBrush
```

### Step 8: Modify `src/Views/EditorView.axaml.cs`

Replace hardcoded `ThemeName.DarkPlus` with theme-aware selection:

```csharp
// Before:
private readonly RegistryOptions _registryOptions = new(ThemeName.DarkPlus);

// After: derive from active theme
private RegistryOptions _registryOptions = new(ThemeName.Light);

// In theme-change handler or init:
var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
_registryOptions = new RegistryOptions(isDark ? ThemeName.DarkPlus : ThemeName.LightPlus);
```

---

## Files to Create / Modify

| File | Action | Lines (est.) |
|------|--------|-------------|
| `src/Styles/ThemeLight.axaml` | Create | ~115 |
| `src/Styles/ThemeDark.axaml` | Create | ~115 |
| `src/Services/ThemeService.cs` | Create | ~120 |
| `src/App.axaml` | Modify | +1 line (ResourceInclude) |
| `src/App.axaml.cs` | Modify | +5 lines (DI + apply) |
| `src/ViewModels/ShellViewModel.cs` | Modify | +15 lines (toggle command) |
| `src/MainWindow.axaml` | Modify | 4 replacements |
| `src/Views/FileExplorerView.axaml` | Modify | 7 replacements |
| `src/Views/OutputView.axaml` | Modify | 4 replacements |
| `src/Views/ProblemsView.axaml` | Modify | 4 replacements |
| `src/Views/FindReplaceOverlay.axaml` | Modify | 2 replacements |
| `src/Views/EditorView.axaml` | Modify | 1 replacement |
| `src/Views/EditorView.axaml.cs` | Modify | ~3 lines |
| `src/Views/GitGraphView.axaml` | Modify | 2 replacements |
| `src/ViewModels/GitDiffViewModel.cs` | Modify | 3 replacements |
| `docs/roadmap/PHASES.md` | Modify | Mark 8.2 items [x] |
| `tests/Services/ThemeServiceTests.cs` | Create | ~80 |

**Estimated total:** ~700 lines new, ~50 lines modified.

---

## Limitations (by design)

1. **No OS theme sync** — User must toggle manually. OS dark/light mode detection deferred to Phase 9 (requires `PlatformThemeVariant` API).
2. **103 tokens, not 115** — TOKENS.md header says 115 but actual inventory is 103. The README says "115" in a few places; this will be corrected during implementation.
3. **Syntax tokens (102-109) are informational only** — They are included in the preset JSON for advanced users but NOT applied via `DynamicResource`. TextMate drives actual syntax colors.
4. **No per-workspace theme** — Theme is global. Per-workspace theme deferred to Phase 9 (settings service would need workspace-scoped overrides).
5. **JSON override is read once at startup** — Changes to `theme-override.json` while the app is running are not hot-reloaded. User must toggle theme or restart to see changes.
6. **GitDiffViewModel resource lookup at render time** — `TryFindResource` may return null if called before `Application.Current` is set. The existing pattern (Phase 8.5 `GlyphGeometry`) handles this with `?? SolidColorBrush` fallback.

---

## Definition of Done (Exit Gates)

- [ ] `dotnet build src/aero.csproj` — 0 errors
- [ ] `dotnet test tests` — 495+ passed, 0 new failures
- [ ] `src/Styles/ThemeLight.axaml` created with 103 color tokens
- [ ] `src/Styles/ThemeDark.axaml` created with 103 color tokens
- [ ] `src/Services/ThemeService.cs` created with `ApplyThemeAsync` + `ToggleThemeAsync`
- [ ] `Ctrl+Shift+T` toggles Light ↔ Dark with no restart
- [ ] All 23 hardcoded hex colors in Views replaced with `{DynamicResource}`
- [ ] All 3 hardcoded `SolidColorBrush` in `GitDiffViewModel.cs` replaced with resource lookups
- [ ] `EditorView.axaml.cs` uses dynamic TextMate theme (Light/LightPlus vs Dark/DarkPlus)
- [ ] `~/.aero/theme-override.json` partial override merges correctly (tested)
- [ ] Empty/missing override file does not crash
- [ ] `SettingsModel.Theme` persists across restart
- [ ] Pre-existing 495 tests all pass
- [ ] `docs/roadmap/PHASES.md` Phase 8.2 items all `[x]`

---

## Tests

| # | Test | Verifies | Type |
|---|------|----------|------|
| 1 | `ThemeService_LightPreset_AllKeysResolve` | All 103 token keys resolve in Light preset | Unit |
| 2 | `ThemeService_DarkPreset_AllKeysResolve` | All 103 token keys resolve in Dark preset | Unit |
| 3 | `ThemeService_ApplyOverride_PartialOverrideMerges` | Override replaces only specified tokens | Unit |
| 4 | `ThemeService_ApplyOverride_EmptyOverrideNoOp` | Empty/null override does not crash | Unit |
| 5 | `ThemeService_ApplyOverride_UnknownKeysIgnored` | Invalid keys in override are silently skipped | Unit |
| 6 | `ThemeService_ApplyOverride_InvalidHexIgnored` | Invalid color values are silently skipped | Unit |
| 7 | `ThemeService_ToggleTheme_SavesToSettings` | Toggle persists "Dark"→"Light" or vice versa | Unit |
| 8 | `ThemeService_ToggleTheme_SwitchesVariant` | Toggle sets `RequestedThemeVariant` correctly | Integration |
| 9 | `SettingsModel_Theme_DefaultsToLight` | New SettingsModel has `Theme = "Light"` | Unit |
| 10 | `SettingsModel_Theme_RoundTrips` | Load→save→load preserves theme string | Integration |
| 11 | Manual: Ctrl+Shift+T toggles theme | Visual verification | Manual |
| 12 | Manual: All panels update on toggle (no stale colors) | Visual verification | Manual |

**Test file:** `tests/Services/ThemeServiceTests.cs` (new, ~80 lines).

---

## Dependency on 8.7 ISettingsService

ThemeService uses `ISettingsService.LoadSettingsAsync()` / `SaveSettingsAsync()` to persist the active theme. This interface and its implementation are already complete (Phase 8.7). No changes to ISettingsService are required.

---

## Keyboard Shortcut

`Ctrl+Shift+T` toggles theme. Implementation:
- Add `KeyGesture(Key.T, KeyModifiers.Shift | KeyModifiers.Control)` binding
- Wire to `ShellViewModel.ToggleThemeCommand`
- File → View menu item "Toggle Theme" also invokes same command

---

## DI Registration

In `src/App.axaml.cs` `BuildServices()`:

```csharp
// Phase 8.2 — Theme Engine
services.AddSingleton<ThemeService>();
```

Resolved after `BuildServices()` in `OnFrameworkInitializationCompleted`:

```csharp
var themeService = _services.GetRequiredService<ThemeService>();
await themeService.ApplyThemeAsync();
```
