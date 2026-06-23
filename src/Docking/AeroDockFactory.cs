using System;
using System.Diagnostics;
using Aero.Docking.DocumentViewModels;
using Aero.Docking.ToolViewModels;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using Alignment = Dock.Model.Core.Alignment;

namespace Aero.Docking;

/// <summary>
/// M1: Factory-driven layout builder for Phase 8.1a dockable panels.
/// Wraps <see cref="Dock.Model.ReactiveUI.Factory"/> to create the 5-zone layout
/// using thin Tool/Document subclasses for DataTemplate type discrimination.
///
/// Scope reduction (TOFIX): Container model classes (AeroRootDock, etc.) are NOT
/// created — the concrete types from Dock.Model.ReactiveUI.Controls work correctly,
/// proven by M0.5. Re-implementing interfaces would be dead boilerplate.
/// </summary>
public static class AeroDockFactory
{
    public static readonly Factory Factory = new();

    internal static class Proportions
    {
        public const double LeftSidebar = 0.22;
        public const double CenterStack = 0.78;
        public const double EditorRow = 0.72;
        public const double BottomRow = 0.28;
    }

    public static IRootDock CreateDefaultLayout()
    {
        Debug.WriteLine("[Dock] M1: CreateDefaultLayout() started");

        var root = Factory.CreateRootDock();
        Debug.WriteLine($"[Dock] Created RootDock: {root.GetType().Name}");

        // ── Root proportional: Horizontal ──
        var proportional = Factory.CreateProportionalDock();
        proportional.Orientation = Orientation.Horizontal;

        // ── Left sidebar (22%) ──
        var left = CreateLeftSidebar();

        // ── Splitter ──
        var splitter = Factory.CreateProportionalDockSplitter();
        splitter.CanResize = true;
        Debug.WriteLine("[Dock] Created ProportionalDockSplitter");

        // ── Right stack (78%): editor + bottom panel ──
        var right = CreateRightStack();

        // ── Assemble root ──
        Factory.AddDockable(proportional, left);
        Factory.AddDockable(proportional, splitter);
        Factory.AddDockable(proportional, right);
        Factory.AddDockable(root, proportional);

        Debug.WriteLine("[Dock] M1: CreateDefaultLayout() completed");
        DumpTree(root, 0);
        return root;
    }

    private static IDockable CreateLeftSidebar()
    {
        var leftProportional = Factory.CreateProportionalDock();
        leftProportional.Orientation = Orientation.Vertical;
        leftProportional.Proportion = Proportions.LeftSidebar;

        var leftToolDock = Factory.CreateToolDock();
        leftToolDock.Alignment = Alignment.Left;
        leftToolDock.IsExpanded = true;

        var explorerTool = new ExplorerTool
        {
            Id = "explorer", Title = "Explorer", CanClose = true
        };
        var gitTool = new GitTool
        {
            Id = "git", Title = "Git", CanClose = true
        };

        Factory.AddDockable(leftToolDock, explorerTool);
        Factory.AddDockable(leftToolDock, gitTool);
        Factory.AddDockable(leftProportional, leftToolDock);

        Debug.WriteLine("[Dock] Left sidebar: ExplorerTool + GitTool");
        return leftProportional;
    }

    private static IDockable CreateRightStack()
    {
        var rightProportional = Factory.CreateProportionalDock();
        rightProportional.Orientation = Orientation.Vertical;
        rightProportional.Proportion = Proportions.CenterStack;

        // ── Editor (72%) ──
        var editorProportional = Factory.CreateProportionalDock();
        editorProportional.Orientation = Orientation.Vertical;
        editorProportional.Proportion = Proportions.EditorRow;

        var documentDock = Factory.CreateDocumentDock();
        documentDock.CanCreateDocument = false;

        var editorDocument = new EditorDocument
        {
            Id = "editor", Title = "Editor", CanClose = true
        };

        Factory.AddDockable(documentDock, editorDocument);
        Factory.AddDockable(editorProportional, documentDock);

        var editorBottomSplitter = Factory.CreateProportionalDockSplitter();
        editorBottomSplitter.CanResize = true;

        // ── Bottom (28%) ──
        var bottomProportional = Factory.CreateProportionalDock();
        bottomProportional.Orientation = Orientation.Vertical;
        bottomProportional.Proportion = Proportions.BottomRow;

        var bottomToolDock = Factory.CreateToolDock();
        bottomToolDock.Alignment = Alignment.Bottom;
        bottomToolDock.IsExpanded = true;

        var problemsTool = new ProblemsTool
        {
            Id = "problems", Title = "Problems", CanClose = true
        };
        var outputTool = new OutputTool
        {
            Id = "output", Title = "Output", CanClose = true
        };

        Factory.AddDockable(bottomToolDock, problemsTool);
        Factory.AddDockable(bottomToolDock, outputTool);
        Factory.AddDockable(bottomProportional, bottomToolDock);

        Factory.AddDockable(rightProportional, editorProportional);
        Factory.AddDockable(rightProportional, editorBottomSplitter);
        Factory.AddDockable(rightProportional, bottomProportional);

        Debug.WriteLine("[Dock] Right stack: EditorDocument + ProblemsTool + OutputTool");
        return rightProportional;
    }

    private static void DumpTree(IDockable dockable, int depth)
    {
        var indent = new string(' ', depth * 2);
        var proportion = dockable.Proportion > 0 ? $" (P={dockable.Proportion:F2})" : "";
        Debug.WriteLine($"[Dock] {indent}{dockable.GetType().Name} id={dockable.Id}{proportion}");

        if (dockable is IDock dock && dock.VisibleDockables != null)
        {
            foreach (var child in dock.VisibleDockables)
                DumpTree(child, depth + 1);
        }
    }
}
