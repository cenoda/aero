using System;
using System.Diagnostics;
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
/// 
/// NOTE: Pure XAML layout failed with AVLN2000 (nested generic types cause internal
/// compiler error). This C# approach tests the rendering engine but conflates two
/// variables: (1) Does Dock.Avalonia render? (2) Does the C# API produce valid layout?
/// </summary>
public static class SpikeDockFactory
{
    /// <summary>
    /// Static factory instance to prevent GC of dockable references.
    /// Issue 8: Previous local factory was disposed after method returned.
    /// </summary>
    public static readonly Factory Factory = new();

    /// <summary>
    /// Creates a minimal spike layout: left tool dock + right document dock with splitter.
    /// Issue 1: Sets Context on each tool/document for DataTemplate rendering.
    /// </summary>
    public static IRootDock CreateSpikeLayout()
    {
        Debug.WriteLine("[Dock] M0.5: CreateSpikeLayout() started");

        var root = Factory.CreateRootDock();
        Debug.WriteLine($"[Dock] Created RootDock: {root.GetType().Name}");

        // Horizontal proportional dock: left (30%) | splitter | right (70%)
        var proportional = Factory.CreateProportionalDock();
        proportional.Orientation = Orientation.Horizontal;
        Debug.WriteLine("[Dock] Created ProportionalDock (horizontal)");

        // Left: ToolDock with two tools
        var leftProportional = Factory.CreateProportionalDock();
        leftProportional.Orientation = Orientation.Vertical;
        leftProportional.Proportion = 0.3;
        Debug.WriteLine("[Dock] Created ProportionalDock (left, vertical, 30%)");

        var leftToolDock = Factory.CreateToolDock();
        leftToolDock.Alignment = Alignment.Left;
        leftToolDock.IsExpanded = true;
        Debug.WriteLine("[Dock] Created ToolDock (left)");

        // Issue 1: Set Context for DataTemplate rendering
        var toolA = Factory.CreateTool();
        toolA.Id = "tool-a";
        toolA.Title = "Tool A";
        toolA.CanClose = true;
        toolA.Context = "Tool A Content";  // For DataTemplate
        Debug.WriteLine("[Dock] Created Tool: tool-a (Tool A)");

        var toolB = Factory.CreateTool();
        toolB.Id = "tool-b";
        toolB.Title = "Tool B";
        toolB.CanClose = true;
        toolB.Context = "Tool B Content";  // For DataTemplate
        Debug.WriteLine("[Dock] Created Tool: tool-b (Tool B)");

        // Use factory to add dockables
        Factory.AddDockable(leftToolDock, toolA);
        Factory.AddDockable(leftToolDock, toolB);
        Factory.AddDockable(leftProportional, leftToolDock);
        Debug.WriteLine("[Dock] Added tools to ToolDock");

        // Splitter
        var splitter = Factory.CreateProportionalDockSplitter();
        splitter.CanResize = true;
        Debug.WriteLine("[Dock] Created ProportionalDockSplitter");

        // Right: DocumentDock with one document
        var rightProportional = Factory.CreateProportionalDock();
        rightProportional.Orientation = Orientation.Vertical;
        rightProportional.Proportion = 0.7;
        Debug.WriteLine("[Dock] Created ProportionalDock (right, vertical, 70%)");

        var documentDock = Factory.CreateDocumentDock();
        documentDock.CanCreateDocument = false;
        Debug.WriteLine("[Dock] Created DocumentDock");

        var docA = Factory.CreateDocument();
        docA.Id = "doc-a";
        docA.Title = "Doc A";
        docA.CanClose = true;
        docA.Context = "Document A Content";  // For DataTemplate
        Debug.WriteLine("[Dock] Created Document: doc-a (Doc A)");

        Factory.AddDockable(documentDock, docA);
        Factory.AddDockable(rightProportional, documentDock);
        Debug.WriteLine("[Dock] Added document to DocumentDock");

        // Build tree
        Factory.AddDockable(proportional, leftProportional);
        Factory.AddDockable(proportional, splitter);
        Factory.AddDockable(proportional, rightProportional);
        Debug.WriteLine("[Dock] Added left|splitter|right to proportional");

        Factory.AddDockable(root, proportional);
        Debug.WriteLine("[Dock] Added proportional to root");

        Debug.WriteLine("[Dock] M0.5: CreateSpikeLayout() completed");
        return root;
    }
}