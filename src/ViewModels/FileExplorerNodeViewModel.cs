using System.Collections.ObjectModel;
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

    /// <summary>The file or directory name without any parent path.</summary>
    public string Name { get; }

    /// <summary>The fully qualified, normalized path.</summary>
    public string FullPath { get; }

    /// <summary>True if this node represents a directory.</summary>
    public bool IsDirectory { get; }

    /// <summary>Material icon kind (e.g. <c>Folder</c>, <c>FileDocument</c>, <c>CSharp</c>).
    /// Held as a string for forward-compat with Material.Icons.Avalonia, but the
    /// view currently renders <see cref="Glyph"/> instead (icons paused — see
    /// docs/phases/phase-2/TOFIX.md R3.1).</summary>
    public string IconKind { get; }

    /// <summary>
    /// Small text glyph shown to the left of the file name in the tree.
    /// Derived from <see cref="IconKind"/> so the VM remains the source of
    /// truth — the view just renders whatever this returns. When icons are
    /// restored, swap the binding in the view to <see cref="IconKind"/>.
    /// </summary>
    public string Glyph => IconKind switch
    {
        "Folder" => "▸",
        "FileDocument" => "•",
        "MicrosoftVisualStudio" => "◆",
        "LanguageCsharp" => "#",
        "Nodejs" => "⬡",
        _ => "•",
    };

    /// <summary>Display label shown in the tree — currently identical to <see cref="Name"/>.</summary>
    public string DisplayName => Name;

    /// <summary>Children. Always non-null; empty for files.</summary>
    public ObservableCollection<FileExplorerNodeViewModel> Children { get; } = new();

    /// <summary>
    /// Whether the node is expanded in the tree. Directories only — for files
    /// the binding setter is a no-op.
    /// </summary>
    [Reactive] public bool IsExpanded { get; set; }

    /// <summary>
    /// Whether children have been populated. Currently always <c>true</c> after
    /// construction because Phase 2 uses eager enumeration. A future lazy-load
    /// optimization would flip this to false until the first expand.
    /// </summary>
    [Reactive] public bool AreChildrenLoaded { get; set; } = true;
}
