using Dock.Avalonia.Controls;
using Dock.Model.Controls;

namespace Aero.Docking.Model;

/// <summary>
/// Proportional dock splitter - separator between proportional dock children.
/// </summary>
public class AeroProportionalDockSplitter : ManagedDockableBase, IProportionalDockSplitter
{
    private bool _canResize = true;
    private bool _resizePreview;

    public AeroProportionalDockSplitter()
    {
        Id = "Splitter";
        Title = "Splitter";
    }

    public bool CanResize
    {
        get => _canResize;
        set => this.SetProperty(ref _canResize, value);
    }

    public bool ResizePreview
    {
        get => _resizePreview;
        set => this.SetProperty(ref _resizePreview, value);
    }
}
