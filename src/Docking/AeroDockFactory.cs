using System;
using System.Collections.Generic;
using System.Windows.Input;
using Dock.Avalonia.Controls;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using Aero.Docking.Model;
using Aero.Docking.ToolViewModels;
using Aero.Docking.DocumentViewModels;

namespace Aero.Docking;

/// <summary>
/// Factory for creating the default dock layout.
/// Extends FactoryBase to provide concrete implementations for all dock types.
/// </summary>
public class AeroDockFactory : FactoryBase
{
    public override IRootDock CreateRootDock() => new AeroRootDock();

    public override IProportionalDock CreateProportionalDock() => new AeroProportionalDock();

    public override IProportionalDockSplitter CreateProportionalDockSplitter() => new AeroProportionalDockSplitter();

    public override IToolDock CreateToolDock() => new AeroToolDock();

    public override IDocumentDock CreateDocumentDock() => new AeroDocumentDock();

    public override ITool CreateTool() => throw new InvalidOperationException("Use specific tool types (ExplorerTool, GitTool, etc.)");

    public override IDocument CreateDocument() => throw new InvalidOperationException("Use EditorDocument");

    public override IDockWindow CreateDockWindow() => new AeroDockWindow();

    // Other dock types - return minimal stubs
    public override IDockDock CreateDockDock() => throw new NotImplementedException();
    public override IStackDock CreateStackDock() => throw new NotImplementedException();
    public override IGridDock CreateGridDock() => throw new NotImplementedException();
    public override IWrapDock CreateWrapDock() => throw new NotImplementedException();
    public override IUniformGridDock CreateUniformGridDock() => throw new NotImplementedException();
    public override ISplitViewDock CreateSplitViewDock() => throw new NotImplementedException();
    public override IGridDockSplitter CreateGridDockSplitter() => throw new NotImplementedException();

    // 10 abstract property getters from FactoryBase - using the correct types
    public override IDictionary<IDockable, object> ToolControls => new Dictionary<IDockable, object>();
    public override IDictionary<IDockable, object> DocumentControls => new Dictionary<IDockable, object>();
    public override IDictionary<IDockable, IDockableControl> VisibleDockableControls => new Dictionary<IDockable, IDockableControl>();
    public override IDictionary<IDockable, object> VisibleRootControls => new Dictionary<IDockable, object>();
    public override IDictionary<IDockable, IDockableControl> PinnedDockableControls => new Dictionary<IDockable, IDockableControl>();
    public override IDictionary<IDockable, object> PinnedRootControls => new Dictionary<IDockable, object>();
    public override IDictionary<IDockable, IDockableControl> TabDockableControls => new Dictionary<IDockable, IDockableControl>();
    public override IDictionary<IDockable, object> TabRootControls => new Dictionary<IDockable, object>();
    public override IList<IDockControl> DockControls => new List<IDockControl>();
    public override IList<IHostWindow> HostWindows => new List<IHostWindow>();

    public override IList<T> CreateList<T>(T[] items) => new List<T>(items);

    public override IRootDock CreateLayout() => CreateDefaultLayout();

    public static IRootDock CreateDefaultLayout()
    {
        // Create a local factory instance for creating the layout
        var factory = new AeroDockFactory();

        // Create all the tools and documents
        var explorer = new ExplorerTool();
        explorer.Factory = factory;
        var git = new GitTool();
        git.Factory = factory;
        var editor = new EditorDocument();
        editor.Factory = factory;
        var problems = new ProblemsTool();
        problems.Factory = factory;
        var output = new OutputTool();
        output.Factory = factory;

        // Build tree bottom-up:

        // Left sidebar: IToolDock(Left) containing Explorer + Git
        var leftToolDock = factory.CreateToolDock();
        leftToolDock.Alignment = Alignment.Left;
        var leftTools = new List<IDockable> { explorer, git };
        leftToolDock.VisibleDockables = leftTools;

        var leftSplitter = factory.CreateProportionalDockSplitter();

        // Left column: ProportionalDock with vertical orientation
        var leftColumn = factory.CreateProportionalDock();
        leftColumn.Orientation = Orientation.Vertical;
        var leftColumnChildren = new List<IDockable> { leftToolDock, leftSplitter };
        leftColumn.VisibleDockables = leftColumnChildren;

        // Bottom panel: IToolDock(Bottom) containing Problems + Output
        var bottomToolDock = factory.CreateToolDock();
        bottomToolDock.Alignment = Alignment.Bottom;
        var bottomTools = new List<IDockable> { problems, output };
        bottomToolDock.VisibleDockables = bottomTools;

        // Center: IDocumentDock containing Editor
        var documentDock = factory.CreateDocumentDock();
        var documents = new List<IDockable> { editor };
        documentDock.VisibleDockables = documents;

        var bottomSplitter = factory.CreateProportionalDockSplitter();

        // Right column: vertical split top (document) | splitter | bottom (tool dock)
        var rightColumn = factory.CreateProportionalDock();
        rightColumn.Orientation = Orientation.Vertical;
        var rightColumnChildren = new List<IDockable> { documentDock, bottomSplitter, bottomToolDock };
        rightColumn.VisibleDockables = rightColumnChildren;

        var topSplitter = factory.CreateProportionalDockSplitter();

        // Root: horizontal split leftColumn | splitter | rightColumn
        var root = factory.CreateRootDock();
        var rootChildren = new List<IDockable> { leftColumn, topSplitter, rightColumn };
        root.VisibleDockables = rootChildren;

        // Set owners
        explorer.Owner = leftToolDock;
        git.Owner = leftToolDock;
        editor.Owner = documentDock;
        problems.Owner = bottomToolDock;
        output.Owner = bottomToolDock;

        // Initialize the layout via factory
        factory.InitLayout(root);

        return root;
    }
}
