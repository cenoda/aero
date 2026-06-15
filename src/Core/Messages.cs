namespace Aero.Core;

// ---------------------------------------------------------------------------
// Editor messages
// ---------------------------------------------------------------------------

/// <summary>A document was opened in the editor.</summary>
public record DocumentOpened(string FilePath);

/// <summary>A document was closed.</summary>
public record DocumentClosed(string FilePath);

/// <summary>The active (focused) document changed.</summary>
public record ActiveDocumentChanged(string? FilePath);

// ---------------------------------------------------------------------------
// Build messages
// ---------------------------------------------------------------------------

/// <summary>A build was started for the given project file.</summary>
public record BuildStarted(string Project);

/// <summary>A build finished with an exit code and captured output.</summary>
public record BuildFinished(int ExitCode, string Output);

// ---------------------------------------------------------------------------
// UI messages
// ---------------------------------------------------------------------------

/// <summary>The application theme was changed.</summary>
public record ThemeChanged(string ThemeName);

/// <summary>A folder was opened as the workspace root.</summary>
public record FolderOpened(string Path);
