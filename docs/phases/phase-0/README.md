# Phase 0: Foundation

> Build the ground the rest stands on.

## Goal

Set up project infrastructure so subsequent phases have a solid base.

## Entry Condition

- Project scaffold exists (Avalonia basic window runs)

## Exit Condition

- `dotnet run --project src` launches without errors
- DI container is wired and resolves core services
- Directory skeleton matches architecture docs
- AvaloniaEdit package is available

## Checklist

- [x] Avalonia project scaffold
- [x] Basic window with title
- [ ] Create directory skeleton (Models/, Services/, ViewModels/, Views/, Agent/, etc.)
- [ ] Core infrastructure: ReactiveUI + Microsoft.Extensions.DependencyInjection setup
- [ ] Add AvaloniaEdit NuGet package
- [ ] DI container configuration (register services in Program.cs)

## Related Documents

- `docs/architecture/CORE_INFRASTRUCTURE.md` — MVVM, DI, MessageBus patterns
- `docs/architecture/OVERVIEW.md` — two-layer architecture
- `docs/LIBRARIES.md` — NuGet packages to install
- `docs/CONVENTIONS.md` — coding conventions

## Notes

- Keep code-behind minimal. All logic goes to ViewModels or Services.
- Verify DI wiring by running the app and checking no `InvalidOperationException` on service resolution.
