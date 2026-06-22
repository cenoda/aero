using Dock.Avalonia.Controls;
using Dock.Model.Controls;

namespace Aero.Docking.ToolViewModels;

public class ProblemsTool : ManagedDockableBase, ITool
{
    public ProblemsTool()
    {
        Id = "Problems";
        Title = "Problems";
    }
}
