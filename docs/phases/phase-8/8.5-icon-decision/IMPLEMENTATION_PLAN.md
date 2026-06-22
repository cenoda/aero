# 8.5 — Icon Decision & Integration: Implementation Plan

**Status:** Draft — pre-implementation (2026-06-22)

---

## M0: Entry Gates

All must be true before coding starts:

- [ ] `dotnet build src/aero.csproj` passes (0 errors)
- [ ] `dotnet test tests` passes (baseline: 416 passed)
- [ ] `docs/phases/phase-8/TOFIX.md` has no open blocker items for 8.5
- [ ] Phase 8.9 (Design System) is complete

---

## 1. Source Verification

All claims verified against current `src/`:

| Claim | Location |
|-------|----------|
| `FileExplorerNodeViewModel.Glyph` returns text glyphs (5 mappings) | `src/ViewModels/FileExplorerNodeViewModel.cs` lines 88-97 |
| `FileExplorerNodeViewModel.IconKind` string property | `src/ViewModels/FileExplorerNodeViewModel.cs` line 74 |
| `FileExplorerViewModel.IconFor` returns 5 icon kinds | `src/ViewModels/FileExplorerViewModel.cs` lines 675-697 |
| `EditorTabViewModel` has no `Glyph` property | `src/ViewModels/EditorTabViewModel.cs` — only `GitStatusGlyph` (line 26) |
| `FileExplorerView.axaml` renders `Glyph` as `<TextBlock>` | `src/Views/FileExplorerView.axaml` lines 102-106 |
| `EditorView.axaml` tab template has no file-type glyph | `src/Views/EditorView.axaml` lines 17-33 |
| `App.axaml` merges via `ResourceDictionary.MergedDictionaries` | `src/App.axaml` lines 4-16 |
| `PathIcon` available in Avalonia 11.3 | Built-in `Avalonia.Controls.PathIcon` — no NuGet |
| Phosphor Icons MIT license | `phosphor-icons/core` — "MIT License Copyright (c) 2023 Phosphor Icons" |
| Phosphor icons `viewBox="0 0 256 256"` | Confirmed via GitHub tree — `assets/regular/` directory |

---

## 2. Scope

### In scope

- **Create `src/Styles/Icons.axaml`** — `ResourceDictionary` with 8 `StreamGeometry` resources from Phosphor Icons (regular weight), MIT attribution header
- **Expand `FileExplorerViewModel.IconFor`** — delegate to `IconResolver` instead of hardcoded 5-icon mapping
- **Rewrite `FileExplorerNodeViewModel.Glyph`** — return resource key strings (e.g. `"Icon.Code"`) instead of Unicode chars; add `GlyphGeometry` for XAML binding
- **Add `Glyph` + `GlyphGeometry` to `EditorTabViewModel`** — derives from file extension via shared helper
- **Add `IconResolver` static helper** — maps extension -> icon key (shared by tree & tabs)
- **Update XAML** — replace `<TextBlock>` with `<PathIcon Data="{Binding GlyphGeometry}">` in tree and tabs
- **Merge `Icons.axaml` in `App.axaml`**
- **Update `docs/LIBRARIES.md`** and `docs/roadmap/PHASES.md`**

### Out of scope

- No `Material.Icons.Avalonia` NuGet — deferred to Avalonia 12 upgrade
- No custom icon creation — extract Phosphor paths as-is
- No icon themes / dark mode variants — monochrome, text foreground color
- No per-language icons beyond 8 categories
- No icons for welcome page, settings dialog, or command palette

---

## 3. Implementation

### 3a. Data: Phosphor Icons -> 8 `StreamGeometry` resources

Extract `d` attribute from each Phosphor SVG at `assets/regular/{name}.svg`:

