# Phase 6: Build & Output

> Press a key. Build your project. See errors. Jump to them.

## Goal

Integrate `dotnet build` (and later other build systems) with the IDE.

## Entry Condition

- Phase 5 complete (Output panel, ProcessRunner)

## Exit Condition

- Ctrl+Shift+B runs `dotnet build` and streams output
- MSBuild errors are parsed and populate the Problems panel
- Clicking an error jumps to file and line in the editor
- Build success/failure is visible in status bar

## Checklist

- [ ] **BuildService** — run `dotnet build` and capture output
- [ ] **Output panel** — stream stdout/stderr
- [ ] Parse MSBuild error format → populate Problems panel
- [ ] Ctrl+Shift+B to build
- [ ] Click error in Problems → jump to file/line

## Related Documents

- `docs/architecture/IDE_CORE.md` — Build System subsystem
- `docs/design/PANELS_AND_DOCKING.md` — Problems panel layout

## Notes

- MSBuild error format is well-documented. Parse file path, line, column, code, message.
- BuildService should be extensible for other build systems later (npm, cargo, etc.).
- Problems panel already exists from Phase 4 (LSP diagnostics). Build errors append to it.
