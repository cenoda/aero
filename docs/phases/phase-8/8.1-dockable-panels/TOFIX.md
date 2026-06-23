# Phase 8.1a — To Fix

> **Status:** M0.5 Resolved (2026-06-23) — All critical/high issues fixed.
> These are deviations from the plan, unknowns resolved during M0.5, and items
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

### T0.5 `Dock.Serializer.SystemTextJson` analyzer version mismatch *(priority: low)*

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

**Required fix:**
- [ ] Monitor — may resolve when .NET 10 SDK ships Roslyn 5.0
- [ ] M5 step 2: test `DockSerializer` with the current compiler; if it fails, use
  manual `JsonDerivedType` attributes or a concrete wrapper type
- [ ] Consider adding `<NoWarn>CS9057</NoWarn>` for the Dock packages if the warning
  is confirmed harmless

**Status:** [ ] Open — monitor

---

### T0.6 Theme include: programmatic is correct, but unverified at runtime *(priority: medium)*

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

### T0.9 `[Dock]` logging insufficient for debugging failures *(priority: high)*

**Description:** The plan (§6) specifies `[Dock]`-prefixed logging at 5 key sites.
Current M0.5 implementation has exactly **1 log line** (`[Dock] M0.5: spike layout
assigned`) in `MainWindow.axaml.cs`. `SpikeDockFactory.CreateSpikeLayout()` has **zero
logging** — no tree dump, no per-object creation trace, no before/after for
`factory.AddDockable()` calls. If the spike fails, there is no way to distinguish
"layout was never built" from "layout was built but not rendered" from "layout was
built incorrectly."

**Impact:** This repeats v1's #5 contributing factor ("No debugging-friendly code").
Without structured logging, debugging reverts to guesswork — the pattern that cost
3+ hours in v1.

**Required fix:**
- [ ] Add tree-dump logging to `SpikeDockFactory.CreateSpikeLayout()`: depth, type,
  id, proportion per child (as specified in §6)
- [ ] Add logging before/after each `factory.AddDockable()` call
- [ ] Add `[Dock]` logging in `InitializeDockSpike()`: factory type, layout type,
  child count, Layout assignment site
- [ ] Add `[Dock]` log when `IsSpikeActive` toggles (both on and off)
- [ ] Add `[Dock]` log for DockControl state snapshot: `Factory != null`,
  `Layout != null`, `VisibleDockables.Count`
- [ ] Remove all `[Dock]` logging in M6 cleanup

**Status:** [ ] Open — fix before M0.5 verification

---

### T0.10 Layout assigned before DockControl template is applied *(priority: medium)*

**Description:** `InitializeDockSpike()` runs during `MainWindow.Initialize()`, which
is called from `App.axaml.cs` **before** `desktop.MainWindow = mainWindow` (i.e. before
the window is shown). At this point, the DockControl's Avalonia template
(`OnApplyTemplate`) has not run. Some Avalonia controls ignore property changes until
the template is applied. The spike works around this by starting invisible and only
becoming visible when the user presses `Ctrl+Shift+D`, but the Layout was assigned at
a time when the control could not process it.

**Impact:** When the user toggles the spike on, the DockControl becomes visible but
its Layout may not take effect because it was set before the template was ready.
This can manifest as a blank/empty DockControl — exactly the v1 symptom.

**Required fix:**
- [ ] Move Layout assignment to a later lifecycle point: either defer to the first
  time `IsSpikeActive` becomes true, or override `OnApplyTemplate` on DockControl
- [ ] Alternatively, assign Layout in a `Loaded` event handler (fires after template
  is applied)

**Status:** [ ] Open — investigate and fix

---

### T0.11 Dual-editor state in M0.5 — two controls share same Grid cell *(priority: low)*

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

**Required fix:**
- [ ] M2 escape valve already documented: if dual-editor causes issues, roll forward
  to M3 (mutually exclusive modes) rather than debugging in M2
- [ ] No action needed in M0.5 — just be aware that the invisible EditorView is still
  active

**Status:** [ ] Open — monitor; escape valve documented in plan

---

### T0.12 C# factory approach defeats M0.5 isolation goal *(priority: medium)*

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

**Required fix:**
- [ ] Update the plan's entry gate diagram / §0 to reflect that M0.5 tests "library
  rendering + factory API" together, and that any failure requires checking both
  paths separately
- [ ] Add a note in the plan that the "pure-XAML" gate is replaced by "C# factory,
  no custom model classes"
- [ ] Add a self-check step to M0.5 verification: if the spike fails, first test
  with an even simpler factory (single ToolDock with one Tool), then add complexity

**Status:** [ ] Open — plan update pending

---

### T0.13 No rollback tag created for M0.5 *(priority: low)*

**Description:** The plan specifies `git tag v2-m0.5-spike` as a verification
checkpoint. No tag exists on the branch. Without a tag, reverting to the pre-M0.5
state requires manually identifying the commit boundary, which is error-prone.

**Impact:** Low for current work. Medium if M0.5 needs to be reverted — the
pre-M0.5 ancestor commit must be found manually.

**Required fix:**
- [ ] Create tag `git tag v2-m0.5-spike` at the M0.5 checkpoint commit before
  proceeding to M1

**Status:** [ ] Open — tag after M0.5 is verified

---

### T0.14 DockControl has no state-tracing after Layout assignment *(priority: medium)*

**Description:** After `DockSpikeControl.Layout = layout` is set, there is no logging
of what the DockControl's internal state looks like:
- `DockSpikeControl.Factory` — is it null?
- `DockSpikeControl.Layout` — is it the expected IRootDock?
- The IRootDock's `VisibleDockables.Count` — does it have children?
- `DockSpikeControl.InitializeFactory` — True or False?

Without these, you cannot distinguish "layout never arrived" from "layout arrived but
was not processed." This is distinct from T0.9 (which covers creation-side logging)
— this is about verifying the *receiving end*.

**Impact:** If DockControl silently ignores Layout (because Factory is null, or
template not applied, or InitializeFactory=False), there is no log evidence.

**Required fix:**
- [ ] After DockSpikeControl.Layout assignment, log: Factory type (or null), Layout
  type (or null), VisibleDockables count, InitializeFactory value
- [ ] Log when IsSpikeActive toggles to true: "DockControl visible, type, factory"
  and to false: "DockControl hidden"
- [ ] Consider adding a weak timer that re-logs the state 500ms after Layout
  assignment (catches delayed rendering issues)

**Status:** [ ] Open — fix before M0.5 verification

---

## Round 4 — Carry-Forward Items

Items from `docs/phases/phase-8/TOFIX.md` Round 4 that are still relevant to 8.1a:

### T0.7 `DockObject` does not exist *(carry-forward from R4.1)*

The M1 plan references `DockObject` as a base class for tool/document types. This type
does not exist in Dock 11.3. The correct approach is either:
- Use `Dock.Model.ReactiveUI` concrete types directly (e.g. `Tool`, `Document`)
- Or subclass `ManagedDockableBase` if custom types are needed

**Status:** [ ] Open — resolve in M1

---

### T0.8 `Dock.Serializer.Newtonsoft` unused dependency *(carry-forward from R4.2)*

**Status:** [ ] Open — remove in M6 cleanup
