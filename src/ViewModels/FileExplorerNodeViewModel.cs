using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for a single node in the file explorer tree. Plain MVVM —
/// knows nothing about disk I/O. The parent <see cref="FileExplorerViewModel"/>
/// is responsible for creating children.
/// </summary>
public class FileExplorerNodeViewModel : ReactiveObject
{
    /// <summary>
    /// Synthetic sentinel child inserted into a directory's <see cref="Children"/>
    /// until the real children are loaded. The view shows the expander arrow
    /// because the collection is non-empty; after the load completes the VM
    /// replaces this placeholder with the real entries.
    /// </summary>
    public static readonly FileExplorerNodeViewModel PlaceholderChild =
        new("\u2026", "", isDirectory: false, iconKind: "Placeholder")
        {
            IsPlaceholder = true,
            AreChildrenLoaded = true,
        };

    public FileExplorerNodeViewModel(
        string name,
        string fullPath,
        bool isDirectory,
        string iconKind)
    {
        Name = name ?? throw new System.ArgumentNullException(nameof(name));
        FullPath = fullPath ?? throw new System.ArgumentNullException(nameof(fullPath));
        IsDirectory = isDirectory;
        IconKind = iconKind ?? throw new System.ArgumentNullException(nameof(iconKind));
    }

    /// <summary>
    /// Git status glyph shown next to the file name (e.g. "M" for modified, "A" for added).
    /// Set by FileExplorerViewModel when GitStatusChanged fires.
    /// </summary>
    [Reactive] public string GitStatusGlyph { get; set; } = "";

    /// <summary>The file or directory name without any parent path.</summary>
    public string Name { get; }

    /// <summary>The fully qualified, normalized path.</summary>
    public string FullPath { get; }

    /// <summary>True if this node represents a directory.</summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// The parent directory node in the tree. <c>null</c> for root-level nodes.
    /// Used by <see cref="Aero.ViewModels.FileExplorerViewModel"/> after
    /// rename/delete to re-enumerate only the affected subtree without
    /// collapsing expansion state.
    /// </summary>
    public FileExplorerNodeViewModel? Parent { get; set; }

    /// <summary>
    /// Reference to the owning <see cref="Aero.ViewModels.FileExplorerViewModel"/>.
    /// Enables the per-node ContextMenu to bind commands like
    /// <c>{Binding Owner.NewFileCommand}</c> without relying on ancestor
    /// traversal from a popup visual tree.
    /// </summary>
    public FileExplorerViewModel? Owner { get; set; }

    /// <summary>Material icon kind (e.g. <c>Folder</c>, <c>FileDocument</c>, <c>CSharp</c>).
    /// Held as a string for forward-compat with Material.Icons.Avalonia, but the
    /// view currently renders <see cref="Glyph"/> instead (icons paused — see
    /// docs/phases/phase-2/TOFIX.md R3.1).</summary>
    public string IconKind { get; }

    /// <summary>
    /// True for the synthetic <see cref="PlaceholderChild"/>. Tests and the
    /// view swap logic use this to distinguish the sentinel from real nodes.
    /// </summary>
    public bool IsPlaceholder { get; private init; }

    /// <summary>
    /// Icon resource key (e.g. <c>"Icon.Folder"</c>, <c>"Icon.Code"</c>) derived
    /// from <see cref="IconKind"/>. Handles both legacy keys (pre-8.5) and new
    /// <see cref="Aero.Services.IconResolver"/> keys.
    /// </summary>
    public string Glyph => IconKind switch
    {
        "Folder" => "Icon.Folder",
        "MicrosoftVisualStudio" or "Icon.Project" => "Icon.Project",
        "LanguageCsharp" or "Icon.Code" => "Icon.Code",
        "Nodejs" => "Icon.Config",
        "FileDocument" or "Placeholder" => "Icon.Unknown",
        _ => IconKind, // Pass through Icon.XXX keys from IconResolver
    };

    /// <summary>
    /// Resolved <see cref="Geometry"/> for the current <see cref="Glyph"/> key.
    /// Used by XAML bindings that cannot resolve resource keys dynamically.
    /// </summary>
    public Geometry? GlyphGeometry
    {
        get
        {
            if (Application.Current is { } app)
            {
                app.TryFindResource(Glyph, out var resource);
                if (resource is Geometry g)
                    return g;
            }

            return null;
        }
    }

    /// <summary>Children. Always non-null; empty for files or unloaded directories.</summary>
    public ObservableCollection<FileExplorerNodeViewModel> Children { get; } = new();

    /// <summary>
    /// Whether the node is expanded in the tree. Directories only — for files
    /// the binding setter is a no-op.
    /// </summary>
    [Reactive] public bool IsExpanded { get; set; }

    /// <summary>
    /// True when this node's <see cref="Children"/> reflect the actual disk.
    /// For files, always <c>true</c>. For directories, <c>false</c> until
    /// <see cref="Aero.ViewModels.FileExplorerViewModel.EnsureChildrenLoadedAsync"/>
    /// populates them — this is what makes the explorer lazy-load-on-expand.
    /// </summary>
    [Reactive] public bool AreChildrenLoaded { get; set; } = true;
}
