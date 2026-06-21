using ReactiveUI;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for a diff tab (displays a GitDiffViewModel).
/// </summary>
public class DiffTabViewModel : ReactiveObject
{
    public DiffTabViewModel(GitDiffViewModel diffViewModel)
    {
        DiffViewModel = diffViewModel ?? throw new System.ArgumentNullException(nameof(diffViewModel));
    }

    /// <summary>The diff content to display.</summary>
    public GitDiffViewModel DiffViewModel { get; }

    /// <summary>Display title for the tab.</summary>
    public string Title => DiffViewModel.Title;

    /// <summary>The file path being diffed.</summary>
    public string FilePath => DiffViewModel.FilePath;
}