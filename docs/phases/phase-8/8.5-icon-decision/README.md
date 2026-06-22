# 8.5 — Icon Decision & Integration

**Goal:** Resolve the icon library question (TOFIX R3.1) and deliver consistent file-type icons in the file tree and editor tabs.

## Decision (implemented)

Phase 8.5 is implemented with **embedded Phosphor vector icons** rendered via Avalonia `PathIcon` (`StreamGeometry` resources), not Unicode text glyphs.

- ✅ Use embedded Phosphor icon paths (MIT-licensed) in `src/Styles/Icons.axaml`
- ✅ Shared extension-to-icon mapping via `src/Services/IconResolver.cs`
- ✅ File tree and editor tabs both use the same icon resolution path
- ✅ No `Material.Icons.Avalonia` NuGet package added in Phase 8
- 🔁 Optional revisit in Phase 9+ if Avalonia 12 migration changes icon-library tradeoffs

This resolves the prior uncertainty around TOFIX R3.1 for Phase 8.

## Scope delivered

- ✅ 8 icon categories implemented and wired:
  - `Icon.Folder`
  - `Icon.Code`
  - `Icon.Text`
  - `Icon.Image`
  - `Icon.Config`
  - `Icon.Markup`
  - `Icon.Project`
  - `Icon.Unknown` (fallback)
- ✅ File explorer nodes render vector icons
- ✅ Editor tabs render file-type vector icons
- ✅ Unknown/untitled paths safely fall back to `Icon.Unknown`
- ✅ Library decision documented as completed for Phase 8

## Non-goals / Out of scope

- ❌ No icon theme editor
- ❌ No per-language icon pack beyond the 8-category mapping
- ❌ No migration to external icon NuGet packages in this phase

## Dependencies

- Independent sub-phase (can run in parallel with 8.9), but integrated outputs must remain compatible with Phase 8 theme and design-system constraints.

## Exit condition

- File tree and tabs display consistent, type-appropriate icons from shared resolver logic.
- TOFIX R3.1 decision is no longer pending for Phase 8.

## Tests expected

- Unit: extension mappings return expected icon keys
- Unit: unknown/null/untitled inputs return fallback icon key
- Manual: verify distinguishable icons in tree/tabs for representative files:
  - `.cs`, `.json`, `.md`, `.axaml`, `.csproj`, `.sln`, folders, unknown extension

## Notes

- Attribution for icon source is maintained in the icon resource definition/docs per repository conventions.
- If future UX/design requirements need richer icon coverage, expand through `IconResolver` and icon resources without changing the phase-level decision.
