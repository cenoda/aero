# Phase 11: Avalonia 12 Migration

> **Goal:** Upgrade the entire Aero codebase from Avalonia 11.3 to Avalonia 12.0.
> **Entry condition:** Phase 10 (Plugin System) is complete, or earlier if the user elects to prioritize the migration.
> **Exit condition:** All packages updated, all breaking changes resolved, build succeeds, all tests pass, app renders correctly.
>
> **Risk level:** HIGH — Avalonia 12 is a major version with significant breaking changes.
> **Estimated effort:** 3–5 focused sessions.

---

## Why This Matters

| Before (Avalonia 11.3) | After (Avalonia 12.0) |
|-------------------------|----------------------|
| Compiled bindings off by default | Compiled bindings on by default (faster, type-safe) |
| No DialogHost.Avalonia (requires ≥ 12.0) | DialogHost.Avalonia unlocked → proper modal dialogs |
| No Material.Icons.Avalonia compatibility confirmed | Material.Icons.Avalonia confirmed compatible |
| `Avalonia.Diagnostics` (free) | `AvaloniaUI.DiagnosticsSupport` (Avalonia Plus) |
| ReactiveUI via `Avalonia.ReactiveUI` | ReactiveUI via `ReactiveUI.Avalonia` (renamed package) |
| .NET 9 target OK | .NET 8+ required (no .NET Standard/Framework) |
| Direct2D1 available | Direct2D1 removed (Skia-only) |
| Old text shaping | HarfBuzz built-in (platform-aware) |
| Single dispatcher | Multiple dispatcher support |
| Old clipboard API | New `IAsyncDataTransfer` clipboard API |
| `IBinding` / `InstancedBinding` | `BindingBase` / `BindingExpressionBase` |
| Old focus events | New `FocusChangedEventArgs` |
| `Screen` constructible | `Screen` is abstract |

---

## Pre-Implementation Checklist

