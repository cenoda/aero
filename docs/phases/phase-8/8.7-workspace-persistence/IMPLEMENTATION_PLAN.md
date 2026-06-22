# 8.7 — Workspace Persistence Implementation Plan

**Status:** ✅ Complete — all items implemented and verified (2026-06-22)

---

## M0: Entry Gates

- [x] `dotnet test tests` passes (baseline: 416 passed)
- [x] `dotnet build src/aero.csproj` succeeds (0 errors)
- [x] `docs/phases/phase-8/TOFIX.md` has no open blocker items for 8.7

---

## Scope

Define `ISettingsService` / `SettingsService` as the shared persistence layer that other
Phase 8 sub-phases (8.4, 8.6, 8.1) depend on. Persists two JSON files under `~/.aero/`:

| File | Content | Consumer |
|------|---------|----------|
| `~/.aero/settings.json` | User preferences (font, theme, tab size, layout mode) | 8.6 Settings Page |
| `~/.aero/workspace.json` | Last folder, open files, active tab, window position, recent folders | 8.4 Welcome Page, ShellViewModel, MainWindow |

This sub-phase implements the **infrastructure only**. The UI consumers (8.4, 8.6, 8.1)
will be built in their own sub-phases.

---

## Source Verification (per plan-rules.md §1)

All claims below are verified against the current `src/` at commit time.

| Claim | Verified In |
|-------|-------------|
| `Microsoft.Extensions.Configuration.Json 9.*` already in csproj | `src/aero.csproj` line 49 |
| `Dock.Serializer.SystemTextJson 11.3.*` already in csproj | `src/aero.csproj` line 34 |
| `ShellViewModel` has `_workspacePath` field and subscribes to `FolderOpened` | `src/ViewModels/ShellViewModel.cs` lines 50, 132-137 |
| `DocumentManager.Documents` returns `IReadOnlyList<TextDocument>` with `FilePath` | `src/Services/DocumentManager.cs` lines 31, 71 |
| `EditorViewModel.Tabs` is `ObservableCollection<EditorTabViewModel>` with `FilePath` | `src/ViewModels/EditorViewModel.cs` lines 47, 152 |
| `MainWindow` extends `Window` with `Width=1200`, `Height=800` defaults | `src/MainWindow.axaml` lines 8-9 |
| `Window.Position` is a CLR property, not a `StyledProperty` (cannot bind in XAML) | Avalonia 11.3 API — confirmed |
| `App.axaml.cs` builds DI and sets `desktop.MainWindow` | `src/App.axaml.cs` lines 42-43 |
| `ExitAsync` in `ShellViewModel` already calls `Dispose()` then `desktop.Shutdown()` | `src/ViewModels/ShellViewModel.cs` lines 348-362 |

---

## Implementation

### 1. Data Models — `src/Models/Settings/`

Create three plain records. One class per file (per AGENTS.md §3).

**`src/Models/Settings/WorkspaceState.cs`**

```csharp
namespace Aero.Models.Settings;

public record WorkspaceState
{
    public string? LastFolderPath { get; init; }
    public List<string> OpenFilePaths { get; init; } = new();
    public int ActiveTabIndex { get; init; }
    public WindowState? Window { get; init; }
    public List<string> RecentFolders { get; init; } = new();
}
```

**`src/Models/Settings/WindowState.cs`**

```csharp
namespace Aero.Models.Settings;

public record WindowState
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 1200;
    public double Height { get; init; } = 800;
    public bool IsMaximized { get; init; }
}
```

**`src/Models/Settings/SettingsModel.cs`**

```csharp
namespace Aero.Models.Settings;

public record SettingsModel
{
    public string Theme { get; init; } = "Light";
    public string FontFamily { get; init; } = "Inter";
    public int FontSize { get; init; } = 13;
    public int TabSize { get; init; } = 4;
    public string LayoutMode { get; init; } = "Tile";
}
```

**Design rationale:**
- Records not classes — Models are plain data per MVVM rules (AGENTS.md §3).
- Defaults embedded in the record — corrupted/missing file falls back to defaults.
- `List<string>` serializes naturally with `System.Text.Json`.
- `WindowState?` is nullable — first launch has no saved state.