| Resource Key | Phosphor Icon | File Types |
|---|---|---|
| `Icon.Folder` | `folder` | Directories |
| `Icon.Code` | `code` | .cs .js .ts .jsx .tsx .py .rs .go .java .cpp .c .h .sql .sh .ps1 .fs |
| `Icon.Text` | `file-text` | .txt .md .log .rst .mdwn .mkd |
| `Icon.Image` | `image` | .png .jpg .jpeg .gif .svg .ico .webp .bmp |
| `Icon.Config` | `gear` | .json .xml .yaml .yml .toml .ini .editorconfig .gitignore .props .targets |
| `Icon.Markup` | `brackets-angle` | .html .htm .css .scss .axaml .xaml |
| `Icon.Project` | `app-window` | .sln .csproj .fsproj |
| `Icon.Unknown` | `file` | Everything else / fallback |

Note: `package.json` resolves to `.json` extension -> `Icon.Config` (via `IconResolver`), not `Icon.Project`.

### 3b. New: `src/Styles/Icons.axaml`

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Icons from Phosphor Icons — MIT License — Copyright (c) 2023 Phosphor Icons -->
    <StreamGeometry x:Key="Icon.Folder">...</StreamGeometry>
    <StreamGeometry x:Key="Icon.Code">...</StreamGeometry>
    <StreamGeometry x:Key="Icon.Text">...</StreamGeometry>
    <StreamGeometry x:Key="Icon.Image">...</StreamGeometry>
    <StreamGeometry x:Key="Icon.Config">...</StreamGeometry>
    <StreamGeometry x:Key="Icon.Markup">...</StreamGeometry>
    <StreamGeometry x:Key="Icon.Project">...</StreamGeometry>
    <StreamGeometry x:Key="Icon.Unknown">...</StreamGeometry>
</ResourceDictionary>
```

### 3c. New: `src/Services/IconResolver.cs`

Static helper — no DI, no mocking:

```csharp
namespace Aero.Services;

public static class IconResolver
{
    public static string GetIconKey(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return "Icon.Unknown";
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext))
            return "Icon.Unknown";
        return ext.ToLowerInvariant() switch
        {
            ".cs" or ".csx" or ".cake" => "Icon.Code",
            ".js" or ".mjs" or ".cjs" or ".es6" => "Icon.Code",
            ".ts" or ".tsx" or ".jsx" => "Icon.Code",
            ".py" or ".pyw" => "Icon.Code",
            ".rs" => "Icon.Code", ".go" => "Icon.Code",
            ".java" or ".jav" => "Icon.Code",
            ".cpp" or ".cc" or ".cxx" or ".hpp" or ".hh" or ".hxx" => "Icon.Code",
            ".c" or ".h" or ".i" => "Icon.Code",
            ".fs" or ".fsi" or ".fsx" => "Icon.Code",
            ".sql" => "Icon.Code",
            ".sh" or ".bash" or ".zsh" => "Icon.Code",
            ".ps1" or ".psm1" or ".psd1" => "Icon.Code",
            ".txt" => "Icon.Text",
            ".md" or ".markdown" or ".mdwn" or ".mdown" or ".mkd" or ".mkdn" => "Icon.Text",
            ".log" or ".rst" => "Icon.Text",
            ".png" or ".jpg" or ".jpeg" or ".gif" => "Icon.Image",
            ".svg" or ".ico" or ".webp" or ".bmp" => "Icon.Image",
            ".json" => "Icon.Config",
            ".xml" or ".xsd" => "Icon.Config",
            ".yaml" or ".yml" => "Icon.Config",
            ".toml" or ".ini" or ".cfg" => "Icon.Config",
            ".editorconfig" or ".gitignore" or ".gitattributes" => "Icon.Config",
            ".props" or ".targets" => "Icon.Config",
            ".html" or ".htm" => "Icon.Markup",
            ".css" or ".scss" or ".less" => "Icon.Markup",
            ".axaml" or ".xaml" => "Icon.Markup",
            ".sln" or ".csproj" or ".fsproj" => "Icon.Project",
            _ => "Icon.Unknown",
        };
    }
}
```

**Why static:** No DI. Pure function. Testable via `IconResolver.GetIconKey(".cs")`.

### 3d. Modify `FileExplorerViewModel.IconFor`

Delegate to `IconResolver`. The `projects` parameter is retained for future multi-root workspace detection:

```csharp
private static string IconFor(FileSystemEntry entry, IReadOnlyList<ProjectInfo> projects)
{
    _ = projects; // Retained for future multi-root workspace detection
    if (entry.Kind == FileSystemEntryKind.Directory)
        return "Folder";
    return IconResolver.GetIconKey(entry.FullPath);
}
```

### 3e. Modify `FileExplorerNodeViewModel.Glyph` + add `GlyphGeometry`

```csharp
// Add:
// using Avalonia;
// using Avalonia.Media;

