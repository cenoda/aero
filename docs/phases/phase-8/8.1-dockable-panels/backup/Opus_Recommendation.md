# Opus_Recommendation.md

> **Agent:** Opus (Claude Opus 4.7)
> **Date:** 2026-06-23
> **Branch:** `phase-8.1a-dockable-panels-v2`
> **Predecessor:** `failed-dockable-panels` — see `docs/POSTMORTEM-phase-8.1a.md`

---

## Approach Overview

The first attempt failed not because the code was wrong but because too many unverified assumptions were stacked into a single integration: a custom factory, five dockables, a five-zone layout, deferred `Context` injection, AND a missing theme — all landed at once with no logging. When rendering broke, every layer was a candidate, and 13 debug rounds couldn't isolate the cause.

This plan inverts that by treating Dock.Avalonia as an **untrusted dependency** until proven otherwise, and by **never replacing the working Grid layout until the dock layout has been independently demonstrated to render correctly**. Concretely:

1. **M0.5 is pure XAML** — a `DockControl` with a hand-built layout sitting *inside* a tab on the existing window, no factory, no `IDockable.Context` indirection, just two static tools and one document. If this doesn't render, every later milestone is dead and we know why before writing C#.
2. **M1–M5 introduce one new piece per commit**, each with explicit verification steps. Anything that was already proven (theme include, basic layout) is held constant.
3. **The Grid layout stays alive in parallel** through M1–M3, behind a `LayoutMode` switch (default Grid). DockControl ships hidden, gets exercised by the user/dev manually, and only becomes the default in M4 once it's at functional parity. This kills the "all-or-nothing big bang" risk that wasted three hours in v1.
4. **DataTemplate strategy: Option A (direct `Context` injection)**. The v1 logs already proved this part works — repeating Option B "for purity" would add risk for zero gain.

The plan is deliberately one milestone heavier than the other recommendations because the cost of an extra commit is low, and the cost of another failed wholesale swap is the entire phase.

---

## Recommended DataTemplate Strategy

**Option A — Direct `Context` injection from code-behind.**

```xml
<!-- App.axaml — no DataContext binding in the template -->
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView/>
</DataTemplate>
```

```csharp
// MainWindow.axaml.cs — called after DataContext is set
foreach (var d in EnumerateDockables(layout))
{
    switch (d)
    {
        case ExplorerTool t: t.Context = shell.FileExplorerViewModel; break;
        case GitTool t:      t.Context = shell.GitViewModel;          break;
        case ProblemsTool t: t.Context = shell.ProblemsViewModel;     break;
        case OutputTool t:   t.Context = shell.OutputViewModel;       break;
        case EditorDocument d2: d2.Context = shell.EditorViewModel;   break;
    }
}
```

**Justification:**

| Criterion | Option A (Context injection) | Option B (`{Binding Context}`) |
|---|---|---|
| Confirmed working in v1 | Yes — logs showed all five tools wired | Never tested in this codebase |
| `DeferredContentControl` timing risk | None — DataContext flows from `Context` via Dock's own templating, no parent walk | Depends on Dock applying `Context` before the template materializes |
| Debuggability | Set a breakpoint on the `case` line | Silent binding failure if timing is wrong |
| Resists the v1 failure mode (`$parent[Window].DataContext` collapsing) | Yes, by construction | Mitigates it but doesn't eliminate the class of binding-timing bugs |
| MVVM purity | Slightly compromised (one `switch` in code-behind) | Cleaner |

The MVVM purity argument loses to "the previous attempt died in three hours of opaque binding failures." We'll re-evaluate Option B in a future cleanup phase if Option A causes friction; until then, code-behind injection is the conservative call.

---

## Milestone Plan

Each milestone is a single git commit with a tag (`v2-m{N}`). Rollback is `git reset --hard v2-m{N-1}` or `git revert <commit>` if already pushed.

