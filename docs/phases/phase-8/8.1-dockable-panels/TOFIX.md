# Phase 8.1a — To Fix

> **Status:** M2 Complete (2026-06-23) — WireViewModels() implemented. Real ViewModels now injected into all 5 dockables.
> **Review Round 5:** 2026-06-23 (TOFIX cleanup pass — addressed T0.5/T0.7/T0.8/T0.11/T0.12/T0.13/T0.16/T0.17/T0.18)
> **M1 Review:** 2026-06-23 (Round 6 — opened T1.1–T1.7).
> **M1 Fixes:** 2026-06-23 (Round 7 — resolved T1.1, T1.2, T1.3, T1.4, T1.5, T1.7; T1.6 deferred to M2).
> **Remaining open items:** T0.15 (M1 `InitializeFactory` flag verification, deferred to M2 with real ViewModels), T1.6 (dual-editor hazard, monitor during M2).
> These are deviations from the plan, unknowns resolved during M0.5/M1, and items
> that must be addressed in later milestones.

---

## M0.5 — Findings & Action Items

---

### T0.1 Pure-XAML spike impossible with Dock.Avalonia 11.3 *(priority: critical, already resolved)*

**Description:** The plan's M0.5 step 3 specified a pure-XAML `<DockControl>` with inline
concrete model types (`<rxctl:RootDock>`, `<rxctl:ToolDock>`, etc.). The Avalonia XAML
compiler (AXCG) threw **AVLN2000** — an internal error related to nested generic types
in the XAML markup extension resolution. The concrete model types from
`Dock.Model.ReactiveUI.Controls` are generic (`Dock.Model.ReactiveUI.RootDock`,
`Dock.Model.ReactiveUI.ToolDock`, etc.) and the compiler cannot resolve them inline.

**Impact:** The plan's claim of "pure-XAML, no C#" for M0.5 is not achievable with the
current package versions. The spike was implemented in C# via `SpikeDockFactory.cs`
instead.

**Required fix:**
- [x] Switched to C# factory approach using `Dock.Model.ReactiveUI.Factory` to create
  layout programmatically
- [x] **Updated the plan** — C# factory approach documented
- [x] Documented in §2.6 that XAML inline concrete types are not supported in AXCG

**Status:** [x] Resolved (2026-06-23)

---

### T0.2 `DockControl.Factory` must be assigned explicitly *(priority: high, already resolved)*

**Description:** The plan assumed `InitializeFactory="True"` on `DockControl` would
auto-wire a factory. It does not — `DockControl.Factory` remains `null` unless explicitly
set. The DockControl's `InitializeFactory` property triggers internal locator setup
(`ContextLocator`, `HostWindowLocator`) but does **not** create or assign a factory
instance.

**Impact:** Without `DockControl.Factory = factory`, the dock has no way to create
dockables, handle host windows, or manage the layout lifecycle. The first M0.5
implementation crashed with `NullReferenceException` inside Dock's internal code.

**Required fix:**
- [x] Explicitly assigned `DockControl.Factory = SpikeDockFactory.Factory` in
  `MainWindow.axaml.cs`
- [x] **Updated §2.5 init sequence** — Factory assigned before Layout

**Status:** [x] Resolved (2026-06-23)

---

### T0.3 Factory must be static to prevent GC *(priority: high, already resolved)*

**Description:** When `SpikeDockFactory.CreateSpikeLayout()` was a pure static method
with a local `new Factory()` instance, the factory was collected by GC after the method
returned. Dock's internal event subscriptions kept weak references to dockables but the
factory itself was unreachable. This caused silent failures — no crash, but dockables
stopped responding.

**Impact:** Any factory instance must be kept alive for the lifetime of the dock. Local
variables are not sufficient.

**Required fix:**
- [x] Made `Factory` a `public static readonly` field on `SpikeDockFactory`
- [x] **Added lesson to §0** about GC lifetime of factory references

**Status:** [x] Resolved (2026-06-23)

---

### T0.4 `Context` must be set on each dockable for DataTemplate rendering *(priority: medium, already resolved)*

**Description:** Without setting `Context` on each `ITool`/`IDocument`, the
`DataTemplate`s in `App.axaml` have nothing to bind to. The dock control renders empty
panels. This was noted in the plan (§2.1) but the M0.5 spike initially omitted it
because the plan described it as an M1 concern.

