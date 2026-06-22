using Dock.Avalonia.Controls;
using Dock.Model.Controls;

namespace Aero.Docking.ToolViewModels;

public class ExplorerTool : ManagedDockableBase, ITool
{
    public ExplorerTool()
    {
        Id = "Explorer";
        Title = "Explorer";
    }
}