### 2. Interface — `src/Services/ISettingsService.cs`

```csharp
namespace Aero.Services;

using Aero.Models.Settings;

public interface ISettingsService
{
    Task<WorkspaceState> LoadWorkspaceStateAsync();
    Task SaveWorkspaceStateAsync(WorkspaceState state);
    Task<SettingsModel> LoadSettingsAsync();
    Task SaveSettingsAsync(SettingsModel settings);

    /// <summary>
    /// Add a folder to the recent list. Normalizes path, deduplicates, enforces 10 max.
    /// Must be called from the UI thread.
    /// </summary>
    void AddRecentFolder(string path);

    /// <summary>
    /// Recent folders list (most recent first, max 10). Consumed by 8.4 Welcome Page.
    /// Safe to read from any thread; writes are UI-thread only.
    /// </summary>
    IReadOnlyList<string> GetRecentFolders();

    /// <summary>The ~/.aero/ config directory path.</summary>
    string ConfigDirectory { get; }
}
```

**Why these methods (YAGNI — plan-rules.md §3):**
- No `GetLastFolder()` / `SetLastFolder()` individually — always full blob save/load. Prevents partial-save bugs.
- No "settings changed" event — 8.6 calls `SaveSettingsAsync`, consumers re-read on next open.
- `GetRecentFolders()` added now because 8.4 Welcome Page is a real upcoming consumer.
- `ConfigDirectory` lets theme engine, dock layout, etc. store files in `~/.aero/`.

### 3. Implementation — `src/Services/SettingsService.cs`

**Fields:**
```csharp
private readonly List<string> _recentFolders = new();
private const int MaxRecentFolders = 10;
```

**Constructor:** `public SettingsService(IMessageBus? bus = null)`
- `IMessageBus` is optional (`null` in tests). When available, publishes `StatusMessage` on corrupt-file fallback.
- No ViewModel dependency.
- On construction, loads recent folders from workspace.json via `LoadRecentFoldersFromDisk()`.
- **Sync I/O note:** `LoadRecentFoldersFromDisk()` reads synchronously. The workspace file is ~1KB, runs during DI build before the window is shown — negligible (<1ms). The async `Load*` methods are for runtime reloads.

**File paths (computed once in constructor):**
```csharp
_configDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aero");
_workspaceFilePath = Path.Combine(_configDir, "workspace.json");
_settingsFilePath = Path.Combine(_configDir, "settings.json");
```

**JsonSerializerOptions (static field):**
```csharp
private static readonly JsonSerializerOptions _jsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```

**Save flow (both Save methods):**
1. `Directory.CreateDirectory(_configDir)` — idempotent.
2. Serialize to JSON.
3. Write to `.tmp` file, then atomic `File.Move` (overwrite) — prevents partial-write corruption.
4. No locking — single-user, single-process IDE.

**Atomic write helper:**
```csharp
private async Task AtomicWriteAsync<T>(string targetPath, T value)
{
    Directory.CreateDirectory(_configDir);
    var tempPath = targetPath + ".tmp";
    var json = JsonSerializer.Serialize(value, _jsonOptions);
    await File.WriteAllTextAsync(tempPath, json);
    File.Move(tempPath, targetPath, overwrite: true);
}
```

**Load flow (both Load methods):**
1. File missing → `return new T()` (record defaults).
2. Read all text, deserialize with `JsonSerializer`.
3. `JsonException` or null result → catch, publish `StatusMessage`, return `new T()`.

**Recent folders management:**
```csharp
private const int MaxRecentFolders = 10;

private void LoadRecentFoldersFromDisk()
{
    try
    {
        if (!File.Exists(_workspaceFilePath)) return;
        var json = File.ReadAllText(_workspaceFilePath);
        var ws = JsonSerializer.Deserialize<WorkspaceState>(json, _jsonOptions);
        if (ws?.RecentFolders != null)
        {
            _recentFolders.Clear();
            _recentFolders.AddRange(ws.RecentFolders);
        }
    }
    catch (Exception ex)
    {
        _bus?.Publish(new StatusMessage(
            $"Failed to load recent folders: {ex.Message}"));
    }
}

public void AddRecentFolder(string path)
{
    if (string.IsNullOrWhiteSpace(path)) return;
    var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
    _recentFolders.RemoveAll(p => p.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    _recentFolders.Insert(0, normalized);
    if (_recentFolders.Count > MaxRecentFolders)
        _recentFolders.RemoveRange(MaxRecentFolders,
            _recentFolders.Count - MaxRecentFolders);
}

public IReadOnlyList<string> GetRecentFolders()
    => _recentFolders.AsReadOnly();
```