| Milestone | Scope | Test Verification | Rollback Point |
|---|---|---|---|
| **M0** | **Baseline gate.** Confirm clean tree: `dotnet build src/aero.csproj` 0 errors; `dotnet test tests` passes; `App.axaml` has no Dock includes; `src/Docking/` does not exist; phase-8 `TOFIX.md` is empty for items blocking 8.1a. No code changes. | All four commands pass. | `git tag v2-m0` |
| **M0.5** | **Pure-XAML rendering spike.** Add `Dock.Avalonia.Themes.Simple` `StyleInclude` to `App.axaml`. Add a *new* tab in the existing sidebar `TabControl` titled "Dock spike" containing a hand-written `<DockControl>` with one `RootDock`/`ProportionalDock`/`ToolDock`/two `Tool`s and one `DocumentDock`/`Document`, all defined inline in XAML with hard-coded `<TextBlock Text="...">` content. **No factory, no model classes, no code-behind.** The Grid layout is untouched. | Run app → click "Dock spike" tab → see a tool dock with two tabs and a document area, each showing its placeholder text. Drag the splitter to resize. **If this fails, stop.** Theme, package version, or Avalonia 11.3 compatibility is the cause; no later milestone can succeed until this does. | `git tag v2-m0.5-spike` |
| **M1** | **Model classes and factory, still side-by-side with Grid.** Create `src/Docking/` with `AeroRootDock`, `AeroToolDock`, `AeroDocumentDock`, `AeroProportionalDock`, `AeroProportionalDockSplitter` (concrete `IDock*` implementations), `ExplorerTool`/`GitTool`/`ProblemsTool`/`OutputTool` (`ITool`), `EditorDocument` (`IDocument`), and `AeroDockFactory : Factory`. Replace the M0.5 inline XAML with `<DockControl x:Name="DockSpike"/>` and have `MainWindow.Initialize()` wire the factory + layout (still inside the "Dock spike" tab). DataTemplates registered in `App.axaml`. | Same visible result as M0.5 (the spike tab still shows the layout) but now driven by `AeroDockFactory.CreateDefaultLayout()`. Logs from `AeroDockFactory` print the layout tree depth-first. | `git tag v2-m1-factory` |
| **M2** | **Wire real ViewModels.** Implement `WireViewModels(layout, shell)` in `MainWindow.axaml.cs`. Set each tool's/document's `Context` to the corresponding `ShellViewModel` property. Still inside the spike tab. | Spike tab now shows the real Explorer tree, real Git panel, real Problems list, real Output buffer, and a real Editor surface. File-tree expansion works; clicking a file opens it in the editor inside the spike tab. | `git tag v2-m2-wired` |
| **M3** | **Promote DockControl to its own window region.** Add a `LayoutMode` setting (default `Grid`, options `Grid`/`Freeform`). When `Freeform`, hide the existing `Grid` and show the dock layout filling the editor region; when `Grid`, the dock layout is `IsVisible=false`. Mode switchable from the View menu. Both modes use the same `ShellViewModel`. | App starts in Grid mode (current behavior, unchanged). View → "Layout: Freeform" switches to dock layout, full screen, all five panels visible at correct proportions. Switch back to Grid — original layout returns identically. | `git tag v2-m3-mode-switch` |
| **M4** | **Toggle-command parity.** Make `ToggleSidebarCommand`, `ToggleSidebarTabCommand`, `ToggleBottomPanelCommand`, `ToggleProblemsCommand`, `ToggleOutputCommand` operate on the dock model when in Freeform mode, on the existing booleans when in Grid mode. Define the dock semantics explicitly (see "Hide/show semantics" below). All keybindings unchanged. | In Freeform mode: View menu items hide/show the right docks; `Ctrl+OemTilde` toggles Output; `Ctrl+W`, `Ctrl+S`, `Ctrl+Tab` etc. unchanged. All 545 existing tests pass (they exercise the booleans, which are still the source of truth). | `git tag v2-m4-toggles` |
| **M5** | **Layout persistence.** Add `LayoutPersistenceService` using `Dock.Serializer.SystemTextJson`. Save dock layout to `~/.aero/layout.json` on `OnClosing`. Load on startup *only if* `LayoutMode == Freeform`. Schema version field included; corrupt or version-mismatched files are deleted and the default layout is loaded with a status message. | Launch → switch to Freeform → rearrange → close → relaunch → arrangement preserved. Corrupt the JSON manually → relaunch → status message "Layout reset", default layout loaded. Switching `LayoutMode = Grid` does not load the file. | `git tag v2-m5-persist` |
| **M6** | **Default flip and cleanup.** Change default `LayoutMode` to `Freeform`. Update `docs/roadmap/PHASES.md` checklist for 8.1a. Update phase-8 `TOFIX.md` with any deliberate reductions (e.g. tab tear-out left for 8.1c). Add `manual_test_phase8_1a.sh` covering the exit checklist. | `dotnet build` 0 errors. `dotnet test tests` passes. Manual smoke covers all five panels, all toggles, persistence round-trip, mode-switch round-trip. PHASES.md updated. | `git tag v2-m6-default-freeform` |

