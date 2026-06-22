# 8.2 — Theme Engine: To Fix

> **Status:** Active — pre-implementation risks recorded (2026-06-22).
> Resolve all open items before declaring 8.2 complete.
>
> This file is the persistent code-quality checklist for 8.2 (Theme Engine).
> Add findings here during and after each implementation/review round;
> mark each item `[x]` when fixed and note the fix inline.

---

## User Review — Pre-Implementation Audit (2026-06-22)

13 issues identified and verified against live `src/`. All confirmed as either addressed
by the implementation plan or low-risk informational items.

| # | Severity | Summary | Plan Addressed? |
|---|----------|---------|-----------------|
| 1 | Medium | Token count 103 ≠ 115 (README/TOKENS.md mismatch) | ✅ R1.1 |
| 2 | High | 23+ hardcoded hex colors in Views | ✅ Plan Step 7 |
| 3 | Low | Tokens with no current matching view (debug, syntax, etc.) | ✅ Informational |
| 4 | High | App.axaml missing theme dictionary include | ✅ Plan Step 4 |
| 5 | — | SettingsModel.Theme verified at line 7 | ✅ Verified |
| 6 | — | ISettingsService.Load/Save verified | ✅ Verified |
| 7 | High | ShellViewModel missing theme toggle | ✅ Plan Step 6 |
| 8 | Medium | EditorView.axaml.cs hardcoded DarkPlus | ✅ R1.3 / Plan Step 8 |
| 9 | Medium | GitDiffViewModel hardcoded brushes | ✅ R1.4 / Plan Step 7 |
| 10 | Low | Ctrl+Shift+T key binding available | ✅ Plan adds it |
| 11 | High | ThemeService not in DI | ✅ Plan Step 5 |
| 12 | Medium | Phase 8 TOFIX persistent checks incomplete | ✅ Precondition |
| 13 | Medium | Roadmap Phase 8.2 still `[ ]` | ✅ Plan Step 10 |

**Conclusion:** Plan is sound — all 13 verified issues have corresponding plan steps.

---

## Round 1 — Pre-Implementation Risks (2026-06-22)

---

### R1.1 ~~TOKENS.md header says "Grand Total: 115 tokens" but actual count is 103~~ *(priority: medium)* ✅

**Description:** `TOKENS.md` line 185 states "Grand Total: 115 tokens" and the README
repeats "115 semantic color tokens" in several places. However, the actual numbered
entries in TOKENS.md add up to **103** (Global 6 + Window 5 + Editor 14 + Tab 7 +
Panel 12 + StatusBar 4 + Menu 5 + Button 5 + Input 5 + Scrollbar 4 + Dialog 6 +
FindReplace 3 + Diff 10 + Graph 5 + Git 5 + Debug 5 + Syntax 8 + Notification 3 +
Badge 3 = 103).

The discrepancy means any test that asserts "103 token keys resolve" will be correct
against the actual file, but the README and plan doc will say "115" — confusing for
future readers.

**Resolution:** The summary table was arithmetically wrong (missing Syntax Highlighting 8 + Notification 3 + Badge 3). TOKENS.md summary table now includes a verified total line. Both AXAML files confirmed at exactly 115 tokens with matching keys.

**Status:** [x] Closed (2026-06-22)

---

### R1.2 `OutputView.axaml` uses dark hardcoded colors while all other panels use light *(priority: low)*

**Description:** `OutputView.axaml` uses `#1E1E1E` / `#2D2D30` / `#D4D4D4` (dark
palette), while `FileExplorerView.axaml` and `ProblemsView.axaml` use `#FAFAFA` /
`#F0F0F0` (light palette). This inconsistency existed before Phase 8.2 and will be
fixed when hardcoded colors are replaced with tokens — but the Output view's light
preset tokens need to be intentionally lighter than its current hardcoded dark values.

This is not a bug (both will become tokens), but the implementing agent must be aware
that `panel.background` in the Light preset should NOT use OutputView's current dark
values.

**Required fix:** None — fixed by token replacement in Step 7. Documented here to
prevent accidental use of dark values in the Light preset.

**Status:** [ ] Open (informational — no code change needed, resolved by Step 7)

---

### R1.3 `EditorView.axaml.cs` hardcodes `ThemeName.DarkPlus` — TextMate theme is not theme-aware *(priority: high)*

**Description:** `src/Views/EditorView.axaml.cs` constructs `RegistryOptions` with
`ThemeName.DarkPlus` unconditionally. When the user switches to the Light theme, the
editor will still use the Dark+ TextMate theme — dark syntax colors on a white
background.

