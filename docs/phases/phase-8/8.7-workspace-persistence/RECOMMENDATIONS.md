# 8.7 — Workspace Persistence: Review Recommendations

> **Reviewer:** Cline
> **Date:** 2026-06-22
> **Review of:** [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md)
> **Verdict:** ✅ Solid — nearly ready for implementation. Minor issues below.

---

## Overall Assessment

The plan is well-structured, tightly scoped, and demonstrates excellent source verification
(line-level citations against the current codebase). The data models are clean, the interface
design applies YAGNI correctly, and the atomic-write pattern is a good defensive choice.
The 14-test plan covers the critical paths.

The issues below are minor — none are blockers, but items 1, 3, and 5 should be addressed
before coding begins.

---

## Issues

### 1. Restore error boundary needs explicit `try/catch` (HIGH)

**Location:** §7 — Startup Restore

The plan says `OnFrameworkInitializationCompleted` uses `async void` with a `try/catch`
guard, but the §7 code block doesn't show the `try/catch` wrapping the full restore
sequence. Currently the only `try/catch` shown is inside `SaveWorkspaceStateAsync` (§5f).

If `LoadWorkspaceStateAsync` or any `OpenFileAsync` call throws outside a guard, the
`async void` method produces an unobserved exception. On .NET 9 this is logged but not
fatal, yet it silently aborts the remaining restore steps (subsequent files won't open,
recent folders won't load).

**Recommendation:** Wrap the entire restore block in a single `try/catch`:

```csharp
// OnFrameworkInitializationCompleted — async void (Avalonia lifecycle override)
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
    }

    if (ws?.LastFolderPath is { } folder && Directory.Exists(folder))
    {
        bus.Publish(new FolderOpened(folder));

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

        foreach (var recent in ws.RecentFolders)
            shell.AddRecentFolder(recent);
    }
}
catch (Exception ex)
{
    bus.Publish(new StatusMessage(
        $"Workspace restore failed: {ex.Message}"));
}
```

---

### 2. Fire-and-forget `_ = SaveWorkspaceStateAsync()` should be documented (LOW)

**Location:** §5d — `_folderOpenedHandler`

```csharp
_ = SaveWorkspaceStateAsync();
```

This suppresses any exception from the returned `Task`. Since `SaveWorkspaceStateAsync`
has its own internal `try/catch` that sets `StatusText`, this is safe in practice — the
task won't throw. But the `_ =` pattern is a code smell if unexplained.

**Recommendation:** Add a comment: `// Fire-and-forget — SaveWorkspaceStateAsync handles its own errors.`

---

### 3. Per-file error handling in the restore loop (HIGH)

**Location:** §7 — Startup Restore

```csharp
foreach (var fp in ws.OpenFilePaths.Where(File.Exists))
    await shell.EditorViewModel.OpenFileAsync(fp);
```

If any single file fails (locked, encoding error, permission denied), the exception
aborts the entire loop — remaining files won't open, the active tab won't be restored,
and recent folders won't load.

**Recommendation:** Wrap each `OpenFileAsync` call in its own `try/catch` so one bad
file doesn't prevent the rest of the workspace from restoring. See the code in item 1
above for the pattern.

---

### 4. `PixelPoint` uses `int` — round-trip loses sub-pixel precision (LOW)

**Location:** §6a (write) and §7 (read)

`Window.PositionChanged` delivers a `PixelPoint` with `int X/Y`. `WindowState` stores
`double X/Y`. The round-trip is: `PixelPoint(int) → double → int`. Sub-pixel values are
truncated, but this is acceptable — window positions are always integer pixels.

**Recommendation:** Add a one-line note in Limitations: "Window position stored as `double`
but restored as `int` (pixel granularity). No sub-pixel precision loss in practice."

---

### 5. Hardcoded `Width`/`Height` in `MainWindow.axaml` must be removed (MEDIUM)

**Location:** §6b

`MainWindow.axaml` lines 8-9 currently hardcode:
```xml
Width="1200"
Height="800"
```

§6b says to bind these to `ShellViewModel`:
```xml
Width="{Binding WindowWidth}"
Height="{Binding WindowHeight}"
```

The plan's "Files to Modify" table mentions adding bindings but doesn't explicitly call
out that the hardcoded values on lines 8-9 must be **replaced**, not just augmented. If
left in place, the XAML static value and the binding will conflict (the binding wins, but
it's confusing to have dead attributes).

**Recommendation:** Add to the file-modification note: "Remove hardcoded `Width="1200"`
and `Height="800"` from `MainWindow.axaml` lines 8-9; the defaults now live in
`ShellViewModel` properties."

---

### 6. `Window.WindowState` binding may not be ready during restore (MEDIUM)

**Location:** §7 — Startup Restore

The plan sets `shell.IsWindowMaximized = win.IsMaximized` and relies on the
`BoolToWindowStateConverter` XAML binding to propagate it to `mainWindow.WindowState`.

During `OnFrameworkInitializationCompleted`, the window is constructed but may not have
completed its layout pass. XAML bindings are typically evaluated on the first layout pass,
which hasn't happened yet at this point.

In contrast, `mainWindow.Position` is set directly in code — reliable.

**Recommendation:** Set `mainWindow.WindowState` directly in code alongside `Position`,
rather than relying on the binding:

```csharp
if (ws?.Window is { } win)
{
    shell.WindowWidth = win.Width;
    shell.WindowHeight = win.Height;
    mainWindow.Position = new PixelPoint((int)win.X, (int)win.Y);
    mainWindow.WindowState = win.IsMaximized
        ? Avalonia.Controls.WindowState.Maximized
        : Avalonia.Controls.WindowState.Normal;
}
```

The `IsWindowMaximized` reactive property and binding can still be used for the
`BoolToWindowStateConverter` on subsequent changes (e.g. user maximizes/restores
during the session). The startup path should set it directly.

---

### 7. `ISettingsService` registration placement in DI (LOW)

**Location:** §4

The plan says to add `ISettingsService` in `BuildServices()`. The current file structure
in `App.axaml.cs` follows a convention: services first (lines 77-117), then ViewModels
(lines 123-129). The `ISettingsService` registration should go in the services section
(e.g. after `BuildServiceFactory` at line 117), not at the end or near the ViewModel block.

**Recommendation:** Add the registration after `services.AddSingleton<GitServiceFactory>()`
(line 120), keeping the services-then-viewmodels ordering consistent.

---

### 8. `AddRecentFolder` during restore is redundant work (LOW)

**Location:** §7

```csharp
foreach (var recent in ws.RecentFolders)
    shell.AddRecentFolder(recent);
```

`AddRecentFolder` normalizes, deduplicates, and caps at 10. The paths loaded from
`workspace.json` are already normalized and deduplicated. Re-processing them is wasteful
but harmless.

**Recommendation:** Two options — either (a) accept the redundancy (it's trivial work),
or (b) add a `SetRecentFolders(IReadOnlyList<string>)` method that directly replaces
the in-memory list without normalization. Option (a) is fine for Phase 8. Note it if
you want, or skip it.

---

### 9. Canonical source of truth for recent folders (LOW — documentation only)

**Location:** §3 (implementation) and §5f (save)

The `_recentFolders` in-memory list is the canonical source. The file is a persistence
mirror. On startup, `LoadRecentFoldersFromDisk()` loads from file to memory. On save,
the in-memory list is written to the file.

If the app crashes, only the in-memory list is lost. The file still has the
last-on-disk state (from previous save/exit). This is correct behavior and already
covered by Limitations #1 ("crash = lost tab state").

**Recommendation:** No change needed. The plan is internally consistent. Just be aware
that `_recentFolders` is the source of truth, not the file.

---

## Summary Table

| # | Issue | Severity | Action Required |
|---|-------|----------|-----------------|
| 1 | Restore error boundary | HIGH | Add `try/catch` around full restore block in §7 |
| 2 | Fire-and-forget comment | LOW | Add one-line comment |
| 3 | Per-file restore errors | HIGH | Wrap each `OpenFileAsync` in `try/catch` |
| 4 | PixelPoint int/double | LOW | Add note to Limitations |
| 5 | Hardcoded Width/Height | MEDIUM | Explicitly note removal in file-modification table |
| 6 | WindowState binding timing | MEDIUM | Set `mainWindow.WindowState` directly in code |
| 7 | DI registration placement | LOW | Place after `GitServiceFactory` |
| 8 | Redundant AddRecentFolder | LOW | Accept or add bulk setter |
| 9 | Source of truth clarity | LOW | No change, just awareness |

---

*All issues are minor. Items 1, 3, and 5-6 should be incorporated before implementation.*
*The plan is otherwise well-designed and ready to execute.*