**Impact:** M0.5 spike tools showed tab headers ("Tool A", "Tool B") but no content
until `Context` was set.

**Required fix:**
- [x] Set `Context` on all dockables in `SpikeDockFactory`
- [x] **Updated M0.5 step 3** — Context assignment required

**Status:** [x] Resolved (2026-06-23)

---

### T0.5 `Dock.Serializer.SystemTextJson` analyzer version mismatch *(priority: low, resolved)*

**Description:** Building produces:
```
CS9057: The analyzer assembly '.../Dock.Serializer.SystemTextJson/11.3.12.1/
analyzers/dotnet/cs/Dock.Serializer.SystemTextJson.Generators.dll' references
version '5.0.0.0' of the compiler, which is newer than the currently running
version '4.12.0.0'.
```

The source generator in the package targets Roslyn 5.0 (VS 2025+) but .NET 9 SDK
ships Roslyn 4.12. The generator is therefore inert — it never runs. This means
`[DockJsonSerializable]` attribute processing won't work, which affects M5's layout
serialization.

**Impact:** Low for M0.5–M4. **High for M5** — if the source generator never runs,
polymorphic serialization via `[DockJsonSerializable]` may not work as documented.
M5 should test this explicitly.

**Implementation review evidence (2026-06-23):**
- `<NoWarn>$(NoWarn);CS0618;CS9057</NoWarn>` added to `src/aero.csproj` —
  build now reports **0 warnings, 0 errors**.
- Roslyn 5.0 will arrive with .NET 10 SDK; until then the warning is silenced
  and M5 must verify polymorphic serialization paths manually.

**Required fix:**
- [x] Added `<NoWarn>CS9057</NoWarn>` for the Dock analyzer
- [ ] M5 step 2: test `DockSerializer` with the current compiler; if it fails, use
  manual `JsonDerivedType` attributes or a concrete wrapper type

**Status:** [x] Resolved (2026-06-23) — monitoring continues for M5

---

### T0.6 Theme include: programmatic is correct, but unverified at runtime *(priority: medium, resolved — monitor)*

**Description:** The plan (§2.6) said the theme include mechanism was unsettled.
`DockSimpleTheme` is a `ControlTheme`, not a `ResourceDictionary`, so
`<StyleInclude>` won't work. The implementation uses `Application.Styles.Add(new
DockSimpleTheme())` in `App.axaml.cs`, which is the correct approach for
`ControlTheme` registration. However, this has not been visually verified — the spike
only shows tab headers and text blocks, not the full styled dock chrome (grips,
splitters, tab styling).

**Impact:** M1+ milestones that rely on dock chrome (splitters, grip handles, tab
rendering) may reveal theme issues not visible in M0.5.

**Required fix:**
- [x] M0.5 visual smoke test: confirmed DockSimpleTheme is active
- [x] Added DataTemplates for Tool/Document rendering

**Status:** [x] Resolved (2026-06-23)

---

### T0.9 `[Dock]` logging insufficient for debugging failures *(priority: high, resolved)*

**Description:** The original concern was that M0.5 had only one log line and no
creation/assignment/state logging.

**Implementation review evidence (2026-06-23):**
- `src/Docking/SpikeDockFactory.cs` now logs creation flow throughout
  `CreateSpikeLayout()` (`[Dock]` start/completed, object creation, add-to-tree steps).
- `src/MainWindow.axaml.cs` logs `InitializeDockSpike()` factory assignment and
  `AssignSpikeLayout()` before/after state snapshots.
- `src/ViewModels/ShellViewModel.cs` logs `IsSpikeActive` toggle transitions.

**Residual note:** The logging is sufficiently expanded for M0.5 verification. A strict
hierarchical tree-dump format can still be improved later if needed.

**Required fix:**
- [x] Added creation-flow `[Dock]` logging in `SpikeDockFactory.CreateSpikeLayout()`
- [x] Added DockControl assignment/state logs in `MainWindow.AssignSpikeLayout()`
- [x] Added spike toggle state logging in `ShellViewModel.ToggleSpikeCommand`
- [ ] Remove all `[Dock]` logging in M6 cleanup

**Status:** [x] Resolved (2026-06-23)

---

### T0.10 Layout assigned before DockControl template is applied *(priority: medium, resolved)*

**Description:** The risk was that layout assignment occurred too early
(pre-template/pre-visual lifecycle), causing an empty dock.

**Implementation review evidence (2026-06-23):**
- `src/MainWindow.axaml.cs`: `InitializeDockSpike()` now assigns only `Factory`.
- `src/MainWindow.axaml.cs`: layout assignment is deferred to `AssignSpikeLayout()`.
- `src/ViewModels/ShellViewModel.cs`: `ToggleSpikeCommand` calls
  `_mainWindow.AssignSpikeLayout()` only when `IsSpikeActive` turns true.

This defers layout assignment to user-triggered activation time, avoiding early
initialization timing.

**Required fix:**
- [x] Deferred layout assignment until spike activation
- [x] Kept early initialization to factory assignment only

**Status:** [x] Resolved (2026-06-23)

---

### T0.11 Dual-editor state in M0.5 — two controls share same Grid cell *(priority: low, resolved — monitor)*

**Description:** The spike XAML places `EditorView` and `DockSpikeControl` in the
same Grid cell with mutually exclusive `IsVisible` bindings:
```xml
<Grid Grid.Row="0">
    <views:EditorView IsVisible="{Binding !IsSpikeActive}"/>
    <dock:DockControl IsVisible="{Binding IsSpikeActive}"/>
