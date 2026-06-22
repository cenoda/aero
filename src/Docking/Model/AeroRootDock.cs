using System;
using System.Collections.Generic;
using System.Windows.Input;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Controls;

namespace Aero.Docking.Model;

/// <summary>
/// Root dock container - implements both IDockable (via ManagedDockableBase) and IDock.
/// </summary>
public class AeroRootDock : ManagedDockableBase, IRootDock
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

    public AeroRootDock()
    {
        Id = "Root";
        Title = "Root";
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

    // IRootDock-specific members
    public bool IsFocusableRoot { get; set; }
    public IList<IDockable>? HiddenDockables { get; set; }
    public IList<IDockable>? LeftPinnedDockables { get; set; }
    public IList<IDockable>? RightPinnedDockables { get; set; }
    public IList<IDockable>? TopPinnedDockables { get; set; }
    public IList<IDockable>? BottomPinnedDockables { get; set; }
    public IToolDock? PinnedDock { get; set; }
    public PinnedDockDisplayMode PinnedDockDisplayMode { get; set; }
    public IDockWindow? Window { get; set; }
    public IList<IDockWindow>? Windows { get; set; }
    public DockFloatingWindowHostMode FloatingWindowHostMode { get; set; }
    public DockCapabilityPolicy? RootDockCapabilityPolicy { get; set; }
    public ICommand? ShowWindows { get; } = new NoOpCommand();
    public ICommand? ExitWindows { get; } = new NoOpCommand();
    public bool EnableAdaptiveGlobalDockTargets { get; set; }
}

/// <summary>
/// Simple no-op command for placeholder commands.
/// </summary>
public class NoOpCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) { }
}
