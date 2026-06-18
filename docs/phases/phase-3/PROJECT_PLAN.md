# Phase 3: Syntax Highlighting — Implementation Plan

> **Goal:** Make code look like code. Auto-detect a file's language from its
> extension, apply TextMate-based syntax highlighting in the editor, and show
> the current language in the status bar — while keeping the Phase 1 editor and
> Phase 2 explorer rock-solid.

---

## 1. Entry Gate: Confirm Phase 2 Is Solid Before Crossing the Boundary

Do not write Phase 3 code until all of these are true:

| Gate | Evidence |
|------|----------|
| `docs/roadmap/PHASES.md` Phase 2 checklist all `[x]` | ✅ |
| `docs/phases/phase-2/TOFIX.md` all closed (Rounds 1–8) | ✅ |
| `dotnet build src/aero.csproj` succeeds with 0 errors | ✅ verified at M0 |
| `dotnet test tests` passes (227/227 as of Phase 2 M5) | ✅ verified at M0 |
| `./manual_test_phase2.sh` smoke test completes | ✅ verified at M0 |
| `docs/issues/INDEX.md` has no open blockers | ✅ verified at M0 |

**First Phase 3 file created:** `docs/phases/phase-3/TOFIX.md` (done) — so every
review finding has a home before code lands.

---

## 2. Scope

### In Scope

- `ILanguageDetectionService` / `LanguageDetectionService`: extension → language
  id + human-readable display name, with a plain-text fallback.
- TextMate grammar integration via the already-referenced `AvaloniaEdit.TextMate`
  + `TextMateSharp.Grammars` packages.
- Apply highlighting to each editor `TextEditor`, re-applying when the active tab
  changes.
- Populate `TextDocument.Language` on open so the existing status-bar binding
  (`EditorViewModel.Language`) shows the detected language.
- Minimum supported languages verified end-to-end: **C#, JSON, XML, Markdown**
  (TextMateSharp bundles 100+ more for free).

### Out of Scope (protects solid state)

- Light/dark theme switching for the highlighter (Phase 8 — theme system). Phase 3
  ships one fixed TextMate theme.
- LSP semantic tokens / server-driven coloring (Phase 4+).
- Per-language editor behavior (indentation rules, comment toggling, folding rules).
- User-configurable language associations / manual "set language" picker
  (revisit in Phase 8 settings).
- Loading user-supplied `.tmLanguage` files from `src/Languages/grammars/`
  (deferred — see ADR-2; the bundled registry covers Phase 3 needs).

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        MainWindow                            │
│  ┌──────────────┬──────┬─────────────────────────────────┐  │
│  │ FileExplorer │Split-│ EditorView (tabs)               │  │
│  │              │ter   │   each tab → AvaloniaEdit        │  │
│  │              │      │   + TextMate installation       │  │
│  └──────────────┴──────┴─────────────────────────────────┘  │
│  └──────── StatusBar: "Ln X, Col Y"   │   Language ────────┘  │
└─────────────────────────────────────────────────────────────┘

Services (new)                 ViewModels (touched)      Views (touched)
─────────────────────────────────────────────────────────────────────────
ILanguageDetectionService      EditorViewModel           EditorView.axaml(.cs)
LanguageDetectionService       (set Language on open)    (TextMate install)
```

`DocumentManager`, `TextDocument`, `EditorTabViewModel`, and the MessageBus
records are **not** rewritten. The only data addition is *populating* the
existing `TextDocument.Language` property. TextMate touches the `TextEditor`
control and therefore lives in the **View layer** (code-behind / attached
behavior), driven by the VM's detected-language string — keeping MVVM clean.

---

## 4. Key Design Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| ADR-1 | Use TextMateSharp's bundled `RegistryOptions` instead of hand-loading `.tmLanguage` JSON | 100+ grammars ship in the package; `GetScopeByLanguageId` / `GetScopeByExtension` give detection + grammar lookup for free. Satisfies the library-first rule; less custom code. The README's "load .tmLanguage JSON" item is realised as "load grammars via TextMateSharp." |
| ADR-2 | Keep `LanguageDetectionService` UI-free; do its own extension→id map rather than depending on TextMate types | Service stays testable without Avalonia/TextMate; the View layer translates the language id into a TextMate scope. Avoids leaking a UI library into a service. |
| ADR-3 | Install TextMate per `TextEditor` in the View layer (attached behavior / code-behind), not in the VM | `InstallTextMate(...)` operates on the control. ViewModels must never reference Views (CONVENTIONS.md MVVM rule). |
| ADR-4 | One fixed TextMate theme for Phase 3 (e.g. `DarkPlus`) | Theme switching belongs to Phase 8. Hard-coding one theme keeps Phase 3 focused. |
| ADR-5 | No new NuGet packages | `AvaloniaEdit.TextMate` + `TextMateSharp.Grammars` are already in `aero.csproj`. |
| ADR-6 | Fallback to no highlighting + "Plain Text" for unknown extensions | Never throw on an unrecognised file; degrade gracefully. |

---

## 5. Component Design

### 5.1 Service

```csharp
// src/Languages/ILanguageDetectionService.cs
namespace Aero.Languages;

