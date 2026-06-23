using Dock.Model.Controls;

namespace Aero.Services;

/// <summary>
/// Service for persisting dock layout state across IDE restarts.
/// </summary>
public interface ILayoutPersistenceService
{
    /// <summary>
    /// Saves the current dock layout to disk.
    /// </summary>
    void Save(IRootDock layout);

    /// <summary>
    /// Loads a previously saved dock layout, or null if none exists.
    /// </summary>
    IRootDock? Load();
}