public string Glyph => IconKind switch
{
    "Folder" => "Icon.Folder",
    "MicrosoftVisualStudio" or "Icon.Project" => "Icon.Project",
    "LanguageCsharp" or "Icon.Code" => "Icon.Code",
    "Nodejs" => "Icon.Config",
    "FileDocument" => "Icon.Unknown",
    _ => IconKind,
};

public Geometry GlyphGeometry =>
    Application.Current?.TryFindResource(Glyph) is Geometry g ? g : Geometry.Empty;
```

### 3f. Modify `EditorTabViewModel` — add `Glyph` + `GlyphGeometry`

```csharp
// Add:
// using Avalonia;
// using Avalonia.Media;
// using Aero.Services;

public string Glyph => IconResolver.GetIconKey(FilePath);

public Geometry GlyphGeometry =>
    Application.Current?.TryFindResource(Glyph) is Geometry g ? g : Geometry.Empty;
```

### 3g. XAML: `FileExplorerView.axaml`

```xml
<!-- Before (lines 102-106): -->
<TextBlock Text="{Binding Glyph}" Width="14" Opacity="0.6"
           VerticalAlignment="Center"
           FontFamily="Consolas, Courier New, monospace"/>
<!-- After: -->
<PathIcon Data="{Binding GlyphGeometry}" Width="14" Height="14"
          Opacity="0.6" VerticalAlignment="Center"/>
```

### 3h. XAML: `EditorView.axaml`

Add before `GitStatusGlyph` in the tab DataTemplate:

```xml
<PathIcon Data="{Binding GlyphGeometry}" Width="12" Height="12"
          VerticalAlignment="Center" Margin="0,0,2,0"/>
```

### 3i. Modify `App.axaml`

After the `Transitions.axaml` include (line 14):

```xml
<ResourceInclude Source="avares://aero/Styles/Icons.axaml" />
```

### 3j. Update `docs/LIBRARIES.md`

Replace the content under `## ICONS & ASSETS`:

```
| **Phosphor Icons (embedded PathIcon)** | 8 vector icons from MIT-licensed Phosphor set. Embedded as StreamGeometry in `src/Styles/Icons.axaml`. | No NuGet. Vector crisp. Monochrome. Attribution: MIT — Copyright (c) 2023 Phosphor Icons. |
| **Material.Icons.Avalonia** | 5000+ Material Design icons. | Deferred to Avalonia 12 upgrade (Phase 9+). |
```

---

## 4. Limitations (by design)

1. **No colored icons** — All monochrome, inherit text foreground color. Color differentiation by file type deferred to Phase 9 (needs theme-aware token colors).
2. **8-icon limit** — New types beyond the mapping get `Icon.Unknown`. No per-language icons. Expandable without breaking changes.
3. **Git status glyph unchanged** — `GitStatusGlyph` (M, A, D, etc.) remains text. Change indicators are semantic, not file-type icons.
4. **Phosphor internal padding** — Phosphor uses 256x256 viewBox with ~16px internal padding. At 14x14 render size, icons may appear ~30% smaller than bounding box. If too small, bump tree size to 16x16 and tab size to 14x14.

---

## 5. Files to Create / Modify

