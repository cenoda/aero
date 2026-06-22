using Dock.Avalonia.Controls;
using Dock.Model.Controls;

namespace Aero.Docking.ToolViewModels;

public class GitTool : ManagedDockableBase, ITool
{
    public GitTool()
    {
        Id = "Git";
        Title = "Git";
    }
}
