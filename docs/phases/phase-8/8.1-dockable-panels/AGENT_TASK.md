# Phase 8.1a — Dockable Panels Recovery Plan

> **Task for AI Agent:** Write a fixed, incremental implementation plan for Phase 8.1a
> **Output file:** `AgentName_Recommendation.md` (e.g. `DeepseekV4_Recommendation.md`)

---

## Background

Aero is a standalone IDE built in C#/Avalonia 11.3 (MVVM, ReactiveUI, Dock.Avalonia).
Phase 8.1a aims to wire existing panels (Explorer, Git, Editor, Problems, Output) into Dock.Avalonia so they are draggable, resizable, hideable, and rearrangeable.

**This is a SECOND attempt.** The first attempt (`failed-dockable-panels` branch) was fully implemented (M1-M6) but the UI did not render correctly. All 545 unit tests passed, the layout structure was correct in logs, but:
- Only the Explorer panel was visible (filled entire window)
- Git, Problems, Output panels existed but were invisible
- Editor area was empty
- 13+ debugging attempts could not identify root cause

The implementation was reverted. We are now on `phase-8.1a-dockable-panels-v2` branch with a clean baseline.

---

## Key Documents (Read Before Planning)

### 1. Post-Mortem: `docs/POSTMORTEM-phase-8.1a.md`
- Full M1-M6 implementation was done in one pass (no incremental testing)
- Dock.Avalonia's internal rendering was not understood
- `$parent[Window].DataContext` binding breaks inside `DeferredContentControl`
- `Dock.Avalonia.Themes.Simple` was missing from App.axaml
- `InitializeDockControl()` was called in constructor before DataContext was set
- No logging from start → hard to debug
- All debugging was done at the end (sunk cost: 20+ commits, 3+ hours)

### 2. Approach Analysis: `docs/phases/phase-8/8.1-dockable-panels/DOCKING_APPROACH_ANALYSIS.md`
- Two viable DataTemplate approaches identified:
  - **Option A** (safer): Inject ViewModel directly into `IDockable.Context` property from code-behind
  - **Option B** (more MVVM): Bind to `{Binding Context}` in DataTemplate (timing uncertain)
- Initialization order must be: Set DataContext → Call Initialize() → InitializeDockControl(shell) → WireViewModels → Assign Layout
- `ProportionalDock` children need explicit `Proportion` values
- `Dock.Avalonia.Themes.Simple` StyleInclude must be in App.axaml

### 3. AGENTS.md Section 9 (Lessons from Phase 8.1a Failure)
Key principles for this re-attempt:
- Validate plan BEFORE implementation (minimal proof-of-concept first)
- One step at a time → test after each change
- Understand the library first (read docs, create test app, verify behavior)
- Add logging from start
- Document what doesn't work

---

## Current v2 Branch State

### Working (Phase 0-7 Complete)
- `App.axaml.cs` — DI container with all services registered
- `MainWindow.axaml` — Using Grid layout (sidebar | splitter | editor + bottom panel)
- `MainWindow.axaml.cs` — `Initialize(IMessageBus bus)` method exists (bus subscriptions, dialogs)
- `ShellViewModel` — Uses boolean toggles for panel visibility (IsSidebarVisible, IsBottomPanelVisible)
- All 5 panels exist as separate Views/ViewModels

### NuGet Packages Already Added
- `Dock.Avalonia` v11.3.*
- `Dock.Avalonia.Themes.Simple` v11.3.*
- `Dock.Serializer.Newtonsoft` v11.3.*
- `Dock.Serializer.SystemTextJson` v11.3.*

### Missing (Needs Implementation)
- No `Docking/` folder or any Dock.Avalonia code
- `App.axaml` does NOT include Dock.Avalonia theme styles
- `MainWindow.axaml` still uses Grid, not DockControl
- `ShellViewModel` has no layout model or dock-aware navigation
- No layout persistence

---

## Task: Write Your Implementation Plan

Design a **safe, incremental** implementation plan for Phase 8.1a Freeform Mode.

### Requirements

1. **Incremental milestones** (M0.5, M1, M2, ...) — each milestone must be testable independently
2. **Checkpoint/rollback strategy** — each milestone is a git commit that can be reverted cleanly
3. **Proof-of-concept first** — before any real code, create a minimal Dock.Avalonia test that validates the library works (can render a tool panel with content)
4. **Logging from start** — specify what logging to add and where (makes debugging possible)
5. **Initialization order** — must match the pattern: Set DataContext → Initialize() → DockControl setup
6. **Explicit proportion values** for all ProportionalDock children
7. **DataTemplate strategy** — pick Option A (Context injection) or Option B (Binding), justify why

### Focus Areas

- How to wire ViewModels into Dock.Avalonia tool dockables (Context vs DataTemplate)
- How to transition from the current Grid layout to DockControl without breaking existing functionality
- How to preserve the toggle behavior (IsSidebarVisible, IsBottomPanelVisible, ToggleSidebarCommand, etc.)
- How to handle layout persistence (what to save, when to save)
- Layout mode switching stub (Freeform vs Tile — Tile is NOT implemented yet, just a placeholder)

### Output Format

Save as `AgentName_Recommendation.md` with this structure:

```markdown
# AgentName_Recommendation.md

## Approach Overview
One paragraph summarizing your strategy.

## Recommended DataTemplate Strategy
Pick Option A or B, with justification.

## Milestone Plan
| Milestone | Scope | Test Verification | Rollback Point |
|-----------|-------|-------------------|----------------|
| M0.5 | ... | ... | commit X |
| M1 | ... | ... | commit Y |
| ... | ... | ... | ... |

## Key Risks & Mitigations
What could go wrong and how to prevent it.

## Initialization Sequence (Pseudo-Code)
Show the exact order of operations in App.axaml.cs and MainWindow.
```

---

## Constraints

- Must work with Avalonia 11.3 (not 12.x)
- Must use Dock.Avalonia v11.3.* (not a newer major version)
- Must not break existing Phase 0-7 functionality
- Must preserve all existing keyboard shortcuts and menu commands
- Must support Freeform mode only (Tile mode is future work)
- No DialogHost.Avalonia (incompatible with Avalonia 11.3)
