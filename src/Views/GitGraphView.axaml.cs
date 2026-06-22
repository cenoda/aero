using System;
using Avalonia.Controls;
using Aero.ViewModels;

namespace Aero.Views;

public partial class GitGraphView : UserControl
{
    public GitGraphView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is GitGraphViewModel vm)
        {
            GraphControl.SetCommitLookup(vm.Commits);
            GraphControl.CommitClicked += c => vm.SelectCommit(c);
        }
    }
}
