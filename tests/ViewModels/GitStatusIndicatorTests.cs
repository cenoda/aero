using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Languages;
using Aero.Models.Git;
using Aero.Services;
using Aero.Tests.Stubs;
using Aero.ViewModels;
using Xunit;

namespace Aero.Tests.ViewModels;

/// <summary>
/// Tests for Git status indicators in EditorViewModel and FileExplorerViewModel.
/// Verifies that GitStatusGlyph is set correctly when GitStatusChanged fires.
/// </summary>
public class GitStatusIndicatorTests
{
    private static (EditorViewModel vm, StubMessageBus bus, IDocumentManagementService dm) CreateEditor()
    {
        var bus = new StubMessageBus();
        var languageDetection = new LanguageDetectionService();
        var dm = new DocumentManager(bus, languageDetection);
        var findReplace = new FindReplaceViewModel();
        var diagnosticStore = new DiagnosticStore(bus);
        var vm = new EditorViewModel(dm, bus, findReplace, languageDetection, diagnosticStore);
        return (vm, bus, dm);
    }

    [Fact]
    public async Task EditorViewModel_GitStatusChanged_SetsStagedGlyph()
    {
        var (vm, bus, dm) = CreateEditor();
        var tempDir = Path.GetTempPath();
        var fileName = $"test_{Guid.NewGuid():N}.cs";
        var filePath = Path.Combine(tempDir, fileName);

        // Create the file so it can be opened
        await File.WriteAllTextAsync(filePath, "// test");

        try
        {
            // Open a file in the editor
            await vm.OpenFileAsync(filePath);

            var tab = vm.Tabs.FirstOrDefault();
            Assert.NotNull(tab);

            // Publish GitStatusChanged with staged file
            var stagedFiles = new List<GitFileStatus>
            {
                new(fileName, null, GitFileStatusKind.Staged, GitFileStatusKind.Unmodified)
            };
            var unstagedFiles = new List<GitFileStatus>();
            bus.Publish(new GitStatusChanged(tempDir, stagedFiles, unstagedFiles, "main"));

            // Verify glyph is set to "A" (staged/added)
            Assert.Equal("A", tab.GitStatusGlyph);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task EditorViewModel_GitStatusChanged_SetsModifiedGlyph()
    {
        var (vm, bus, dm) = CreateEditor();
        var tempDir = Path.GetTempPath();
        var fileName = $"test_{Guid.NewGuid():N}.cs";
        var filePath = Path.Combine(tempDir, fileName);

        // Create the file so it can be opened
        await File.WriteAllTextAsync(filePath, "// test");

        try
        {
            // Open a file in the editor
            await vm.OpenFileAsync(filePath);

            var tab = vm.Tabs.FirstOrDefault();
            Assert.NotNull(tab);

            // Publish GitStatusChanged with unstaged file
            var stagedFiles = new List<GitFileStatus>();
            var unstagedFiles = new List<GitFileStatus>
            {
                new(fileName, null, GitFileStatusKind.Staged, GitFileStatusKind.Modified)
            };
            bus.Publish(new GitStatusChanged(tempDir, stagedFiles, unstagedFiles, "main"));

            // Verify glyph is set to "M" (modified)
            Assert.Equal("M", tab.GitStatusGlyph);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task EditorViewModel_GitStatusChanged_PrefersStagedOverUnstaged()
    {
        var (vm, bus, dm) = CreateEditor();
        var tempDir = Path.GetTempPath();
        var fileName = $"test_{Guid.NewGuid():N}.cs";
        var filePath = Path.Combine(tempDir, fileName);

        // Create the file so it can be opened
        await File.WriteAllTextAsync(filePath, "// test");

        try
        {
            // Open a file in the editor
            await vm.OpenFileAsync(filePath);

            var tab = vm.Tabs.FirstOrDefault();
            Assert.NotNull(tab);

            // Publish GitStatusChanged with file in both staged and unstaged
            var stagedFiles = new List<GitFileStatus>
            {
                new(fileName, null, GitFileStatusKind.Staged, GitFileStatusKind.Unmodified)
            };
            var unstagedFiles = new List<GitFileStatus>
            {
                new(fileName, null, GitFileStatusKind.Staged, GitFileStatusKind.Modified)
            };
            bus.Publish(new GitStatusChanged(tempDir, stagedFiles, unstagedFiles, "main"));

            // Verify staged takes precedence
            Assert.Equal("A", tab.GitStatusGlyph);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task EditorViewModel_GitStatusChanged_ClearsGlyphWhenUntracked()
    {
        var (vm, bus, dm) = CreateEditor();
        var tempDir = Path.GetTempPath();
        var fileName = $"test_{Guid.NewGuid():N}.cs";
        var filePath = Path.Combine(tempDir, fileName);

        // Create the file so it can be opened
        await File.WriteAllTextAsync(filePath, "// test");

        try
        {
            // Open a file in the editor
            await vm.OpenFileAsync(filePath);

            var tab = vm.Tabs.FirstOrDefault();
            Assert.NotNull(tab);

            // First set a glyph
            var stagedFiles = new List<GitFileStatus>
            {
                new(fileName, null, GitFileStatusKind.Staged, GitFileStatusKind.Unmodified)
            };
            var unstagedFiles = new List<GitFileStatus>();
            bus.Publish(new GitStatusChanged(tempDir, stagedFiles, unstagedFiles, "main"));
            Assert.Equal("A", tab.GitStatusGlyph);

            // Then publish with no changes for that file
            stagedFiles = new List<GitFileStatus>();
            unstagedFiles = new List<GitFileStatus>();
            bus.Publish(new GitStatusChanged(tempDir, stagedFiles, unstagedFiles, "main"));

            // Verify glyph is cleared
            Assert.Equal("", tab.GitStatusGlyph);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task EditorViewModel_GitRepositoryChanged_ClearsAllGlyphs()
    {
        var (vm, bus, dm) = CreateEditor();
        var tempDir = Path.GetTempPath();
        var fileName = $"test_{Guid.NewGuid():N}.cs";
        var filePath = Path.Combine(tempDir, fileName);

        // Create the file so it can be opened
        await File.WriteAllTextAsync(filePath, "// test");

        try
        {
            // Open a file in the editor
            await vm.OpenFileAsync(filePath);

            var tab = vm.Tabs.FirstOrDefault();
            Assert.NotNull(tab);

            // Set a glyph first
            var stagedFiles = new List<GitFileStatus>
            {
                new(fileName, null, GitFileStatusKind.Staged, GitFileStatusKind.Unmodified)
            };
            var unstagedFiles = new List<GitFileStatus>();
            bus.Publish(new GitStatusChanged(tempDir, stagedFiles, unstagedFiles, "main"));
            Assert.Equal("A", tab.GitStatusGlyph);

            // Publish repository closed
            bus.Publish(new GitRepositoryChanged(tempDir, false));

            // Verify glyph is cleared
            Assert.Equal("", tab.GitStatusGlyph);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}