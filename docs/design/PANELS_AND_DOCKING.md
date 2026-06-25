# Panel & Docking Design

## Layout Zones

```
+-----------+------------------+-----------+
|           |                  |           |
| Sidebar   |   Editor Area    |  Right    |
| (left)    |   (center)       |  Panel    |
|           |                  |           |
+-----------+------------------+-----------+
|          Bottom Panel                    |
+------------------------------------------+
|          Status Bar                       |
+------------------------------------------+
```

## Panel Types

| Panel        | Default Zone | Can Close? | Singleton? |
|--------------|-------------|------------|------------|
| FileExplorer | left        | yes        | yes        |
| Git          | left        | yes        | yes        |
| Outline      | left/right  | yes        | yes        |
| Editor tabs  | center      | no         | yes        |
| Properties   | right       | yes        | yes        |
| Terminal     | bottom      | yes        | no (multi) |
| Output       | bottom      | yes        | yes        |
| Problems     | bottom      | yes        | yes        |
| StatusBar    | bottom-fixed| no         | yes        |

## Panel Lifecycle

```
interface IPanel {
    string Title { get; }
    object Icon { get; }
    bool CanClose { get; }
    void OnShown();
    void OnHidden();
}

class PanelHost {
    ObservableCollection<IPanel> LeftPanels;
    ObservableCollection<IPanel> RightPanels;
    ObservableCollection<IPanel> BottomPanels;
    ObservableCollection<IPanel> CenterPanels; // editors
}
```

## Docking Approach

**Decision (2026-06-25):** Fixed `Grid` with `GridSplitter` layout. Dock.Avalonia was evaluated and abandoned after two failed integration attempts — the library's internal rendering is too opaque to debug effectively.

**Option A (abandoned):** `Dock.Avalonia` NuGet package.
- ~~Provides VS-style docking with drag-to-rearrange~~
- ~~Supports pin/unpin, tab groups, float windows~~
- **ABANDONED:** Two integration attempts failed. Internal rendering is opaque.

**Option B (chosen):** Manual `Grid` with `GridSplitter` controls.
- Fixed zones: sidebar (Explorer+Git) | editor | bottom panel (Problems+Output)
- No drag-to-rearrange, but covers the 95% use case
- Simple, debuggable, already working

## View → ViewModel Wiring

Each panel follows MVVM:

```xml
<!-- Example: FileExplorer.axaml -->
<UserControl xmlns="..."
             x:Class="Aero.Views.FileExplorer">
    <TreeView ItemsSource="{Binding RootNodes}">
        <TreeView.ItemTemplate>
            <TreeDataTemplate ItemsSource="{Binding Children}">
                <StackPanel Orientation="Horizontal">
                    <Image Source="{Binding Icon}" />
                    <TextBlock Text="{Binding Name}" />
                </StackPanel>
            </TreeDataTemplate>
        </TreeView.ItemTemplate>
    </TreeView>
</UserControl>
```

```csharp
// FileExplorerViewModel.cs
public class FileExplorerViewModel : ObservableObject, IPanel {
    public string Title => "Explorer";
    public ObservableCollection<ProjectNode> RootNodes { get; }

    public FileExplorerViewModel(FileService fileService) {
        RootNodes = new();
        // subscribe to FolderOpened events
    }
}
```
