using System;
using System.IO;

namespace Aero.Core;

/// <summary>
/// Simple file logger for GUI debugging. Writes to ~/.aero/debug.log
/// </summary>
public static class DebugLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".aero", "debug.log");
    
    static DebugLogger()
    {
        var dir = Path.GetDirectoryName(LogPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
    
    public static void Log(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            File.AppendAllText(LogPath, $"[{timestamp}] {message}\n");
        }
        catch
        {
            // Ignore logging errors
        }
    }
    
    public static void Clear()
    {
        try { File.WriteAllText(LogPath, ""); } catch { }
    }
}
