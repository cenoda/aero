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
/// Dock layout factory that creates Aero-specific dock types.
/// </summary>
public class AeroDockFactory : FactoryBase
{
    /// <summary>Creates a root dock container.</summary>
    public override IRootDock CreateRootDock() => new AeroRootDock();

    /// <summary>Creates a proportional dock container.</summary>
    public override IProportionalDock CreateProportionalDock() => new AeroProportionalDock();

    /// <summary>Creates a proportional dock splitter.</summary>
    public override IProportionalDockSplitter CreateProportionalDockSplitter() => new AeroProportionalDockSplitter();

    /// <summary>Creates a tool dock container.</summary>
    public override IToolDock CreateToolDock() => new AeroToolDock();

    /// <summary>Creates a document dock container.</summary>
    public override IDocumentDock CreateDocumentDock() => new AeroDocumentDock();

    /// <summary>Not supported — use specific tool types directly.</summary>
    public override ITool CreateTool() => throw new InvalidOperationException("Use specific tool types (ExplorerTool, GitTool, etc.)");

    /// <summary>Not supported — use EditorDocument directly.</summary>
    public override IDocument CreateDocument() => throw new InvalidOperationException("Use EditorDocument");

    /// <summary>Creates a floating dock window.</summary>
    public override IDockWindow CreateDockWindow() => new AeroDockWindow();

    /// <summary>Not used by Aero — Dock layout does not require this type.</summary>
    public override IDockDock CreateDockDock() => throw new NotImplementedException();
    /// <summary>Not used by Aero — Dock layout does not require this type.</summary>
    public override IStackDock CreateStackDock() => throw new NotImplementedException();
    /// <summary>Not used by Aero — Dock layout does not require this type.</summary>
    public override IGridDock CreateGridDock() => throw new NotImplementedException();
    /// <summary>Not used by Aero — Dock layout does not require this type.</summary>
    public override IWrapDock CreateWrapDock() => throw new NotImplementedException();
    /// <summary>Not used by Aero — Dock layout does not require this type.</summary>
    public override IUniformGridDock CreateUniformGridDock() => throw new NotImplementedException();
    /// <summary>Not used by Aero — Dock layout does not require this type.</summary>
    public override ISplitViewDock CreateSplitViewDock() => throw new NotImplementedException();
    /// <summary>Not used by Aero — Dock layout does not require this type.</summary>
    public override IGridDockSplitter CreateGridDockSplitter() => throw new NotImplementedException();

    // Field-backed singletons — Dock.Avalonia may access these repeatedly during layout operations.
    private readonly Dictionary<IDockable, object> _toolControls = new();
    private readonly Dictionary<IDockable, object> _documentControls = new();
    private readonly Dictionary<IDockable, IDockableControl> _visibleDockableControls = new();
    private readonly Dictionary<IDockable, object> _visibleRootControls = new();
    private readonly Dictionary<IDockable, IDockableControl> _pinnedDockableControls = new();
    private readonly Dictionary<IDockable, object> _pinnedRootControls = new();
    private readonly Dictionary<IDockable, IDockableControl> _tabDockableControls = new();
    private readonly Dictionary<IDockable, object> _tabRootControls = new();
    private readonly List<IDockControl> _dockControls = new();
    private readonly List<IHostWindow> _hostWindows = new();

    public override IDictionary<IDockable, object> ToolControls => _toolControls;
    public override IDictionary<IDockable, object> DocumentControls => _documentControls;
    public override IDictionary<IDockable, IDockableControl> VisibleDockableControls => _visibleDockableControls;
    public override IDictionary<IDockable, object> VisibleRootControls => _visibleRootControls;
    public override IDictionary<IDockable, IDockableControl> PinnedDockableControls => _pinnedDockableControls;
    public override IDictionary<IDockable, object> PinnedRootControls => _pinnedRootControls;
    public override IDictionary<IDockable, IDockableControl> TabDockableControls => _tabDockableControls;
    public override IDictionary<IDockable, object> TabRootControls => _tabRootControls;
    public override IList<IDockControl> DockControls => _dockControls;
    public override IList<IHostWindow> HostWindows => _hostWindows;

    /// <summary>Creates a new list containing the given items.</summary>
    public override IList<T> CreateList<T>(T[] items) => new List<T>(items);

    /// <summary>Creates the default dock layout (delegates to <see cref="CreateDefaultLayout"/>).</summary>
    public override IRootDock CreateLayout() => CreateDefaultLayout();

    /// <summary>
    /// Builds the default Aero dock layout: left sidebar (Explorer + Git),
    /// center editor, bottom panel (Problems + Output).
    /// </summary>
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
