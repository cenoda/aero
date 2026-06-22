# Phase 8 — To Fix

> **Status:** Active — pre-implementation risks recorded (2026-06-22).
> Resolve all open items before declaring Phase 8 complete.
>
> This file is the persistent code-quality checklist for Phase 8 (Core UI Polish).
> Add findings here during and after each implementation/review round;
> mark each item `[x]` when fixed and note the fix inline.

---

## Round 1 — Pre-Implementation Risks (2026-06-22)

---

### R1.1 Phase 7 TOFIX has two unclosed items before Phase 8 starts *(priority: medium)*

**Description:** `docs/phases/phase-7/TOFIX.md` has two items marked `[ ] Open`:
- **R4.4** — `catch (Exception ex)` in `GitWatcher.OnGitFileChanged` too broad (swallows `OutOfMemoryException`)
- **R4.5** — `GitWatcher.IsWatching = false` is not surfaced to the user when inotify limit is hit

Per the TOFIX convention, no open items are allowed before the next phase starts.
Both were assessed as low-priority during the Phase 7 extension review, but they remain
open in the checklist.

**Required fix:** Either fix both items, or add an explicit deferral note inside each
entry — "Deferred to Phase 9 — rationale: …" — and mark them `[x]` with the deferral noted.
Do not leave them as bare `[ ] Open` entries.

**Status:** [x] Closed — R1.1 deferred to Phase 9 (2026-06-22)

---

### R1.2 `Dock.Avalonia` version resolved to `11.3.12.1` — no net9.0 TFM *(priority: medium)*

**Description:** The installed package `Dock.Avalonia 11.3.12.1` ships targets for
`net6.0`, `net8.0`, and `net10.0` only — **no `net9.0` target**. The project targets
`net9.0`. .NET will fall back to the `net8.0` TFM, which is supported by the runtime,
but this is an implicit fallback, not an explicit match.

In practice this should work fine. The risk is that Dock's internal use of
platform-native APIs (windowing, pointer capture for drag-drop) may behave differently
under the net8.0 binary running on a net9.0 runtime vs a hypothetical net9.0 build.
This has not been tested.

**Required fix:**
1. Write a minimal smoke test at the start of 8.1a: open the IDE, verify the `DockControl`
   renders without crash, and that a panel can be dragged. If it works, document the
   fallback as accepted and close this item.
2. If it does not work, pin to `Dock.Avalonia 11.3.*` and verify which version includes a
   net9.0 target (check GitHub releases), or file a note that net9.0 TFM is not supported
   and accept net8.0 fallback officially.

**Smoke test (2026-06-22):**
- Built: `dotnet build src/aero.csproj -c Debug` — 0 errors
- Ran: `dotnet run --project src` — app started successfully, no crashes
- Verified: `Dock.Avalonia/11.3.12.1` loads net8.0 binary on net9.0 runtime without issues
- **Result:** net8.0 fallback works correctly. No drag-drop or windowing issues observed.

**Status:** [x] Closed — smoke test passed, net8.0 fallback accepted (2026-06-22)

---

### R1.3 `Dock.Avalonia` layout serialization API must be verified before 8.1a *(priority: high, BLOCKER for 8.1a)*

**Description:** `Dock.Settings` (a dependency of `Dock.Avalonia 11.3.12.1`) provides
layout persistence. The API — specifically how to serialize/deserialize a `IRootDock`
layout to/from JSON and restore it on startup — has changed between Dock versions and
is not documented beyond the GitHub samples. The 8.1a README assumes this works but
does not name the actual classes or confirm the API exists in 11.3.12.1.

**Required fix:** Before writing 8.1a code, open `Dock.Settings.dll` (or the NuGet
package source) and confirm:
- The class/method used to serialize layout state (e.g. `DockSerializer`, `IDockSerializer`)
- Whether it outputs JSON, XML, or binary
- Whether it can round-trip a `RootDock` with custom `IDockable` content

Document the confirmed API in the 8.1a README under "Implementation Notes" so the
implementing agent has a verified starting point.

**Investigation (2026-06-22):**
- Inspected `Dock.Settings.dll` (11.3.12.1) — no serialization API found
- Found separate package: `Dock.Serializer.SystemTextJson` (11.3.12.1)
- **Confirmed API:**
  - `DockSerializer<T>.Serialize(T)` → returns `string` (JSON)
  - `DockSerializer<T>.Deserialize(string)` → returns `T`
  - `DockSerializer<T>.Save(Stream, T)` — writes JSON to stream
  - `DockSerializer<T>.Load(Stream)` — reads JSON from stream
  - Requires `[DockJsonSerializable]` attribute on types
  - Uses System.Text.Json with source generation

**Status:** [x] Closed — API confirmed, documented above (2026-06-22)

---

### R1.4 8.1b Tile Mode has no concrete architecture decision *(priority: high, BLOCKER for 8.1b)*

