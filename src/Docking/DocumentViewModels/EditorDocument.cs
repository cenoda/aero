using Dock.Avalonia.Controls;
using Dock.Model.Controls;

namespace Aero.Docking.DocumentViewModels;

public class EditorDocument : ManagedDockableBase, IDocument
{
    public EditorDocument()
    {
        Id = "Editor";
        Title = "Editor";
    }
}
