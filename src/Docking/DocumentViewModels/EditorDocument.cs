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

    /// <summary>The actual EditorViewModel bound to this document's view.</summary>
    public object? ViewModel { get; set; }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Dock.Model.Core.IDockable d && d.Id == Id;
    /// <inheritdoc />
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
}
