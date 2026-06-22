using System.Collections.Generic;

namespace Aero.Models.Settings;

/// <summary>
/// Workspace persistence model — remembers last folder, open files,
/// active tab, window geometry, and recent folders across restarts.
/// Stored at <c>~/.aero/workspace.json</c>.
/// </summary>
public record WorkspaceState
{
    public string? LastFolderPath { get; init; }
    public List<string> OpenFilePaths { get; init; } = new();
    public int ActiveTabIndex { get; init; }
    public WindowState? Window { get; init; }
    public List<string> RecentFolders { get; init; } = new();
}
