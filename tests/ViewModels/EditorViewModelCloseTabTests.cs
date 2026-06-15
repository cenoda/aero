using System.Linq;
using Aero.Core;
using Aero.Services;
using Aero.ViewModels;
using Aero.Tests.Stubs;
using Xunit;

namespace Aero.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="EditorViewModel.CloseActiveTab"/> /
/// <see cref="EditorViewModel.CloseTab"/> — verifies the publish contract
/// that <c>ConfirmDirtyClose</c> reaches the bus when closing a dirty tab,
/// so any UI subscriber can show a confirmation dialog (ISSUE-001).
/// </summary>
public class EditorViewModelCloseTabTests
{
    private static (EditorViewModel vm, StubMessageBus bus, DocumentManager dm) Create()
    {
        var bus = new StubMessageBus();
        var dm = new DocumentManager(bus);
        var findReplace = new FindReplaceViewModel();
        var vm = new EditorViewModel(dm, bus, findReplace);
        return (vm, bus, dm);
    }

    [Fact]
    public void CloseActiveTab_DirtyDoc_PublishesConfirmDirtyClose()
    {
        var (vm, bus, dm) = Create();
        vm.NewFile();
        var doc = vm.ActiveTab!.Document;
        doc.Content = "hello";
        dm.MarkDirty(doc);
        Assert.True(doc.IsDirty);

        // Ignore startup messages from NewFile / MarkDirty
        bus.Published.Clear();

        vm.CloseActiveTab();

        var prompts = bus.MessagesOf<ConfirmDirtyClose>().ToList();
        Assert.Single(prompts);
        Assert.Equal(doc.FileName, prompts[0].FileName);
    }

    [Fact]
    public void CloseActiveTab_CleanDoc_DoesNotPublishConfirmDirtyClose()
    {
        var (vm, bus, _) = Create();
        vm.NewFile();
        // Do not modify the document — it stays clean

        bus.Published.Clear();

        vm.CloseActiveTab();

        Assert.Empty(bus.MessagesOf<ConfirmDirtyClose>());
    }

    [Fact]
    public void CloseTab_DirtyDoc_PublishesConfirmDirtyClose()
    {
        var (vm, bus, dm) = Create();
        vm.NewFile();
        var tab = vm.ActiveTab!;
        var doc = tab.Document;
        doc.Content = "dirty content";
        dm.MarkDirty(doc);

        bus.Published.Clear();

        vm.CloseTab(tab);

        Assert.Single(bus.MessagesOf<ConfirmDirtyClose>());
    }

    [Fact]
    public void CloseActiveTab_NoActiveDoc_IsNoOp()
    {
        var (vm, bus, _) = Create();
        // No document created
        bus.Published.Clear();

        vm.CloseActiveTab();

        Assert.Empty(bus.Published);
    }
}
