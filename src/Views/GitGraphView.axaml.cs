using System;
using Avalonia.Controls;
using Aero.ViewModels;

namespace Aero.Views;

public partial class GitGraphView : UserControl
{
    private Action<string>? _clickHandler;

    public GitGraphView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // G1 fix: Unsubscribe previous handler before subscribing new one
        if (_clickHandler != null)
            GraphControl.CommitClicked -= _clickHandler;
        _clickHandler = null;

        if (DataContext is GitGraphViewModel vm)
        {
            // G2 fix: Pass SHA string — ViewModel looks up from its Commits list
            _clickHandler = sha => vm.SelectCommitBySha(sha);
            GraphControl.CommitClicked += _clickHandler;
        }
    }
}
