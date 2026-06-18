using System.IO;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Languages;
using Aero.Models.Editor;
using Aero.Services;
using Aero.Tests.Stubs;
using Aero.ViewModels;
using Xunit;


namespace Aero.Tests.ViewModels;

public class EditorViewModelLanguageTests
{
    private static (EditorViewModel vm, StubMessageBus bus, DocumentManager dm, ILanguageDetectionService detector) Create()
    {
        var bus = new StubMessageBus();
        var detector = new LanguageDetectionService();
        var dm = new DocumentManager(bus, detector);
        var findReplace = new FindReplaceViewModel();
        var vm = new EditorViewModel(dm, bus, findReplace, detector);
        return (vm, bus, dm, detector);
    }

    [Fact]
    public void NewFile_SetsLanguageToPlainText()
    {
        var (vm, _, _, _) = Create();

        vm.NewFile();

        var doc = vm.ActiveTab!.Document;
        Assert.Equal("Plain Text", doc.Language);
        Assert.Equal("plaintext", vm.ActiveTab.LanguageId);
    }

    [Fact]
    public void OpenFileAsync_CsFile_SetsLanguageToCSharp()
    {
        SingleThread.Run(async () =>
        {
            var (vm, _, _, _) = Create();
            var path = Path.GetTempFileName() + ".cs";
            await File.WriteAllTextAsync(path, "class Foo {}");

            try
            {
                await vm.OpenFileAsync(path);

                var doc = vm.ActiveTab!.Document;
                Assert.Equal("C#", doc.Language);
                Assert.Equal("csharp", vm.ActiveTab.LanguageId);
            }
            finally
            {
                File.Delete(path);
            }
        });
    }

    [Fact]
    public void OpenFileAsync_JsonFile_SetsLanguageToJson()
    {
        SingleThread.Run(async () =>
        {
            var (vm, _, _, _) = Create();
            var path = Path.GetTempFileName() + ".json";
            await File.WriteAllTextAsync(path, "{}");

            try
            {
                await vm.OpenFileAsync(path);

                var doc = vm.ActiveTab!.Document;
                Assert.Equal("JSON", doc.Language);
                Assert.Equal("json", vm.ActiveTab.LanguageId);
            }
            finally
            {
                File.Delete(path);
            }
        });
    }

    [Fact]
    public void OpenFileAsync_UnknownExtension_SetsLanguageToPlainText()
    {
        SingleThread.Run(async () =>
        {
            var (vm, _, _, _) = Create();
            var path = Path.GetTempFileName() + ".xyz";
            await File.WriteAllTextAsync(path, "data");

            try
            {
                await vm.OpenFileAsync(path);

                var doc = vm.ActiveTab!.Document;
                Assert.Equal("Plain Text", doc.Language);
                Assert.Equal("plaintext", vm.ActiveTab.LanguageId);
            }
            finally
            {
                File.Delete(path);
            }
        });
    }

    [Fact]
    public void ActiveTabSwitch_UpdatesLanguageInStatus()
    {
        // Wrap in a single-thread pump because AvaloniaEdit's TextDocument is
        // thread-affine and xUnit async tests otherwise hop thread-pool threads.
        SingleThread.Run(async () =>
        {
            var (vm, _, _, _) = Create();
            var csPath = Path.GetTempFileName() + ".cs";
            var jsonPath = Path.GetTempFileName() + ".json";
            await File.WriteAllTextAsync(csPath, "class A {}");
            await File.WriteAllTextAsync(jsonPath, "{}");

            try
            {
                await vm.OpenFileAsync(csPath);
                var csTab = vm.ActiveTab!;

                await vm.OpenFileAsync(jsonPath);
                var jsonTab = vm.ActiveTab!;

                // switch back to cs tab
                vm.ActivateTab(csTab);

                Assert.Equal("C#", vm.Language);
            }
            finally
            {
                File.Delete(csPath);
                File.Delete(jsonPath);
            }
        });
    }

    [Fact]
    public void SaveAs_UpdatesTabLanguageId()
    {
        SingleThread.Run(async () =>
        {
            var (vm, _, _, _) = Create();
            var path = Path.GetTempFileName() + ".cs";

            try
            {
                vm.NewFile();
                var tab = vm.ActiveTab!;
                Assert.Equal("plaintext", tab.LanguageId);

                await vm.SaveAsAsync(path);

                Assert.Equal("C#", tab.Document.Language);
                Assert.Equal("csharp", tab.LanguageId);
                // Status bar label must refresh on Save As, not lag until next caret move.
                Assert.Equal("C#", vm.Language);
            }
            finally
            {
                File.Delete(path);
            }
        });
    }
}
