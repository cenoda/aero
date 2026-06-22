# 8.7 — Workspace Persistence Review Recommendations

**Reviewer:** Cline (automated review)
**Date:** 2026-06-22
**Plan reviewed:** `IMPLEMENTATION_PLAN.md` (Draft — pre-implementation)

---

## Overall Assessment

**Ready for implementation** after addressing Issues 1–3 below. The architecture is clean, follows conventions, and the test plan is comprehensive. Source claims are all verified accurate against the current `src/`.

---

## Checklist Before Implementation

- [ ] Fix README.md line 12 to match implementation (`System.Text.Json`, not `IConfiguration`)
- [ ] Resolve `AddRecentFolder` visibility (Option B: call from `App.axaml.cs` directly)
- [ ] Add CLI-args-take-precedence guard around workspace restore
- [ ] Use `StatusMessage` bus publish instead of direct `StatusText` in `SaveWorkspaceStateAsync` catch
- [ ] Add `BoolToWindowStateConverter` implementation to plan or create file
- [ ] Remove redundant `foreach` loop for recent folders in startup restore

---

## Action Items

### Issue 1 (Medium): README says `Microsoft.Extensions.Configuration.Json` but implementation uses `System.Text.Json`

**Where:** README.md line 12 vs IMPLEMENTATION_PLAN.md §3

The README states:

> Store user preferences in `~/.aero/settings.json` (via `Microsoft.Extensions.Configuration.Json`)

But the implementation uses `System.Text.Json.JsonSerializer` directly — no `IConfigurationRoot` or `ConfigurationBuilder`. This is the right call (direct `System.Text.Json` is simpler for flat save/load), but the README is misleading. `Microsoft.Extensions.Configuration.Json` is designed for `IConfiguration`-style read/watch/merge patterns, not atomic save-to-disk.

**Recommendation:** Update README line 12 to read:

> Store user preferences in `~/.aero/settings.json` (via `System.Text.Json`)

Or remove the parenthetical entirely.

---

### Issue 2 (Medium): `AddRecentFolder` visibility mismatch with startup restore caller

**Where:** IMPLEMENTATION_PLAN.md §5c vs §7

§5c proposes `private void AddRecentFolder(string path)` on `ShellViewModel`. But §7 startup restore calls it from `App.axaml.cs`:

```csharp
foreach (var recent in ws.RecentFolders)
    shell.AddRecentFolder(recent);
```

A `private` method cannot be called from another class. Two options:

- **Option A:** Make the method `public`. Functional but exposes an internal helper.
- **Option B (recommended):** Call `_settingsService.AddRecentFolder()` directly from `App.axaml.cs` instead. This removes the unnecessary wrapper on `ShellViewModel` and keeps the startup restore self-contained.

---

### Issue 3 (Medium): CLI args and workspace restore race condition

**Where:** IMPLEMENTATION_PLAN.md §7 vs current `App.axaml.cs` lines 46–52

The plan inserts workspace restore **after** `desktop.MainWindow = mainWindow` but **before** the CLI arg check. This means:

1. Workspace restore fires `FolderOpened(savedFolder)` — file explorer loads saved folder
2. CLI arg check fires `FolderOpened(cliArg)` — file explorer immediately switches

Both fire. The user sees a flash of the wrong folder. Both set `_workspacePath` in `ShellViewModel` (line 135), and the final value depends on message ordering.

**Recommendation:** Skip workspace restore when CLI args are present:

```csharp
if (desktop.Args is { Length: > 0 } args && System.IO.Directory.Exists(args[0]))
{
    bus.Publish(new FolderOpened(System.IO.Path.GetFullPath(args[0])));
}
else
{
    // workspace restore
    var settings = _services.GetRequiredService<ISettingsService>();
    var ws = await settings.LoadWorkspaceStateAsync();
    // ... restore window, open files, activate tab
}
```

This ensures CLI args always take precedence and avoids double-loading the file explorer.

---

### Issue 4 (Low): Fire-and-forget save may update `StatusText` off the UI thread

**Where:** IMPLEMENTATION_PLAN.md §5d and §5f

§5d uses fire-and-forget:

```csharp
_ = SaveWorkspaceStateAsync();
```

