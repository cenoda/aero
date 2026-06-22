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
}