The `LayoutMode` switch in M3 is the load-bearing piece of this plan: it converts what was a single risky cutover in v1 into a feature flag we can toggle at runtime. M3 → M4 → M5 → M6 each ship with Grid still working as a known-good fallback. The only commit that removes the safety net is M6, and by then the dock path has been exercised through the entire app for several commits.

---

## Hide/show semantics (M4)

`Avalonia.Control.IsVisible = false` on a `Dock` *control* is **not** the right primitive for hiding a tool. Dock.Avalonia treats hidden tools as a layout concern, not a render concern. The mapping I'm committing to:

| Action | Grid mode (today) | Freeform mode (M4) |
|---|---|---|
| Hide sidebar | `IsSidebarVisible = false` | Remove the left `IToolDock` from its parent's `VisibleDockables`; remember its insertion index |
| Show sidebar | `IsSidebarVisible = true` | Re-insert at the remembered index, restore `Proportion` |
| Switch sidebar tab | `ActiveSidebarTabIndex = 1` | `leftToolDock.ActiveDockable = leftToolDock.VisibleDockables[1]` |
| Toggle Output | `IsBottomPanelVisible = true; ActiveBottomTabIndex = 1` | Ensure bottom `IToolDock` is in `VisibleDockables`; set `ActiveDockable = outputTool` |

The four `Reactive` properties on `ShellViewModel` (`IsSidebarVisible`, `IsBottomPanelVisible`, `ActiveSidebarTabIndex`, `ActiveBottomTabIndex`) **stay**. They are the source of truth and are watched by the existing tests. In Freeform mode, the toggle command implementations read/write those properties first, then push the state onto the dock model. This way the persistence story (`SaveWorkspaceStateAsync` already serializes them) keeps working unchanged.

---

## Initialization Sequence (Pseudo-Code)

The v1 initialization bug was calling `InitializeDockControl()` from the constructor, before `DataContext` was set, so `WireViewModels` saw a null shell. Fix: every dock setup runs from `Initialize()`, never from the constructor.

### App.axaml — additions

```xml
<Application.Styles>
    <StyleInclude Source="avares://Avalonia.Themes.Simple/SimpleTheme.xaml" />
    <StyleInclude Source="avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml" />
    <!-- Phase 8.1a M0.5 — REQUIRED for DockControl to render with non-zero size. -->
    <StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/DockSimpleTheme.axaml" />
    <StyleInclude Source="avares://aero/Styles/ControlThemes.axaml" />
</Application.Styles>
```

> **Note on the theme include URI.** The exact resource path under `Dock.Avalonia.Themes.Simple` must be confirmed during M0.5 by inspecting the package contents (e.g. `unzip -l ~/.nuget/packages/dock.avalonia.themes.simple/<ver>/lib/...`). Do not commit a guessed path. The v1 commit log mentions adding the package but doesn't quote the StyleInclude URI; that's a verification gate before M0.5 closes.

### App.axaml — DataTemplates (added in M1)

```xml
<Application.DataTemplates>
    <DataTemplate DataType="dockTools:ExplorerTool"><views:FileExplorerView/></DataTemplate>
    <DataTemplate DataType="dockTools:GitTool"><views:GitPanelView/></DataTemplate>
    <DataTemplate DataType="dockTools:ProblemsTool"><views:ProblemsView/></DataTemplate>
    <DataTemplate DataType="dockTools:OutputTool"><views:OutputView/></DataTemplate>
    <DataTemplate DataType="dockDocs:EditorDocument"><views:EditorView/></DataTemplate>
</Application.DataTemplates>
```

### App.axaml.cs — `OnFrameworkInitializationCompleted` (unchanged structure)

```csharp
var shell = _services.GetRequiredService<ShellViewModel>();
var bus   = _services.GetRequiredService<IMessageBus>();

_services.GetRequiredService<LSPManager>();
_services.GetRequiredService<GitViewModel>();

// 1. Construct + DataContext set BEFORE Initialize.
var mainWindow = new MainWindow { DataContext = shell };

// 2. Initialize: bus subscriptions THEN dock control.
//    Signature unchanged — Initialize reads shell from DataContext.
mainWindow.Initialize(bus);

desktop.MainWindow = mainWindow;
```

