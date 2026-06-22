namespace Aero.Models.Settings;

/// <summary>
/// Window position and size model — persisted across restarts
/// so the IDE reopens at the same location and dimensions.
/// </summary>
public record WindowState
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 1200;
    public double Height { get; init; } = 800;
    public bool IsMaximized { get; init; }
}
