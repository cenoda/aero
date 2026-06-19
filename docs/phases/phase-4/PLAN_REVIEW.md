# Phase 4 Implementation Plan Review

> Reviewed against Phases 0–3 (foundation, editor, explorer, syntax highlighting).  
> Date: 2026-06-19  
> Reviewer: Kimi Code CLI

---

## 1. Executive Summary

| Criterion | Verdict |
|-----------|---------|
| Phase 3 entry gate | ✅ Pass — `PHASES.md` Phase 3 items are all `[x]`, `docs/phases/phase-3/TOFIX.md` is empty, build passes, and `dotnet test tests` reports 254/254 passing. |
| Plan completeness | ✅ Strong — scope, milestones, risks, file plan, and testing strategy are all present. |
| Architecture alignment | ✅ Good — the plan respects MVVM, DI, MessageBus, and service boundaries established in Phases 0–2. |
| Implementation readiness | ⚠️ Ready with minor clarifications — a handful of consistency gaps with older design docs and one under-specified edge case should be captured before coding starts. |

**Overall recommendation:** Approve the plan for implementation after recording the findings in this review as `TOFIX.md` items (see §6).

---

## 2. Entry Condition Verification (M0)

The plan requires Phase 3 to be complete. Verified:

- `docs/roadmap/PHASES.md` Phase 3 checklist: all `[x]`.
- `docs/phases/phase-3/TOFIX.md`: no open items.
- `dotnet build src/aero.csproj`: succeeds, 0 warnings, 0 errors.
- `dotnet test tests`: 254 passed, 0 failed.
- `src/aero.csproj` does **not** yet reference `StreamJsonRpc` — consistent with the plan's "open gap" statement.

Phase 4 may proceed.

---

## 3. Alignment with Pre-Existing Phases

### 3.1 Phase 0 — Foundation (DI, MVVM, MessageBus)

- ✅ Plan registers new services in `App.axaml.cs` and follows singleton lifetimes.
- ✅ `LSPManager` implements `IDisposable` and hooks the existing `App.OnDesktopExit` disposal path.
- ✅ Plan correctly requires eager resolution of `LSPManager` (like `ShellViewModel`) so it is constructed and subscribed before `FolderOpened` fires.
- ✅ New MessageBus records (`DocumentTextChanged`, `DiagnosticsUpdated`) follow the existing record-based pattern in `src/Core/Messages.cs`.

### 3.2 Phase 1 — Editor

- ✅ Plan reuses `TextDocument`, `DocumentManager`, `EditorViewModel`, and `EditorTabViewModel` rather than replacing them.
- ✅ Adds URI/version metadata to `TextDocument` as additive changes only.
- ✅ Completion UI reuses the established `EditorViewModel` → `EditorView.axaml.cs` event-bridge pattern (already used by `FindReplaceRequested`).
- ⚠️ The plan assumes AvaloniaEdit provides a usable `CompletionWindow`. The current codebase only uses `TextEditor` for display/editing; AvaloniaEdit 11.3's completion API should be verified in M5 before committing to that seam.

### 3.3 Phase 2 — File Explorer & Project System

- ✅ `LSPManager` subscribes to the existing `FolderOpened` message for `rootUri` instead of referencing `FileExplorerViewModel`, preserving the service→ViewModel boundary.
- ✅ Session scope is "one C# session per opened folder," which avoids premature project/solution inference before Phase 6.
- ⚠️ Multi-folder behavior is not explicitly defined. If a user opens folder A and later folder B, does the old session close or do multiple concurrent roots exist? This should be locked down (recommend: single active root; opening a new folder closes the previous session in Phase 4).

### 3.4 Phase 3 — Syntax Highlighting

- ✅ Plan correctly leverages `ILanguageDetectionService` as the language identity source of truth.
- ⚠️ The LSP `textDocument/didOpen` `languageId` field and the TextMate `LanguageInfo.Id` are assumed to be the same string (e.g., `"csharp"`). The plan should explicitly state that `LanguageInfo.Id` values are used directly as LSP `languageId`s, or add a mapping if they diverge. This affects `LanguageDetectionService` coverage from Phase 3.

---

## 4. Consistency with Design & Architecture Documents

### 4.1 `docs/design/LSP_DESIGN.md`

The design doc is more aspirational and covers the full LSP surface. Conflicts with the Phase 4 plan:

| Design Doc | Phase 4 Plan | Consistency |
|------------|--------------|-------------|
| Shows `textDocument/didChange` as "incremental or full" | Locks to **full-document sync** | ⚠️ Design doc should be annotated that Phase 4 overrides it to full sync. |
| Shows `_version++` inside `OnTextChanged()` | Increments version only when a change is actually sent | ⚠️ Design doc snippet is simplified; Phase 4 plan is more correct for debouncing. |
| Shows `record DocumentOpened(TextDocument Document)` | Actual code is `record DocumentOpened(string FilePath)` | ❌ Design doc is stale. Plan correctly notes `DocumentManager` lookup is needed. |
| Lists many language servers | Phase 4 targets only `csharp-ls` | ✅ Acceptable; design doc is forward-looking. |

