# ISSUE-007: Tab close button binding crashes app when a tab is created

- **Label:** BUG
- **Priority:** critical
- **Status:** closed
- **Opened:** 2026-06-17
- **Closed:** 2026-06-17

## Description

The application launches and renders the empty editor correctly, but crashes as
soon as the user creates a new file (`Ctrl+N`) or opens an existing file.

The exception originates from the tab `DataTemplate` in
`src/Views/EditorView.axaml`:

```
System.ArgumentException: Unable to resolve type vm:EditorViewModel from any of the following locations:
   at Avalonia.Markup.Xaml.XamlIl.Runtime.XamlIlRuntimeHelpers.XamlTypeResolver.Resolve(String qualifiedTypeName)
   ...
   at Aero.Views.EditorView.XamlClosure_1.Build_1(IServiceProvider) in src/Views/EditorView.axaml:line 22
```

Line 22 contains the close-button binding:

```xml
Command="{Binding $parent[TabControl].((vm:EditorViewModel)DataContext).CloseTabCommand}"
```

The `vm:` namespace prefix is declared on the root `UserControl`, but the XAML
compiler cannot resolve the type cast `((vm:EditorViewModel)DataContext)` inside
the `DataTemplate`. Because the `DataTemplate` is instantiated lazily when the
first tab appears, the defect is not visible on the welcome page.

## Expected Behavior

- `Ctrl+N` creates a new untitled tab.
- `Ctrl+O` opens a file tab.
- The × button on each tab invokes `CloseTabCommand`.

## Actual Behavior

- App crashes immediately when the first tab is created.

## Debug Log

### Attempt 1
- **Hypothesis:** The app is not running because there is no `$DISPLAY` in the environment.
- **Action:** Started `Xvfb :99` and launched `DISPLAY=:99 dotnet run --project src`.
- **Result:** App launched successfully; empty editor rendered with menu bar and welcome message.

### Attempt 2
- **Hypothesis:** Keystrokes can be sent to the app window with `xdotool`.
- **Action:** Sent `Ctrl+N` to the running window.
- **Result:** App crashed with `System.ArgumentException: Unable to resolve type vm:EditorViewModel`.
- **Error / Output:**
  ```
  Unhandled exception. System.ArgumentException: Unable to resolve type vm:EditorViewModel from any of the following locations:
     at Avalonia.Markup.Xaml.XamlIl.Runtime.XamlIlRuntimeHelpers.XamlTypeResolver.Resolve(String qualifiedTypeName)
     ...
     at Aero.Views.EditorView.XamlClosure_1.Build_1(IServiceProvider) in /home/cenoda/aero/src/Views/EditorView.axaml:line 22
  ```

## Resolution

Named the `TabControl` (`EditorTabControl`) and changed the close-button binding to
use an element-name binding, which Avalonia can resolve inside the `DataTemplate`:

```xml
<TabControl x:Name="EditorTabControl" ...>
    ...
    <Button Command="{Binding #EditorTabControl.DataContext.CloseTabCommand}" ... />
</TabControl>
```

## Files Changed

- `src/Views/EditorView.axaml`

## Verification

- `dotnet build` — clean (0 warnings, 0 errors)
- `dotnet test` — 89/89 passing
- Manual GUI test in `Xvfb`:
  - `Ctrl+N` creates a tab without crashing
  - Clicking the tab's × button triggers the dirty-close confirmation dialog
    (when the document has unsaved changes)

## Related

- Phase 1 editor tab implementation
