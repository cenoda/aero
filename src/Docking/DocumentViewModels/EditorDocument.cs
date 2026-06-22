using System;
using System.Collections.Generic;

namespace Aero.Docking.DocumentViewModels;

/// <summary>
/// Document dock implementation for the editor area.
/// </summary>
public class EditorDocument : Dock.Avalonia.Controls.ManagedDockableBase, Dock.Model.Controls.IDocument
{
    public EditorDocument()
    {
        Id = "Editor";
        Title = "Editor";
    }
}