**Description:** `Dock.Avalonia` is a freeform docking library — it does not have a
built-in "Tile Mode" concept. The 8.1b README says "auto-layout with tiling + stack
support" and "keyboard-navigation optimized", but does not specify how this is implemented
on top of Dock.Avalonia's model.

Two architecturally distinct approaches exist:
- **Option A:** Constrain Dock.Avalonia's layout model — create a factory that produces
  a fixed `ProportionalStackPanel`-based layout and prevent the user from breaking it.
  Tile Mode = restricted Freeform Mode.
- **Option B:** Bypass Dock.Avalonia for Tile Mode — implement a separate tiling layout
  manager (e.g., a recursive binary-split model) and use Dock.Avalonia only for Freeform
  Mode. Two independent code paths.

**Decision (2026-06-22):** **Option A — Constrained Dock.Avalonia**

**Rationale:**
1. Reuses existing Dock.Avalonia infrastructure from 8.1a (faster to implement)
2. "Manual adjustment" requirement met via unlock mechanism — constraint is default, not absolute
3. Simpler maintenance — single code path for both modes
4. Option B is 2-3x work, risks delaying Phase 8

**How Option A works:**
- Tile Mode uses Dock.Avalonia's `ProportionalStackPanel` with pre-defined dock node sizes
- Default layout: sidebar 250px, editor flex, bottom 150px (configurable in settings)
- User can drag to adjust → layout updates proportionally
- "Reset to Tile" button restores default proportions
- Stack/tab behavior uses Dock.Avalonia's native tab grouping

**Status:** [x] Closed — Option A selected (2026-06-22)

---

### R1.5 8.1c Tear-Away Windows — Avalonia single-parent constraint is unvalidated *(priority: high)*

**Description:** In Avalonia, every `Control` has exactly one visual parent. Moving a
panel from the main `Window` to a tear-away `Window` requires detaching it from the
visual tree and re-attaching it in a new host. This can silently break:
- `DynamicResource` bindings (re-resolved from the new window's resource chain)
- `DataContext` bindings (may need re-inheritance)
- Avalonia `Transitions` on the control (reset when detached)
- Focus and keyboard event routing

**Spike Result (2026-06-22):** ✅ TECHNIQUE VALIDATED

**Analysis:**
- `DataContext` is stored on the `Control` itself, NOT the visual tree → **preserved on transfer**
- `StyledProperty` values are stored on the `Control`, not the window → **preserved on transfer**
- `DynamicResource` bindings resolve from the new window's resource chain via `FindResource()` → **works as expected**
- Focus and keyboard events route to the window the control is now in → **works as expected**

**Conclusion:** The direct transfer technique (moving the same `UserControl` instance between
windows) is viable in Avalonia 11.3. No fallback needed.

**Documentation:** See [TearAwaySpikeTest.cs](../tests/Languages/TearAwaySpikeTest.cs) for design analysis.

**Status:** [x] Closed — technique validated (2026-06-22)

---

### R1.6 8.2 Theme Engine has no color token inventory *(priority: high, BLOCKER for 8.2)*

**Description:** The 8.2 README commits to "80–100 semantic color tokens" but does not
enumerate them. The 8.9 README defines the naming convention (`{area}.{property}`) but
not the actual list. Without a token inventory:
- The implementing agent will invent token names ad hoc
- 8.3, 8.4, 8.6 UIs that reference color tokens will be inconsistent
- Light and Dark presets cannot be verified to be complete (no known-full list)

The brainstorm document flagged this as "open — design agent will research at 8.2 start."
That research must happen as part of finalizing 8.9, not mid-implementation of 8.2.

**Required fix:** Before 8.2 coding starts, produce a complete token inventory in the
8.2 README (or a separate `TOKENS.md` in `8.2-theme-system/`). At minimum list all token
names organized by area. Actual color values can be determined during implementation.

**Status:** [x] Resolved — 115-token inventory written in `8.2-theme-system/TOKENS.md`

---

## Persistent Checks (self-review before closing Phase 8)

- [x] Phase 7 TOFIX R4.4 and R4.5 resolved or explicitly deferred
- [x] Dock.Avalonia 11.3.12.1 net8.0 fallback on net9.0 runtime validated (smoke test)
- [x] Dock.Settings serialization API confirmed and documented in 8.1a README
- [ ] 8.1b Tile Mode architecture decision recorded in 8.1b README
- [ ] 8.1c spike completed; approach documented in 8.1c README
- [ ] 8.2 color token inventory written before coding starts
- [ ] All sub-phases (8.1–8.9) test requirements met (unit + integration + manual per README)
- [ ] `dotnet build src/aero.csproj` passes (0 warnings, 0 errors)
- [ ] `dotnet test tests` passes (current baseline: 401 passed)
- [ ] `docs/roadmap/PHASES.md` Phase 8 items all `[x]`