**Required fix:** In Step 8 of the implementation, replace the hardcoded constructor
with a dynamic selection based on `Application.Current?.ActualThemeVariant`:

```csharp
private RegistryOptions _registryOptions =
    new(Application.Current?.ActualThemeVariant == ThemeVariant.Dark
        ? ThemeName.DarkPlus
        : ThemeName.Light);
```

Also register for `ActualThemeVariantChanged` to update `_registryOptions` at runtime
when the user toggles themes.

**Status:** [ ] Open

---

### R1.4 `GitDiffViewModel.cs` color lookups may fail before Application is set *(priority: low)*

**Description:** `GitDiffViewModel.cs` creates `SolidColorBrush` instances in property
initializers (e.g., `Addition => new SolidColorBrush(0xFF008000)`). When replaced
with `Application.Current?.TryFindResource(...)`, the lookup will return `null` during
unit tests (no Avalonia app) and potentially during early init before
`Application.Current` is set.

**Required fix:** Use the same fallback pattern as Phase 8.5 `GlyphGeometry`:

```csharp
Application.Current?.TryFindResource("diff.insertedText") as SolidColorBrush
    ?? new SolidColorBrush(0xFF008000) // fallback for tests / early init
```

**Status:** [ ] Open

---

### R1.5 `ISettingsService` has no `ThemeChanged` event — runtime toggle cannot notify other ViewModels *(priority: medium)*

**Description:** When `ThemeService.ToggleThemeAsync()` changes the theme, only
`Application.Current.RequestedThemeVariant` is updated. ViewModels that need to react
(e.g., `EditorViewModel` to update TextMate theme, or future 8.3 Command Palette to
re-color) have no notification mechanism.

`DynamicResource` in XAML handles visual updates automatically, but code-behind
properties (like `_registryOptions` in `EditorView.axaml.cs`) need explicit
notification.

**Required fix options:**
- (a) Use `Application.Current.ActualThemeVariantChanged` event (preferred — already
  exists in Avalonia 11.3, no custom event needed)
- (b) Add `IObservable<string> ThemeChanged` to `ISettingsService` (heavier)

Recommended: option (a). Document in implementation plan that ViewModels should subscribe
to `ActualThemeVariantChanged` rather than a custom event.

**Status:** [ ] Open (recommendation: use Avalonia built-in event)

---

### R1.6 `DynamicResource` does not work on `TemplatedControl` template bindings *(priority: medium, may be blocker)*

**Description:** Avalonia 11.3 `DynamicResource` works in XAML element property
bindings (e.g., `<Border Background="{DynamicResource panel.background}">`). However,
inside `ControlTemplate` definitions (used by `ControlThemes.axaml` for Button, TextBox
etc.), `DynamicResource` bindings may not re-resolve when the theme changes — this is
a known Avalonia 11.x limitation documented in GitHub issue #14657.

If `ControlThemes.axaml` uses `{StaticResource}` (as it does today), the values are
parsed once at load time. If changed to `{DynamicResource}`, they should re-resolve at
runtime — but this must be verified.

**Required fix:** During Step 7, verify that buttons, textboxes, and other templated
controls visually update when the theme is toggled. If they don't, the fallback is to
remove `ControlThemes.axaml` setters for color properties and instead set them in the
Light/Dark preset ResourceDictionaries directly (affecting the Avalonia theme-level
defaults).

**Status:** [ ] Open — spike required during implementation

---

### R1.7 JSON override path `~/.aero/theme-override.json` must not be created at startup *(priority: low)*

**Description:** If `ThemeService.ApplyThemeAsync()` tries to read the override file
and it doesn't exist, it should silently use the preset with no override — NOT create
an empty file. Creating the file on first read is confusing (user sees an empty file
they didn't create) and violates YAGNI.

**Required fix:** `LoadOverrideAsync()` should return empty dict if file doesn't exist.
The override file should only be created when the user explicitly saves overrides
(future 8.6 Settings Page).

**Status:** [ ] Open

---

## Round 2 — Expected During Implementation (Predictive)

*Items below are anticipated based on plan analysis. Add actual findings here as they
are discovered during coding.*

### R2.1 (Predictive) Syntax tokens 102-109 may cause confusion in JSON override

The syntax tokens are informational only (TextMate drives highlighting), but if a user
overrides them in `theme-override.json`, nothing will happen. Consider adding a comment
in the override example or a README note that these are informational.

**Status:** [ ] Open (will verify during implementation)
