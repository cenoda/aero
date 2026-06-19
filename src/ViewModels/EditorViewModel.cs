using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Aero.Core;
using Aero.Languages;
using Aero.Models.Editor;
using Aero.Services;
using IMessageBus = Aero.Core.IMessageBus;
using DocMsg = Aero.Core;
using DirtyCloseResponse = Aero.Core.DirtyCloseResponse;

namespace Aero.ViewModels;

/// <summary>
/// Payload for find/replace operations delegated to the view's code-behind.
/// The ViewModel raises FindReplaceRequested; the view executes it against the live TextEditor.
/// </summary>
public enum FindReplaceOperation { FindNext, ReplaceNext, ReplaceAll }

public record FindReplaceArgs(
    FindReplaceOperation Operation,
    string SearchText,
    string ReplaceText,
    bool CaseSensitive,
    bool WholeWord);

/// <summary>
/// ViewModel for the editor panel with tabs.
/// Manages open documents, active tab, cursor position.
/// </summary>
public class EditorViewModel : ReactiveObject, IDisposable
{
    private readonly DocumentManager _documentManager;
    private readonly IMessageBus _bus;
    private readonly ILanguageDetectionService _languageDetection;
    private readonly ObservableCollection<EditorTabViewModel> _tabs = new();

    // Stored handlers for unsubscribe
    private Action<DocMsg.DocumentOpened>? _documentOpenedHandler;
    private Action<DocMsg.DocumentClosed>? _documentClosedHandler;
    private Action<DocMsg.ActiveDocumentChanged>? _activeDocumentChangedHandler;
    private Action<DocMsg.DocumentSaved>? _documentSavedHandler;
    private bool _disposed;

    [Reactive] public EditorTabViewModel? ActiveTab { get; set; }
    [Reactive] public string CursorPosition { get; set; } = "Ln 1, Col 1";
    [Reactive] public string Language { get; set; } = "Plain Text";
    [Reactive] public bool HasDocument { get; set; }
    [Reactive] public bool IsFindReplaceVisible { get; set; }
    [Reactive] public string StatusText { get; set; } = "";

    /// <summary>
    /// Raised when a find/replace action should be executed against the active TextEditor.
    /// The code-behind subscribes to this and performs the operation on the control.
    /// </summary>
    public event Action<FindReplaceArgs>? FindReplaceRequested;

    public FindReplaceViewModel FindReplace { get; }

    public ReactiveCommand<EditorTabViewModel, Unit> CloseTabCommand { get; }

    public EditorViewModel(DocumentManager documentManager, IMessageBus bus, FindReplaceViewModel findReplace, ILanguageDetectionService languageDetection)
    {
        _documentManager = documentManager ?? throw new ArgumentNullException(nameof(documentManager));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        FindReplace = findReplace ?? throw new ArgumentNullException(nameof(findReplace));
        _languageDetection = languageDetection ?? throw new ArgumentNullException(nameof(languageDetection));

        // Create commands
        CloseTabCommand = ReactiveCommand.Create<EditorTabViewModel>(CloseTab);

        // Wire up Find/Replace actions
        FindReplace.SetActions(
            (searchText, replaceText, caseSensitive, wholeWord) => FindNext(searchText, caseSensitive, wholeWord),
            (searchText, replaceText, caseSensitive, wholeWord) => ReplaceNext(searchText, replaceText, caseSensitive, wholeWord),
            (searchText, replaceText, caseSensitive, wholeWord) => ReplaceAll(searchText, replaceText, caseSensitive, wholeWord),
            () => HideFindReplace());

        // Subscribe to document messages — store handlers for unsubscribe in Dispose()
        _documentOpenedHandler = msg => OnDocumentOpened(msg);
        _documentClosedHandler = msg => OnDocumentClosed(msg);
        _activeDocumentChangedHandler = msg => OnActiveDocumentChanged(msg);
        _documentSavedHandler = msg => OnDocumentSaved(msg);
        _bus.Subscribe(_documentOpenedHandler);
        _bus.Subscribe(_documentClosedHandler);
        _bus.Subscribe(_activeDocumentChangedHandler);
        _bus.Subscribe(_documentSavedHandler);
    }

    /// <summary>All open tabs.</summary>
    public ObservableCollection<EditorTabViewModel> Tabs => _tabs;

    /// <summary>Open a file.</summary>
    public async Task OpenFileAsync(string filePath)
    {
        var doc = await _documentManager.OpenDocumentAsync(filePath);
        EnsureTabForDocument(doc);
    }

    /// <summary>Create a new document.</summary>
    public void NewFile()
    {
        var doc = _documentManager.NewDocument();
        EnsureTabForDocument(doc);
    }

    /// <summary>Save the active document.</summary>
    /// <returns>
    /// <c>true</c> if the document was saved; <c>false</c> if the document is new
    /// and the caller must prompt for a path (Save As).
    /// </returns>
    public async Task<bool> SaveAsync()
    {
        if (ActiveTab?.Document == null)
            return true;

        return await _documentManager.SaveDocumentAsync(ActiveTab.Document);
    }

