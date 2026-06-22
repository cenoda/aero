using System;
using System.Windows.Input;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Controls;

namespace Aero.Docking.Model;

/// <summary>
/// Dock window - represents a floating window in the dock layout.
/// Extends ManagedDockableBase since that provides SetProperty for INotifyPropertyChanged.
/// </summary>
public class AeroDockWindow : ManagedDockableBase, IDockWindow
{
    private string _id = string.Empty;
    private double _x;
    private double _y;
    private double _width = 800;
    private double _height = 600;
    private DockWindowState _windowState;
    private bool _topmost;
    private string _title = string.Empty;
    private DockWindowOwnerMode _ownerMode;
    private IDockWindow? _parentWindow;
    private bool _isModal;
    private bool? _showInTaskbar = true;
    private IDockable? _owner;
    private IFactory? _factory;
    private IRootDock? _layout;
    private IHostWindow? _host;

    public string Id
    {
        get => _id;
        set => this.SetProperty(ref _id, value);
    }

    public double X
    {
        get => _x;
        set => this.SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => this.SetProperty(ref _y, value);
    }

    public double Width
    {
        get => _width;
        set => this.SetProperty(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => this.SetProperty(ref _height, value);
    }

    public DockWindowState WindowState
    {
        get => _windowState;
        set => this.SetProperty(ref _windowState, value);
    }

    public bool Topmost
    {
        get => _topmost;
        set => this.SetProperty(ref _topmost, value);
    }

    public string Title
    {
        get => _title;
        set => this.SetProperty(ref _title, value);
    }

    public DockWindowOwnerMode OwnerMode
    {
        get => _ownerMode;
        set => this.SetProperty(ref _ownerMode, value);
    }

    public IDockWindow? ParentWindow
    {
        get => _parentWindow;
        set => this.SetProperty(ref _parentWindow, value);
    }

    public bool IsModal
    {
        get => _isModal;
        set => this.SetProperty(ref _isModal, value);
    }

    public bool? ShowInTaskbar
    {
        get => _showInTaskbar;
        set => this.SetProperty(ref _showInTaskbar, value);
    }

    public IDockable? Owner
    {
        get => _owner;
        set => this.SetProperty(ref _owner, value);
    }

    public IFactory? Factory
    {
        get => _factory;
        set => this.SetProperty(ref _factory, value);
    }

    public IRootDock? Layout
    {
        get => _layout;
        set => this.SetProperty(ref _layout, value);
    }

    public IHostWindow? Host
    {
        get => _host;
        set => this.SetProperty(ref _host, value);
    }

// IDockWindow methods - no-op implementations
    public void OnClose() { }
    public bool OnMoveDragBegin() => false;
    public void OnMoveDrag() { }
    public void OnMoveDragEnd() { }
    public void Save() { }
    public void Present(bool isSelected) { }
    public void Exit() { }
    public void SetActive() { }
}
