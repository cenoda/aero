using System;
using System.Collections.Generic;
using System.Windows.Input;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Controls;

namespace Aero.Docking.Model;

/// <summary>
/// Tool dock container - holds tool dockables (Explorer, Git, Problems, Output).
/// </summary>
public class AeroToolDock : ManagedDockableBase, IToolDock
{
    private IList<IDockable>? _visibleDockables;
    private IDockable? _activeDockable;
    private IDockable? _defaultDockable;
    private IDockable? _focusedDockable;
    private bool _isActive;
    private bool _canCloseLastDockable;
    private DockCapabilityPolicy? _dockCapabilityPolicy;
    private bool _enableGlobalDocking;
    private int _openedDockablesCount;
    private Alignment _alignment;
    private bool _isExpanded = true;
    private bool _autoHide;
    private GripMode _gripMode = GripMode.Visible;

    public AeroToolDock()
    {
        Id = "ToolDock";
        Title = "ToolDock";
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IDockable d && d.Id == Id;
    /// <inheritdoc />
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;

    // IDock members
    public IList<IDockable>? VisibleDockables
    {
        get => _visibleDockables;
        set => this.SetProperty(ref _visibleDockables, value);
    }

    public IDockable? ActiveDockable
    {
        get => _activeDockable;
        set => this.SetProperty(ref _activeDockable, value);
    }

    public IDockable? DefaultDockable
    {
        get => _defaultDockable;
        set => this.SetProperty(ref _defaultDockable, value);
    }

    public IDockable? FocusedDockable
    {
        get => _focusedDockable;
        set => this.SetProperty(ref _focusedDockable, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => this.SetProperty(ref _isActive, value);
    }

    public int OpenedDockablesCount
    {
        get => _openedDockablesCount;
        set => this.SetProperty(ref _openedDockablesCount, value);
    }

    public bool CanCloseLastDockable
    {
        get => _canCloseLastDockable;
        set => this.SetProperty(ref _canCloseLastDockable, value);
    }

    public DockCapabilityPolicy? DockCapabilityPolicy
    {
        get => _dockCapabilityPolicy;
        set => this.SetProperty(ref _dockCapabilityPolicy, value);
    }

    public bool CanGoBack => false;
    public bool CanGoForward => false;
    public ICommand? GoBack { get; } = new NoOpCommand();
    public ICommand? GoForward { get; } = new NoOpCommand();
    public ICommand? Navigate { get; } = new NoOpCommand();
    public ICommand? Close { get; } = new NoOpCommand();

    public bool EnableGlobalDocking
    {
        get => _enableGlobalDocking;
        set => this.SetProperty(ref _enableGlobalDocking, value);
    }

    // IToolDock members
    public Alignment Alignment
    {
        get => _alignment;
        set => this.SetProperty(ref _alignment, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.SetProperty(ref _isExpanded, value);
    }

    public bool AutoHide
    {
        get => _autoHide;
        set => this.SetProperty(ref _autoHide, value);
    }

    public GripMode GripMode
    {
        get => _gripMode;
        set => this.SetProperty(ref _gripMode, value);
    }

    public void AddTool(IDockable dockable)
    {
        if (VisibleDockables == null)
        {
            VisibleDockables = new List<IDockable>();
        }
        VisibleDockables.Add(dockable);
    }
}


