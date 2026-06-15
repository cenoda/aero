using System;
using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Aero.ViewModels;

/// <summary>
/// ViewModel for the Find/Replace overlay panel.
/// </summary>
public class FindReplaceViewModel : ReactiveObject
{
    private Action<string, string, bool, bool> _findNextAction = (_, _, _, _) => { };
    private Action<string, string, bool, bool> _replaceNextAction = (_, _, _, _) => { };
    private Action<string, string, bool, bool> _replaceAllAction = (_, _, _, _) => { };
    private Action _closeAction = () => { };

    [Reactive] public string SearchText { get; set; } = "";
    [Reactive] public string ReplaceText { get; set; } = "";
    [Reactive] public bool CaseSensitive { get; set; }
    [Reactive] public bool WholeWord { get; set; }
    [Reactive] public bool IsVisible { get; set; }

    public ReactiveCommand<Unit, Unit> FindNextCommand { get; }
    public ReactiveCommand<Unit, Unit> ReplaceCommand { get; }
    public ReactiveCommand<Unit, Unit> ReplaceAllCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public FindReplaceViewModel()
    {
        FindNextCommand = ReactiveCommand.Create(FindNextExecute);
        ReplaceCommand = ReactiveCommand.Create(ReplaceExecute);
        ReplaceAllCommand = ReactiveCommand.Create(ReplaceAllExecute);
        CloseCommand = ReactiveCommand.Create(() => _closeAction());
    }

    public void SetActions(
        Action<string, string, bool, bool> findNext,
        Action<string, string, bool, bool> replaceNext,
        Action<string, string, bool, bool> replaceAll,
        Action close)
    {
        _findNextAction = findNext;
        _replaceNextAction = replaceNext;
        _replaceAllAction = replaceAll;
        _closeAction = close;
    }

    private void FindNextExecute()
    {
        _findNextAction(SearchText, ReplaceText, CaseSensitive, WholeWord);
    }

    private void ReplaceExecute()
    {
        _replaceNextAction(SearchText, ReplaceText, CaseSensitive, WholeWord);
    }

    private void ReplaceAllExecute()
    {
        _replaceAllAction(SearchText, ReplaceText, CaseSensitive, WholeWord);
    }

    public void Show()
    {
        IsVisible = true;
    }

    public void Hide()
    {
        IsVisible = false;
    }
}
