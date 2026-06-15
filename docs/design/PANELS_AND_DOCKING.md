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

**Option A (recommended):** Use `Dock.Avalonia` NuGet package.
- Provides VS-style docking with drag-to-rearrange
- Supports pin/unpin, tab groups, float windows
- Mature and well-tested

**Option B (simpler first):** Manual `Grid` with `GridSplitter` controls.
- Fixed zones, no drag-to-rearrange
- Easier to implement, less flexible
- Good for Phase 1-2, migrate to Dock.Avalonia later

Recommend starting with Option B (manual grid) to ship faster, then migrate to Option A in Phase 8 (UI Polish).

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
