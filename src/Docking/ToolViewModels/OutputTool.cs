using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace Aero.Docking.ToolViewModels;

public class OutputTool : ManagedDockableBase, ITool
{
    public OutputTool()
    {
        Id = "Output";
        Title = "Output";
    }

    /// <summary>The actual OutputViewModel bound to this tool's view.</summary>
    public object? ViewModel { get; set; }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IDockable d && d.Id == Id;
    /// <inheritdoc />
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
}