### 4. Register in DI — `src/App.axaml.cs`

In `BuildServices()`, in the services section after `GitServiceFactory` (line ~120), keeping the services-then-viewmodels ordering:
```csharp
services.AddSingleton<ISettingsService, SettingsService>();
```

### 5. ShellViewModel Changes

**5a. Add reactive properties for window state:**
```csharp
[Reactive] public double WindowX { get; set; }
[Reactive] public double WindowY { get; set; }
[Reactive] public double WindowWidth { get; set; } = 1200;
[Reactive] public double WindowHeight { get; set; } = 800;
[Reactive] public bool IsWindowMaximized { get; set; }
```

**5b. Inject `ISettingsService`:** Add parameter to constructor, store as `_settingsService`.

**5c. Recent folders:** The `ISettingsService` manages the recent folders list internally (normalized, deduplicated, max 10). ShellViewModel does not own the list. Callers use `_settingsService.AddRecentFolder(path)` directly — no wrapper method needed.

**5d. Save on FolderOpened** — in `_folderOpenedHandler`:
```csharp
_folderOpenedHandler = msg =>
{
    StatusText = msg.Path;
    _workspacePath = msg.Path;
    _settingsService.AddRecentFolder(msg.Path);
    // Fire-and-forget — SaveWorkspaceStateAsync handles its own errors internally
    _ = SaveWorkspaceStateAsync();
};
```

**5e. Save on Exit** — in `ExitAsync()` before `Dispose()`:
```csharp
await SaveWorkspaceStateAsync();
```

**5f. `SaveWorkspaceStateAsync` method:**
```csharp
private async Task SaveWorkspaceStateAsync()
{
    var state = new WorkspaceState
    {
        LastFolderPath = _workspacePath,
        OpenFilePaths = _editorViewModel.Tabs
            .Select(t => t.FilePath).Where(p => p != null).ToList()!,
        ActiveTabIndex = _editorViewModel.ActiveTab != null
            ? _editorViewModel.Tabs.IndexOf(_editorViewModel.ActiveTab) : 0,
        Window = new WindowState
        {
            X = WindowX, Y = WindowY,
            Width = WindowWidth, Height = WindowHeight,
            IsMaximized = IsWindowMaximized
        },
        RecentFolders = _settingsService.GetRecentFolders().ToList()
    };
    try { await _settingsService.SaveWorkspaceStateAsync(state); }
    catch (Exception ex) { _bus.Publish(new StatusMessage($"Save failed: {ex.Message}")); }
}
```

### 6. MainWindow Changes

**6a. Wire `PositionChanged` to update ShellViewModel:**
```csharp
// In MainWindow constructor after InitializeComponent():
this.PositionChanged += (_, args) =>
{
    if (DataContext is ShellViewModel shell)
    {
        shell.WindowX = args.Point.X;
        shell.WindowY = args.Point.Y;
    }
};
```

**6b. Bind Width, Height, WindowState in XAML:**

Replace the hardcoded `Width="1200"` and `Height="800"` on `MainWindow.axaml` lines 8-9 with bindings (defaults now live in `ShellViewModel`):
```xml
<Window Width="{Binding WindowWidth}"
        Height="{Binding WindowHeight}"
        WindowState="{Binding IsWindowMaximized, Converter={StaticResource BoolToWindowStateConverter}}">
```