| File | Action | Lines (est.) |
|------|--------|-------------|
| `src/Styles/Icons.axaml` | Create | ~30 |
| `src/Services/IconResolver.cs` | Create | ~50 |
| `src/ViewModels/FileExplorerNodeViewModel.cs` | Modify | ~10 |
| `src/ViewModels/FileExplorerViewModel.cs` | Modify | ~5 |
| `src/ViewModels/EditorTabViewModel.cs` | Modify | +6 |
| `src/Views/FileExplorerView.axaml` | Modify | ~5 |
| `src/Views/EditorView.axaml` | Modify | +4 |
| `src/App.axaml` | Modify | +1 |
| `docs/LIBRARIES.md` | Modify | ~3 |
| `docs/roadmap/PHASES.md` | Modify | Mark 8.5 [x] |

### NOT modified (YAGNI)

- `LanguageDetectionService.cs` — icon mapping is separate from language detection
- `ShellViewModel.cs`, `EditorViewModel.cs` — no changes needed
- `SettingsService.cs` — no icon settings

---

## 6. Definition of Done (Exit Gates)

- [ ] `dotnet build src/aero.csproj` — 0 errors, 0 warnings from 8.5 changes
- [ ] `dotnet test tests` — 416+ passed, 0 new failures
- [ ] `src/Styles/Icons.axaml` created with 8 Phosphor geometries + MIT header
- [ ] `src/Services/IconResolver.cs` created with `GetIconKey` mapping
- [ ] File tree shows Phosphor PathIcon glyphs instead of text glyphs
- [ ] Editor tabs show file-type PathIcon before the title
- [ ] Unknown/untitled files show generic file icon (no crash)
- [ ] `docs/LIBRARIES.md` updated with final icon decision
- [ ] `docs/roadmap/PHASES.md` Phase 8.5 items all `[x]`
- [ ] `docs/phases/phase-8/TOFIX.md` no new open items from this sub-phase

---

## 7. Tests

| # | Test | Verifies | Type |
|---|------|----------|------|
| 1 | `IconResolver_ReturnsCode_ForCsharp` | `.cs` -> `"Icon.Code"` | Unit |
| 2 | `IconResolver_ReturnsText_ForMarkdown` | `.md` -> `"Icon.Text"` | Unit |
| 3 | `IconResolver_ReturnsImage_ForPng` | `.png` -> `"Icon.Image"` | Unit |
| 4 | `IconResolver_ReturnsConfig_ForJson` | `.json` -> `"Icon.Config"` | Unit |
| 5 | `IconResolver_ReturnsMarkup_ForHtml` | `.html` -> `"Icon.Markup"` | Unit |
| 6 | `IconResolver_ReturnsProject_ForSln` | `.sln` -> `"Icon.Project"` | Unit |
| 7 | `IconResolver_ReturnsUnknown_ForUnknownExt` | `.xyz` -> `"Icon.Unknown"` | Unit |
| 8 | `IconResolver_ReturnsUnknown_ForNullPath` | `null` -> `"Icon.Unknown"` | Unit |
| 9 | `EditorTabViewModel_Glyph_ReturnsCorrectKey` | Tab with file path shows correct glyph | Unit |
| 10 | `EditorTabViewModel_Glyph_ReturnsUnknown_ForUntitled` | Untitled tab -> `"Icon.Unknown"` | Unit |
| 11 | `Glyph_OldKeys_MapCorrectly` | `"MicrosoftVisualStudio"` -> `"Icon.Project"` | Unit |
| 12 | Manual: tree shows distinguishable icons for all 8 types | Visual check | Manual |

**Test files:** `tests/Services/IconResolverTests.cs` (new), `tests/ViewModels/EditorTabViewModelTests.cs` (new).

---

## 8. Binding Strategy

All icon bindings use `GlyphGeometry` (a `Geometry` property resolved via `Application.Current?.TryFindResource(Glyph)`). This avoids the `{StaticResource {Binding Key}}` limitation in Avalonia 11.3, where `StaticResource` resolves at XAML parse time and cannot accept a runtime binding.

The `Glyph` string property remains the source of truth; `GlyphGeometry` is a derived view property for XAML consumption only. Both ViewModels define the same pattern.

No pre-implementation spike needed — this is the production design.