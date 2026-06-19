using System;

namespace Aero.Core;

// ---------------------------------------------------------------------------
// Editor messages
// ---------------------------------------------------------------------------

/// <summary>A document was opened in the editor.</summary>
public record DocumentOpened(string FilePath);

/// <summary>A document was closed.</summary>
public record DocumentClosed(string FilePath, Aero.Models.Editor.TextDocument Document);

/// <summary>The active (focused) document changed.</summary>
public record ActiveDocumentChanged(Aero.Models.Editor.TextDocument? Document);

/// <summary>A document was modified (has unsaved changes).</summary>
public record DocumentModified(string FilePath, Aero.Models.Editor.TextDocument Document);

/// <summary>A document's text changed (per-keystroke signal for LSP sync).</summary>
public record DocumentTextChanged(Aero.Models.Editor.TextDocument Document);

/// <summary>A document was saved.</summary>
public record DocumentSaved(string FilePath, Aero.Models.Editor.TextDocument Document);

/// <summary>A document is about to close (can be cancelled).</summary>
public record DocumentClosing(string FilePath);

/// <summary>
/// Prompt user for dirty document close decision.
/// Response: "Save", "Don'tSave", or "Cancel".
/// </summary>
public record ConfirmDirtyClose(
    string FileName,
    Action<string> OnResponse);

/// <summary>
/// Response values for ConfirmDirtyClose.
/// </summary>
public static class DirtyCloseResponse
{
    public const string Save = "Save";
    public const string DontSave = "Don'tSave";
    public const string Cancel = "Cancel";
}

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

/// <summary>
/// The file system under the watched workspace root has changed. Consumers
/// should refresh the affected folder. The message is intentionally flat
/// (no change kind) — per PROJECT_PLAN §5.5 / TOFIX R1.3, that detail is
/// deferred until Phase 7 when Git status badges need it.
/// </summary>
public record FolderChanged(string Path);

/// <summary>
/// A transient status message for the status bar or log. Published by services
/// that need to surface non-fatal warnings without referencing the shell.
/// </summary>
public record StatusMessage(string Text);

// ---------------------------------------------------------------------------
// File explorer user-prompt messages
// ---------------------------------------------------------------------------

/// <summary>
/// Prompt the user for a name when creating a new file or folder.
/// <c>OnResult(null)</c> means the dialog was cancelled.
/// </summary>
public record PromptNewItem(
    string ParentPath,
    bool IsFile,
    Action<string?> OnResult);

/// <summary>
/// Prompt the user for a new name when renaming a file or folder.
/// <c>OnResult(null)</c> means the dialog was cancelled.
/// </summary>
public record PromptRename(
    string Path,
    Action<string?> OnResult);

/// <summary>
/// Confirm a destructive action (delete file/folder).
/// <c>OnResult(true)</c> = confirmed, <c>OnResult(false)</c> = rejected/cancelled.
/// </summary>
public record ConfirmDelete(
    string Path,
    Action<bool> OnResult);