**Recommendation:** Update `docs/design/LSP_DESIGN.md` to reflect the Phase 4 constraints (full sync, `DocumentOpened` string path, single C# server), or add a prominent note that Phase 4 supersedes the older design doc.

### 4.2 `docs/architecture/IDE_CORE.md`

- ✅ The bottom-panel host concept in the architecture tree aligns with the plan's reusable bottom-panel container.
- ✅ Language Services subsystem is the right home for `LSPManager`/`LSPSession`.

---

## 5. Risk Assessment

### 5.1 Already Well-Mitigated Risks

- **Thread-affine document access:** Plan correctly requires capturing `doc.Content` on the UI thread before scheduling the JSON-RPC send.
- **Off-thread diagnostics:** Plan requires `Dispatcher.UIThread` marshaling using the existing `ShellViewModel.GetUiDispatcher()` pattern.
- **LSP process lifetime:** Plan requires bounded process kill and DI disposal.
- **FolderOpened idempotency:** Plan tracks sessions by `rootUri` to avoid duplicate servers.

### 5.2 Risks That Need Tighter Specification

1. **AvaloniaEdit diagnostic marker seam.** The plan says "TextMarkerService-style component." Assembly metadata inspection of AvaloniaEdit 11.3.0 confirms `TextMarkerService` does not exist, and there are no squiggle-specific types. The supported seam is `IBackgroundRenderer` (registered on `TextEditor.TextView.BackgroundRenderers`), with `TextSegment`/`TextSegmentCollection<T>` for range tracking. The plan and implementation must target `IBackgroundRenderer`; the fallback is line-level background highlight.
2. **Completion window anchoring.** AvaloniaEdit's `CompletionWindow` API must be tested in M5. If it cannot be bound to an `IList<CompletionItem>` from the ViewModel, the fallback overlay needs more design.
3. **Save As / path change.** When `SaveDocumentAsAsync` changes a document's path, the LSP server still has the old URI open. The plan does not state whether to send `didClose` for the old URI and `didOpen` for the new URI. In Phase 4 this may be acceptable as a known limitation, but it should be documented.
4. **Opening a file before opening a folder.** Plan says LSP is disabled and shows a status message. Good. But if a folder is opened *after* files are already open, those files should be back-filled with `didOpen`. The plan does not state whether `LSPManager` scans existing open documents on `FolderOpened`. Recommend adding this to M2.

---

## 6. Recommended TOFIX Additions

Add the following open items to `docs/phases/phase-4/TOFIX.md` before implementation starts:

### R6.1 Update `docs/design/LSP_DESIGN.md` for Phase 4 constraints *(priority: medium)*

The design doc shows incremental-or-full sync and a `DocumentOpened` record carrying `TextDocument`, both of which conflict with the Phase 4 plan. Add a note that Phase 4 uses full sync and the actual `DocumentOpened(string FilePath)` message, or update the design doc.

### R6.2 Verify `LanguageInfo.Id` values are valid LSP `languageId`s *(priority: medium, BLOCKER for M2)*

`didOpen` requires an LSP `languageId`. The plan reuses `LanguageInfo.Id` from Phase 3, but this has not been explicitly verified for LSP compatibility. Confirm that TextMate ids such as `"csharp"`, `"json"`, `"xml"`, `"markdown"` are accepted by `csharp-ls` / the LSP spec, or add a mapping layer.

### R6.3 Define multi-folder / root replacement behavior *(priority: medium, BLOCKER for M1)*

If `FolderOpened` fires for a different path while a session already exists, does Phase 4 close the old session and open a new one, or keep both? Recommend single-active-root semantics for Phase 4.

### R6.4 Define back-fill behavior for already-open files when a folder is opened *(priority: medium, BLOCKER for M2)*

If documents are open before `FolderOpened` fires, `LSPManager` must either send `didOpen` for them or declare them out of scope. Recommend sending `didOpen` for all open documents with valid URIs once the session initializes.

### R6.5 Verify AvaloniaEdit diagnostic marker / squiggle API *(priority: high, BLOCKER for M3)*

The plan assumes a marker-service approach. AvaloniaEdit 11.3's surface for error squiggles must be verified early; if unavailable, the fallback to line-level markers must be implemented and documented.

### R6.6 Handle Save As URI change in LSP sync *(priority: low-medium)*

When a document is saved to a new path, the LSP server holds the old URI. Decide whether to send `didClose` + `didOpen` or document it as a Phase 4 limitation.

---

## 7. Conclusion

The Phase 4 implementation plan is **well-prepared and consistent with Phases 0–3**. It correctly builds on:

- `ILanguageDetectionService` (Phase 3)
- `DocumentManager` / `TextDocument` (Phases 1–3)
- `FolderOpened` / `MessageBus` (Phases 0–2)
- `App.axaml.cs` DI and disposal (Phase 0)
- `ShellViewModel` status-bar / dispatcher patterns (Phase 1)

The plan should be approved for implementation after the six items in §6 are added to `docs/phases/phase-4/TOFIX.md`. No changes to prior-phase code are required to begin M1.
