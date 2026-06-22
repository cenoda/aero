# 8.7 — Workspace Persistence: Implementation Recommendations

> Generated from plan review (2026-06-22).
> Supplements `IMPLEMENTATION_PLAN.md` — not a standalone plan.

---

## Verdict: ✅ Plan is ready for implementation

The plan is thorough, source-verified, and well-structured. All claims in the Source Verification table match the actual codebase. The following recommendations are minor improvements — none are blockers.

---

## R1. Set Window Size Directly on Startup (Prevent Flash)

**Section:** §7 — Startup Restore

**Problem:** The plan sets `WindowWidth`/`WindowHeight` on `ShellViewModel` and relies on XAML bindings to propagate to the actual `Window`. On Avalonia startup, the window renders with XAML defaults (1200×800) first, then the binding evaluates and resizes. This may cause a visible flash/resize on startup.

**Recommendation:** Set `Width` and `Height` directly on `mainWindow` alongside the already-planned `Position` and `WindowState` assignments:

```csharp
// In §7 startup restore, after line 378:
mainWindow.Position = new PixelPoint((int)win.X, (int)win.Y);
mainWindow.Width = win.Width;     // ← ADD: avoid binding-delay flash
mainWindow.Height = win.Height;   // ← ADD: avoid binding-delay flash
mainWindow.WindowState = win.IsMaximized
    ? Avalonia.Controls.WindowState.Maximized
    : Avalonia.Controls.WindowState.Normal;
```

The XAML bindings still serve runtime changes (maximize toggle, future 8.1 resize). The direct assignment only overrides the initial frame.

**Priority:** Low — cosmetic only, but zero-cost to implement.

---

## R2. Document Thread-Safety Assumption on Recent Folders

**Section:** §3 — Recent Folders Management

**Problem:** `_recentFolders` is a plain `List<string>` mutated by `AddRecentFolder` (UI thread) and read by `GetRecentFolders`. The current plan assumes single-thread access but does not enforce it. If a future consumer calls `AddRecentFolder` from a background thread, the list could corrupt.

**Recommendation:** Add XML doc comments on both methods in `ISettingsService`:

```csharp
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
```

No lock needed now (single-process IDE, UI-thread callers). The doc comment future-proofs the contract.

**Priority:** Low — documentation only.

---

## R3. Constructor Sync I/O Is Acceptable but Worth Noting

**Section:** §3 — Constructor / `LoadRecentFoldersFromDisk()`

**Problem:** `SettingsService` constructor calls `File.ReadAllText()` synchronously during DI container build. This runs on the UI thread. For a small JSON file (~1KB) this is negligible (<1ms), but it is a behavioral pattern that differs from the `Async` methods on the interface.

**Recommendation:** Add a brief comment in `SettingsService.cs`:

```csharp
// NOTE: Synchronous read at construction time. The workspace file is small
// (~1KB) and this runs during DI build before the window is shown.
// The async Load* methods are for runtime reloads.
public SettingsService(IMessageBus? bus = null)
```

**Priority:** Very low — informational only.

---

## R4. Fire-and-Forget on FolderOpened Save — Awareness for Phase 9

**Section:** §5d — Save on FolderOpened

**Problem:** The plan uses `_ = SaveWorkspaceStateAsync();` (fire-and-forget) in the `FolderOpened` handler. The method has its own try/catch so unobserved exceptions are handled. However, if the app is shutting down when this fires, the write may race with the exit save or the DI container disposal.

**Recommendation:** No change needed for Phase 8.7. The exit flow calls `SaveWorkspaceStateAsync()` synchronously before `Dispose()`, and the fire-and-forget save captures an earlier state. If both complete, the exit save wins (it captures the final state). If the fire-and-forget is still in-flight when `Dispose()` runs, the atomic write (`File.Move`) is atomic at the OS level — no corruption possible.

Just be aware of this if adding periodic auto-save in Phase 9 — that would need a `CancellationTokenSource` tied to app lifecycle.

**Priority:** None — no change. Awareness note for Phase 9.

---

## R5. SettingsModel Theme Default May Diverge from Phase 8.2

**Section:** §1 — `SettingsModel`

**Problem:** `SettingsModel.Theme` defaults to `"Light"`. Phase 8.2 (Theme Engine) may use a different initial value (e.g., `"Default"` for system-theme detection). If 8.2 expects `"System"` as the initial token and 8.7 persists `"Light"` on first save, the user could get a stale theme on second launch.

**Recommendation:** Coordinate with 8.2 before implementation. Either:
- (a) Use a neutral default like `"Default"` and let 8.2 resolve it at runtime, or
- (b) Accept that 8.6 overrides the default when it runs and documents the theme values.

The plan already notes "Values validated by 8.6, not 8.7" — so (b) is likely fine. Just ensure 8.6 reads the current theme value before writing defaults.

**Priority:** Low — cross-sub-phase coordination only.

---

## R6. Test Baseline Should Be 415, Not 411

**Section:** Definition of Done

**Problem:** The plan estimates 14 new tests but uses the formula "baseline: 401 + new: 10 = 411". With 14 unit tests the expected total is **415**, not 411.

Additionally, one extra test would strengthen coverage:

| # | Test | What It Verifies |
|---|------|------------------|
| 15 | `LoadWorkspaceStateAsync_CorruptRecentFolders_ReturnsDefaults` | Partial corruption (valid JSON but wrong `RecentFolders` type) degrades gracefully |

**Recommendation:** Update exit gate to `415+`. Add test 15 if time permits.

**Priority:** Low — bookkeeping.

---

## R7. YAGNI "Files NOT to Modify" List Is Correct

No action needed. Verified against the actual codebase:

- `EditorViewModel.OpenFileAsync(filePath)` — public method, called from §7 restore code as-is.
- `EditorViewModel.Tabs` / `.ActiveTab` — read-only access for saving state. No modification needed.
- `EditorTabViewModel.FilePath` — property already exists (line 79). No modification needed.
- `DocumentManager` — not touched. Restore goes through `EditorViewModel.OpenFileAsync`.

---

## Summary

| # | Recommendation | Priority | Action |
|---|---------------|----------|--------|
| R1 | Set Width/Height directly on window at startup | Low | Add 2 lines to §7 |
| R2 | Document thread-safety assumption on recent folders | Low | Add XML doc comments |
| R3 | Note sync I/O in constructor | Very Low | Add comment |
| R4 | Awareness: fire-and-forget save + shutdown race | None | Note for Phase 9 |
| R5 | Coordinate SettingsModel.Theme default with 8.2 | Low | Confirm with 8.2 author |
| R6 | Update test baseline to 415 | Low | Fix exit gate number |
| R7 | YAGNI list is correct | None | Verified, no action |

**Overall: The plan is well-crafted and ready for implementation. These are polish items, not blockers.**
