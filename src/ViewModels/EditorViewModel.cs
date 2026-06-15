using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Aero.Core;
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
public class EditorViewModel : ReactiveObject
{
    private readonly DocumentManager _documentManager;
    private readonly IMessageBus _bus;
    private readonly ObservableCollection<EditorTabViewModel> _tabs = new();

    [Reactive] public EditorTabViewModel? ActiveTab { get; set; }
    [Reactive] public string CursorPosition { get; set; } = "Ln 1, Col 1";
    [Reactive] public string Language { get; set; } = "Plain Text";
    [Reactive] public bool HasDocument { get; set; }
    [Reactive] public bool IsFindReplaceVisible { get; set; }

    /// <summary>
    /// Raised when a find/replace action should be executed against the active TextEditor.
    /// The code-behind subscribes to this and performs the operation on the control.
    /// </summary>
    public event Action<FindReplaceArgs>? FindReplaceRequested;

public FindReplaceViewModel FindReplace { get; }

    public ReactiveCommand<EditorTabViewModel, Unit> CloseTabCommand { get; }

public EditorViewModel(DocumentManager documentManager, IMessageBus bus, FindReplaceViewModel findReplace)
    {
        _documentManager = documentManager ?? throw new ArgumentNullException(nameof(documentManager));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        FindReplace = findReplace ?? throw new ArgumentNullException(nameof(findReplace));

        // Create commands
        CloseTabCommand = ReactiveCommand.Create<EditorTabViewModel>(CloseTab);

        // Wire up Find/Replace actions
        FindReplace.SetActions(
            (searchText, replaceText, caseSensitive, wholeWord) => FindNext(searchText, caseSensitive, wholeWord),
            (searchText, replaceText, caseSensitive, wholeWord) => ReplaceNext(searchText, replaceText, caseSensitive, wholeWord),
            (searchText, replaceText, caseSensitive, wholeWord) => ReplaceAll(searchText, replaceText, caseSensitive, wholeWord),
            () => HideFindReplace());

        // Subscribe to document messages
        _bus.Subscribe<DocMsg.DocumentOpened>(msg => OnDocumentOpened(msg));
        _bus.Subscribe<DocMsg.DocumentClosed>(msg => OnDocumentClosed(msg));
        _bus.Subscribe<DocMsg.ActiveDocumentChanged>(msg => OnActiveDocumentChanged(msg));
        _bus.Subscribe<DocMsg.DocumentModified>(msg => OnDocumentModified(msg));
        _bus.Subscribe<DocMsg.DocumentSaved>(msg => OnDocumentSaved(msg));
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
            PromptDirtyClose(doc, () => CloseActiveTabImpl(ActiveTab));
            return;
        }

        CloseActiveTabImpl(ActiveTab);
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

    /// <summary>Actually close the active tab (after dirty check resolved).</summary>
    private void CloseActiveTabImpl(EditorTabViewModel tab)
    {
        if (tab?.Document == null)
            return;

        // Dispose the tab's subscriptions to prevent memory leaks
        tab.Dispose();
        _documentManager.CloseDocument(tab.Document);
    }

    /// <summary>Actually close a specific tab (after dirty check resolved).</summary>
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
        var fileName = doc.FileName;

        // Create a continuation that will be called with the user's response
        void HandleResponse(string response)
        {
            if (response == DirtyCloseResponse.Save)
            {
                // Save the document first, then close
                // Note: Fire-and-forget is intentional here - the save happens in background
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
        _bus.Publish(new ConfirmDirtyClose(fileName, HandleResponse));
    }

/// <summary>Save document and then close (async helper).</summary>
    private async Task SaveAndCloseAsync(TextDocument doc, Action onClose)
    {
        await _documentManager.SaveDocumentAsync(doc);
        onClose();
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
        if (doc != null)
            _documentManager.MarkDirty(doc);
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
    public void ShowFindReplace()
    {
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

    /// <summary>
    /// Find the next occurrence of search in text starting at start.
    /// Used by the code-behind to implement the actual search logic.
    /// </summary>
    public int FindInText(string text, string search, int start, StringComparison comparison, bool wholeWord)
    {
        if (!wholeWord)
            return text.IndexOf(search, start, comparison);

        var idx = start;
        while ((idx = text.IndexOf(search, idx, comparison)) >= 0)
        {
            var before = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]) && text[idx - 1] != '_';
            var end = idx + search.Length;
            var after = end >= text.Length || !char.IsLetterOrDigit(text[end]) && text[end] != '_';
            if (before && after) return idx;
            idx += search.Length;
        }
        return -1;
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
var tab = new EditorTabViewModel(doc, _bus);
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

private void OnDocumentModified(DocMsg.DocumentModified msg)
    {
        // Match by document reference for reliable untitled doc handling
        var tab = _tabs.FirstOrDefault(t => t.Document == msg.Document);
        if (tab != null)
        {
            // Force UI refresh - the Title property will update
            this.RaisePropertyChanged(nameof(ActiveTab));
        }
    }

    private void OnDocumentSaved(DocMsg.DocumentSaved msg)
    {
        // Match by document reference for reliable untitled doc handling
        var tab = _tabs.FirstOrDefault(t => t.Document == msg.Document);
        if (tab != null)
        {
            this.RaisePropertyChanged(nameof(ActiveTab));
        }
    }
}