§5f sets `StatusText` directly in the catch block:

```csharp
catch (Exception ex) { StatusText = $"Save failed: {ex.Message}"; }
```

Since `SaveWorkspaceStateAsync` is `async Task`, the catch block may execute on a thread pool thread after the `await` yields. Setting `StatusText` (a `[Reactive]` property) off the UI thread can cause an Avalonia binding exception.

The existing `_statusMessageHandler` (ShellViewModel lines 139–151) already has thread marshaling via `Dispatcher.UIThread.Post()`. Use the same pattern.

**Recommendation:** Replace the direct `StatusText` assignment with a bus publish:

```csharp
catch (Exception ex)
{
    _bus.Publish(new StatusMessage($"Save failed: {ex.Message}"));
}
```

---

### Issue 5 (Low): `BoolToWindowStateConverter` implementation not shown

**Where:** IMPLEMENTATION_PLAN.md §6c

The plan creates `src/Converters/BoolToWindowStateConverter.cs` (~15 lines) but does not include the implementation. The converter should handle edge cases to avoid XAML binding errors on startup.

**Recommendation:** Add to the plan:

```csharp
namespace Aero.Converters;

using System;
using System.Globalization;
using Avalonia;
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

---

### Issue 6 (Low): Synchronous I/O in `SettingsService` constructor

**Where:** IMPLEMENTATION_PLAN.md §3 — `LoadRecentFoldersFromDisk()`

The constructor calls `File.ReadAllText()` synchronously. This runs on the UI thread via DI resolution. For a ~1KB JSON file this is sub-millisecond and acceptable.

**Recommendation:** No change needed. Already documented in Limitations. If the config directory ever holds larger files, revisit as async-first.

---

### Issue 7 (Low): Recent folders loaded twice on startup

**Where:** IMPLEMENTATION_PLAN.md §3 and §7

On startup:
1. `SettingsService` constructor calls `LoadRecentFoldersFromDisk()` — populates `_recentFolders`
2. Startup restore in `App.axaml.cs` iterates `ws.RecentFolders` and calls `AddRecentFolder()` for each — re-inserts into the same list

The deduplication logic in `AddRecentFolder` makes this harmless (existing entries move to front), but it's unnecessary work and confusing to read.

**Recommendation:** Remove the `foreach` loop from startup restore in §7. Rely on the constructor preload. The recent folders are already in `_recentFolders` by the time anyone reads them. This requires zero code changes in §7 — just delete the block.

---

## Source Verification Summary

All claims in the Source Verification table (IMPLEMENTATION_PLAN.md §Source Verification) have been independently confirmed against the current `src/`:

| Claim | Verified | Location |
|-------|----------|----------|
| `Microsoft.Extensions.Configuration.Json 9.*` in csproj | ✅ | `src/aero.csproj` line 49 |
| `Dock.Serializer.SystemTextJson 11.3.*` in csproj | ✅ | `src/aero.csproj` line 34 |
| `ShellViewModel._workspacePath` field | ✅ | `src/ViewModels/ShellViewModel.cs` line 50 |
| `ShellViewModel` subscribes to `FolderOpened` | ✅ | `src/ViewModels/ShellViewModel.cs` lines 132–137 |
| `EditorViewModel.Tabs` is `ObservableCollection<EditorTabViewModel>` | ✅ | `src/ViewModels/EditorViewModel.cs` lines 47, 152 |
| `MainWindow` Width=1200, Height=800 | ✅ | `src/MainWindow.axaml` lines 8–9 |
| `Window.Position` is CLR property (not bindable) | ✅ | Avalonia 11.3 API confirmed |
| `App.axaml.cs` builds DI and sets `desktop.MainWindow` | ✅ | `src/App.axaml.cs` lines 27–44 |
| `ExitAsync` calls `Dispose()` then `desktop.Shutdown()` | ✅ | `src/ViewModels/ShellViewModel.cs` lines 348–361 |
| Test baseline: 401 passed | ✅ | `dotnet test tests` — 401/401 passed |
| `ISettingsService` not yet defined in `src/` | ✅ | No hits |
| `AddRecentFolder` not yet in `ShellViewModel` | ✅ | No hits |