I'm deliberately **not** changing the `Initialize(IMessageBus)` signature to take `ShellViewModel` (as Claude_Recommendation suggests). The window already has access to `DataContext` at this point; passing the shell as a parameter is redundant and forks the existing call site for no benefit. Inside `Initialize` we read `(ShellViewModel)DataContext` and assert non-null.

### MainWindow.axaml.cs — extended `Initialize` (M1+)

```csharp
public void Initialize(IMessageBus bus)
{
    if (bus == null) throw new ArgumentNullException(nameof(bus));
    if (DataContext is not ShellViewModel shell)
        throw new InvalidOperationException(
            "MainWindow.Initialize called before DataContext was set.");

    _bus = bus;

    // Existing bus subscriptions (unchanged) ...
    _bus.Subscribe(_confirmDirtyCloseHandler = OnConfirmDirtyClose);
    _bus.Subscribe(_promptNewItemHandler   = OnPromptNewItem);
    _bus.Subscribe(_promptRenameHandler    = OnPromptRename);
    _bus.Subscribe(_confirmDeleteHandler   = OnConfirmDelete);

    // NEW from M1 onward — only after DataContext + bus are wired.
    InitializeDockControl(shell);
}

private void InitializeDockControl(ShellViewModel shell)
{
    Log("[Dock] init: begin");

    // a. Factory must exist before assigning Layout.
    var factory = new AeroDockFactory();
    DockControl.Factory = factory;
    Log("[Dock] init: factory assigned");

    // b. Build layout tree with explicit Proportion on every ProportionalDock child.
    var layout = factory.CreateDefaultLayout();
    Log($"[Dock] init: layout built, {CountDockables(layout)} dockables");

    // c. Inject Context BEFORE assigning Layout, so the first render has the data.
    WireViewModels(layout, shell);
    Log("[Dock] init: ViewModels wired");

    // d. Assign Layout LAST — this is what triggers DataTemplate materialization.
    DockControl.Layout = layout;
    Log("[Dock] init: layout assigned, render incoming");
}
```

### Proportion constants (M1, used by `AeroDockFactory`)

```csharp
internal static class DockProportions
{
    public const double LeftSidebar  = 0.22;  // Explorer + Git
    public const double CenterStack  = 0.78;  // Editor + bottom panel column
    public const double EditorRow    = 0.72;  // Inside center, editor takes the bulk
    public const double BottomRow    = 0.28;  // Problems + Output
}
```

Every `IProportionalDock` child gets one of these. No `Proportion` left at default — defaults are how the v1 layout collapsed into "Explorer fills the whole window."

---

## Logging Strategy

