# Phase 8.1a Implementation Plan — Review

> **Review Date:** 2026-06-22  
> **Reviewer:** GitHub Copilot  
> **Plan File:** `IMPLEMENTATION_PLAN_8.1a.md`  
> **Status:** Reviewed — Ready with Recommendations

---

## Executive Summary

The Phase 8.1a implementation plan is well-structured and detailed, with clear milestones and verified entry gates. The architecture research is thorough, and the risk mitigation strategy is proactive. However, there are several technical uncertainties that should be resolved before implementation begins.

**Verdict:** Approved pending 3 verifications (see Section 4).

---

## 1. Strengths ✅

### 1.1 Entry Gates Verified
All 8 entry gates are checked and confirmed:
- Phase 8 TOFIX items R1.1–R1.4 all closed
- `dotnet build src/aero.csproj` passes (0 errors)
- `dotnet test tests` passes (baseline: 527 passed)
- 8.9 Design System complete
- 8.5 Icon Decision complete
- Dock.Avalonia packages installed and verified

### 1.2 Architecture Research Excellence
- Dock.Avalonia API verified against actual 11.3.12.1 NuGet XML docs
- Layout model tree structure documented with clear Aero mappings
- All key concepts (DockControl, IFactory, IRootDock, ITool, IDocument) explained

### 1.3 Well-Scoped Milestones
- M1-M7 each have clear goals, steps, deliverables, and test criteria
- Incremental progress allows testing at each step
- Dependencies between milestones are logical

### 1.4 Comprehensive Risk Mitigation
- 7 risks identified with concrete mitigations
- Highest priority risks (IFactory implementation, serialization) have fallbacks

### 1.5 Test Coverage
- Unit tests: 8 test cases planned
- Integration tests: 2 test cases planned
- Manual tests: 8 scenarios documented

---

## 2. Issues & Concerns ⚠️

### 2.1 Dock.Model.Mvvm Assembly Uncertainty (High Priority)

**Issue:** The plan references `Dock.Model.Mvvm` assembly for base classes like `ToolViewModelBase` and `DocumentViewModelBase`, but:
- XML docs search didn't confirm these types exist
- Plan says "If those types aren't public, use Dock.Avalonia's built-in factory methods" — should be verified **before** M1

**Impact:** If base classes don't exist, the entire factory implementation approach needs to change.

**Recommendation:** Add a pre-M1 verification step (M0.5) to confirm `Dock.Model.Mvvm` namespace and base class availability.

### 2.2 DataTemplate Strategy Ambiguity

**Issue:** The plan shows two approaches:
- XAML `DataTemplate` registration in `MainWindow.axaml` (Steps M1.5, M2.1)
- `DockControl.AutoCreateDataTemplates` property (mentioned in M3.4)

**Impact:** Mixed signals on which approach to use.

**Recommendation:** Pick one approach. XAML `DataTemplate`s are more Avalonia-idiomatic and explicit. If using XAML, remove references to `AutoCreateDataTemplates`.

### 2.3 IFactory Implementation Unclear

**Issue:** The plan shows `AeroDockFactory` implementing `IFactory` with overrides like `CreateRootDock()`, `CreateTool()`, etc., but:
- Doesn't specify if we implement `IFactory` directly or extend a base class
- Doesn't show actual method signatures from verified API

**Impact:** Implementation may stall waiting for API clarification.

**Recommendation:** Add a code skeleton showing exact interface methods to implement, based on verified XML docs.

### 2.4 ShellViewModel Toggle Command Complexity

**Issue:** Plan shows tree-walking code to find dockables by ID:
```csharp
var sidebarDock = FindToolDock(_layout, "Explorer");
```
Dock.Avalonia may have built-in APIs for finding dockables (e.g., `DockManager.FindDockable()`).

**Impact:** Implementing custom tree walkers is fragile and may break with Dock.Avalonia updates.

**Recommendation:** Investigate Dock.Avalonia's built-in APIs for dockable lookup before implementing custom tree walkers.

### 2.5 Layout Persistence Path Handling

**Issue:** Using `~/.aero/layout.json` but doesn't handle:
- Cross-platform `SpecialFolder` differences (Windows uses `%USERPROFILE%`, not `~`)
- Concurrent access if multiple IDE instances run

**Impact:** May fail on Windows or with multiple IDE instances.

**Recommendation:** Use `Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)` consistently and add file lock for concurrent access.

---

## 3. Suggestions for Improvement 🔧

### 3.1 Add M0.5 — Verify MVVM Types (Before M1)

**Goal:** Confirm Dock.Model.Mvvm base classes are usable

**Steps:**
1. Create a test project referencing Dock.Avalonia
2. Verify `ToolViewModelBase`, `DocumentViewModelBase` exist and are public
3. If not, pivot to implementing `ITool`/`IDocument` directly

### 3.2 Clarify EditorView Hosting

**Current:** "EditorView internal tabs remain managed by EditorViewModel"

