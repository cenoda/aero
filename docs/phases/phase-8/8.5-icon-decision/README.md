# 8.5 — Icon Decision & Integration

**Goal:** Resolve the icon library question (TOFIX R3.1) and apply file-type icons.

**Context:**
`Material.Icons.Avalonia` was paused because the versions compatible with Avalonia 11.3 are not stable, and newer versions require Avalonia 12. Aero targets Avalonia 11.3 (stable LTS).

**Decision (recommended):**
- **Continue with text glyphs for Phase 8** — the current approach (Unicode glyphs in Consolas/Courier New) is stable and lightweight
- **Document the icon decision explicitly** so it's not a pending question
- **Revisit icon library in Phase 9** (or when upgrading to Avalonia 12) — at that point `Material.Icons.Avalonia` or an alternative may have stable Avalonia 12 support
- If icons become a blocking quality concern mid-phase, consider a lightweight SVG approach using `PathIcon` with embedded icon paths (no NuGet dependency)

**Scope:**
- ✅ Commit to text glyphs for Phase 8
- ✅ Apply consistent file-type glyphs in FileExplorerView tree nodes and editor tabs (8 distinct glyphs: folder, code, text, image, config, markup, project, unknown)
- ✅ Update `docs/LIBRARIES.md` with the final decision
- ❌ No new NuGet icon package added in Phase 8

**Dependencies:**
- Independent of other sub-phases (can run in parallel with 8.9)

**Exit condition:** Files in the tree and tabs show appropriate type glyphs. `TOFIX R3.1` is resolved.

**Tests:**
- Unit: Each file extension maps to the expected glyph
- Unit: Unknown extensions fall back to the default glyph
- Manual: File tree shows distinguishable glyphs for .cs, .json, .md, .axaml, .csproj, .sln, folders

