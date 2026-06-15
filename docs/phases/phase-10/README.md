# Phase 10: Plugin System

> Let others extend Aero.

## Goal

Implement a plugin API so third-party code can extend the IDE.

## Entry Condition

- Phase 9 complete (Advanced Features)

## Exit Condition

- Plugins can be loaded at runtime
- IPlugin interface is stable and documented
- Extension points exist for commands, languages, themes
- PluginHost scans and loads assemblies safely

## Checklist

- [ ] **IPlugin interface** — Initialize(), Shutdown(), metadata
- [ ] **PluginHost** — scan & load assemblies
- [ ] **Extension points** — register commands, languages, themes
- [ ] **Plugin marketplace** — optional: discover & install

## Related Documents

- `docs/LIBRARIES.md` — McMaster.NETCore.Plugins
- `docs/architecture/IDE_CORE.md` — Plugin System subsystem

## Notes

- Plugin marketplace is optional. Focus on IPlugin + PluginHost + extension points first.
- Assembly isolation is important — McMaster.NETCore.Plugins handles this.
- Agents (Phase A1+) are technically plugins. Ensure the plugin system can host them.