**Verify:**
- EditorView still receives correct `DataContext` when hosted in `IDocument`
- Keybindings (Ctrl+S, Ctrl+Tab) still work inside Dock layout

### 3.3 Add Rollback Plan

**If Dock.Avalonia doesn't work as expected:**
- Keep Grid layout code as fallback behind feature flag
- Allow switching back to fixed layout via `appsettings.json`

### 3.4 Status Bar Exclusion Verification

**Current:** "Status bar stays outside the Dock"

**Verify:** `DockControl` doesn't consume entire window. Document expected `MainWindow.axaml` structure:
```xml
<DockPanel>
    <Menu DockPanel.Dock="Top"/>
    <!-- DockControl goes here, fills remaining space -->
    <StatusBar DockPanel.Dock="Bottom"/>
</DockPanel>
```

---

## 4. Action Items Before Implementation 📋

### Required (Must Do)
1. ✅ **Verify `Dock.Model.Mvvm` base classes exist** and are usable (M0.5)
2. ✅ **Choose XAML DataTemplate approach** over `AutoCreateDataTemplates`
3. ✅ **Investigate Dock.Avalonia's built-in dockable lookup APIs** before implementing tree walkers

### Recommended (Should Do)
4. Add code skeleton for `AeroDockFactory` showing exact `IFactory` method signatures
5. Add file locking to `LayoutPersistenceService` for concurrent access
6. Add feature flag for falling back to Grid layout if Dock.Avalonia fails

### Optional (Nice to Have)
7. Create a sample app referencing Dock.Avalonia to verify API before implementing in aero
8. Document expected `MainWindow.axaml` structure with DockControl + StatusBar

---

## 5. Minor Issues 📝

1. **M7 Cleanup ambiguity** — Says "remove old Grid layout code if not already removed in M1" — should be definitive: M1 removes it, M7 verifies removal
2. **`DockableControl` description** — Section 3.1 says it's "internal state tracker" — correct but could confuse readers who might try to use it as a wrapper
3. **Serialization caveat** — The `[DockJsonSerializable]` attribute requirement is mentioned twice (M5 and Notes) — consolidate to M5

---

## 6. Detailed Milestone Review

### M1 — Dock Infrastructure Skeleton
**Status:** Ready pending M0.5 verification  
**Concerns:** IFactory implementation approach unclear  
**Recommendation:** Add code skeleton before starting

### M2 — Wrap Existing Panels as Dockables
**Status:** Ready  
**Concerns:** DataTemplate strategy needs clarification  
**Recommendation:** Commit to XAML DataTemplate approach

### M3 — Drag-and-Drop Rearrangement
**Status:** Ready  
**Concerns:** None — relies on Dock.Avalonia built-in functionality  
**Recommendation:** Proceed as planned

### M4 — Panel Visibility Toggle Commands
**Status:** Needs revision  
**Concerns:** Custom tree walker implementation  
**Recommendation:** Investigate built-in Dock.Avalonia APIs first

### M5 — Layout Persistence
**Status:** Ready pending path handling fix  
**Concerns:** Cross-platform path, concurrent access  
**Recommendation:** Use `Environment.GetFolderPath()` and add file lock

### M6 — Settings Integration
**Status:** Ready  
**Concerns:** None  
**Recommendation:** Proceed as planned

### M7 — Cleanup and Final Polish
**Status:** Ready  
**Concerns:** Depends on M1 cleanup being definitive  
**Recommendation:** Make M1 cleanup definitive, M7 verifies

---

## 7. Risk Assessment Matrix

| Risk | Probability | Impact | Mitigation Quality | Status |
|------|-------------|--------|-------------------|--------|
| Dock.Avalonia API changed | Low | Medium | Good (verified XML docs) | ✅ |
| net8.0 TFM fallback issues | Low | Low | Good (smoke test passed) | ✅ |
| Concrete model types need [DockJsonSerializable] | Medium | Medium | Partial (attribute location unclear) | ⚠️ |
| IFactory overrides return wrong types | High | High | Partial (no code skeleton) | ⚠️ |
| Existing ViewModel DataContext breaks | Medium | Medium | Good (DataTemplate approach) | ✅ |
| Layout serialization produces huge JSON | Low | Low | Good (structure only) | ✅ |
| GridSplitter removal breaks resize | Low | Low | Good (Dock has splitter) | ✅ |

---

## 8. Final Verdict

**The plan is solid and ready for implementation pending 3 verifications:**

1. ✅ Verify `Dock.Model.Mvvm` base classes exist and are usable (M0.5)
2. ✅ Choose XAML DataTemplate approach over `AutoCreateDataTemplates`
3. ✅ Investigate Dock.Avalonia's built-in dockable lookup APIs before implementing tree walkers

**Once these are resolved, the plan provides a clear path to implementing dockable panels.** The milestone structure allows for incremental progress and testing at each step.

**Recommended next step:** Create M0.5 verification script to confirm Dock.Model.Mvvm API before starting M1.

---

*Review complete. Plan approved with recommendations.*
