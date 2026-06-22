using System.Collections.Generic;
using System.Threading.Tasks;
using Aero.Models.Settings;

namespace Aero.Services;

/// <summary>
/// Shared persistence layer for workspace state and user settings.
/// Stores JSON files under <c>~/.aero/</c>.
/// Consumed by 8.4 Welcome Page, 8.6 Settings Page, and 8.7 Workspace Persistence.
/// </summary>
public interface ISettingsService
{
    Task<WorkspaceState> LoadWorkspaceStateAsync();
    Task SaveWorkspaceStateAsync(WorkspaceState state);
    Task<SettingsModel> LoadSettingsAsync();
    Task SaveSettingsAsync(SettingsModel settings);

    /// <summary>
    /// Add a folder to the recent list. Normalizes path, deduplicates, enforces 10 max.
    /// Must be called from the UI thread.
    /// </summary>
    void AddRecentFolder(string path);

    /// <summary>
    /// Recent folders list (most recent first, max 10). Consumed by 8.4 Welcome Page.
    /// Safe to read from any thread; writes are UI-thread only.
    /// </summary>
    IReadOnlyList<string> GetRecentFolders();

    /// <summary>The <c>~/.aero/</c> config directory path.</summary>
    string ConfigDirectory { get; }
}
