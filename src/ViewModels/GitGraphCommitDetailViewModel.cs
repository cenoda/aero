using System;
using System.Collections.Generic;
using Aero.Services.Git;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for the commit detail pane shown when a commit node is clicked
/// in the branch graph. Displays SHA, author, date, message, and branch labels.
/// </summary>
public class GitGraphCommitDetailViewModel : ReactiveObject
{
    [Reactive] public bool IsVisible { get; set; }
    [Reactive] public string ShortSha { get; set; } = string.Empty;
    [Reactive] public string FullSha { get; set; } = string.Empty;
    [Reactive] public string Author { get; set; } = string.Empty;
    [Reactive] public string Date { get; set; } = string.Empty;
    [Reactive] public string Message { get; set; } = string.Empty;
    [Reactive] public IReadOnlyList<string> BranchLabels { get; set; } = Array.Empty<string>();

    /// <summary>Populates fields from a commit and shows the pane.</summary>
    public void Show(GitGraphCommit commit)
    {
        if (commit == null)
        {
            Hide();
            return;
        }
        FullSha = commit.Sha;
        ShortSha = commit.Sha.Length >= 7 ? commit.Sha[..7] : commit.Sha;
        Author = commit.Author;
        Date = commit.AuthorDate.ToString("yyyy-MM-dd HH:mm");
        Message = commit.Message;
        BranchLabels = commit.BranchLabels;
        IsVisible = true;
    }

    /// <summary>Clears fields and hides the pane.</summary>
    public void Hide()
    {
        IsVisible = false;
        ShortSha = string.Empty;
        FullSha = string.Empty;
        Author = string.Empty;
        Date = string.Empty;
        Message = string.Empty;
        BranchLabels = Array.Empty<string>();
    }
}
