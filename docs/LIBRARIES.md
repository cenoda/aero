# Library Catalog for Aero IDE

Every library explained in plain English — what it does, why you'd want it, and when it matters.

---

## EDITOR & TEXT (Phase 1-3)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **AvaloniaEdit** | A real code editor widget — text rendering, cursor, selection, scroll, line numbers, folding. | Without it, you're building from a bare `<TextBox>`. Months of work saved. #1 must-have. |
| **AvaloniaEdit.TextMate** | Teaches AvaloniaEdit to read TextMate grammars (VS Code's format for coloring code). | Drop in `.tmLanguage` files → instant syntax highlighting. |
| **TextMateSharp.Grammars** | A bundle of 100+ pre-made TextMate grammars. | C#, Python, JS, Rust, Go, CSS, HTML, Markdown — all covered. No hunting for grammar files. |


## LANGUAGE SERVERS (Phase 4)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **StreamJsonRpc** | JSON-RPC over streams — matches requests to responses, frames messages. | LSP servers speak JSON-RPC. This handles protocol plumbing so you work with typed C# objects. |
| **OmniSharp.Extensions.LanguageServer** | Full LSP client framework in C#. Pre-built handlers for completion, hover, diagnostics. | Heavier but gives you LSP logic almost for free. Good for fast progress. |

## TERMINAL (Phase 5)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **Pty.Net** | Wraps OS pseudo-terminals (Linux `/dev/ptmx`, Windows ConPTY). | Without a pty, programs like `git diff`, `htop`, or colored output break because they detect "not a real terminal". |
| **VtNetCore** | Parses VT100/xterm escape codes (`\e[31mHELLO\e[0m`) into structured data. | Decodes "HELLO in red" so you can render it. Mandatory for a working terminal. |
| **CliWrap** | Clean C# wrapper around `Process.Start`. | Instead of 15 lines of `ProcessStartInfo`, you write: `await Cli.Wrap("dotnet").WithArguments("build").ExecuteAsync()`. Great for build, git, and LSP spawning too. |

## DOCKING & LAYOUT (Phase 8)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **Dock.Avalonia** | VS Code-style dockable/floatable panels with drag-to-rearrange. | This makes an IDE feel like an IDE. Users expect to drag panels around. |
| **DialogHost.Avalonia** | Modal popup overlays. | Command palette, goto-line, find-in-files picker, settings popups. |



## MVVM & REACTIVE

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **ReactiveUI** | Reactive MVVM framework for Avalonia. Automatic change tracking with `WhenAnyValue`. | Replaces manual `PropertyChanged` boilerplate. E.g. `this.WhenAnyValue(x => x.SearchText).Throttle(300ms).Subscribe(DoSearch)`. |
| **DynamicData** | Reactive collections — auto-diff, filter, sort, transform. | Define a pipeline instead of manually updating `ObservableCollection`. Much less bug-prone with large lists. |

## CONFIG & DI

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **Microsoft.Extensions.DependencyInjection** | .NET's built-in DI container: `services.AddSingleton<I, T>()`. | Replaces manual `ServiceLocator`. Handles constructor injection, lifetimes, disposal. Battle-tested. |
| **Microsoft.Extensions.Configuration.Json** | Reads `.json` config with nested sections and reload-on-change. | Your `~/.aero/settings.json` becomes trivial. Auto-reloads when the file changes. |
| **Microsoft.Extensions.Logging** | Structured logging abstraction. `logger.LogInformation("Opened {Path}", path)`. | Switch between trace, file, console output without changing code. Essential for debugging LSP/build/terminal issues. |
| **Serilog** | Richer logging with file sinks, JSON output, timestamps. | If you want logs to `~/.aero/logs/` with structured format. Plugs into Microsoft.Extensions.Logging. |



## GIT (Phase 7)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **LibGit2Sharp** | Full git in .NET — status, diff, log, branches, commits. No CLI needed. | Parsing `git` CLI output is fragile. This gives typed objects for everything. |
| **DiffPlex** | Diff algorithm — tells you which lines were added/removed/changed. | Needed for git diff viewer + "unsaved changes" comparison dialog. |

## SEARCH (Phase 8)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **FuzzySharp** | Fuzzy string matching. | Command palette & Ctrl+P: typing "mwin" should match "MainWindow.cs". |

## PLUGINS (Phase 10)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **McMaster.NETCore.Plugins** | Runtime assembly loading with isolation, shared types, unloading. | Raw `AssemblyLoadContext` is tricky. This makes plugins a few lines. |

## ICONS & ASSETS

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **Material.Icons.Avalonia** | 5000+ Material Design icons as Avalonia controls. | `<MaterialIcon Kind="Folder" />` — instant icons. No hunting files. |

## TESTING

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **xUnit** | Modern .NET test framework: `[Fact]`, `[Theory]`. | Test TextBuffer, UndoManager, LSP parser, Git logic. |
| **NSubstitute** | Mocking: `var bus = Substitute.For<IMessageBus>()`. | Test ViewModels in isolation. |

## QUALITY OF LIFE

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **Humanizer.Core** | `1234.ToMetric() → "1.23k"`, `DateTime.Humanize() → "2 hours ago"`. | File sizes in explorer, dates in git log, status bar. |
| **Polly** | Retry, timeout, circuit-breaker patterns. | LSP crashed? Retry with backoff. Build timed out? Set policy. |
| **YamlDotNet** | YAML parsing. | If you want `.yaml` config or to read GitHub Actions/Docker Compose. |


---

## QUICK PICK: Install These Day 1

```
Avalonia.AvaloniaEdit      11.3.*
AvaloniaEdit.TextMate      11.3.*
TextMateSharp.Grammars     1.*
Dock.Avalonia              11.3.*
Material.Icons.Avalonia    1.*
Microsoft.Extensions.DependencyInjection  9.*
Microsoft.Extensions.Configuration.Json   9.*
Microsoft.Extensions.Logging             9.*
CliWrap                    3.*
FuzzySharp                 2.*
```

Then add **StreamJsonRpc** at Phase 4, **LibGit2Sharp + DiffPlex** at Phase 7.


---

## Full Dependency Timeline

```
Phase 1: + AvaloniaEdit
Phase 3: + AvaloniaEdit.TextMate, TextMateSharp.Grammars
Phase 4: + StreamJsonRpc, CliWrap
Phase 5: + Pty.Net, VtNetCore
Phase 7: + LibGit2Sharp, DiffPlex
Phase 8: + Dock.Avalonia, DialogHost.Avalonia, FuzzySharp,
          Material.Icons.Avalonia, Microsoft.Extensions.*
Phase 10: + McMaster.NETCore.Plugins
Anytime: + Humanizer, Polly, xUnit, NSubstitute
```