    /// <summary>Save as to a new file.</summary>
    public async Task SaveAsAsync(string filePath)
    {
        if (ActiveTab?.Document == null || string.IsNullOrEmpty(filePath))
            return;

        await _documentManager.SaveDocumentAsAsync(ActiveTab.Document, filePath);
    }

    /// <summary>Close the active tab with dirty check.</summary>
    public void CloseActiveTab()
    {
        if (ActiveTab?.Document == null)
            return;

        var doc = ActiveTab.Document;

        // Check if document is dirty and prompt user
        if (doc.IsDirty)
        {
            PromptDirtyClose(doc, () => CloseTabImpl(ActiveTab));
            return;
        }

        CloseTabImpl(ActiveTab);
    }

    /// <summary>Close a specific tab with dirty check.</summary>
    public void CloseTab(EditorTabViewModel tab)
    {
        if (tab?.Document == null)
            return;

        var doc = tab.Document;

        // Check if document is dirty and prompt user
        if (doc.IsDirty)
        {
            PromptDirtyClose(doc, () => CloseTabImpl(tab));
            return;
        }

        CloseTabImpl(tab);
    }

    /// <summary>Actually close a tab (after dirty check resolved).</summary>
    private void CloseTabImpl(EditorTabViewModel tab)
    {
        if (tab?.Document == null)
            return;

        // Dispose the tab's subscriptions to prevent memory leaks
        tab.Dispose();
        _documentManager.CloseDocument(tab.Document);
    }

    /// <summary>Prompt user for dirty document close decision.</summary>
    private void PromptDirtyClose(TextDocument doc, Action onProceed)
    {
        var displayName = doc.DisplayName;

        // Create a continuation that will be called with the user's response
        void HandleResponse(string response)
        {
            if (response == DirtyCloseResponse.Save)
            {
                // Save the document first, then close.
                // Fire-and-forget is intentional — the UI dialog has already dismissed
                // by the time we get here, so we can't show another dialog for errors.
                // Errors are surfaced via the status bar where possible.
                _ = SaveAndCloseAsync(doc, onProceed);
            }
            else if (response == DirtyCloseResponse.DontSave)
            {
                // Close without saving (discard changes)
                onProceed();
            }
            // If Cancel, do nothing (don't close)
        }

        // Publish the confirmation request - UI should subscribe and show dialog
        _bus.Publish(new ConfirmDirtyClose(displayName, HandleResponse));
    }

    /// <summary>Save document and then close (async helper).</summary>
    private async Task SaveAndCloseAsync(TextDocument doc, Action onClose)
    {
        try
        {
            var saved = await _documentManager.SaveDocumentAsync(doc);
            if (saved)
            {
                onClose();
            }
            else
            {
                // Untitled document — SaveDocumentAsync returned false because
                // there is no file path. The dirty-close dialog is already dismissed,
                // so surface this in the status bar and keep the tab open.
                StatusText = "Untitled file — use Save As (Ctrl+Shift+S) first";
            }
        }
        catch (Exception ex)
        {
            // Surface the error to the status bar — the dialog is already dismissed,
            // so we cannot re-prompt. The document stays open with unsaved changes.
            StatusText = $"Save failed: {ex.Message}";
        }
    }

    /// <summary>Switch to a tab.</summary>
    public void ActivateTab(EditorTabViewModel tab)
    {
        if (tab?.Document == null)
            return;

        _documentManager.ActivateDocument(tab.Document);
    }

    /// <summary>Undo in active document.</summary>
    public void Undo()
    {
        ActiveTab?.Document.Undo();
    }

    /// <summary>Redo in active document.</summary>
    public void Redo()
    {
        ActiveTab?.Document.Redo();
    }

    /// <summary>Called by the view when the active TextEditor's text changes.</summary>
    public void NotifyTextChanged()
    {
        var doc = ActiveTab?.Document;
        if (doc == null)
            return;

        _documentManager.MarkDirty(doc);
        _bus.Publish(new DocMsg.DocumentTextChanged(doc));
    }

    /// <summary>Called by the view when the caret moves in the active TextEditor.</summary>
    public void NotifyCaretChanged(int offset)
    {
        var doc = ActiveTab?.Document;
        if (doc == null) return;
        doc.CaretOffset = offset;
        UpdateStatus(doc);
    }

    /// <summary>Show the find/replace overlay.</summary>
    /// <param name="focusReplace">If true, focus the Replace field on open (Ctrl+H).</param>
    public void ShowFindReplace(bool focusReplace = false)
    {
        FindReplace.FocusReplaceOnOpen = focusReplace;
        IsFindReplaceVisible = true;
    }

    /// <summary>Hide the find/replace overlay.</summary>
    public void HideFindReplace()
    {
        IsFindReplaceVisible = false;
    }

