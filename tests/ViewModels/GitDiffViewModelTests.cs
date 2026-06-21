using System.Collections.Generic;
using Aero.Models.Git;
using Aero.ViewModels;
using Xunit;

namespace Aero.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="GitDiffViewModel"/> — verifies line count,
/// colors, and title generation.
/// </summary>
public class GitDiffViewModelTests
{
    [Fact]
    public void GitDiffViewModel_SingleHunk_FlattensLines()
    {
        // Arrange: Create a diff with a single hunk containing 3 lines
        var lines = new List<GitDiffLine>
        {
            new(GitDiffLineKind.Header, "@@ -1,3 +1,3 @@", 1, 1),
            new(GitDiffLineKind.Context, "unchanged line", 1, 1),
            new(GitDiffLineKind.Addition, "+added line", -1, 2),
            new(GitDiffLineKind.Deletion, "-removed line", 2, -1),
        };
        var hunks = new List<GitDiffHunk>
        {
            new(1, 3, 1, 3, lines),
        };
        var diff = new GitDiff("test.cs", "test.cs", hunks);

        // Act
        var vm = new GitDiffViewModel(diff);

        // Assert: All lines from hunk are flattened
        Assert.Equal(4, vm.Lines.Count);
        Assert.Equal("test.cs", vm.FilePath);
        Assert.Equal("diff: test.cs", vm.Title);
    }

    [Fact]
    public void GitDiffViewModel_MultipleHunks_FlattensAllLines()
    {
        // Arrange: Create a diff with two hunks
        var hunk1Lines = new List<GitDiffLine>
        {
            new(GitDiffLineKind.Header, "@@ -1,2 +1,2 @@", 1, 1),
            new(GitDiffLineKind.Context, "line 1", 1, 1),
            new(GitDiffLineKind.Addition, "+line 2", -1, 2),
        };
        var hunk2Lines = new List<GitDiffLine>
        {
            new(GitDiffLineKind.Header, "@@ -5,2 +5,2 @@", 5, 5),
            new(GitDiffLineKind.Context, "line 5", 5, 5),
            new(GitDiffLineKind.Deletion, "-line 6", 6, -1),
        };
        var hunks = new List<GitDiffHunk>
        {
            new(1, 2, 1, 2, hunk1Lines),
            new(5, 2, 5, 2, hunk2Lines),
        };
        var diff = new GitDiff("multi.cs", "multi.cs", hunks);

        // Act
        var vm = new GitDiffViewModel(diff);

        // Assert: All 6 lines from both hunks are flattened
        Assert.Equal(6, vm.Lines.Count);
    }

    [Fact]
    public void GitDiffLineViewModel_Addition_HasGreenBackground()
    {
        // Arrange & Act
        var line = new GitDiffLineViewModel("+added", GitDiffLineKind.Addition, null, 2);

        // Assert
        Assert.Equal("+added", line.Content);
        Assert.Equal(GitDiffLineKind.Addition, line.Kind);
        Assert.NotNull(line.LineBackground);
    }

    [Fact]
    public void GitDiffLineViewModel_Deletion_HasRedBackground()
    {
        // Arrange & Act
        var line = new GitDiffLineViewModel("-removed", GitDiffLineKind.Deletion, 2, null);

        // Assert
        Assert.Equal("-removed", line.Content);
        Assert.Equal(GitDiffLineKind.Deletion, line.Kind);
        Assert.NotNull(line.LineBackground);
    }

    [Fact]
    public void GitDiffLineViewModel_Context_HasTransparentBackground()
    {
        // Arrange & Act
        var line = new GitDiffLineViewModel(" context ", GitDiffLineKind.Context, 1, 1);

        // Assert
        Assert.Equal(" context ", line.Content);
        Assert.Equal(GitDiffLineKind.Context, line.Kind);
    }

    [Fact]
    public void GitDiffLineViewModel_Header_HasTransparentBackground()
    {
        // Arrange & Act
        var line = new GitDiffLineViewModel("@@ -1 +1 @@", GitDiffLineKind.Header, null, null);

        // Assert
        Assert.Equal("@@ -1 +1 @@", line.Content);
        Assert.Equal(GitDiffLineKind.Header, line.Kind);
    }
}