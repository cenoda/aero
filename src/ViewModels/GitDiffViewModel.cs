using System.Collections.Generic;
using System.Linq;
using Aero.Models.Git;
using Avalonia.Media;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for a single diff line in the view.
/// </summary>
public class GitDiffLineViewModel
{
    public GitDiffLineViewModel(string content, GitDiffLineKind kind, int? oldLineNumber, int? newLineNumber)
    {
        Content = content;
        Kind = kind;
        OldLineNumber = oldLineNumber;
        NewLineNumber = newLineNumber;
        Gutter = kind switch
        {
            GitDiffLineKind.Addition => "+",
            GitDiffLineKind.Deletion => "-",
            GitDiffLineKind.Header => "@@",
            _ => " "
        };
        // Issue #2 fix: Use contrasting foreground colors for gutter text visibility
        GutterForeground = kind switch
        {
            GitDiffLineKind.Addition => new SolidColorBrush(0xFF008000), // Dark green
            GitDiffLineKind.Deletion => new SolidColorBrush(0xFF800000), // Dark red
            GitDiffLineKind.Header => new SolidColorBrush(0xFF0000FF), // Dark blue
            _ => new SolidColorBrush(0xFF808080) // Gray for context
        };
        LineBackground = kind switch
        {
            GitDiffLineKind.Addition => new SolidColorBrush(0x2000FF00), // Green with alpha
            GitDiffLineKind.Deletion => new SolidColorBrush(0x20FF0000), // Red with alpha
            GitDiffLineKind.Header => new SolidColorBrush(0x200000FF), // Blue with alpha
            _ => Brushes.Transparent
        };
        IsHeader = kind == GitDiffLineKind.Header;
    }

    /// <summary>The text content of the line.</summary>
    public string Content { get; }

    /// <summary>The kind of diff line (context, addition, deletion, header).</summary>
    public GitDiffLineKind Kind { get; }

    /// <summary>Background brush based on line kind.</summary>
    public IBrush LineBackground { get; }

    /// <summary>Gutter character (+, -, or space).</summary>
    public string Gutter { get; }

    /// <summary>Foreground brush for gutter text (contrasting color for visibility).</summary>
    public IBrush GutterForeground { get; }

    /// <summary>Old file line number (1-based), null if not applicable.</summary>
    public int? OldLineNumber { get; }

    /// <summary>New file line number (1-based), null if not applicable.</summary>
    public int? NewLineNumber { get; }

    /// <summary>Whether this is a hunk header line.</summary>
    public bool IsHeader { get; }
}

/// <summary>
/// ViewModel for displaying a unified diff in an editor tab.
/// </summary>
public class GitDiffViewModel
{
    public GitDiffViewModel(GitDiff diff)
    {
        FilePath = diff.FilePath;
        Title = $"diff: {System.IO.Path.GetFileName(diff.FilePath)}";

        // Flatten all hunks into a single list of lines with line numbers
        // R1.8: GitDiffLine has 1-based line numbers from LibGit2Sharp.
        // If clicking a diff line navigates to the editor, subtract 1 before passing to TextRange.
        var lines = new List<GitDiffLineViewModel>();
        foreach (var hunk in diff.Hunks)
        {
            // Track line numbers within this hunk
            int oldLine = hunk.OldStart;
            int newLine = hunk.NewStart;
            foreach (var line in hunk.Lines)
            {
                int? oldNum = null;
                int? newNum = null;
                switch (line.Kind)
                {
                    case GitDiffLineKind.Context:
                        oldNum = oldLine++;
                        newNum = newLine++;
                        break;
                    case GitDiffLineKind.Deletion:
                        oldNum = oldLine++;
                        break;
                    case GitDiffLineKind.Addition:
                        newNum = newLine++;
                        break;
                    case GitDiffLineKind.Header:
                        // Header lines don't have line numbers
                        break;
                }
                lines.Add(new GitDiffLineViewModel(line.Content, line.Kind, oldNum, newNum));
            }
        }
        Lines = lines;
    }

    /// <summary>The file path being diffed.</summary>
    public string FilePath { get; }

    /// <summary>Display title for the tab header.</summary>
    public string Title { get; }

    /// <summary>All lines in the diff, flattened from hunks.</summary>
    public IReadOnlyList<GitDiffLineViewModel> Lines { get; }
}