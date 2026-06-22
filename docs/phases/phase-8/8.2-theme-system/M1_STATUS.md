# M1 Status — Theme Engine Boot

> **Created:** 2026-06-22  
> **Status:** ✅ COMPLETE — Build passes, 518 tests pass (495 baseline + 23 new)
> **Decision:** Sentinel-key approach (Approach 2) — `WireThemeDictionaries()` moved to `ThemeService`

---

## What's Done

| Step | File | Status | Notes |
|------|------|--------|-------|
| 1 | `src/Styles/ThemeLight.axaml` | ✅ Done | 115 `SolidColorBrush` entries, Rider/VS Code Light+ inspired |
| 2 | `src/Styles/ThemeDark.axaml` | ✅ Done | 115 keys, identical names to Light, dark-appropriate values |
| 3 | `src/Services/ThemeService.cs` | ✅ Done | Full implementation including `WireThemeDictionaries()`. Scans `MergedDictionaries` for `_themeVariant` sentinel keys to identify Light/Dark. Namespace `Aero.Services`, `ISettingsService` injected, `_overridePath` for `~/.aero/theme-override.json` |
| 4 | `src/App.axaml` | ✅ Done | Both theme `ResourceInclude`s in `MergedDictionaries` after `Icons.axaml` |
| 5 | `src/App.axaml.cs` | ✅ Done | `themeService.WireThemeDictionaries()` called before `ApplyThemeAsync()`. No more `CS0103` error. |

---

## Resolved: WireThemeDictionaries

**Decision:** Sentinel-key approach (Approach 2 from `WIRETHEME_DECISION.md`).

**What changed:**
1. Added `<sys:String x:Key="_themeVariant">Light</sys:String>` to `ThemeLight.axaml`
2. Added `<sys:String x:Key="_themeVariant">Dark</sys:String>` to `ThemeDark.axaml`
3. Added `WireThemeDictionaries()` public method to `ThemeService` — scans `Application.Current.Resources.MergedDictionaries` for `_themeVariant` keys and assigns to `LightTheme` / `DarkTheme`
4. `App.axaml.cs` calls `themeService.WireThemeDictionaries()` before `ApplyThemeAsync()`

**Why this approach:** Explicit, 100% reliable, no heuristics, no reflection, easy to debug. The `sys:String` sentinel costs negligible memory and is invisible at runtime.

---

## Tests Added

`tests/Services/ThemeServiceTests.cs` — 23 unit tests:
- `LoadOverrideAsync`: file missing, empty, whitespace, invalid JSON, valid JSON, empty object, null values
- `TryParseHexColor`: 4 valid formats (`#RRGGBB`, `#RRGGBBAA`, `#RGB`, no-prefix), 4 invalid formats
- `ApplyOverride`: known keys set, unknown keys ignored, invalid hex skipped, empty/whitespace hex skipped
- Integration: load + apply end-to-end
- Added `internal ThemeService(ISettingsService, string)` constructor for test path injection
- Added `ThemeService.cs` to `tests/aero.Tests.csproj` `<Compile Include>` list

## Documentation Updates

- `docs/phases/phase-8/8.2-theme-system/TOKENS.md` — summary table corrected to show 115 total
- `docs/phases/phase-8/8.2-theme-system/TOFIX.md` — R1.1 marked `[x]` (summary was arithmetically wrong; actual tokens were always 115)
- `docs/roadmap/PHASES.md` — 8.2 items 1–4 marked `[x]`

---

## Scope Boundary (What M1 Does NOT Include)

Per the plan, these are deferred to later milestones:
- ❌ Replace hardcoded colors in Views → **M2**
- ❌ Add `ToggleThemeCommand` to `ShellViewModel` → **M2**
- ❌ Change `EditorView.axaml.cs` TextMate theme → **M2**
- ❌ Ctrl+Shift+T keybinding → **M2**
- ❌ `menu.hoverBackground` and other missing tokens from R1.1 → **already in the 115**

---

## What's Next (Post-M1)

1. ~~Add `WireThemeDictionaries`~~ → **Done** (sentinel-key approach in `ThemeService`)
2. Create `tests/Services/ThemeServiceTests.cs` with override/parsing tests
3. Fix TOKENS.md summary table (R1.1 → mark `[x]`)
4. Update `docs/roadmap/PHASES.md` with 8.2 M1 status
5. M2: Replace hardcoded colors in Views
6. M2: Add `ToggleThemeCommand` to `ShellViewModel`
7. M2: Ctrl+Shift+T keybinding