    /// <summary>Request a FindNext operation — executed by the code-behind against the live TextEditor.</summary>
    public void FindNext(string searchText, bool caseSensitive, bool wholeWord)
    {
        if (string.IsNullOrEmpty(searchText))
            return;
        FindReplaceRequested?.Invoke(new FindReplaceArgs(
            FindReplaceOperation.FindNext, searchText, "", caseSensitive, wholeWord));
    }

    /// <summary>Request a ReplaceNext operation.</summary>
    public void ReplaceNext(string searchText, string replaceText, bool caseSensitive, bool wholeWord)
    {
        if (string.IsNullOrEmpty(searchText))
            return;
        FindReplaceRequested?.Invoke(new FindReplaceArgs(
            FindReplaceOperation.ReplaceNext, searchText, replaceText ?? "", caseSensitive, wholeWord));
    }

    /// <summary>Request a ReplaceAll operation.</summary>
    public void ReplaceAll(string searchText, string replaceText, bool caseSensitive, bool wholeWord)
    {
        if (string.IsNullOrEmpty(searchText))
            return;
        FindReplaceRequested?.Invoke(new FindReplaceArgs(
            FindReplaceOperation.ReplaceAll, searchText, replaceText ?? "", caseSensitive, wholeWord));
    }

    private void EnsureTabForDocument(TextDocument doc)
    {
        var existing = _tabs.FirstOrDefault(t => t.Document == doc);
        if (existing != null)
        {
            ActiveTab = existing;
        }
        else
        {
            var info = _languageDetection.Detect(doc.FilePath);
            var tab = new EditorTabViewModel(doc, _bus, info.Id);
            _tabs.Add(tab);
            ActiveTab = tab;
        }

        HasDocument = true;
        UpdateStatus(doc);
    }

    private void UpdateStatus(TextDocument? doc)
    {
        if (doc == null)
        {
            Language = "Plain Text";
            CursorPosition = "Ln 1, Col 1";
            return;
        }

        Language = doc.Language;
        var (line, col) = doc.GetLineColumn(doc.CaretOffset);
        CursorPosition = $"Ln {line}, Col {col}";
    }

    private void OnDocumentOpened(DocMsg.DocumentOpened msg)
    {
        // The tab is already created by NewFile() / OpenFileAsync() before the message fires.
        // This handler is a safety net for external openers (e.g. file tree in Phase 2).
        // Match by FilePath for saved files; for untitled docs the tab is already present.
        var doc = _documentManager.Documents.FirstOrDefault(d =>
            d.FilePath != null && d.FilePath == msg.FilePath);
        if (doc != null)
        {
            EnsureTabForDocument(doc);
        }
    }

    private void OnDocumentClosed(DocMsg.DocumentClosed msg)
    {
        // Match by document reference (FilePath is unreliable for untitled docs)
        var tab = _tabs.FirstOrDefault(t => t.Document == msg.Document);
        if (tab != null)
        {
            _tabs.Remove(tab);
            if (ActiveTab == tab)
            {
                ActiveTab = _tabs.FirstOrDefault();
            }
        }

        HasDocument = _tabs.Any();
    }

    private void OnActiveDocumentChanged(DocMsg.ActiveDocumentChanged msg)
    {
        // Match by document reference (FilePath is unreliable for untitled docs)
        var tab = msg.Document != null
            ? _tabs.FirstOrDefault(t => t.Document == msg.Document)
            : null;
        ActiveTab = tab;
        UpdateStatus(tab?.Document);
    }

    private void OnDocumentSaved(DocMsg.DocumentSaved msg)
    {
        // Refresh the tab's grammar id when Save As gives the document a new path.
        var tab = _tabs.FirstOrDefault(t => t.Document == msg.Document);
        if (tab != null)
        {
            tab.LanguageId = _languageDetection.Detect(msg.Document.FilePath).Id;

            // Save As can change the document's language (e.g. untitled → foo.cs).
            // DocumentManager already refreshed doc.Language; mirror it into the
            // status bar now so the label doesn't lag behind the grammar until the
            // next caret move or tab switch.
            if (tab == ActiveTab)
                UpdateStatus(msg.Document);
        }
    }

    /// <summary>Dispose message bus subscriptions to prevent stale-handler leaks.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_documentOpenedHandler != null)
            _bus.Unsubscribe<DocMsg.DocumentOpened>(_documentOpenedHandler);
        if (_documentClosedHandler != null)
            _bus.Unsubscribe<DocMsg.DocumentClosed>(_documentClosedHandler);
        if (_activeDocumentChangedHandler != null)
            _bus.Unsubscribe<DocMsg.ActiveDocumentChanged>(_activeDocumentChangedHandler);
        if (_documentSavedHandler != null)
            _bus.Unsubscribe<DocMsg.DocumentSaved>(_documentSavedHandler);
    }
}
