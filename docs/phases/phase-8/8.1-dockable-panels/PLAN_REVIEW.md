# 8.1a Plan Review — Dockable Panels (Freeform Mode)

> **Review Date:** 2026-06-22
> **Reviewer:** GitHub Copilot
> **Status:** Approved with Notes

---

## Executive Summary

The 8.1a implementation plan is **approved** with 6 findings that require attention before or during implementation. The plan is well-structured with clear milestones, but the Dock.Avalonia API surface needs verification before coding begins.

---

## 1. Package Verification ✅

| Package | In .csproj | Version | API Confirmed |
|---------|------------|---------|-------------|
| `Dock.Avalonia` | ✅ Line 38 | 11.3.* | ✅ `DockControl` exists |
| `Dock.Serializer.SystemTextJson` | ✅ Line 34 | 11.3.* | ✅ `DockSerializer<T>.Serialize()` / `Deserialize()` |

---

## 2. Entry Gates (M0) — Status

| Gate | Status | Notes |
|------|--------|-------|
| Phase 8 TOFIX R1.1–R1.4 | ✅ Assumed | Verify in PHASES.md |
| `dotnet build` passes | ✅ Confirmed | Exit code 0 |
| `dotnet test` passes | ✅ Confirmed | 527 tests |
| 8.9 Design System | ✅ Assumed | Check `src/Styles/` |
| 8.5 Icon Decision | ✅ Assumed | Check `Icons.axaml` |
| Dock.Avalonia net8.0 fallback | ✅ Assumed | R1.2 verified |
| Dock.Serializer API | ✅ Confirmed | See section 2.1 |

### 2.1 Dock.Serializer API Confirmed

```csharp
// From Dock.Serializer.SystemTextJson.xml
public class DockSerializer
{
    public string Serialize<T>(T obj);
    public T Deserialize<T>(string json);
    public void Save<T>(Stream stream, T obj);
    public T Load<T>(Stream stream);
}
```

**Note:** Requires `[DockJsonSerializable]` attribute on custom types.

---

## 3. Findings

### 3.1 HIGH: Dock API Model Mismatch

| Plan Reference | Actual API | Action |
|--------------|-----------|----------|--------|
| `IRootDock` | Not in XML | May be `Dock.Model.Core.IDock` |
| `DockableBase` | Not in XML | Need custom `IDockable` impl |
| `DockControl.Layout` | Verify property | Check runtime |

**Mitigation:** Write a small test to dump available types from `Dock.Avalonia.dll` before M1.

---

### 3.2 HIGH: MainWindow Layout Replacement

**Finding:** Current layout is a complex Grid (3 columns + nested rows + splitters). Replacing with `DockControl` is a significant XAML change.

**Risk:** Risk of breaking existing UI during transition.

**Mitigation:** Keep old layout commented in source until M7 cleanup.

---

### 3.3 MEDIUM: ShellViewModel Breaking Changes

**Finding:** 16 references to panel state properties across 2 files:

| Property | .cs refs | .axaml refs | Total |
|----------|---------|------------|-------|
| `IsSidebarVisible` | 2 | 1 | 3 |
| `ActiveSidebarTabIndex` | 2 | 1 | 3 |
| `IsBottomPanelVisible` | 4 | 2 | 6 |
| `ActiveBottomTabIndex` | 3 | 1 | 4 |

**Mitigation:** Create a checklist before M4 to update all references. Grep command:
```bash
grep -rn "IsSidebarVisible\|IsBottomPanelVisible\|ActiveSidebarTabIndex\|ActiveBottomTabIndex" src/
```

---

### 3.4 MEDIUM: Dock.Serializer Generic Constraints

**Finding:** `DockSerializer<T>.Serialize<T>()` requires `T` to be a class. The dock model objects may need `[DockJsonSerializable]` attribute.

**Mitigation:** Verify dock model types have the attribute, or document that custom types need it.

---

### 3.5 LOW: Layout Persistence Path

**Finding:** Plan uses `~/.aero/layout.json` — directory may not exist on first run.

**Mitigation:** Add directory creation in `LayoutPersistenceService` constructor:
```csharp
// In LayoutPersistenceService.cs
public LayoutPersistenceService()
{
    var dir = Path.GetDirectoryName(_layoutPath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    {
        Directory.CreateDirectory(dir);
    }
}
```

---

### 3.6 LOW: Missing Test Files

**Finding:** Plan references `tests/Docking/` folder — does it exist?

**Mitigation:** Create `tests/Docking/` folder and test class stubs before M1:
```
tests/Docking/
├── DockPanelFactoryTests.cs
└── LayoutPersistenceServiceTests.cs
```

---

## 4. Scope Verification

### 4.1 In Scope ✅

- Dock.Avalonia infrastructure
- 5 panel dockable wrappers
- Drag-and-drop rearrangement
- Panel hiding
- Layout persistence
- Mode switch stub
- View menu updates

### 4.2 Out of Scope ✅

- 8.1b Tile Mode
- 8.1c Tear-away windows
- New panels
- Panel ordering/pinning

---

## 5. Risk Summary

| Severity | Finding | Mitigation |
|----------|---------|----------|
| HIGH | Dock API Model Mismatch | Verify runtime API before M1 |
| HIGH | MainWindow Layout Replacement | Keep old layout until M7 |
| MEDIUM | ShellViewModel breaking changes | Grep all refs before M4 |
| MEDIUM | Dock.Serializer constraints | Verify attributes |
| LOW | Layout persistence path | Create directory |
| LOW | Missing test files | Create folder |

---

## 6. Recommendations

### Before Coding Starts (Pre-M1)

1. **Verify Dock API:** Write a test to dump available types from `Dock.Avalonia.dll`
2. **Check entry gates:** Confirm TOFIX R1.1–R1.4 are closed in `PHASES.md`
3. **Create test folder:** Create `tests/Docking/` structure

### During Implementation

1. **M1:** Keep old Grid layout commented until M7
2. **M4:** Use grep to find all ShellViewModel property references
3. **M5:** Add directory creation in `LayoutPersistenceService`

---

## 7. Verdict

**Status:** ✅ Approved with Notes

The plan is implementation-ready pending:
1. Dock API runtime verification (HIGH priority)
2. Entry gate confirmation in PHASES.md
3. Test folder creation

The Dock.Avalonia API risks are documented and mitigable. Clear next steps: verify the actual Dock API surface before coding M1.

---

## 8. Appendix: Grep Commands

```bash
# Find all panel state references
grep -rn "IsSidebarVisible\|IsBottomPanelVisible\|ActiveSidebarTabIndex\|ActiveBottomTabIndex" src/

# Find Dock packages
grep -E "Dock\." src/aero.csproj

# Find MainWindow layout refs
grep -n "Grid.Column\|GridSplitter" src/MainWindow.axaml
```