using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Models.Settings;

namespace Aero.Services;

/// <summary>
/// Persists workspace state and user settings as JSON files under <c>~/.aero/</c>.
/// Atomic writes via temp file + <c>File.Move(overwrite: true)</c>.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IMessageBus? _bus;
    private readonly List<string> _recentFolders = new();
    private const int MaxRecentFolders = 10;

    private readonly string _configDir;
    private readonly string _workspaceFilePath;
    private readonly string _settingsFilePath;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ConfigDirectory => _configDir;

    /// <summary>
    /// Creates a new <see cref="SettingsService"/>.
    /// </summary>
    /// <param name="bus">
    /// Optional message bus. When provided, publishes <see cref="StatusMessage"/>
    /// on corrupt-file fallback errors. Pass <c>null</c> in tests.
    /// </param>
    public SettingsService(IMessageBus? bus = null)
    {
        _bus = bus;
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aero");
        _workspaceFilePath = Path.Combine(_configDir, "workspace.json");
        _settingsFilePath = Path.Combine(_configDir, "settings.json");

        LoadRecentFoldersFromDisk();
    }

    /// <summary>
    /// Creates a new <see cref="SettingsService"/> with a custom config directory.
    /// Used by tests to avoid writing to the real <c>~/.aero/</c>.
    /// </summary>
    internal SettingsService(string configDir, IMessageBus? bus = null)
    {
        _bus = bus;
        _configDir = configDir;
        _workspaceFilePath = Path.Combine(configDir, "workspace.json");
        _settingsFilePath = Path.Combine(configDir, "settings.json");

        LoadRecentFoldersFromDisk();
    }

    public async Task<WorkspaceState> LoadWorkspaceStateAsync()
    {
        return await LoadAsync<WorkspaceState>(_workspaceFilePath);
    }

    public async Task SaveWorkspaceStateAsync(WorkspaceState state)
    {
        await AtomicWriteAsync(_workspaceFilePath, state);
    }

    public async Task<SettingsModel> LoadSettingsAsync()
    {
        return await LoadAsync<SettingsModel>(_settingsFilePath);
    }

    public async Task SaveSettingsAsync(SettingsModel settings)
    {
        await AtomicWriteAsync(_settingsFilePath, settings);
    }

    public void AddRecentFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        _recentFolders.RemoveAll(p => p.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        _recentFolders.Insert(0, normalized);
        if (_recentFolders.Count > MaxRecentFolders)
            _recentFolders.RemoveRange(MaxRecentFolders,
                _recentFolders.Count - MaxRecentFolders);
    }

    public IReadOnlyList<string> GetRecentFolders()
        => _recentFolders.AsReadOnly();

    // -------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------

    private void LoadRecentFoldersFromDisk()
    {
        try
        {
            if (!File.Exists(_workspaceFilePath)) return;
            var json = File.ReadAllText(_workspaceFilePath);
            var ws = JsonSerializer.Deserialize<WorkspaceState>(json, _jsonOptions);
            if (ws?.RecentFolders != null)
            {
                _recentFolders.Clear();
                _recentFolders.AddRange(ws.RecentFolders);
            }
        }
        catch (Exception ex)
        {
            _bus?.Publish(new StatusMessage(
                $"Failed to load recent folders: {ex.Message}"));
        }
    }

    private async Task<T> LoadAsync<T>(string filePath) where T : class, new()
    {
        try
        {
            if (!File.Exists(filePath)) return new T();
            var json = await File.ReadAllTextAsync(filePath);
            var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            return result ?? new T();
        }
        catch (JsonException ex)
        {
            _bus?.Publish(new StatusMessage(
                $"Corrupt {Path.GetFileName(filePath)}, using defaults: {ex.Message}"));
            return new T();
        }
        catch (Exception ex)
        {
            _bus?.Publish(new StatusMessage(
                $"Failed to load {Path.GetFileName(filePath)}: {ex.Message}"));
            return new T();
        }
    }

    private async Task AtomicWriteAsync<T>(string targetPath, T value)
    {
        Directory.CreateDirectory(_configDir);
        var tempPath = targetPath + ".tmp";
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, targetPath, overwrite: true);
    }
}
