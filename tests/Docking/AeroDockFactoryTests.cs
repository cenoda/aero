using System.Collections.Generic;
using System.Linq;
using Aero.Docking;
using Aero.Docking.DocumentViewModels;
using Aero.Docking.ToolViewModels;
using Dock.Model.Controls;
using Dock.Model.Core;
using Xunit;

namespace Aero.Tests.Docking;

/// <summary>
/// T1.7: Automated coverage for <see cref="AeroDockFactory"/>.
/// Also addresses T1.5 (proportion verification).
/// </summary>
public class AeroDockFactoryTests
{
    private static IRootDock CreateLayout() => AeroDockFactory.CreateDefaultLayout();

    [Fact]
    public void CreateDefaultLayout_HasOneRootChild()
    {
        var root = CreateLayout();
        Assert.NotNull(root.VisibleDockables);
        Assert.Single(root.VisibleDockables);
    }

    [Fact]
    public void LayoutTree_ContainsAllFiveDockables()
    {
        var root = CreateLayout();
        var all = WalkTree(root).ToList();

        Assert.Contains(all, d => d is ExplorerTool && d.Id == "explorer");
        Assert.Contains(all, d => d is GitTool && d.Id == "git");
        Assert.Contains(all, d => d is EditorDocument && d.Id == "editor");
        Assert.Contains(all, d => d is ProblemsTool && d.Id == "problems");
        Assert.Contains(all, d => d is OutputTool && d.Id == "output");
    }

    [Fact]
    public void LayoutTree_DockablesHaveExpectedAlignment()
    {
        var root = CreateLayout();
        var all = WalkTree(root).ToList();

        var toolDocks = all.OfType<Dock.Model.ReactiveUI.Controls.ToolDock>().ToList();
        Assert.Equal(2, toolDocks.Count);

        var leftDock = toolDocks.FirstOrDefault(t => t.Alignment == Alignment.Left);
        var bottomDock = toolDocks.FirstOrDefault(t => t.Alignment == Alignment.Bottom);

        Assert.NotNull(leftDock);
        Assert.NotNull(bottomDock);

        Assert.Equal("explorer", leftDock.VisibleDockables?.FirstOrDefault()?.Id);
        Assert.Equal("problems", bottomDock.VisibleDockables?.FirstOrDefault()?.Id);
    }

    [Fact]
    public void LayoutTree_ProportionsAreCorrect()
    {
        var root = CreateLayout();

        // Root has one child: a horizontal ProportionalDock
        var rootChild = Assert.Single(root.VisibleDockables!);
        var horizProportional = Assert.IsAssignableFrom<IProportionalDock>(rootChild);
        Assert.Equal(Orientation.Horizontal, horizProportional.Orientation);
        Assert.Equal(3, horizProportional.VisibleDockables!.Count);

        // Left child: Proportion 0.22
        var left = horizProportional.VisibleDockables[0];
        Assert.True(left.Proportion > 0.21 && left.Proportion < 0.23,
            $"Expected ~0.22, got {left.Proportion}");

        // Right child: Proportion 0.78
        var right = horizProportional.VisibleDockables[2];
        Assert.True(right.Proportion > 0.77 && right.Proportion < 0.79,
            $"Expected ~0.78, got {right.Proportion}");

        // Right child is vertical ProportionalDock with editor + bottom
        var rightProportional = Assert.IsAssignableFrom<IProportionalDock>(right);
        Assert.Equal(Orientation.Vertical, rightProportional.Orientation);
        Assert.Equal(3, rightProportional.VisibleDockables!.Count);

        // Editor (first child): Proportion 0.72
        var editor = rightProportional.VisibleDockables[0];
        Assert.True(editor.Proportion > 0.71 && editor.Proportion < 0.73,
            $"Expected ~0.72, got {editor.Proportion}");

        // Bottom (third child): Proportion 0.28
        var bottom = rightProportional.VisibleDockables[2];
        Assert.True(bottom.Proportion > 0.27 && bottom.Proportion < 0.29,
            $"Expected ~0.28, got {bottom.Proportion}");
    }

    [Fact]
    public void LayoutTree_ContextsAreSetToM2PendingPlaceholder()
    {
        var root = CreateLayout();
        var tools = WalkTree(root)
            .Where(d => d is ExplorerTool or GitTool or ProblemsTool or OutputTool or EditorDocument)
            .ToList();

        Assert.Equal(5, tools.Count);
        Assert.All(tools, d => Assert.Equal("M2-pending", d.Context));
    }

    [Fact]
    public void LayoutTree_ToolDocksHaveActiveDockableSet()
    {
        var root = CreateLayout();
        var toolDocks = WalkTree(root)
            .OfType<Dock.Model.ReactiveUI.Controls.ToolDock>()
            .ToList();

        Assert.Equal(2, toolDocks.Count);
        Assert.All(toolDocks, td => Assert.NotNull(td.ActiveDockable));
    }

    private static IEnumerable<IDockable> WalkTree(IDockable root)
    {
        yield return root;

        if (root is IDock dock && dock.VisibleDockables != null)
        {
            foreach (var child in dock.VisibleDockables)
            {
                foreach (var descendant in WalkTree(child))
                    yield return descendant;
            }
        }
    }
}
