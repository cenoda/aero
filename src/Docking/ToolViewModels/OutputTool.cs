using Dock.Avalonia.Controls;
using Dock.Model.Controls;

namespace Aero.Docking.ToolViewModels;

public class OutputTool : ManagedDockableBase, ITool
{
    public OutputTool()
    {
        Id = "Output";
        Title = "Output";
    }
}
