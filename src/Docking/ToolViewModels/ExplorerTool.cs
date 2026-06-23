using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace Aero.Docking.ToolViewModels;

public class ExplorerTool : ManagedDockableBase, ITool
{
    public ExplorerTool()
    {
        Id = "Explorer";
        Title = "Explorer";
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IDockable d && d.Id == Id;
    /// <inheritdoc />
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
}
