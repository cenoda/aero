# ISSUE-010: Adopt Avalonia.Headless UI harness for interactive verification

**Label:** CHORE
**Status:** open
**Priority:** medium
**Reported by:** follow-up from `ISSUE-009`
**Assigned to:** —
**Related:** `ISSUE-009`, `manual_test_phase2_m4.sh`

## Description

`ISSUE-009` established that Xvfb + `xdotool` is not a trustworthy way to
verify Avalonia popup/dialog interaction for the Phase 2 M4 File Explorer flow.

The current repository has:
- unit coverage for the ViewModel logic behind New File / New Folder / Rename /
  Delete, and
- a headless smoke script that verifies only launch + startup-folder tree load.

What is missing is a deterministic UI-level verification method for the actual
interactive flow.

This issue tracks the infrastructure work to adopt a proper in-process Avalonia
UI harness so that interactive UI paths can be verified without desktop
pixel-coordinate automation.

## Proposed Scope

Use `Avalonia.Headless` with `Avalonia.Headless.XUnit` in a separate UI test
project (likely `tests/Aero.UiTests/`) that references the real app assembly.

Initial target:
- one automated test that exercises the M4 File Explorer interactive path:
  tree item → context menu → command → dialog → submit → tree update.

Keep the current fast unit-test project unchanged unless a small shared helper is
genuinely needed.

## Constraints / Stop-and-Ask

Per `AGENTS.md` §7, implementation must stop and ask before proceeding because:

1. this adds new NuGet dependencies
2. this likely introduces a new test project

Before implementation:
- add `Avalonia.Headless` and `Avalonia.Headless.XUnit` to
  `docs/LIBRARIES.md`
- confirm the separate UI-test-project approach

## Acceptance Criteria

- [ ] `docs/LIBRARIES.md` catalogues `Avalonia.Headless` and
      `Avalonia.Headless.XUnit` with purpose and phase usage.
- [ ] A separate UI test project exists and references the real app assembly.
- [ ] One deterministic UI test covers the Phase 2 M4 interactive
      context-menu → dialog → tree-update flow.
- [ ] The new test runs headlessly without X11 pixel-coordinate automation.
- [ ] `ISSUE-009` is updated with the verified result or linked resolution.

## Notes

This is intentionally a single follow-up infrastructure issue, not a new phase
or roadmap track.