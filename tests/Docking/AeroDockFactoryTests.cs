using System.Linq;
using Aero.Docking;
using Aero.Docking.DocumentViewModels;
using Aero.Docking.ToolViewModels;
using Dock.Model.Controls;
using Dock.Model.Core;
using Xunit;

namespace Aero.Tests.Docking;

/// <summary>
/// Tests for AeroDockFactory — verifies layout creation and structure.
/// </summary>
public class AeroDockFactoryTests
{
    [Fact]
    public void CreateDefaultLayout_Returns_ValidRootDock()
    {
        var layout = AeroDockFactory.CreateDefaultLayout();

        Assert.NotNull(layout);
        Assert.Equal("Root", layout.Id);
        Assert.NotNull(layout.VisibleDockables);
        Assert.NotEmpty(layout.VisibleDockables);
    }

    [Fact]
    public void CreateDefaultLayout_Contains_ExplorerTool()
    {
        var layout = AeroDockFactory.CreateDefaultLayout();
        var explorer = FindDockable(layout, "Explorer");

        Assert.NotNull(explorer);
        Assert.IsType<ExplorerTool>(explorer);
    }

    [Fact]
    public void CreateDefaultLayout_Contains_GitTool()
    {
        var layout = AeroDockFactory.CreateDefaultLayout();
        var git = FindDockable(layout, "Git");

        Assert.NotNull(git);
        Assert.IsType<GitTool>(git);
    }

    [Fact]
    public void CreateDefaultLayout_Contains_EditorDocument()
    {
        var layout = AeroDockFactory.CreateDefaultLayout();
        var editor = FindDockable(layout, "Editor");

        Assert.NotNull(editor);
        Assert.IsType<EditorDocument>(editor);
    }

    [Fact]
    public void CreateDefaultLayout_Contains_ProblemsTool()
    {
        var layout = AeroDockFactory.CreateDefaultLayout();
        var problems = FindDockable(layout, "Problems");

        Assert.NotNull(problems);
        Assert.IsType<ProblemsTool>(problems);
    }

    [Fact]
    public void CreateDefaultLayout_Contains_OutputTool()
    {
        var layout = AeroDockFactory.CreateDefaultLayout();
        var output = FindDockable(layout, "Output");

        Assert.NotNull(output);
        Assert.IsType<OutputTool>(output);
    }

    [Fact]
    public void CreateDefaultLayout_LeftZone_Has_ExplorerAndGit()
    {
        var layout = AeroDockFactory.CreateDefaultLayout();

        // Find the left IToolDock (Alignment.Left)
        var leftDock = FindToolDockByAlignment(layout, Alignment.Left);

        Assert.NotNull(leftDock);
        var ids = leftDock.VisibleDockables?.Select(d => d.Id).ToList();
        Assert.Contains("Explorer", ids);
        Assert.Contains("Git", ids);
    }

    [Fact]
    public void CreateDefaultLayout_BottomZone_Has_ProblemsAndOutput()
    {
        var layout = AeroDockFactory.CreateDefaultLayout();

        var bottomDock = FindToolDockByAlignment(layout, Alignment.Bottom);

        Assert.NotNull(bottomDock);
        var ids = bottomDock.VisibleDockables?.Select(d => d.Id).ToList();
        Assert.Contains("Problems", ids);
        Assert.Contains("Output", ids);
    }

    [Fact]
    public void CreateDefaultLayout_CenterZone_Has_EditorDocument()
    {
        var layout = AeroDockFactory.CreateDefaultLayout();

        var documentDock = FindDocumentDock(layout);

        Assert.NotNull(documentDock);
        var ids = documentDock.VisibleDockables?.Select(d => d.Id).ToList();
        Assert.Contains("Editor", ids);
    }

    [Fact]
    public void Factory_HasLayout_AfterInitLayout()
    {
        var layout = AeroDockFactory.CreateDefaultLayout();

        // After CreateDefaultLayout, the Factory reference should be set on each dockable
        var explorer = FindDockable(layout, "Explorer");
        Assert.NotNull(explorer?.Factory);
    }

    // --- Helpers ---

    private static IDockable? FindDockable(IDock dock, string id)
    {
        if (dock is IDockable d && d.Id == id) return d;
        if (dock.VisibleDockables == null) return null;
        foreach (var child in dock.VisibleDockables)
        {
            if (child is IDockable cd && cd.Id == id) return cd;
            if (child is IDock childDock)
            {
                var found = FindDockable(childDock, id);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static IToolDock? FindToolDockByAlignment(IDock dock, Alignment alignment)
    {
        if (dock is IToolDock toolDock && toolDock.Alignment == alignment) return toolDock;
        if (dock.VisibleDockables == null) return null;
        foreach (var child in dock.VisibleDockables)
        {
            if (child is IDock childDock)
            {
                var found = FindToolDockByAlignment(childDock, alignment);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static IDocumentDock? FindDocumentDock(IDock dock)
    {
        if (dock is IDocumentDock documentDock) return documentDock;
        if (dock.VisibleDockables == null) return null;
        foreach (var child in dock.VisibleDockables)
        {
            if (child is IDock childDock)
            {
                var found = FindDocumentDock(childDock);
                if (found != null) return found;
            }
        }
        return null;
    }
}
