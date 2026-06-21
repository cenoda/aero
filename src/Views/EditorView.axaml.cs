using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using TextMateSharp.Grammars;
using Aero.Core;
using Aero.Languages;
using Aero.ViewModels;
using IMessageBus = Aero.Core.IMessageBus;

namespace Aero.Views;

public partial class EditorView : UserControl
{
    private TextEditor? _activeEditor;
    private int _subscribeGeneration;
    private Action<FindReplaceArgs>? _findReplaceHandler;
    private Action? _diagnosticsChangedHandler;
    private CompositeDisposable? _reactiveSubscriptions;

    // One shared TextMate registry/theme for the editor panel.
    private readonly RegistryOptions _registryOptions = new(ThemeName.DarkPlus);

    // Track TextMate installations per TextEditor so they are disposed when the
    // editor control goes away (tab close) instead of leaking across open/close.
    private readonly Dictionary<TextEditor, TextMate.Installation> _textMateInstallations = new();

    // Track diagnostic renderers per TextEditor so repeated tab activations
    // do not stack duplicate BackgroundRenderers on the same editor instance.
    private readonly Dictionary<TextEditor, EditorDiagnosticRenderer> _diagnosticRenderers = new();

    // Subscription to the active tab's LanguageId changes (e.g. Save As).
    private IDisposable? _languageIdSubscription;

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

        // Stop listening to the previous active tab's LanguageId changes
        _languageIdSubscription?.Dispose();
        _languageIdSubscription = null;

        // Unsubscribe from previous ViewModel's DiagnosticsChanged
        if (DataContext is EditorViewModel previousVmForDiag && _diagnosticsChangedHandler != null)
        {
            previousVmForDiag.DiagnosticsChanged -= _diagnosticsChangedHandler;
            _diagnosticsChangedHandler = null;
        }

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

            // When DiagnosticsUpdated arrives (forwarded by the VM), redraw the active editor.
            _diagnosticsChangedHandler = OnDiagnosticsChanged;
            vm.DiagnosticsChanged += _diagnosticsChangedHandler;
        }
    }

    private void OnDiagnosticsChanged()
    {
        // Trigger a redraw on the active editor to show updated diagnostics.
        if (_activeEditor != null)
        {
            _activeEditor.TextArea.TextView.Redraw();
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

        // Stop listening to the previous tab's LanguageId changes.
        _languageIdSubscription?.Dispose();
        _languageIdSubscription = null;

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

                // Ensure TextMate is installed once per editor, then apply the grammar.
                if (!_textMateInstallations.TryGetValue(_activeEditor, out var installation))
                {
                    installation = _activeEditor.InstallTextMate(_registryOptions);
                    _textMateInstallations[_activeEditor] = installation;
                    _activeEditor.Unloaded += OnEditorUnloaded;
                }

                ApplyGrammar(installation, newTab.LanguageId);

                // Re-apply the grammar if the tab's LanguageId changes (e.g. Save As).
                _languageIdSubscription = newTab.WhenAnyValue(x => x.LanguageId)
                    .Subscribe(id => ApplyGrammar(installation, id));

                // Add diagnostic renderer once per editor instance.
                RegisterDiagnosticRendererForEditor(newTab.Document);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Applies a grammar based on the language id.
    /// </summary>
    private void ApplyGrammar(TextMate.Installation installation, string languageId)
    {
        var scope = _registryOptions.GetScopeByLanguageId(languageId);
        if (!string.IsNullOrEmpty(scope))
        {
            installation.SetGrammar(scope);
        }
        // Empty scope means plain text / unknown language — leave the editor uncolored.
    }

    /// <summary>
    /// Called when the editor control is unloaded (e.g., tab closed) — cleans up TextMate
    /// and the diagnostic renderer.
    /// </summary>
    private void OnEditorUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not TextEditor editor)
            return;

        editor.Unloaded -= OnEditorUnloaded;

        if (_textMateInstallations.TryGetValue(editor, out var installation))
        {
            installation.Dispose();
            _textMateInstallations.Remove(editor);
        }

        _diagnosticRenderers.Remove(editor);
    }

    /// <summary>
    /// Creates and adds the diagnostic renderer to the given editor — at most once per
    /// TextEditor instance. The renderer is constructed from the VM's DiagnosticStore so
    /// the store reference is always valid (constructor DI, not a runtime message fetch).
    /// </summary>
    private void RegisterDiagnosticRendererForEditor(Models.Editor.TextDocument? doc)
    {
        if (_activeEditor == null)
            return;

        // Guard: only install one renderer per TextEditor instance.
        if (_diagnosticRenderers.ContainsKey(_activeEditor))
            return;

        if (DataContext is not EditorViewModel vm)
            return;

        // Build a renderer that queries the store at draw time using the current document URI.
        var renderer = new EditorDiagnosticRenderer(
            vm.DiagnosticStore,
            () => GetActiveDocumentUri(doc));

        _activeEditor.TextArea.TextView.BackgroundRenderers.Add(renderer);
        _diagnosticRenderers[_activeEditor] = renderer;
    }

    /// <summary>
    /// Gets the Absolute URI for the given document, matching the format used by
    /// LSPManager.ToFileUri() so diagnostic lookups work correctly.
    /// </summary>
    private static string? GetActiveDocumentUri(Models.Editor.TextDocument? doc)
    {
        if (doc == null)
            return null;

        var fileName = doc.FilePath;
        if (string.IsNullOrEmpty(fileName))
            return null;

        // Use .AbsoluteUri to match LSPManager's format (not .ToString() which produces
        // different strings for paths with spaces).
        return new Uri(Path.GetFullPath(fileName), UriKind.Absolute).AbsoluteUri;
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
