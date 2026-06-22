using System;
using System.Collections.Generic;
using Dock.Avalonia.Controls;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace Aero.Docking;

/// <summary>
/// Factory for creating the default dock layout.
/// TODO M1.1: Implement proper layout creation after Dock.Avalonia API verification.
/// </summary>
public static class AeroDockFactory
{
    public static IRootDock CreateDefaultLayout()
    {
        // TODO M1.1: Create proper layout tree
        // For now, return null - DockControl will use default layout
        // This allows the build to pass while we verify the Dock.Avalonia API
        return null!;
    }
}
