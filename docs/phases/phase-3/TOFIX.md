# Phase 3 — To Fix

> **Status:** Active — no review findings yet.
> Resolve all open items before declaring Phase 3 complete.
>
> This file is the persistent code-quality checklist for Phase 3 (Syntax
> Highlighting). Add findings here during/after every review round; mark each
> `[x]` when fixed and note the fix inline. Do not start Phase 4 while any item
> is unchecked. See `.kiro/steering/tofix-rules.md`.

---

## Round 1 — M2 Review (2026-06-19)

### R1.1 Two divergent language-detection sources of truth *(priority: high, BLOCKER for M3)*

**Description:** After M2 there are two independent extension→language maps that
disagree, and which one determines `TextDocument.Language` depends on the code
path:

- `DocumentManager.DetectLanguage(filePath)` (pre-existing, static, display-name
  only) covers `.fs`, `.xaml`, `.scss`, `.yaml`/`.yml`, `.sql`, `.rs`, `.go`,
  `.java`, `.cpp`/`.cc`/`.cxx`, `.c`/`.h`, `.sh`/`.bash`, `.ps1`, etc.
- `LanguageDetectionService.Detect(filePath)` (new, id + display name) covers
  `.axaml`, `.csproj`, `.markdown`, `.html`, `.css`, `.txt`, but **not** `.xaml`,
  `.fs`, `.yaml`, and the other DocumentManager-only extensions.

Resulting inconsistencies:
- **Open via tree/`OpenFileAsync`:** `DocumentManager` sets `Language`, then
  `EditorViewModel.EnsureTabForDocument` overwrites it with the service value.
  A `.xaml` file is detected as "XAML" then silently overwritten to "Plain Text"
  (service has no `.xaml`). Regression.
- **Save As (untitled → `foo.xaml`):** only `DocumentManager.DetectLanguage`
  runs, so `Language` = "XAML", but the tab's `LanguageId` was fixed to
  "plaintext" at tab creation and is never refreshed. Status label and grammar id
  disagree.

Because M3 picks the TextMate grammar from `EditorTabViewModel.LanguageId`
(service-only) while the status label may come from either source, this produces
"highlighted but mislabeled" / "labeled but not highlighted" cases.

**Fix hint (proposed, needs decision — touches DocumentManager ctor = checkpoint):**
Make `ILanguageDetectionService` the single source of truth. Options:
1. Inject `ILanguageDetectionService` into `DocumentManager`; replace the static
   `DetectLanguage` with `_languageDetection.Detect(path).DisplayName`. Removes
   the divergent map; updates DI + test ctors. **Recommended.**
2. Remove `DocumentManager`'s language-setting entirely and let `EditorViewModel`
   own it (but then Save As / new-file paths need the VM to re-detect, and the
   tab `LanguageId` must update on Save As).
   Either way, fold the DocumentManager-only extensions (`.xaml`, `.fs`, `.yaml`,
   `.sql`, `.rs`, `.go`, `.java`, `.cpp`, `.c`, `.sh`, `.ps1`, `.scss`) into the
   service map (with TextMate ids) so coverage doesn't regress, and ensure
   `LanguageId` is refreshed when a document's path changes (Save As).

**Status:** ✅ RESOLVED (2026-06-19) — `ILanguageDetectionService` is now the single source of truth. `DocumentManager` receives it via constructor injection; the static `DetectLanguage` map was removed and its extensions folded into `LanguageDetectionService` with verified TextMate ids. `EditorViewModel` keeps `EditorTabViewModel.LanguageId` in sync on tab creation and refreshes it on `DocumentSaved` (Save As).

### R1.2 Malformed/unused import in `EditorTabViewModel.cs` *(priority: trivial, cleanup)*

**Description:** First line was `using Aero.Languages;using System;` — two
usings on one line, and `Aero.Languages` was unused (`LanguageId` is a plain
`string`). Same hygiene class as Phase 1 R4.4 / Phase 2 R2.7.
**Fix:** Split the usings and removed the unused `using Aero.Languages;`.
**Status:** ✅ RESOLVED (2026-06-19)

### R1.3 Status-bar language lagged behind grammar on Save As *(priority: low-medium, found during exit review)*

**Description:** On Save As (e.g. untitled → `foo.cs`), `OnDocumentSaved`
refreshed `tab.LanguageId` (so the TextMate grammar switched and the editor
highlighted as C#), but the status-bar `Language` property was only refreshed by
`UpdateStatus` on caret move / tab activation / tab creation. Result: immediately
after Save As the editor highlighted correctly while the status bar still showed
"Plain Text" until the next interaction — a narrow label/grammar mismatch and a
miss against the Phase 3 exit condition "status bar shows current language."
**Fix:** `OnDocumentSaved` now also calls `UpdateStatus(msg.Document)` when the
saved tab is the active tab. Extended `SaveAs_UpdatesTabLanguageId` to assert
`vm.Language == "C#"` after Save As.
**Status:** ✅ RESOLVED (2026-06-19)

---

## Persistent Checks

Use these as a self-review checklist before closing Phase 3:

- [x] No new NuGet packages were added without updating `docs/LIBRARIES.md`
      (`AvaloniaEdit.TextMate` and `TextMateSharp.Grammars` are already referenced —
      no others added).
- [x] All new services are registered in `src/App.axaml.cs` and documented in
      `docs/architecture/CORE_INFRASTRUCTURE.md`.
- [x] All new public service methods are covered by unit tests.
- [x] Language detection falls back to plain text for unknown extensions (no throw).
- [x] TextMate installation is disposed/cleaned up with the editor control (no leak
      across tab open/close).
- [x] No `async void` outside Avalonia event handlers.
- [x] No `!` null-forgiving operator without a comment explaining safety.
- [x] Phase 1 + Phase 2 regression tests still pass (`dotnet test tests`).
- [x] Manual smoke test for syntax highlighting passes.
- [x] `docs/phases/phase-3/TOFIX.md` has no open items.
