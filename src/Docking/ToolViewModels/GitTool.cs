using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace Aero.Docking.ToolViewModels;

public class GitTool : ManagedDockableBase, ITool
{
    public GitTool()
    {
        Id = "Git";
        Title = "Git";
    }

    /// <summary>The actual GitViewModel bound to this tool's view.</summary>
    public object? ViewModel { get; set; }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IDockable d && d.Id == Id;
    /// <inheritdoc />
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
}
