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
    public GitDiffLineViewModel(string content, GitDiffLineKind kind)
    {
        Content = content;
        Kind = kind;
        LineBackground = kind switch
        {
            GitDiffLineKind.Addition => new SolidColorBrush(0x2000FF00), // Green with alpha
            GitDiffLineKind.Deletion => new SolidColorBrush(0x20FF0000), // Red with alpha
            _ => Brushes.Transparent
        };
    }

    /// <summary>The text content of the line.</summary>
    public string Content { get; }

    /// <summary>The kind of diff line (context, addition, deletion, header).</summary>
    public GitDiffLineKind Kind { get; }

    /// <summary>Background brush based on line kind.</summary>
    public IBrush LineBackground { get; }
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

        // Flatten all hunks into a single list of lines
        // R1.8: GitDiffLine has 1-based line numbers from LibGit2Sharp.
        // If clicking a diff line navigates to the editor, subtract 1 before passing to TextRange.
        var lines = new List<GitDiffLineViewModel>();
        foreach (var hunk in diff.Hunks)
        {
            foreach (var line in hunk.Lines)
            {
                lines.Add(new GitDiffLineViewModel(line.Content, line.Kind));
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