public interface ILanguageDetectionService
{
    /// <summary>Detects the language for a file path (by extension).</summary>
    /// <returns>A LanguageInfo; never null — falls back to plain text.</returns>
    LanguageInfo Detect(string? filePath);
}
```

```csharp
// src/Languages/LanguageInfo.cs
namespace Aero.Languages;

/// <param name="Id">TextMate language id, e.g. "csharp", "json", "xml", "markdown".</param>
/// <param name="DisplayName">Status-bar label, e.g. "C#", "JSON", "XML", "Markdown".</param>
public record LanguageInfo(string Id, string DisplayName)
{
    public static readonly LanguageInfo PlainText = new("plaintext", "Plain Text");
}
```

**`LanguageDetectionService` implementation notes:**
- Extension → `LanguageInfo` map (case-insensitive). Minimum set:
  `.cs`→C#, `.json`→JSON, `.xml`/`.axaml`/`.csproj`→XML, `.md`/`.markdown`→Markdown,
  plus a handful of common ones (`.js`, `.ts`, `.py`, `.html`, `.css`, `.txt`).
- Unknown / null / extension-less → `LanguageInfo.PlainText`.
- The TextMate id strings must match `TextMateSharp.Grammars` language ids so the
  View can resolve a scope. Verified at M3.

### 5.2 ViewModel touch points

- `EditorViewModel` (or `DocumentManager` open path): when a document is opened,
  call `ILanguageDetectionService.Detect(doc.FilePath)` and set
  `doc.Language = info.DisplayName`. `UpdateStatus` already pushes
  `doc.Language` into the `Language` reactive property bound to the status bar.
- Expose the detected language **id** to the View so it can pick the grammar.
  Options (decide at M3): add an `EditorTabViewModel.LanguageId` string property,
  or have the View re-detect from `Document.FilePath` via the injected service.
  Preference: a `LanguageId` property on the tab VM (single source of truth, no
  duplicate detection).

### 5.3 View layer (TextMate wiring)

- `EditorView.axaml.cs` already locates the active `TextEditor` by document
  reference in `ResubscribeEditor`. Extend that path to install/refresh TextMate:
  1. On first bind to a `TextEditor`, call `editor.InstallTextMate(registryOptions)`
     and keep the `TextMate.Installation` handle.
  2. Set the grammar from the active tab's `LanguageId`
     (`registryOptions.GetScopeByLanguageId(id)` → `installation.SetGrammar(scope)`).
  3. For unknown languages, set no grammar (plain text).
- A single shared `RegistryOptions` (one theme) can be created once for the view.
- **Lifecycle:** ensure the TextMate installation is disposed when the editor goes
  away so we don't leak across tab open/close (TOFIX persistent check).
- **Alternative considered:** a reusable `TextMateBehavior` attached property so
  every `TextEditor` realized by the `TabControl.ContentTemplate` self-installs.
  Cleaner for multiple simultaneously-visible editors, but heavier; decide at M3
  based on how the single-active-editor model behaves.

### 5.4 Status bar

No structural change — `EditorViewModel.Language` is already bound. Phase 3 only
ensures it carries the detected display name instead of always "Plain Text".

---

## 6. File & Folder Layout

| Path | Action | Purpose |
|------|--------|---------|
| `src/Languages/ILanguageDetectionService.cs` | new | Detection abstraction |
| `src/Languages/LanguageDetectionService.cs` | new | Extension→language map |
| `src/Languages/LanguageInfo.cs` | new | Language id + display-name record |
| `src/ViewModels/EditorTabViewModel.cs` | modify | Add `LanguageId` (source of truth for grammar) |
| `src/ViewModels/EditorViewModel.cs` | modify | Set `doc.Language` on open via detector |
| `src/Views/EditorView.axaml.cs` | modify | Install/refresh TextMate on the active editor |
| `src/App.axaml.cs` | modify | Register `ILanguageDetectionService` (singleton) |
| `tests/Languages/LanguageDetectionServiceTests.cs` | new | Extension→language, fallback, case-insensitivity |
| `docs/phases/phase-3/TOFIX.md` | created | Phase 3 quality checklist |
| `docs/phases/phase-3/PROJECT_PLAN.md` | this file | Committed plan |
| `docs/architecture/CORE_INFRASTRUCTURE.md` | modify | Document the new DI registration |
| `docs/roadmap/PHASES.md` | modify | Mark Phase 3 items `[x]` as milestones land |
| `manual_test_phase3.sh` | new | Headless smoke for highlighting + language label |

---

## 7. Milestone Plan (Solid-State Sprints)

Each milestone ends with **`dotnet build src/aero.csproj` + `dotnet test tests` +
a short `dotnet run` smoke**. If a gate fails, fix before continuing.

### M0 — Entry Gate
- Verify Phase 2 gates from §1 (build, tests, manual smoke, no open blockers).
- Confirm `AvaloniaEdit.TextMate` + `TextMateSharp.Grammars` restore cleanly.

### M1 — Language Detection Service (UI-free)
- Implement `LanguageInfo`, `ILanguageDetectionService`, `LanguageDetectionService`.
- Register in `App.axaml.cs`; document in `CORE_INFRASTRUCTURE.md`.
- Unit tests: known extensions, unknown → plain text, case-insensitivity, null path.
- **Gate:** `LanguageDetectionServiceTests` pass; build + full suite green.

### M2 — Surface Language in the Status Bar
- Set `doc.Language` (display name) on open; add `LanguageId` to the tab VM.
- Verify the status bar shows the right language on open and on tab switch.
- **Gate:** Opening `.cs`/`.json`/`.xml`/`.md` shows correct labels; unknown shows
  "Plain Text". No Phase 1/2 regression.

### M3 — Wire TextMate to AvaloniaEdit
- Create a shared `RegistryOptions` (fixed theme) in the View.
- Install TextMate on the active editor and set the grammar from `LanguageId`;
  refresh on active-tab change; plain text for unknown.
- Confirm grammar ids line up with `TextMateSharp.Grammars`.
- Ensure the installation is cleaned up to avoid leaks.
- **Gate:** Opening C#, JSON, XML, Markdown shows correct coloring.

### M4 — Exit Gate
- Full regression: Phase 1 + Phase 2 automated + manual tests still pass.
- Create and run `manual_test_phase3.sh`.
- Record any review findings in `TOFIX.md` and close them.
- Update `docs/roadmap/PHASES.md` and the phase-3 `README.md` status.
- **Gate:** `dotnet test` passes, app runs, Phase 3 checklist complete, TOFIX empty.

---

## 8. Testing Strategy

### 8.1 Unit Tests
- `LanguageDetectionServiceTests`: each supported extension maps to the expected
  `LanguageInfo`; unknown/empty/null → `PlainText`; case-insensitive (`.CS` == `.cs`);
  compound names (`Foo.csproj`) resolve correctly.

### 8.2 Regression Tests
- Run the full `dotnet test tests` suite after every milestone. The existing
  Phase 1 + Phase 2 tests (227 as of Phase 2 M5) must keep passing unchanged.
- Keep `DocumentManager` / `TextDocument` public surfaces stable.

### 8.3 Manual Smoke Test
Create `manual_test_phase3.sh` (style matches `manual_test_phase2*.sh`) that:
1. Launches Aero under Xvfb (reusing the CLI startup-folder hook from Phase 2 M3).
2. Opens sample `.cs`, `.json`, `.xml`, `.md` files.
3. Captures screenshots and confirms colored tokens render and the language label
   updates per tab.

---

## 9. Solid-State Safeguards

| Safeguard | How it is enforced |
|-----------|-------------------|
| **Additive changes only** | Only *populate* `TextDocument.Language`; no rewrite of Phase 1/2 services or messages. |
| **MVVM boundary** | Detection is a UI-free service; TextMate (a control concern) stays in the View layer. |
| **Graceful fallback** | Unknown extensions → plain text, never throw. |
| **No leaks** | TextMate installation lifecycle tied to the editor; disposed on teardown. |
| **No new dependencies** | Uses already-referenced TextMate packages. |
| **No async void** | No new async event paths beyond existing Avalonia handlers. |
| **Theme deferral** | Single fixed theme; theme switching left to Phase 8 to avoid scope creep. |

---

## 10. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| TextMate grammar ids don't match our detection ids | Medium | Wrong/no highlighting | Verify ids against `TextMateSharp.Grammars` at M3; centralize the mapping. |
| `InstallTextMate` interacts badly with tab-switch re-subscribe logic | Medium | Highlighting drops on switch | Reuse the existing generation-counter / document-match pattern in `EditorView.axaml.cs`. |
| TextMate installation leaks across many opened tabs | Low-Medium | Memory growth | Dispose installation on editor teardown; cover in TOFIX persistent checks. |
| Fixed theme clashes with the app's Simple theme background | Low | Poor contrast | Pick a theme that reads well on the current background; full theming is Phase 8. |
| Large files slow down TextMate tokenization | Low | UI jank on huge files | Accept for Phase 3; revisit with virtualization/limits if it surfaces. |

---

## 11. Documentation & Commit Plan

### Docs to Update
- `docs/phases/phase-3/TOFIX.md` — keep current; add findings per review round.
- `docs/phases/phase-3/PROJECT_PLAN.md` — this file (committed plan).
- `docs/roadmap/PHASES.md` — mark Phase 3 items `[x]` as milestones land.
- `docs/architecture/CORE_INFRASTRUCTURE.md` — document the new DI registration.
- `docs/LIBRARIES.md` — no changes (packages already catalogued).

### Suggested Commit Sequence
```
languages: add ILanguageDetectionService and extension→language map
editor: set document language on open and show it in the status bar
editor: install TextMate highlighting on the active editor
docs: update Phase 3 roadmap, architecture docs, and TOFIX
```

---

## 12. Exit Criteria

Phase 3 is complete when **all** of the following are true:

- [ ] `docs/roadmap/PHASES.md` Phase 3 checklist is fully `[x]`.
- [ ] `dotnet build src/aero.csproj` succeeds with 0 errors.
- [ ] `dotnet test tests` passes (Phase 1 + Phase 2 + new Phase 3 tests).
- [ ] `dotnet run --project src` launches; opening C#/JSON/XML/Markdown highlights.
- [ ] The status bar shows the correct language per active tab.
- [ ] `manual_test_phase3.sh` completes successfully.
- [ ] `docs/phases/phase-3/TOFIX.md` has no open items.
- [ ] No regressions in Phase 1/2 features.

---

## 13. One Recommended Path Forward

A single, conservative approach: build a small UI-free detection service first
(M1), surface the language in the existing status bar (M2), then wire TextMate
into the View layer reusing the editor's existing re-subscribe machinery (M3),
verifying every milestone. This keeps the editor and explorer in a solid,
shippable state throughout Phase 3.

If approved, the first concrete actions are:
1. Verify the M0 entry gate (build + tests + manual smoke).
2. Implement M1 (`LanguageDetectionService` + tests).
3. Checkpoint before touching `EditorView.axaml.cs` for TextMate in M3.