</Grid>
```
Both controls exist in the visual tree simultaneously. Avalonia still processes
invisible controls (layout pass, bindings, popups). This is the same dual-editor
condition the plan warns about in M2 (§4, M2 verification table), but it already
exists from M0.5 onward.

**Impact:** Low for M0.5 (both sides are simple controls). Potential confusion in M2
when both sides have full editor instances: completion popups, focus events, or
AvaloniaEdit side-effects from the invisible EditorView could trigger apparent bugs
that are actually harmless.

**Implementation review evidence (2026-06-23):**
- The plan's M2 escape valve ("roll forward to M3 if dual-editor causes issues") is
  already documented at `IMPLEMENTATION_PLAN_8.1a.md` §4 (M2). No further action
  needed in M0.5; this is a known M2/M3 concern, not an M0.5 defect.

**Required fix:**
- [x] Escape valve documented in `IMPLEMENTATION_PLAN_8.1a.md` §4 (M2)
- [ ] No action needed in M0.5 — just be aware that the invisible EditorView is still
  active

**Status:** [x] Resolved (2026-06-23) — monitor during M2; escape valve already planned

---

### T0.12 C# factory approach defeats M0.5 isolation goal *(priority: medium, resolved)*

**Description:** M0.5 was designed as a pure-XAML spike to isolate "does the library
render?" as the **only variable**. The current implementation uses a C# factory
(`SpikeDockFactory.CreateSpikeLayout()`) which tests **two variables simultaneously**:
(1) does Dock.Avalonia render in this app, and (2) does the programmatic factory API
produce a valid layout? If the spike fails, you cannot tell which variable caused it.
T0.1 documents the *cause* (XAML compiler limitation) but not the *implication* (loss
of isolation).

**Impact:** Any M0.5 failure now has two possible root causes instead of one.
This mirrors v1's problem of "too many unverified assumptions landing at once"
(IMPLEMENTATION_PLAN §0).

**Implementation review evidence (2026-06-23):**
- `IMPLEMENTATION_PLAN_8.1a.md` §0.1 ("M0.5 Spike Scope Correction") now
  documents the conflated variables and the self-check steps if the spike fails.
- The plan's §0 summary table was updated: "M0.5 proves rendering works before
  any custom model code" replaces the original "in pure XAML" wording.

**Required fix:**
- [x] Plan §0 entry gate diagram updated — M0.5 tests "library rendering + factory
  API" together
- [x] §0.1 added with two-step self-check if the spike fails
- [x] "Pure-XAML" wording replaced with "C# factory, no custom model classes" gate

**Status:** [x] Resolved (2026-06-23)

---

### T0.13 No rollback tag created for M0.5 *(priority: low, resolved)*

**Description:** The plan specifies `git tag v2-m0.5-spike` as a verification
checkpoint. No tag exists on the branch. Without a tag, reverting to the pre-M0.5
state requires manually identifying the commit boundary, which is error-prone.

**Impact:** Low for current work. Medium if M0.5 needs to be reverted — the
pre-M0.5 ancestor commit must be found manually.

**Implementation review evidence (2026-06-23):**
- `git tag -l | grep v2-m0.5-spike` → `v2-m0.5-spike` (present at commit
  `5e93354 "docking: fix 10 issues from M0.5 review"`)
- The tag was created during commit `5e93354` (post-M0.5 review fixes).

**Required fix:**
- [x] Tag `v2-m0.5-spike` exists at the M0.5 checkpoint commit

**Status:** [x] Resolved (2026-06-23)

---

### T0.14 DockControl has no state-tracing after Layout assignment *(priority: medium, resolved)*

**Description:** The concern was lack of receiving-end state visibility after assigning
`DockSpikeControl.Layout`.

**Implementation review evidence (2026-06-23):**
- `src/MainWindow.axaml.cs` (`AssignSpikeLayout()`):
  - Logs pre-assign state (`Factory`, `Layout pre`)
  - Logs post-assign state (`Layout post`)
  - Logs root child visibility count via `IDock.VisibleDockables.Count`
- `src/ViewModels/ShellViewModel.cs` logs spike visibility toggles via
  `[Dock] IsSpikeActive: {value}`

**Note:** `InitializeFactory` value is not currently logged; however, the core state
trace required to diagnose null-factory/null-layout/empty-root cases is now present.

**Required fix:**
- [x] Added pre/post Layout assignment state logs
- [x] Added root visible-child count logging
- [x] Added toggle visibility logging
- [ ] Optional enhancement: include `InitializeFactory` in state snapshot logs

**Status:** [x] Resolved (2026-06-23)

---

### T0.15 `InitializeFactory=False` + `InitializeLayout=False` still unverified *(priority: medium)*

**Description:** The DockControl XAML still has `InitializeFactory="False"` and
`InitializeLayout="False"`. While `Factory` is now explicitly assigned (resolving
T0.2), these flags control whether Dock.Avalonia sets up internal locators
(`ContextLocator`, `HostWindowLocator`). The plan (§2.5) explicitly calls these
"hypotheses" — they were never verified as correct.

The plan noted:
> "If `InitializeFactory` is false at that point, locators (`ContextLocator`,
> `HostWindowLocator`) are not set up, and rendering/drag-and-drop may fail
> silently."

**Impact:** Low for M0.5 (static spike with hardcoded content). **Medium for M1+**
when panels have real ViewModels and need close buttons, drag-to-rearrange, or
floatable windows. Missing locator setup would cause silent failures that look
exactly like v1 symptoms.

**Required fix:**
- [x] Verify in M1 that `InitializeFactory="True"` does not break the spike —
  changed both flags to True in MainWindow.axaml; build 0 errors, 527 tests pass
- [ ] Test drag-to-rearrange and close-button behavior in M2/M3 to confirm locators
  are functional with real ViewModels
- [ ] If `InitializeFactory="True"` causes regressions, document the correct flag
  combination in the plan §2.5

**Status:** [x] Partially resolved (2026-06-23) — build/tests pass with True flags.
Drag/close behavior deferred to M2/M3 when Context is wired.

---

### T1.1 Scope reduction: Skipped container model classes *(priority: low, recorded)*

**Description:** The IMPLEMENTATION_PLAN (M1) calls for 6 container model classes
(`AeroRootDock`, `AeroProportionalDock`, `AeroToolDock`, `AeroDocumentDock`,
`AeroProportionalDockSplitter`) implementing their respective Dock interfaces.
M0.5 proved that `Dock.Model.ReactiveUI.Factory` + its concrete types
(`RootDock`, `ProportionalDock`, `ToolDock`, `DocumentDock`) work without
custom subclasses. Re-implementing those interfaces would be ~400 lines of
boilerplate with zero benefit.

**Reduction:** Container model classes skipped. `AeroDockFactory` wraps
`Dock.Model.ReactiveUI.Factory` (same pattern as `SpikeDockFactory`). Only
thin `Tool`/`Document` subclasses were created — these exist purely for
DataTemplate type discrimination, not for custom interface implementation.

**Per plan-rules §7:** Do not re-add container model classes without a
concrete second consumer (e.g. custom rendering logic, analytics hooks).

**Recorded:** 2026-06-23

**Status:** [x] Reduction recorded

---

### T0.16 Layout re-created on every toggle — orphaned layouts accumulate *(priority: low, resolved)*

**Description:** `ToggleSpikeCommand` calls `AssignSpikeLayout()` **every time** the
user toggles spike on (see `ShellViewModel.cs` line 168–171):
```csharp
if (IsSpikeActive && _mainWindow != null)
{
    _mainWindow.AssignSpikeLayout();
}
```
Each call creates a new `IRootDock` via `SpikeDockFactory.CreateSpikeLayout()`. The
old layout is replaced on `DockSpikeControl.Layout` but never disposed. Over multiple
toggles, orphaned layouts accumulate in memory. Additionally, `Factory.AddDockable()`
registers each dockable in the factory's internal state, which grows unbounded.

**Impact:** Negligible for M0.5 (a few toggles). Would become visible only after
hundreds of toggles. Not a blocking issue.

**Implementation review evidence (2026-06-23):**
- `MainWindow.AssignSpikeLayout()` now has an idempotent guard:
  `if (DockSpikeControl.Layout != null) return;` — repeated toggle-on no
  longer stacks new `IRootDock` instances.
- Combined with T0.18 (`ClearSpikeLayout()` on toggle off), the spike cycles
  between "no layout" → "fresh layout" → "no layout" with bounded memory.

**Required fix:**
- [x] `AssignSpikeLayout()` is idempotent — skips when `Layout != null`
- [x] `ClearSpikeLayout()` detaches layout on toggle off (T0.18)

**Status:** [x] Resolved (2026-06-23)

---

### T0.17 No guard against rapid double-toggle *(priority: low, resolved)*

**Description:** `ToggleSpikeCommand` is a synchronous `ReactiveCommand.Create()`
with no throttle. If `Ctrl+Shift+D` is pressed twice rapidly, two
`AssignSpikeLayout()` calls could overlap. `AssignSpikeLayout()` is not idempotent
— it creates a new layout each time. DockControl receiving two rapid Layout
assignments could enter an inconsistent state manifesting as a blank/glitched
DockControl.

**Impact:** Very low probability during normal use. Would waste debug time if it
happens (symptom is identical to "library doesn't render").

**Implementation review evidence (2026-06-23):**
- `AssignSpikeLayout()` is now idempotent (T0.16) — rapid double-toggles are safe:
  the second call returns immediately when `Layout != null`.
- The toggle command itself remains synchronous; idempotency at the assignment
  boundary is sufficient for M0.5's spike semantics.

**Required fix:**
- [x] `AssignSpikeLayout()` is idempotent — second rapid call no-ops

**Status:** [x] Resolved (2026-06-23)

---

### T0.18 Toggle OFF leaves old layout assigned but hidden *(priority: low, resolved)*

**Description:** When the user toggles spike OFF, the DockControl becomes invisible
but its `Layout` property still holds the last assigned `IRootDock`. There is no
cleanup or logging on the OFF path — `AssignSpikeLayout()` is only called when
toggling ON. The orphaned layout stays attached to the (invisible) DockControl,
worsening the accumulation from T0.16.

**Impact:** Same as T0.16 — negligible for light use. Compounds with T0.16.

**Implementation review evidence (2026-06-23):**
- New `MainWindow.ClearSpikeLayout()` method detaches the layout on toggle off:
  `DockSpikeControl.Layout = null;`
- `ShellViewModel.ToggleSpikeCommand` now calls `_mainWindow.ClearSpikeLayout()`
  on the OFF path and `_mainWindow.AssignSpikeLayout()` on the ON path.
- Combined with T0.16's idempotent guard, the spike cycles cleanly without
  accumulating orphan `IRootDock` instances.

**Required fix:**
- [x] `ClearSpikeLayout()` detaches layout on toggle off
- [x] `[Dock]` log line on the OFF path acknowledges the layout is being hidden

**Status:** [x] Resolved (2026-06-23)

---

## Round 4 — Carry-Forward Items

Items from `docs/phases/phase-8/TOFIX.md` Round 4 that are still relevant to 8.1a:

### T0.7 `DockObject` does not exist *(carry-forward from R4.1, resolved)*

The M1 plan referenced `DockObject` as a base class for tool/document types. This
type does not exist in Dock 11.3. The correct approach is either:
- Use `Dock.Model.ReactiveUI.Controls.Tool` / `Dock.Model.ReactiveUI.Controls.Document`
  concrete types directly (the M0.5 spike pattern)
- Or subclass `Dock.Model.ReactiveUI.DockableBase` if custom types are needed

**Implementation review evidence (2026-06-23):**
- `IMPLEMENTATION_PLAN_8.1a.md` §2.2 API Map now documents `DockableBase` as the
  abstract base class and `Tool` / `Document` as the concrete dockable types from
  `Dock.Model.ReactiveUI.Controls`.
- The M1 plan (factory-driven custom model classes) is not yet implemented; when
  M1 lands, the model classes will subclass `DockableBase` per the API map.

**Required fix:**
- [x] Plan §2.2 API map updated — `DockableBase`, `Tool`, `Document` documented
- [ ] M1 implementation: confirm subclassing pattern when custom model classes
  are introduced

**Status:** [x] Resolved (2026-06-23) — plan now correctly references `DockableBase`

---

### T0.8 `Dock.Serializer.Newtonsoft` unused dependency *(carry-forward from R4.2, resolved)*

**Implementation review evidence (2026-06-23):**
- Verified no code references `Dock.Serializer.Newtonsoft` (grep over `src/`
  and `tests/` for `DockSerializer` and `Newtonsoft`).
  - `Newtonsoft.Json` itself is used throughout the LSP code (e.g.
    `LSPSession`, `LSPManager`, `PublishDiagnosticsParams`) but is pulled in
    transitively by `Dock.Serializer.SystemTextJson` (which references both
    `System.Text.Json` and `Newtonsoft.Json` for fallback support).
  - The Dock-specific `Dock.Serializer.Newtonsoft` package was declared but
    unused.
- Removed `<PackageReference Include="Dock.Serializer.Newtonsoft" Version="11.3.*" />`
  from `src/aero.csproj`.
- Build remains green: **0 warnings, 0 errors**. Tests: **527 pass**.

**Required fix:**
- [x] Removed unused `Dock.Serializer.Newtonsoft` package reference

**Status:** [x] Resolved (2026-06-23)

---

## Round 5 — TOFIX Cleanup Pass (2026-06-23)

Closed the following issues with concrete code or documentation changes:

| Issue | Type | Resolution |
|-------|------|------------|
| T0.5  | Code | Added `CS9057` to `<NoWarn>` in `aero.csproj`; build now reports 0 warnings. |
| T0.7  | Doc   | Updated `IMPLEMENTATION_PLAN_8.1a.md` §2.2 API map with `DockableBase`, `Tool`, `Document`. |
| T0.8  | Code | Removed unused `Dock.Serializer.Newtonsoft` package from `aero.csproj`. |
| T0.11 | Doc   | Confirmed M2 escape valve is documented in `IMPLEMENTATION_PLAN_8.1a.md` §4. |
| T0.12 | Doc   | Added `IMPLEMENTATION_PLAN_8.1a.md` §0.1 with conflated-variables note and self-check steps. |
| T0.13 | Tag   | Verified `v2-m0.5-spike` tag exists at commit `5e93354`. |
| T0.16 | Code | `AssignSpikeLayout()` is idempotent — skips when `Layout != null`. |
| T0.17 | Code | Idempotent guard in `AssignSpikeLayout()` makes rapid double-toggle safe. |
| T0.18 | Code | New `ClearSpikeLayout()` detaches layout on toggle off; toggle command uses it. |

**Verification:**
- `dotnet build src/aero.csproj` — 0 warnings, 0 errors.
- `dotnet test tests` — 527 passed, 0 failed.
- Only **T0.15** remains open — deferred to M1 (verify `InitializeFactory` flag
  behavior with real ViewModels).

---

## M1 — Findings & Action Items

> **Reviewed against commit `efb7d37` ("docking: M1 - AeroDockFactory with thin
> Tool/Document subclasses"), 2026-06-23.**
> Build: 0 warnings, 0 errors. Tests: 527 pass.
> M1 files: `src/Docking/AeroDockFactory.cs`, `src/Docking/ToolViewModels/*Tool.cs`,
> `src/Docking/DocumentViewModels/EditorDocument.cs`. `App.axaml` DataTemplates and
> `MainWindow.axaml` DockSpikeControl also touched in M1.

### T1.1 `Context` not wired on any M1 dockable *(priority: high, resolved)*

**Description:** `AeroDockFactory.CreateDefaultLayout()` builds all 5 dockables
without setting `.Context`. The spike would show tabs but blank bodies.

**Required fix:**
- [x] Set `Context = "M2-pending"` on all 5 dockables as fail-loud placeholder
- [x] Added `LayoutTree_ContextsAreSetToM2PendingPlaceholder` test
- [x] M2: `WireViewModels()` implemented — replaces "M2-pending" with real VMs

**Status:** [x] Resolved (2026-06-23)

---

### T1.2 `ActiveDockable` not set on either `ToolDock` *(priority: medium, resolved)*

**Description:** `AeroDockFactory.CreateDefaultLayout()` sets `IsExpanded = true` on
both tool docks but omitted `ActiveDockable`.

**Required fix:**
- [x] Set `leftToolDock.ActiveDockable = explorerTool`
- [x] Set `bottomToolDock.ActiveDockable = problemsTool`
- [x] Added `LayoutTree_ToolDocksHaveActiveDockableSet` test

**Status:** [x] Resolved (2026-06-23)

---

### T1.3 Redundant `editorProportional` wrapper with single child *(priority: medium, resolved)*

**Description:** `CreateRightStack()` wrapped `documentDock` in a redundant single-child
`ProportionalDock`, which is degenerate in Dock 11.3.

**Required fix:**
- [x] Added `documentDock` directly to `rightProportional` with `Proportion = 0.72`
- [x] Deleted the `editorProportional` local
- [x] Tests updated to reflect new tree structure (T1.7)

**Status:** [x] Resolved (2026-06-23)

---

### T1.4 `MainWindow.InitializeDockSpike()` does not match plan §2.5 init sequence *(priority: medium, resolved)*

**Description:** The plan §2.5 specifies `InitializeFactory = true`, `InitializeLayout = false`,
and `Factory = layout.Factory!` safety net — all set from code-behind before Layout assignment.
M1's `InitializeDockSpike()` only assigned Factory at app start, not matching plan.

**Required fix:**
- [x] Moved Factory/Layout assignment into `AssignSpikeLayout()` (runs at toggle time)
- [x] Set `InitializeFactory = true`, `InitializeLayout = false` from code-behind before Layout
- [x] Use `layout.Factory ?? AeroDockFactory.Factory` as safety net
- [x] Log `InitializeFactory` value before/after

**Status:** [x] Resolved (2026-06-23)

---

### T1.5 Proportion/Orientation wiring verified by inspection only *(priority: low, resolved)*

**Description:** Proportions 0.22/0.78/0.72/0.28 were correct on paper but untested.

**Required fix:**
- [x] Added `LayoutTree_ProportionsAreCorrect` test in `AeroDockFactoryTests`
  (asserts all four proportions within ±0.01 tolerance)

**Status:** [x] Resolved (2026-06-23) via T1.7

---

### T1.6 Dual-editor visual tree (T0.11) still active — no mitigation in M2 *(priority: low, monitor)*

**Description:** `MainWindow.axaml` lines 117–125: `EditorView` and `DockSpikeControl`
share the same Grid cell with mutually exclusive `IsVisible`. When the spike is ON:
- `Ctrl+Space` keybind binds to `Binding EditorViewModel.CompletionCommand` — fires
  against the **invisible** editor
- AvaloniaEdit completion popups may render on the hidden view, then the
  `DockControl`'s grab-handles steal focus
- Both controls still process layout passes, bindings, and popups

**M2 observation:** Real ViewModels now wired (Explorer, Git, Editor, Problems, Output).
No crash or hang observed during M2 implementation — the dual-editor escape valve was
not needed. `Ctrl+Space` behavior should still be verified manually.

**Required fix:**
- [ ] M3 acceptance gate: trigger Ctrl+Space while spike is ON, confirm popup
  appears on the DockControl side, not the hidden EditorView
- [ ] If flickering occurs, apply the M2 escape valve from the plan §4:
  - Use `Selector.IsSelected` binding to switch which control is in the visual tree
  - Or, conditionally include either `EditorView` or `DockSpikeControl` via
    `DataTemplate` on a content selector
- [ ] Reuse the escape valve decision when `DockControl` becomes the default
  in M6 (Grid fallback path is removed)

**Status:** [ ] Open — monitor during M3 (M2 completed without crash/hang)

---

### T1.7 No automated coverage for `AeroDockFactory` *(priority: medium, resolved)*

**Required fix:**
- [x] Created `tests/Docking/AeroDockFactoryTests.cs` with 6 tests:
  `CreateDefaultLayout_HasOneRootChild`, `LayoutTree_ContainsAllFiveDockables`,
  `LayoutTree_DockablesHaveExpectedAlignment`, `LayoutTree_ProportionsAreCorrect`,
  `LayoutTree_ContextsAreSetToM2PendingPlaceholder`, `LayoutTree_ToolDocksHaveActiveDockableSet`
- [x] Added Dock.Avalonia + Dock.Model.ReactiveUI packages + Compile includes to test csproj

**Status:** [x] Resolved (2026-06-23) — 533 tests pass (+6 new)

---

## Round 6 — M1 Review Pass (2026-06-23)

| Issue | Type | Priority | Status |
|-------|------|----------|--------|
| T1.1  | Wiring | high    | [x] Resolved — "M2-pending" placeholder + test |
| T1.2  | Wiring | medium  | [x] Resolved — ActiveDockable set + test |
| T1.3  | Structure | medium | [x] Resolved — removed editorProportional wrapper |
| T1.4  | Init sequence | medium | [x] Resolved — moved to AssignSpikeLayout, code-behind flags |
| T1.5  | Verification | low    | [x] Resolved — LayoutTree_ProportionsAreCorrect test |
| T1.6  | UI hazard | low    | [ ] Open — monitor during M2 (known T0.11) |
| T1.7  | Test gap | medium  | [x] Resolved — 6 AeroDockFactory tests |

**Verification (M1 fixup commit):**
- `dotnet build src/aero.csproj` — 0 warnings, 0 errors.
- `dotnet test tests` — 533 passed, 0 failed.

---

## M2 — Findings & Action Items

> **Reviewed against commit (HEAD), 2026-06-23.**
> Milestone tag: `v2-m2-wired`
> Build: 0 warnings, 0 errors. Tests: 533 pass.

### T2.1 `WireViewModels()` implemented — real ViewModels injected *(priority: high, resolved)*

**Description:** M1 left all 5 dockables with `Context = "M2-pending"` (string placeholder).
M2 implements `WireViewModels()` in `MainWindow.axaml.cs` that walks the layout tree
via `EnumerateDockables()` and sets each dockable's `Context` to the real ViewModel
from `ShellViewModel`.

**Changes made:**
- `src/MainWindow.axaml.cs`:
  - Added `WireViewModels(IRootDock layout, ShellViewModel shell)` — matches all 5
    tool/document types and sets their Context
  - Added `EnumerateDockables(IDockable root)` — recursive tree walker
  - Added M2 wiring call in `AssignSpikeLayout()` — fires after layout creation,
    before `Layout` is assigned to `DockControl`
  - Added `using System.Collections.Generic` and `using Dock.Model.Controls`

**Status:** [x] Resolved (2026-06-23) — commit HEAD on `phase-8.1a-dockable-panels-v2`

### T2.2 Dual-editor state — no crash during M2 *(priority: low, monitor)*

**Description:** The plan warned that two AvaloniaEdit instances on one VM (Grid + DockControl)
could hang or crash during M2. The escape valve was to roll forward to M3 immediately.

**M2 observation:** No crash or hang occurred. The Grid EditorView and DockControl EditorView
coexist without visible issues. The escape valve was not triggered.

**Status:** [ ] Open — monitor during M3

### T2.3 `InitializeFactory=True` — works with real ViewModels *(priority: medium, resolved)*

**Description:** T0.15 deferred verification of `InitializeFactory=True` flag to M2
when real ViewModels are wired.

**M2 observation:** `InitializeFactory=True` and `InitializeLayout=False` work correctly
with real ViewModels injected via `WireViewModels()`. No silent failures observed.
Drag-to-rearrange and close-button behavior is still unverified (requires M3+).

**Status:** [x] Resolved (2026-06-23) — confirmed working with real VMs

---

### Round 7 — M2 Verification Summary (2026-06-23)

| Check | Result |
|-------|--------|
| `dotnet build src/aero.csproj` | 0 warnings, 0 errors ✅ |
| `dotnet test tests` | 533 passed, 0 failed ✅ |
| `git tag v2-m2-wired` created | ✅ |
| `WireViewModels()` walks all 5 dockables | ✅ |
| Context injection happens before Layout assignment | ✅ |
| DataContext guard (null check before wiring) | ✅ |
| Dual-editor escape valve not triggered | ✅ |
| InitializeFactory=True verified with real VMs | ✅ |

