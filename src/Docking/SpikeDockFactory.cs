using System;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using Avalonia.Controls;
using Alignment = Dock.Model.Core.Alignment;

namespace Aero.Docking;

/// <summary>
/// M0.5: Creates a minimal dock layout for the spike test.
/// Uses Dock.Model.ReactiveUI.Factory to create and manage dockables.
/// </summary>
public static class SpikeDockFactory
{
    /// <summary>
    /// Creates a minimal spike layout: left tool dock + right document dock with splitter.
    /// </summary>
    public static IRootDock CreateSpikeLayout()
    {
        // Use ReactiveUI Factory to create dockables
        var factory = new Factory();

        var root = factory.CreateRootDock();

        // Horizontal proportional dock: left (30%) | splitter | right (70%)
        var proportional = factory.CreateProportionalDock();
        proportional.Orientation = Orientation.Horizontal;

        // Left: ToolDock with two tools
        var leftProportional = factory.CreateProportionalDock();
        leftProportional.Orientation = Orientation.Vertical;
        leftProportional.Proportion = 0.3;

        var leftToolDock = factory.CreateToolDock();
        leftToolDock.Alignment = Alignment.Left;
        leftToolDock.IsExpanded = true;

        var toolA = factory.CreateTool();
        toolA.Id = "tool-a";
        toolA.Title = "Tool A";
        toolA.CanClose = true;

        var toolB = factory.CreateTool();
        toolB.Id = "tool-b";
        toolB.Title = "Tool B";
        toolB.CanClose = true;

        // Use factory to add dockables
        factory.AddDockable(leftToolDock, toolA);
        factory.AddDockable(leftToolDock, toolB);
        factory.AddDockable(leftProportional, leftToolDock);

        // Splitter
        var splitter = factory.CreateProportionalDockSplitter();
        splitter.CanResize = true;

        // Right: DocumentDock with one document
        var rightProportional = factory.CreateProportionalDock();
        rightProportional.Orientation = Orientation.Vertical;
        rightProportional.Proportion = 0.7;

        var documentDock = factory.CreateDocumentDock();
        documentDock.CanCreateDocument = false;

        var docA = factory.CreateDocument();
        docA.Id = "doc-a";
        docA.Title = "Doc A";
        docA.CanClose = true;

        factory.AddDockable(documentDock, docA);
        factory.AddDockable(rightProportional, documentDock);

        // Build tree
        factory.AddDockable(proportional, leftProportional);
        factory.AddDockable(proportional, splitter);
        factory.AddDockable(proportional, rightProportional);

        factory.AddDockable(root, proportional);

        return root;
    }
}