- [ ] Read the [Avalonia 12 Breaking Changes doc](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes)
- [ ] Read the [Avalonia 12 Release Blog Post](https://avaloniaui.net/blog/avalonia-12)
- [ ] Verify all third-party libraries have Avalonia 12 releases
- [ ] Create a dedicated branch (`avalonia-12-migration`)
- [ ] Ensure all tests pass on `main` before starting (baseline)
- [ ] Tag the last Avalonia 11.3 commit for rollback

---

## Dependency Inventory

### Packages to UPDATE (version bump required)

| Package | Current (11.3) | Target (12.x) | Notes |
|---------|---------------|---------------|-------|
| `Avalonia` | 11.3.* | 12.0.* | Core framework |
| `Avalonia.Desktop` | 11.3.* | 12.0.* | Desktop platform support |
| `Avalonia.Themes.Simple` | 11.3.* | 12.0.* | Base theme |
| `Avalonia.Fonts.Inter` | 11.3.* | 12.0.* | Inter font bundle |
| `Avalonia.Diagnostics` | 11.3.* | **REMOVED** | See replacement below |
| `Avalonia.AvaloniaEdit` | 11.3.* | 12.0.* | ✅ 12.0.0 exists on NuGet |
| `AvaloniaEdit.TextMate` | 11.3.* | 12.0.* | ✅ 12.0.0 exists on NuGet |
| `Dock.Avalonia` | 11.3.* | **REMOVE** | Abandoned (see Phase 8 lessons) |
| `Dock.Avalonia.Themes.Simple` | 11.3.* | **REMOVE** | Abandoned |
| `Dock.Serializer.Newtonsoft` | 11.3.* | **REMOVE** | Abandoned |
| `Dock.Serializer.SystemTextJson` | 11.3.* | **REMOVE** | Abandoned |
| `ReactiveUI` | 20.* | 23.* | Major update — check `[Reactive]` attribute compat |
| `ReactiveUI.Fody` | 19.* | 19.* (latest) | May still be needed; verify |
| `Avalonia.ReactiveUI` | 11.3.* | **RENAME** → `ReactiveUI.Avalonia` 12.0.* | Package was renamed |

### Packages to ADD (new opportunities)

| Package | Version | Why |
|---------|---------|-----|
| `DialogHost.Avalonia` | 0.12.* | Unlocked! Phase 8.3 command palette can now use proper modals |
| `AvaloniaUI.DiagnosticsSupport` | 2.2.* | Replaces `Avalonia.Diagnostics` (requires Avalonia Plus) |

### Packages UNAFFECTED (no change needed)

| Package | Notes |
|---------|-------|
| `TextMateSharp.Grammars` | Pure text processing, no Avalonia dependency |
| `CliWrap` | Process wrapper, no Avalonia dependency |
| `StreamJsonRpc` | JSON-RPC, no Avalonia dependency |
| `FuzzySharp` | String matching, no Avalonia dependency |
| `LibGit2Sharp` | Git library, no Avalonia dependency |
| `ReactiveUI.Fody` | Fody weaver, version-independent |
| `Microsoft.Extensions.*` | DI, Config, Logging — independent |

---

## Breaking Changes Audit (Aero-Specific)

### HIGH Impact (will break the build)

| Change | Files Affected | Fix |
|--------|---------------|-----|
| `Avalonia.ReactiveUI` → `ReactiveUI.Avalonia` | `Program.cs`, `aero.csproj`, `aero.Tests.csproj` | Rename package + `using` statement |
| `Avalonia.Diagnostics` removed | `aero.csproj` (conditional Debug ref) | Remove or replace with `AvaloniaUI.DiagnosticsSupport` |
| Dock.Avalonia packages removed | `aero.csproj` | Remove all 3 Dock packages (already abandoned) |
| Compiled bindings ON by default | All `.axaml` files with bindings | May surface binding errors at build time that were silent before |
| `IBinding` → `BindingBase` | Check for any C# `IBinding` usage | Search: `new Binding(`, `IBinding` |
| `.UseReactiveUI()` namespace | `Program.cs` | Update `using Avalonia.ReactiveUI` → `using ReactiveUI.Avalonia` |

### MEDIUM Impact (may break at runtime)

| Change | Files Affected | Fix |
|--------|---------------|-----|
| `FocusChangedEventArgs` replaces `GotFocusEventArgs` | Search all focus handlers | Update event handler signatures |
| `FuncMultiValueConverter` takes `IReadOnlyList` | Any custom multi-value converters | Update lambda parameter type |
| `Window.WindowState` is now a direct property | `MainWindow.axaml` style bindings | Cannot set `WindowState` from styles anymore |
| `TopLevel` no longer root of visual tree | Any code casting to `TopLevel` | Use `TopLevel.GetTopLevel(visual)` |
| Access keys triggered by symbol | `AccessText.AccessKey` changed to `string?` | Check for char→string usage |
| `Screen` is now abstract | Any `new Screen(...)` calls | Remove; use `Screens.All`/`Primary` |

### LOW Impact (unlikely to affect Aero)

| Change | Notes |
|--------|-------|
| Clipboard `IDataObject` → `IAsyncDataTransfer` | Aero doesn't use clipboard programmatically |
| `Gestures.*` events moved to `InputElement` | Only affects XAML if `Gestures.` prefix is used |
| `ResourcesChangedEventArgs` is struct | Only affects custom resource change handlers |
| `Screen` is abstract | Aero gets screens from `Screens.Primary`, not constructing |
| Text shaping config | `UsePlatformDetect()` handles this automatically |
| `PropertyPath` removed | Internal to Avalonia, not used directly |
| Android/iOS/Browser changes | Aero is desktop-only (Linux/Windows/macOS) |

---

## Implementation Milestones

### M1: Branch, Snapshot, and Package Update

**Goal:** Create migration branch, update all package references, attempt build.

- [ ] Create branch `avalonia-12-migration` from current `main`
- [ ] Tag current commit as `pre-avalonia-12`
- [ ] Run full test suite on `main` to establish baseline (record pass count)
- [ ] Update `src/aero.csproj`:
  - [ ] Bump all `Avalonia.*` packages from `11.3.*` → `12.0.*`
  - [ ] Rename `Avalonia.ReactiveUI` → `ReactiveUI.Avalonia`
  - [ ] Remove `Avalonia.Diagnostics` (or replace with `AvaloniaUI.DiagnosticsSupport`)
  - [ ] Remove `Dock.Avalonia`, `Dock.Avalonia.Themes.Simple`, `Dock.Serializer.*`
  - [ ] Add `DialogHost.Avalonia 0.12.*` (optional, for Phase 8.3 unlock)
  - [ ] Update `ReactiveUI` from `20.*` → `23.*`
- [ ] Update `tests/aero.Tests.csproj`:
  - [ ] Bump `Avalonia.AvaloniaEdit` → `12.0.*`
  - [ ] Update `ReactiveUI` → `23.*`
  - [ ] Rename `Avalonia.ReactiveUI` → `ReactiveUI.Avalonia` if referenced
- [ ] Run `dotnet restore`
- [ ] Attempt `dotnet build src/aero.csproj`
- [ ] Document all build errors in this file

**Exit:** All packages resolve; build errors catalogued.

---

### M2: Fix Compilation Errors

**Goal:** Get `dotnet build` to succeed with zero errors.

- [ ] Fix all namespace/reference errors from package renames
  - [ ] `Program.cs`: `using Avalonia.ReactiveUI` → `using ReactiveUI.Avalonia`
  - [ ] Any other files importing old namespaces
- [ ] Fix binding-related compilation errors (compiled bindings now default)
  - [ ] Add `x:DataType` to any AXAML views missing it
  - [ ] Fix any binding path errors surfaced by compiled bindings
- [ ] Fix `IBinding` → `BindingBase` changes in C# code
- [ ] Fix `InstancedBinding` → `BindingExpressionBase` if used
- [ ] Remove any dead Dock.Avalonia references
- [ ] Fix `FuncMultiValueConverter` parameter type changes
- [ ] Run `dotnet build src/aero.csproj` — must be error-free

**Exit:** `dotnet build src/aero.csproj` succeeds with zero errors.

---

### M3: Fix Warnings and Runtime Issues

**Goal:** Eliminate warnings, verify runtime behavior.

- [ ] Address any CS0618 obsolete warnings from Avalonia API changes
- [ ] Update `FocusChangedEventArgs` if any focus handlers exist
- [ ] Verify `Window.WindowState` binding still works (now a direct property)
- [ ] Verify `BoolToWindowStateConverter` still functions
- [ ] Verify `TopLevel` access patterns (should use `TopLevel.GetTopLevel()`)
- [ ] Run `dotnet run --project src` — verify app launches
  - [ ] Window renders correctly
  - [ ] Menu bar works
  - [ ] Sidebar renders (File Explorer + Git panel)
  - [ ] Editor opens files, syntax highlighting works
  - [ ] Tabs switch correctly
  - [ ] Status bar updates
  - [ ] Bottom panel (Problems + Output) renders
  - [ ] Theme toggle works (Light ↔ Dark)
  - [ ] Ctrl+` output panel toggle works
  - [ ] Keyboard shortcuts function

**Exit:** App launches and all visual elements render correctly.

---

### M4: Test Suite Migration

**Goal:** All tests pass with updated dependencies.

- [ ] Update test project references and namespaces
- [ ] Fix any test compilation errors
- [ ] Run `dotnet test tests`
- [ ] Fix any test failures (compare against M1 baseline)
- [ ] If ReactiveUI.Fody is still needed, verify Fody weaver works with v12
- [ ] If ReactiveUI.Fody is no longer needed (ReactiveUI 23 may have alternatives), evaluate removal

**Exit:** All tests pass (same count as M1 baseline).

---

### M5: Cleanup and Documentation

**Goal:** Remove dead code, update docs, prepare for merge.

- [ ] Remove any remaining `Dock.Avalonia` dead code/comments
- [ ] Update `docs/LIBRARIES.md`:
  - [ ] Update Avalonia version to 12.0.*
  - [ ] Add `DialogHost.Avalonia` as available (was blocked on 11.3)
  - [ ] Update `Avalonia.ReactiveUI` → `ReactiveUI.Avalonia`
  - [ ] Remove `Avalonia.Diagnostics`, add `AvaloniaUI.DiagnosticsSupport`
  - [ ] Update any "deferred to Avalonia 12" notes
- [ ] Update `docs/CONVENTIONS.md` if any patterns changed
- [ ] Update `AGENTS.md`:
  - [ ] Change "Avalonia 11.3" references to "Avalonia 12.0"
  - [ ] Update library-first table
- [ ] Update `src/aero.csproj`:
  - [ ] Remove `NoWarn CS0618` if no longer needed
- [ ] Run full manual smoke test
- [ ] Merge to `main`

**Exit:** All docs updated, code clean, merged.

---

### M6 (Optional): Unlock New Capabilities

**Goal:** Take advantage of Avalonia 12 features that were blocked before.

- [ ] **DialogHost.Avalonia** — Replace custom command palette overlay with proper modal
- [ ] **Material.Icons.Avalonia** — Replace Phosphor text glyphs with real icon library
- [ ] **Compiled bindings** — Audit all views for `x:DataType`, enable stricter typing
- [ ] **Performance** — Leverage default compiled bindings for faster startup/rendering

**Exit:** New capabilities integrated and tested.

---

## Rollback Plan

1. **Branch:** All work on `avalonia-12-migration` — `main` is untouched
2. **Tag:** `pre-avalonia-12` marks the last stable commit
3. **Revert:** `git checkout main` to return to Avalonia 11.3
4. **Partial revert:** If migration stalls, cherry-pick any non-breaking fixes back to `main`

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Third-party library not compatible | Verified: AvaloniaEdit 12.0.0, ReactiveUI.Avalonia 12.0.3, TextMateSharp independent |
| Compiled bindings break XAML at build | M2 explicitly tackles this; `x:DataType` already used on most views |
| ReactiveUI [Reactive] attribute breaks | Test in M2; ReactiveUI 23.x should be backward-compatible |
| App looks different after upgrade | M3 visual smoke test catches rendering regressions |
| Test suite fails for non-obvious reasons | Compare against M1 baseline; bisect if needed |

---

## Lessons from Phase 8.1a (Applied Here)

> **Incremental.** Each milestone produces a working checkpoint.
> **Test after every change.** Don't accumulate 20 fixes and then test.
> **Understand the library first.** We've read the breaking changes doc BEFORE planning.
> **Know when to walk away.** If a sub-library (e.g., AvaloniaEdit) doesn't work with 12.0, we investigate alternatives early rather than debugging for days.

---

## Debug Log

> Record every attempt here if things go wrong.

### Attempt Template
```
### Attempt N — [Date]
- **Hypothesis:** ...
- **Action:** ...
- **Result:** ...
- **Error/Output:** ...
```

---

*Created: 2026-06-25*
*Status: PLANNING*
*Branch: `avalonia-12-migration` (not yet created)*