Logging is added in M1 (the first commit with C#) and stays through the phase. Channel: `System.Diagnostics.Debug.WriteLine` with a `[Dock]` prefix, mirrored into the existing Output panel via `IMessageBus.Publish(new StatusMessage(...))` for any log at `Info` level or higher. No new logging dependency.

| Site | Level | Message |
|---|---|---|
| `AeroDockFactory.CreateDefaultLayout` | Debug | Tree dump (depth, type, id, proportion) |
| `AeroDockFactory.GetDockable` | Debug | Each `ITool`/`IDocument` constructed (type, id) |
| `MainWindow.InitializeDockControl` | Info | The four "init: ..." lines above |
| `MainWindow.WireViewModels` | Debug | One line per `Context` injection |
| `DockControl.Layout` setter (post-assign) | Info | `dockable count = N, root.IsActive = …` |
| `MainWindow.OnClosing` | Debug | Layout JSON length + path on save |
| `LayoutPersistenceService.Load` | Info | "loaded N bytes" or "default layout (reason)" |

If a milestone fails verification, the first action is to read these logs end-to-end, not to start guessing.

---

## Layout Tree (M1, Freeform default)

```
AeroRootDock
└── AeroProportionalDock (Horizontal)
    ├── AeroProportionalDock (Vertical, Proportion=0.22)
    │   └── AeroToolDock (Alignment=Left)
    │       ├── ExplorerTool (Context = FileExplorerViewModel)
    │       └── GitTool       (Context = GitViewModel)
    ├── AeroProportionalDockSplitter
    └── AeroProportionalDock (Vertical, Proportion=0.78)
        ├── AeroDocumentDock (Proportion=0.72)
        │   └── EditorDocument (Context = EditorViewModel)
        ├── AeroProportionalDockSplitter
        └── AeroToolDock (Alignment=Bottom, Proportion=0.28)
            ├── ProblemsTool (Context = ProblemsViewModel)
            └── OutputTool   (Context = OutputViewModel)
```

---

## Key Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Theme `StyleInclude` URI is wrong (v1 may have used a non-existent path) | Medium | Critical | M0.5 verifies the URI by inspecting the installed `.nupkg` contents before the include is committed. Failed include surfaces as the spike tab being empty — caught by visual check, not later. |
| Dock.Avalonia 11.3 has an undocumented behavior change vs. examples online | Medium | High | M0.5 is purely XAML against this exact installed version; no online example is trusted until it renders here. |
| `IRootDock`/`IFactory` concrete-type API has shifted between Dock 11.3 minor versions | Low | High | M1 starts by extending `Factory` (the documented base class) and only adds members that compile against the restored `Dock.Model.Core` assembly. If a member is missing, the build fails immediately and we adjust before proceeding. |
| The Grid replacement breaks one of the existing keybindings | Medium | Medium | M3's mode switch keeps Grid intact; M4 wires toggles per mode and the test suite (which targets the booleans) catches regressions. M6 only flips the default after manual smoke. |
| Layout JSON corrupts on shutdown and bricks startup | Medium | High | M5 wraps load in try/catch + version check, deletes the file on any failure, logs a status message, and falls back to default. Save runs only on `OnClosing` (no per-edit autosave) so corruption windows are tiny. |
| `Context` injection happens too late for the initial render | Low | Critical | Initialization order in `InitializeDockControl` puts `WireViewModels` strictly before `DockControl.Layout = layout`. The setter is the trigger for materialization, so by the time templates run, contexts are present. |
| Toggle commands diverge between Grid and Freeform modes (drift) | Medium | Medium | The `Reactive` booleans on `ShellViewModel` remain the canonical state. Mode-specific code only translates booleans → dock model in Freeform. Tests against the booleans cover both modes. |
| Scope creep: someone adds tear-out / Tile mode in this phase | Medium | High | Explicit "out of scope" list mirrors `IMPLEMENTATION_PLAN_8.1a.md` §2: 8.1b/8.1c deferred. Any reduction recorded in `TOFIX.md` per `plan-rules.md` §7. |

---

## What I am explicitly NOT doing

These are the temptations I'm rejecting up front, with reasons, so a later review doesn't quietly add them back:

- **Not changing `MainWindow.Initialize`'s signature.** The shell is on `DataContext`; passing it again is redundant and forks the call site.
- **Not building a `LayoutPersistenceService` until M5.** The dock model has to render and toggle correctly first; persisting a broken layout would just hide bugs.
- **Not ripping out `IsSidebarVisible` / `IsBottomPanelVisible` / `ActiveSidebarTabIndex` / `ActiveBottomTabIndex`.** They're depended on by the test suite, by `WorkspaceState`, and by the Grid fallback. Removing them in this phase is gold-plating.
- **Not introducing an `IDockingService` abstraction.** Per `AGENTS.md` §4, abstractions are introduced when there's a concrete second implementation. Tile mode (8.1b) is a future phase; the abstraction should ship there, not here.
- **Not chasing Option B (`{Binding Context}`) for purity.** Option A was verified working in v1; Option B is unverified.
- **Not adding a new logging dependency.** `Debug.WriteLine` + `StatusMessage` cover the need without expanding `LIBRARIES.md`.

---

## Definition of Done (gate to leave 8.1a)

All must be true before declaring 8.1a complete and starting 8.1b:

- `dotnet build src/aero.csproj` — 0 errors, 0 warnings introduced by this phase
- `dotnet test tests` — all existing tests pass (≥545)
- `manual_test_phase8_1a.sh` — passes the smoke checklist (panels render, toggles work, persist round-trip)
- `LayoutMode` defaults to `Freeform`; switching to `Grid` and back works without restart
- All five panels render real content in Freeform mode
- All keyboard shortcuts from `MainWindow.axaml` `Window.KeyBindings` still fire their commands
- `docs/phases/phase-8/TOFIX.md` has no unchecked items blocking 8.1a
- `docs/roadmap/PHASES.md` 8.1a checklist marked complete
- Any deliberate scope reductions recorded in `TOFIX.md` with `plan-rules.md` §7 wording
