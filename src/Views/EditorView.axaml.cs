using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using ReactiveUI;
using Aero.ViewModels;

namespace Aero.Views;

public partial class EditorView : UserControl
{
    private TextEditor? _activeEditor;
    private CancellationTokenSource? _resubscribeCts;
    private EditorTabViewModel? _pendingTab;

    public EditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is EditorViewModel vm)
        {
            // When the active tab changes, rebind to the new TextEditor
            vm.WhenAnyValue(x => x.ActiveTab)
              .Subscribe(tab => _ = ResubscribeEditorAsync(tab));

            // Execute find/replace operations against the live editor control
            vm.FindReplaceRequested += OnFindReplaceRequested;
        }
    }

    /// <summary>
    /// Finds the currently visible TextEditor in the TabControl's content
    /// and subscribes to its TextChanged and CaretPositionChanged events.
    /// Uses async retry with exponential backoff to handle lazy-loaded content
    /// when tab changes and the UI updates asynchronously.
    /// </summary>
    private async Task ResubscribeEditorAsync(EditorTabViewModel? newTab)
    {
        // Cancel any pending resubscribe operation
        _resubscribeCts?.Cancel();
        _resubscribeCts = new CancellationTokenSource();
        var token = _resubscribeCts.Token;

        // Unsubscribe from previous editor
        if (_activeEditor != null)
        {
            _activeEditor.TextChanged -= OnTextChanged;
            _activeEditor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            _activeEditor = null;
        }

        if (newTab == null)
            return;

        _pendingTab = newTab;

        // Retry with exponential backoff: 10ms, 25ms, 50ms, 100ms, 200ms (max ~400ms total)
        var delays = new[] { 10, 25, 50, 100, 200 };
        foreach (var delay in delays)
        {
            // Check for cancellation or tab change
            if (token.IsCancellationRequested || _pendingTab != newTab)
                return;

            // Wait for the visual tree to update
            await Task.Delay(delay, token);

            // Check again after delay
            if (token.IsCancellationRequested || _pendingTab != newTab)
                return;

            // Try to find the TextEditor in the visual tree
            var editor = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_pendingTab != newTab)
                    return (TextEditor?)null;

                // Find the TextEditor inside the TabControl's content presenter
                return this.FindDescendantOfType<TextEditor>();
            });

            if (editor != null)
            {
                // Verify the editor belongs to the expected tab
                if (_pendingTab == newTab)
                {
                    _activeEditor = editor;
                    _activeEditor.TextChanged += OnTextChanged;
                    _activeEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
                    _pendingTab = null;
                    return;
                }
            }
        }
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

        var idx = vm.FindInText(text, searchText, startIndex, comparison, wholeWord);
        if (idx < 0)
            idx = vm.FindInText(text, searchText, 0, comparison, wholeWord); // wrap around

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

        while ((idx = vm.FindInText(text, searchText, idx, comparison, wholeWord)) >= 0)
        {
            offsets.Add(idx);
            idx += searchText.Length;
        }

        // Replace in reverse order to preserve earlier offsets
        for (var i = offsets.Count - 1; i >= 0; i--)
            _activeEditor.Document.Replace(offsets[i], searchText.Length, replaceText);
    }
}
