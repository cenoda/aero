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

## Architecture (Abstraction-First)

### Plugin Interface

```csharp
public interface IPlugin
{
    // Metadata
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    
    // Lifecycle
    Task InitializeAsync(PluginContext context);
    Task ShutdownAsync();
}

public record PluginContext(
    IServiceProvider Services,
    ILogger Logger,
    IPluginHost Host
);
```

### Extension Points

```csharp
public interface IExtensionPoint
{
    string Name { get; }
    Type ContractType { get; }
}

public static class ExtensionPoints
{
    public static IExtensionPoint Commands { get; } = ...;
    public static IExtensionPoint Languages { get; } = ...;
    public static IExtensionPoint Themes { get; } = ...;
    public static IExtensionPoint BuildServices { get; } = ...;
    public static IExtensionPoint LspServices { get; } = ...;
}
```

### Plugin Host

```csharp
public interface IPluginHost
{
    void RegisterExtension<T>(IExtensionPoint point, T extension);
    IEnumerable<T> GetExtensions<T>(IExtensionPoint point);
    void LoadPlugin(string path);
    void UnloadPlugin(string pluginId);
}
```

## Checklist

- [ ] **IPlugin interface** — Initialize(), Shutdown(), metadata
- [ ] **PluginHost** — scan & load assemblies
- [ ] **Extension points** — register commands, languages, themes, build services, LSP
- [ ] **Plugin marketplace** — optional: discover & install

## Related Documents

- `docs/LIBRARIES.md` — McMaster.NETCore.Plugins
- `docs/architecture/IDE_CORE.md` — Plugin System subsystem

## Notes

- Plugin marketplace is optional. Focus on IPlugin + PluginHost + extension points first.
- Assembly isolation is important — McMaster.NETCore.Plugins handles this.
- Agents (Phase A1+) are technically plugins. Ensure the plugin system can host them.
- Extension points should include BuildServices and LspServices for extensibility.
