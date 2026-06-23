using System;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using Avalonia.Controls;
using Alignment = Dock.Model.Core.Alignment;

namespace Aero.Docking;

/// <summary>
/// M0.5: Creates a minimal dock layout for the spike test.
/// Uses concrete types from Dock.Model.ReactiveUI (RootDock, ToolDock, DocumentDock, Tool, Document).
/// </summary>
public static class SpikeDockFactory
{
    /// <summary>
    /// Creates a minimal spike layout: left tool dock + right document dock with splitter.
    /// </summary>
    public static IRootDock CreateSpikeLayout()
    {
        var root = new RootDock();

        // Horizontal proportional dock: left (30%) | splitter | right (70%)
        var proportional = new ProportionalDock
        {
            Orientation = Orientation.Horizontal
        };

        // Left: ToolDock with two tools
        var leftProportional = new ProportionalDock
        {
            Orientation = Orientation.Vertical,
            Proportion = 0.3
        };

        var leftToolDock = new ToolDock
        {
            Alignment = Alignment.Left,
            IsExpanded = true
        };

        var toolA = new Tool
        {
            Id = "tool-a",
            Title = "Tool A",
            CanClose = true
        };

        var toolB = new Tool
        {
            Id = "tool-b",
            Title = "Tool B",
            CanClose = true
        };

        leftToolDock.VisibleDockables.Add(toolA);
        leftToolDock.VisibleDockables.Add(toolB);
        leftProportional.VisibleDockables.Add(leftToolDock);

        // Splitter
        var splitter = new ProportionalDockSplitter
        {
            CanResize = true
        };

        // Right: DocumentDock with one document
        var rightProportional = new ProportionalDock
        {
            Orientation = Orientation.Vertical,
            Proportion = 0.7
        };

        var documentDock = new DocumentDock
        {
            CanCreateDocument = false
        };

        var docA = new Document
        {
            Id = "doc-a",
            Title = "Doc A",
            CanClose = true
        };

        documentDock.VisibleDockables.Add(docA);
        rightProportional.VisibleDockables.Add(documentDock);

        // Build tree
        proportional.VisibleDockables.Add(leftProportional);
        proportional.VisibleDockables.Add(splitter);
        proportional.VisibleDockables.Add(rightProportional);

        root.VisibleDockables.Add(proportional);

        return root;
    }
}