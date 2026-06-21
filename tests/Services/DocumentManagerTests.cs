using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Languages;
using Aero.Models.Editor;
using Aero.Services;
using Aero.Tests.Stubs;
using Xunit;

namespace Aero.Tests.Services;

public class DocumentManagerTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (IDocumentManagementService dm, StubMessageBus bus) Create()
    {
        var bus = new StubMessageBus();
        var languageDetection = new LanguageDetectionService();
        var dm = new DocumentManager(bus, languageDetection);
        return (dm, bus);
    }

    // -----------------------------------------------------------------------
    // NewDocument
    // -----------------------------------------------------------------------

    [Fact]
    public void NewDocument_AddsDocumentToCollection()
    {
        var (dm, _) = Create();
        var doc = dm.NewDocument();
        Assert.Contains(doc, dm.Documents);
    }

    [Fact]
    public void NewDocument_SetsActiveDocument()
    {
        var (dm, _) = Create();
        var doc = dm.NewDocument();
        Assert.Same(doc, dm.ActiveDocument);
    }

    [Fact]
    public void NewDocument_IsNew_True()
    {
        var (dm, _) = Create();
        var doc = dm.NewDocument();
        Assert.True(doc.IsNew);
    }

    [Fact]
    public void NewDocument_DisplayName_FirstIsUntitled()
    {
        var (dm, _) = Create();
        var doc = dm.NewDocument();
        Assert.Equal("Untitled", doc.DisplayName);
    }

    [Fact]
    public void NewDocument_DisplayName_SecondIsUntitled2()
    {
        var (dm, _) = Create();
        dm.NewDocument();                 // Untitled
        var doc2 = dm.NewDocument();      // Untitled-2
        Assert.Equal("Untitled-2", doc2.DisplayName);
    }

    [Fact]
    public void NewDocument_DoesNotPublishDocumentOpened()
    {
        var (dm, bus) = Create();
        dm.NewDocument();
        Assert.Empty(bus.MessagesOf<DocumentOpened>());
    }

    // -----------------------------------------------------------------------
    // OpenDocumentAsync
    // -----------------------------------------------------------------------

    [Fact]
    public void OpenDocumentAsync_ReadsFileContent()
    {
        SingleThread.Run(async () =>
        {
            var (dm, _) = Create();
            var tmp = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmp, "hello world");
                var doc = await dm.OpenDocumentAsync(tmp);
                Assert.Equal("hello world", doc.Content);
            }
            finally { File.Delete(tmp); }
        });
    }

    [Fact]
    public async Task OpenDocumentAsync_AddsDocumentToCollection()
    {
        var (dm, _) = Create();
        var tmp = Path.GetTempFileName();
        try
        {
            var doc = await dm.OpenDocumentAsync(tmp);
            Assert.Contains(doc, dm.Documents);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task OpenDocumentAsync_SetsActiveDocument()
    {
        var (dm, _) = Create();
        var tmp = Path.GetTempFileName();
        try
        {
            var doc = await dm.OpenDocumentAsync(tmp);
            Assert.Same(doc, dm.ActiveDocument);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task OpenDocumentAsync_SameFileTwice_ReturnsExistingDoc()
    {
        var (dm, _) = Create();
        var tmp = Path.GetTempFileName();
        try
        {
            var doc1 = await dm.OpenDocumentAsync(tmp);
            var doc2 = await dm.OpenDocumentAsync(tmp);
            Assert.Same(doc1, doc2);
            Assert.Single(dm.Documents);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task OpenDocumentAsync_PublishesDocumentOpened()
    {
        var (dm, bus) = Create();
        var tmp = Path.GetTempFileName();
        try
        {
            await dm.OpenDocumentAsync(tmp);
            Assert.Single(bus.MessagesOf<DocumentOpened>());
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task OpenDocumentAsync_DetectsLanguage_CSharp()
    {
        var (dm, _) = Create();
        var tmp = Path.ChangeExtension(Path.GetTempFileName(), ".cs");
        try
        {
            File.WriteAllText(tmp, "");
            var doc = await dm.OpenDocumentAsync(tmp);
            Assert.Equal("C#", doc.Language);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task OpenDocumentAsync_EmptyPath_Throws()
    {
        var (dm, _) = Create();
        await Assert.ThrowsAsync<ArgumentNullException>(() => dm.OpenDocumentAsync(""));
    }

    // -----------------------------------------------------------------------
    // CloseDocument
    // -----------------------------------------------------------------------

    [Fact]
    public void CloseDocument_RemovesFromCollection()
    {
        var (dm, _) = Create();
        var doc = dm.NewDocument();
        dm.CloseDocument(doc);
        Assert.DoesNotContain(doc, dm.Documents);
    }

    [Fact]
    public void CloseDocument_ActiveDocumentBecomesNull_WhenLastDoc()
    {
        var (dm, _) = Create();
        var doc = dm.NewDocument();
        dm.CloseDocument(doc);
        Assert.Null(dm.ActiveDocument);
    }

    [Fact]
    public void CloseDocument_SwitchesActiveToRemaining()
    {
        var (dm, _) = Create();
        var doc1 = dm.NewDocument();
        var doc2 = dm.NewDocument();

        // doc2 is active; closing it should switch to doc1
        dm.CloseDocument(doc2);
        Assert.Same(doc1, dm.ActiveDocument);
    }

    [Fact]
    public void CloseDocument_PublishesDocumentClosed()
    {
        var (dm, bus) = Create();
        var doc = dm.NewDocument();
        dm.CloseDocument(doc);
        Assert.Single(bus.MessagesOf<DocumentClosed>());
    }

    [Fact]
    public void CloseDocument_NullArgument_Throws()
    {
        var (dm, _) = Create();
        Assert.Throws<ArgumentNullException>(() => dm.CloseDocument(null!));
    }

    // -----------------------------------------------------------------------
    // MarkDirty
    // -----------------------------------------------------------------------

    [Fact]
    public void MarkDirty_ReportsDirtyStateFromUndoStack()
    {
        var (dm, _) = Create();
        var doc = dm.NewDocument();

        // IsDirty is now derived from UndoStack.IsOriginalFile.
        // A new untouched document is clean.
        Assert.False(doc.IsDirty);

        // After modifying content, the undo stack is no longer at the original.
        doc.Content = "hello";
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void MarkDirty_PublishesDocumentModified()
    {
        var (dm, bus) = Create();
        var doc = dm.NewDocument();
        dm.MarkDirty(doc);
        Assert.Single(bus.MessagesOf<DocumentModified>());
    }

    [Fact]
    public void MarkDirty_AlreadyDirty_DoesNotPublishAgain()
    {
        var (dm, bus) = Create();
        var doc = dm.NewDocument();
        dm.MarkDirty(doc);
        dm.MarkDirty(doc);   // second call — already dirty
        Assert.Single(bus.MessagesOf<DocumentModified>());
    }

    [Fact]
    public void MarkDirty_AfterUndoBackToClean_PublishesTransitionToClean()
    {
        var (dm, bus) = Create();
        var doc = dm.NewDocument();

        // User types — becomes dirty
        doc.Content = "hello";
        dm.MarkDirty(doc);
        Assert.True(doc.IsDirty);

        // User undoes back to original empty content
        doc.Undo();
        dm.MarkDirty(doc);
        Assert.False(doc.IsDirty);

        // Two transitions published: clean→dirty, then dirty→clean
        Assert.Equal(2, bus.MessagesOf<DocumentModified>().Count());
    }

    // -----------------------------------------------------------------------
    // SaveDocumentAsync
    // -----------------------------------------------------------------------

    [Fact]
    public void SaveDocumentAsync_WritesContentToFile()
    {
        SingleThread.Run(async () =>
        {
            var (dm, _) = Create();
            var tmp = Path.GetTempFileName();
            try
            {
                var doc = await dm.OpenDocumentAsync(tmp);
                doc.Content = "saved content";
                dm.MarkDirty(doc);

                await dm.SaveDocumentAsync(doc);

                Assert.Equal("saved content", await File.ReadAllTextAsync(tmp));
            }
            finally { File.Delete(tmp); }
        });
    }

    [Fact]
    public void SaveDocumentAsync_ClearsDirtyFlag()
    {
        SingleThread.Run(async () =>
        {
            var (dm, _) = Create();
            var tmp = Path.GetTempFileName();
            try
            {
                var doc = await dm.OpenDocumentAsync(tmp);
                dm.MarkDirty(doc);
                await dm.SaveDocumentAsync(doc);
                Assert.False(doc.IsDirty);
            }
            finally { File.Delete(tmp); }
        });
    }

    [Fact]
    public void SaveDocumentAsync_PublishesDocumentSaved()
    {
        SingleThread.Run(async () =>
        {
            var (dm, bus) = Create();
            var tmp = Path.GetTempFileName();
            try
            {
                var doc = await dm.OpenDocumentAsync(tmp);
                await dm.SaveDocumentAsync(doc);
                Assert.Single(bus.MessagesOf<DocumentSaved>());
            }
            finally { File.Delete(tmp); }
        });
    }

    [Fact]
    public async Task SaveDocumentAsync_NewDocument_DoesNotThrow()
    {
        // New (untitled) documents can't be saved without a path — should be a no-op
        var (dm, _) = Create();
        var doc = dm.NewDocument();
        var ex = await Record.ExceptionAsync(() => dm.SaveDocumentAsync(doc));
        Assert.Null(ex);
    }

    // -----------------------------------------------------------------------
    // SaveDocumentAsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveDocumentAsAsync_WritesContentAndUpdatesFilePath()
    {
        var (dm, _) = Create();
        var tmp = Path.GetTempFileName();
        try
        {
            var doc = dm.NewDocument();
            doc.Content = "new content";
            await dm.SaveDocumentAsAsync(doc, tmp);

            Assert.Equal(tmp, doc.FilePath);
            Assert.Equal("new content", await File.ReadAllTextAsync(tmp));
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task SaveDocumentAsAsync_ClearsDirtyFlag()
    {
        var (dm, _) = Create();
        var tmp = Path.GetTempFileName();
        try
        {
            var doc = dm.NewDocument();
            dm.MarkDirty(doc);
            await dm.SaveDocumentAsAsync(doc, tmp);
            Assert.False(doc.IsDirty);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task SaveDocumentAsAsync_SetsLanguageFromPath()
    {
        var (dm, _) = Create();
        var tmp = Path.ChangeExtension(Path.GetTempFileName(), ".cs");
        try
        {
            var doc = dm.NewDocument();
            await dm.SaveDocumentAsAsync(doc, tmp);
            Assert.Equal("C#", doc.Language);
        }
        finally { File.Delete(tmp); }
    }

    // -----------------------------------------------------------------------
    // Language detection (via ILanguageDetectionService)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(".cs", "C#")]
    [InlineData(".xaml", "XAML")]
    [InlineData(".fs", "F#")]
    [InlineData(".yaml", "YAML")]
    [InlineData(".sql", "SQL")]
    [InlineData(".unknown", "Plain Text")]
    public async Task OpenDocumentAsync_DetectsLanguage(string extension, string expectedLanguage)
    {
        var (dm, _) = Create();
        var tmp = Path.ChangeExtension(Path.GetTempFileName(), extension);
        try
        {
            File.WriteAllText(tmp, "");
            var doc = await dm.OpenDocumentAsync(tmp);
            Assert.Equal(expectedLanguage, doc.Language);
        }
        finally { File.Delete(tmp); }
    }

    // -----------------------------------------------------------------------
    // ActivateDocument
    // -----------------------------------------------------------------------

    [Fact]
    public void ActivateDocument_ChangesActiveDocument()
    {
        var (dm, _) = Create();
        var doc1 = dm.NewDocument();
        var doc2 = dm.NewDocument();

        dm.ActivateDocument(doc1);
        Assert.Same(doc1, dm.ActiveDocument);
    }

    [Fact]
    public void ActivateDocument_PublishesActiveDocumentChanged()
    {
        var (dm, bus) = Create();
        var doc1 = dm.NewDocument();
        var doc2 = dm.NewDocument();   // this also publishes one ActiveDocumentChanged

        var countBefore = bus.MessagesOf<ActiveDocumentChanged>().Count();
        dm.ActivateDocument(doc1);
        Assert.Equal(countBefore + 1, bus.MessagesOf<ActiveDocumentChanged>().Count());
    }

    [Fact]
    public void ActivateDocument_DocumentNotInCollection_Throws()
    {
        var (dm, _) = Create();
        var foreign = new TextDocument("x", "/tmp/foreign.txt");
        Assert.Throws<InvalidOperationException>(() => dm.ActivateDocument(foreign));
    }


}
