using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using ReactiveUI;
using Aero.Core;
using Aero.ViewModels;

namespace Aero.Views;

public partial class EditorView : UserControl
{
    private TextEditor? _activeEditor;
    private int _subscribeGeneration;
    private Action<FindReplaceArgs>? _findReplaceHandler;
    private CompositeDisposable? _reactiveSubscriptions;

    public EditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Dispose previous ViewModel's reactive subscriptions and event handlers
        _reactiveSubscriptions?.Dispose();
        _reactiveSubscriptions = null;

        // Unsubscribe from previous ViewModel's FindReplaceRequested to prevent leak
        if (DataContext is EditorViewModel previousVm && _findReplaceHandler != null)
        {
            previousVm.FindReplaceRequested -= _findReplaceHandler;
            _findReplaceHandler = null;
        }

        if (DataContext is EditorViewModel vm)
        {
            _reactiveSubscriptions = new CompositeDisposable();

            // When the active tab changes, rebind to the new TextEditor
            vm.WhenAnyValue(x => x.ActiveTab)
              .Subscribe(tab => ResubscribeEditor(tab))
              .DisposeWith(_reactiveSubscriptions);

            // Execute find/replace operations against the live editor control
            _findReplaceHandler = OnFindReplaceRequested;
            vm.FindReplaceRequested += _findReplaceHandler;
        }
    }

    /// <summary>
    /// Unsubscribes from the current TextEditor and posts a deferred re-subscription
    /// at <see cref="DispatcherPriority.Loaded"/>, which fires after the TabControl's
    /// ContentPresenter has completed its layout pass and the new TextEditor is in the
    /// visual tree. The editor is matched by document reference to avoid latching onto
    /// a transitioning editor during rapid tab switches.
    /// </summary>
    private void ResubscribeEditor(EditorTabViewModel? newTab)
    {
        // Unsubscribe from the previous editor immediately
        if (_activeEditor != null)
        {
            _activeEditor.TextChanged -= OnTextChanged;
            _activeEditor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            _activeEditor = null;
        }

        if (newTab == null)
            return;

        // A generation counter lets stale posts (from rapid tab switching) bail out early
        var generation = ++_subscribeGeneration;

        // Post at Loaded priority so we run after layout/render: by then the TabControl's
        // ContentPresenter has created and arranged the new TextEditor in the visual tree.
        Dispatcher.UIThread.Post(() =>
        {
            if (generation != _subscribeGeneration)
                return; // A newer tab switch superseded this one

            // Match by document reference — avoids picking up a transitioning editor
            var innerDoc = newTab.Document?.InnerDocument;
            _activeEditor = innerDoc == null
                ? null
                : this.GetVisualDescendants()
                      .OfType<TextEditor>()
                      .FirstOrDefault(e => e.Document == innerDoc);

            if (_activeEditor != null)
            {
                _activeEditor.TextChanged += OnTextChanged;
                _activeEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorViewModel vm)
            vm.NotifyTextChanged();
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorViewModel vm && _activeEditor != null)
            vm.NotifyCaretChanged(_activeEditor.CaretOffset);
    }

    /// <summary>
    /// Executes a find/replace operation against the active TextEditor.
    /// Invoked by EditorViewModel.FindReplaceRequested — keeps the UI control out of the ViewModel.
    /// </summary>
    private void OnFindReplaceRequested(FindReplaceArgs args)
    {
        if (_activeEditor?.Document == null)
            return;

        if (DataContext is not EditorViewModel vm)
            return;

        var comparison = args.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        switch (args.Operation)
        {
            case FindReplaceOperation.FindNext:
                ExecuteFindNext(vm, args.SearchText, comparison, args.WholeWord);
                break;

            case FindReplaceOperation.ReplaceNext:
                ExecuteReplaceNext(vm, args.SearchText, args.ReplaceText, comparison, args.WholeWord);
                break;

            case FindReplaceOperation.ReplaceAll:
                ExecuteReplaceAll(vm, args.SearchText, args.ReplaceText, comparison, args.WholeWord);
                break;
        }
    }

    private void ExecuteFindNext(EditorViewModel vm, string searchText,
        StringComparison comparison, bool wholeWord)
    {
        var text = _activeEditor!.Document.Text;
        var startIndex = _activeEditor.CaretOffset;

        var idx = TextSearchHelper.FindInText(text, searchText, startIndex, comparison, wholeWord);
        if (idx < 0)
            idx = TextSearchHelper.FindInText(text, searchText, 0, comparison, wholeWord); // wrap around

        if (idx >= 0)
            _activeEditor.Select(idx, searchText.Length);
    }

    private void ExecuteReplaceNext(EditorViewModel vm, string searchText, string replaceText,
        StringComparison comparison, bool wholeWord)
    {
        // Replace the current selection if it matches
        if (_activeEditor!.SelectionLength > 0)
        {
            var selected = _activeEditor.SelectedText;
            var isMatch = wholeWord
                ? string.Equals(selected, searchText, comparison)
                : selected.IndexOf(searchText, comparison) >= 0;

            if (isMatch)
                _activeEditor.Document.Replace(
                    _activeEditor.SelectionStart, _activeEditor.SelectionLength, replaceText);
        }

        // Find and select the next occurrence
        ExecuteFindNext(vm, searchText, comparison, wholeWord);
    }

    private void ExecuteReplaceAll(EditorViewModel vm, string searchText, string replaceText,
        StringComparison comparison, bool wholeWord)
    {
        var text = _activeEditor!.Document.Text;
        var offsets = new List<int>();
        var idx = 0;

        while ((idx = TextSearchHelper.FindInText(text, searchText, idx, comparison, wholeWord)) >= 0)
        {
            offsets.Add(idx);
            idx += searchText.Length;
        }

        if (offsets.Count == 0)
            return;

        // Group all replacements into a single undo operation so one Ctrl+Z reverts
        // the entire Replace All (matches VS Code / standard editor behavior).
        _activeEditor.Document.BeginUpdate();
        try
        {
            // Replace in reverse order to preserve earlier offsets
            for (var i = offsets.Count - 1; i >= 0; i--)
                _activeEditor.Document.Replace(offsets[i], searchText.Length, replaceText);
        }
        finally
        {
            _activeEditor.Document.EndUpdate();
        }
    }
}
