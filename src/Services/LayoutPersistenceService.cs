using System;
using System.IO;
using Dock.Model.Controls;
using Dock.Serializer;

namespace Aero.Services;

/// <summary>
/// Persists dock layout state to disk using Dock.Serializer.Newtonsoft.
/// Note: Newtonsoft is used intentionally — it handles object-reference cycles in the
/// dock layout tree automatically. The Dock.Serializer.SystemTextJson variant requires
/// [DockJsonSerializable] source-gen wiring on all concrete model types; that work is
/// deferred (see TOFIX R4.2).
/// Newtonsoft.Json is already a hard project dependency (LSP layer), so this adds no
/// new transitive dependency.
/// </summary>
public class LayoutPersistenceService : ILayoutPersistenceService
{
    private readonly string _layoutPath;
    private readonly DockSerializer _serializer;

    public LayoutPersistenceService()
    {
        // Cross-platform: use Environment.GetFolderPath for user profile
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _layoutPath = Path.Combine(userProfile, ".aero", "layout.json");

        // Create directory on construction if needed
        var dir = Path.GetDirectoryName(_layoutPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _serializer = new DockSerializer();
    }

    public void Save(IRootDock layout)
    {
        try
        {
            var json = _serializer.Serialize(layout);

            // Atomic write: write to temp file then move to prevent corruption
            var tempPath = _layoutPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _layoutPath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Best-effort: log error but don't crash
            System.Diagnostics.Debug.WriteLine($"Failed to save layout: {ex.Message}");
        }
    }

    public IRootDock? Load()
    {
        if (!File.Exists(_layoutPath))
            return null;

        try
        {
            var json = File.ReadAllText(_layoutPath);
            return _serializer.Deserialize<IRootDock>(json);
        }
        catch (Exception ex)
        {
            // Corrupted layout → fall back to default
            System.Diagnostics.Debug.WriteLine($"Failed to load layout: {ex.Message}");
            return null;
        }
    }
}