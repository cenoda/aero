namespace Aero.Models.Settings;

/// <summary>
/// User preferences model — font, theme, tab size, layout mode.
/// Stored at <c>~/.aero/settings.json</c>.
/// </summary>
public record SettingsModel
{
    public string Theme { get; init; } = "Light";
    public string FontFamily { get; init; } = "Inter";
    public int FontSize { get; init; } = 13;
    public int TabSize { get; init; } = 4;
    public string LayoutMode { get; init; } = "Tile";

    // Panel state (Phase 8.1 M7)
    public bool IsSidebarVisible { get; init; } = true;
    public bool IsBottomPanelVisible { get; init; } = true;
    public double SidebarWidth { get; init; } = 250;
    public double BottomPanelHeight { get; init; } = 150;
}