**6c. BoolToWindowStateConverter:** Create `src/Converters/BoolToWindowStateConverter.cs`:
```csharp
namespace Aero.Converters;

using System;
using System.Globalization;
using Avalonia.Data.Converters;

public class BoolToWindowStateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (value is bool isMaximized)
            return isMaximized
                ? Avalonia.Controls.WindowState.Maximized
                : Avalonia.Controls.WindowState.Normal;
        return Avalonia.Controls.WindowState.Normal;
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (value is Avalonia.Controls.WindowState state)
            return state == Avalonia.Controls.WindowState.Maximized;
        return false;
    }
}
```

Register in `MainWindow.axaml` `<Window.Resources>` (scoped, not global):
```xml
<Window.Resources>
    <converters:BoolToWindowStateConverter x:Key="BoolToWindowStateConverter"/>
</Window.Resources>
```
Add `xmlns:converters="using:Aero.Converters"` to the Window element.

### 7. Startup Restore — `src/App.axaml.cs`

After `desktop.MainWindow = mainWindow`.
`OnFrameworkInitializationCompleted` uses `async void` (Avalonia lifecycle override).

CLI args take precedence over workspace restore. Restore only when no folder argument is given:

```csharp
if (desktop.Args is { Length: > 0 } args && System.IO.Directory.Exists(args[0]))
{
    bus.Publish(new FolderOpened(System.IO.Path.GetFullPath(args[0])));
}
else
{
    // Workspace restore — skip when CLI arg is present
    try
    {
        var settings = _services.GetRequiredService<ISettingsService>();
        var ws = await settings.LoadWorkspaceStateAsync();

        if (ws?.Window is { } win)
        {
            shell.WindowWidth = win.Width;
            shell.WindowHeight = win.Height;
            shell.IsWindowMaximized = win.IsMaximized;
            mainWindow.Position = new PixelPoint((int)win.X, (int)win.Y);
            // Set Size and WindowState directly — XAML bindings may not be evaluated yet
            mainWindow.Width = win.Width;
            mainWindow.Height = win.Height;
            mainWindow.WindowState = win.IsMaximized
                ? Avalonia.Controls.WindowState.Maximized
                : Avalonia.Controls.WindowState.Normal;
        }

        if (ws?.LastFolderPath is { } folder && Directory.Exists(folder))
        {
            bus.Publish(new FolderOpened(folder));

            // Per-file try/catch so one bad file doesn't abort the rest
            foreach (var fp in ws.OpenFilePaths.Where(File.Exists))
            {
                try { await shell.EditorViewModel.OpenFileAsync(fp); }
                catch (Exception ex)
                {
                    bus.Publish(new StatusMessage(
                        $"Failed to restore {fp}: {ex.Message}"));
                }
            }

            if (ws.ActiveTabIndex >= 0
                && ws.ActiveTabIndex < shell.EditorViewModel.Tabs.Count)
                shell.EditorViewModel.ActivateTab(
                    shell.EditorViewModel.Tabs[ws.ActiveTabIndex]);
        }
    }
    catch (Exception ex)
    {
        bus.Publish(new StatusMessage(
            $"Workspace restore failed: {ex.Message}"));
    }
}
```

---

## Files to Create

| File | Lines (est.) | Purpose |
|------|-------------|---------|
| `src/Models/Settings/WorkspaceState.cs` | ~15 | Workspace persistence model |
| `src/Models/Settings/WindowState.cs` | ~12 | Window position/size model |
| `src/Models/Settings/SettingsModel.cs` | ~12 | User preferences model |
| `src/Services/ISettingsService.cs` | ~25 | Interface definition |
| `src/Services/SettingsService.cs` | ~100 | Implementation with atomic save |
| `src/Converters/BoolToWindowStateConverter.cs` | ~15 | XAML converter |

## Files to Modify

| File | Change |
|------|--------|
| `src/App.axaml.cs` | Register `ISettingsService`, add startup restore logic |
| `src/ViewModels/ShellViewModel.cs` | Add window state properties, inject `ISettingsService`, save on exit & folder open |
| `src/MainWindow.axaml.cs` | Wire `PositionChanged` event |
| `src/MainWindow.axaml` | Bind `Width`, `Height`, `WindowState` to `ShellViewModel` |
| `src/MainWindow.axaml` | Add `BoolToWindowStateConverter` to `<Window.Resources>`, add converters xmlns |
| `docs/roadmap/PHASES.md` | Mark 8.7 items `[x]` |

