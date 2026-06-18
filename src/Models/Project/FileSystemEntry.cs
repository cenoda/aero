namespace Aero.Models.Project;

/// <summary>
/// Whether a <see cref="FileSystemEntry"/> represents a file or a directory.
/// </summary>
public enum FileSystemEntryKind
{
    File,
    Directory,
}

/// <summary>
/// A plain tree node produced by <c>IFileSystemService</c>. Models are
/// UI-agnostic — <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>
/// lives in the ViewModel layer per project MVVM conventions.
/// </summary>
/// <param name="Name">The file or directory name without any parent path.</param>
/// <param name="FullPath">The fully qualified, normalized path.</param>
/// <param name="Kind">Whether this entry is a file or a directory.</param>
public record FileSystemEntry(
    string Name,
    string FullPath,
    FileSystemEntryKind Kind);