## Files NOT to Modify (per YAGNI — plan-rules.md §3)

- `EditorViewModel.cs` — no changes.
- `FileExplorerViewModel.cs` — no changes.
- `DocumentManager.cs` — no changes.
- `EditorTabViewModel.cs` — no changes.

---

## Limitations (by design — plan-rules.md §4)

1. **No incremental auto-save** — Saved only on folder open and app exit. Crash = lost tab state. Deferred to Phase 9.
2. **No per-workspace settings** — `settings.json` is global. Deferred to Phase 9.
3. **Window position on disconnected monitor** — Saved monitor disconnected to (0,0). Acceptable first cut.
4. **Open file paths only, no scroll/caret** — Per-document scroll state deferred.
5. **Layout state (sidebar width, dock layout)** — Left for 8.1.
6. **`SettingsModel` fields are placeholders** — Values validated by 8.6, not 8.7.
7. **Window position stored as `double`, restored as `int`** — `PixelPoint` uses integer coordinates. No sub-pixel precision loss in practice (window positions are always whole pixels).

---

## Tests

**File:** `tests/Services/SettingsServiceTests.cs`

All tests use `Path.GetTempPath()` + unique GUID subdirectory (never real `~/.aero/`).

| # | Test | What It Verifies |
|---|------|------------------|
| 1 | `SaveWorkspaceStateAsync_CreatesFile` | After save, `workspace.json` exists with valid JSON |
| 2 | `SaveWorkspaceStateAsync_RoundTrip` | Save to Load to all fields match |
| 3 | `LoadWorkspaceStateAsync_FileMissing_ReturnsDefaults` | No file returns defaults |
| 4 | `LoadWorkspaceStateAsync_CorruptJson_ReturnsDefaults` | Garbage file returns defaults, no crash |
| 5 | `SaveAndLoadSettings_RoundTrip` | Settings round-trip preserves all fields |
| 6 | `AtomicWrite_NoPartialWriteOnCrash` | Simulate crash: original intact or fully written |
| 7 | `ConfigDirectory_ReturnsDotAero` | Path ends with `.aero` |
| 8 | `FirstSave_CreatesConfigDirectory` | First save creates `~/.aero/` |
| 9 | `RecentFolders_MaxTenItems` | Add 12 folders, GetRecentFolders returns 10 |
| 10 | `RecentFolders_Ordering` | Most recent folder is first in list |
| 11 | `AddRecentFolder_NormalizesPath` | `/path/` → `/path` (trims trailing separator) |
| 12 | `AddRecentFolder_Deduplicates` | Same folder added twice → single entry |
| 13 | `AddRecentFolder_MovesExistingToTop` | Folder1, Folder2, Folder1 → Folder1 first |
| 14 | `GetRecentFolders_ReturnsReadOnly` | Returned list throws on Add |
| 15 | `LoadWorkspaceStateAsync_CorruptJson_PublishesStatusMessage` | Corrupt JSON publishes a StatusMessage via bus |

**Test patterns** (matching `tests/Services/DocumentManagerTests.cs`):
- Use `StubMessageBus` to capture `StatusMessage`.
- Use real file I/O (temp directory) — no file-system mocking for persistence tests.

---

## Definition of Done (Exit Gates)

- [x] `dotnet build src/aero.csproj` passes (0 errors)
- [x] `dotnet test tests` passes (baseline: 401 + new: 15 = 416)
- [x] Closing and reopening the IDE restores:
  - Last opened folder (tree loads automatically)
  - Previously open files (tabs restored)
  - Active tab index
  - Window size and position
  - Maximized state
- [x] `~/.aero/workspace.json` and `~/.aero/settings.json` exist after first save
- [x] Corrupt or missing JSON files cause graceful fallback to defaults (no crash)
- [x] Recent folders list persists with correct ordering
- [x] `ISettingsService` is registered in DI and can be consumed by 8.4 and 8.6
- [x] `docs/roadmap/PHASES.md` Phase 8.7 items all `[x]`
