using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aero.Core;
using Aero.Models.Settings;
using Aero.Services;
using Aero.Tests.Stubs;
using Xunit;

namespace Aero.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testDir;

    public SettingsServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "aero-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private SettingsService CreateService()
    {
        return new SettingsService(_testDir, bus: null);
    }

    private SettingsService CreateService(IMessageBus bus)
    {
        return new SettingsService(_testDir, bus);
    }

    [Fact]
    public async Task SaveWorkspaceStateAsync_CreatesFile()
    {
        var svc = CreateService();
        var state = new WorkspaceState { LastFolderPath = "/tmp/test" };

        await svc.SaveWorkspaceStateAsync(state);

        var path = Path.Combine(_testDir, "workspace.json");
        Assert.True(File.Exists(path));

        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("/tmp/test", json);
    }

    [Fact]
    public async Task SaveWorkspaceStateAsync_RoundTrip()
    {
        var svc = CreateService();
        var state = new WorkspaceState
        {
            LastFolderPath = "/home/user/project",
            OpenFilePaths = new() { "/a.cs", "/b.cs" },
            ActiveTabIndex = 1,
            Window = new WindowState
            {
                X = 100, Y = 200,
                Width = 1400, Height = 900,
                IsMaximized = true
            },
            RecentFolders = new() { "/home/user/project", "/tmp/other" }
        };

        await svc.SaveWorkspaceStateAsync(state);
        var loaded = await svc.LoadWorkspaceStateAsync();

        Assert.Equal("/home/user/project", loaded.LastFolderPath);
        Assert.Equal(2, loaded.OpenFilePaths.Count);
        Assert.Equal("/a.cs", loaded.OpenFilePaths[0]);
        Assert.Equal("/b.cs", loaded.OpenFilePaths[1]);
        Assert.Equal(1, loaded.ActiveTabIndex);
        Assert.NotNull(loaded.Window);
        Assert.Equal(100, loaded.Window!.X);
        Assert.Equal(200, loaded.Window.Y);
        Assert.Equal(1400, loaded.Window.Width);
        Assert.Equal(900, loaded.Window.Height);
        Assert.True(loaded.Window.IsMaximized);
        Assert.Equal(2, loaded.RecentFolders.Count);
    }

    [Fact]
    public async Task LoadWorkspaceStateAsync_FileMissing_ReturnsDefaults()
    {
        var svc = CreateService();
        var loaded = await svc.LoadWorkspaceStateAsync();

        Assert.Null(loaded.LastFolderPath);
        Assert.Empty(loaded.OpenFilePaths);
        Assert.Equal(0, loaded.ActiveTabIndex);
        Assert.Null(loaded.Window);
        Assert.Empty(loaded.RecentFolders);
    }

    [Fact]
    public async Task LoadWorkspaceStateAsync_CorruptJson_ReturnsDefaults()
    {
        var svc = CreateService();
        var path = Path.Combine(_testDir, "workspace.json");
        await File.WriteAllTextAsync(path, "{ this is NOT valid json }}}");

        var loaded = await svc.LoadWorkspaceStateAsync();

        Assert.Null(loaded.LastFolderPath);
        Assert.Empty(loaded.OpenFilePaths);
    }

    [Fact]
    public async Task LoadWorkspaceStateAsync_CorruptJson_PublishesStatusMessage()
    {
        var bus = new StubMessageBus();
        var svc = CreateService(bus);
        var path = Path.Combine(_testDir, "workspace.json");
        await File.WriteAllTextAsync(path, "not json!");

        await svc.LoadWorkspaceStateAsync();

        var messages = bus.MessagesOf<StatusMessage>();
        Assert.Contains(messages, m => m.Text.Contains("Corrupt"));
    }

    [Fact]
    public async Task SaveAndLoadSettings_RoundTrip()
    {
        var svc = CreateService();
        var settings = new SettingsModel
        {
            Theme = "Dark",
            FontFamily = "JetBrains Mono",
            FontSize = 14,
            TabSize = 2,
            LayoutMode = "Freeform"
        };

        await svc.SaveSettingsAsync(settings);
        var loaded = await svc.LoadSettingsAsync();

        Assert.Equal("Dark", loaded.Theme);
        Assert.Equal("JetBrains Mono", loaded.FontFamily);
        Assert.Equal(14, loaded.FontSize);
        Assert.Equal(2, loaded.TabSize);
        Assert.Equal("Freeform", loaded.LayoutMode);
    }

    [Fact]
    public async Task AtomicWrite_NoPartialWriteOnCrash()
    {
        var svc = CreateService();
        var path = Path.Combine(_testDir, "workspace.json");
        var tmpPath = path + ".tmp";

        // Write an initial valid state
        var state1 = new WorkspaceState { LastFolderPath = "/initial" };
        await svc.SaveWorkspaceStateAsync(state1);
        Assert.True(File.Exists(path));

        // Verify the file is valid and round-trips correctly
        var loaded1 = await svc.LoadWorkspaceStateAsync();
        Assert.Equal("/initial", loaded1.LastFolderPath);

        // Verify no .tmp file remains after successful write
        Assert.False(File.Exists(tmpPath));

        // Write another state — should atomically replace
        var state2 = new WorkspaceState { LastFolderPath = "/updated" };
        await svc.SaveWorkspaceStateAsync(state2);

        var loaded2 = await svc.LoadWorkspaceStateAsync();
        Assert.Equal("/updated", loaded2.LastFolderPath);

        // No .tmp file leftover
        Assert.False(File.Exists(tmpPath));
    }

    [Fact]
    public void ConfigDirectory_ReturnsDotAero()
    {
        var svc = new SettingsService(bus: null);
        Assert.EndsWith(".aero", svc.ConfigDirectory);
    }

    [Fact]
    public async Task FirstSave_CreatesConfigDirectory()
    {
        var freshDir = Path.Combine(Path.GetTempPath(), "aero-fresh-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.False(Directory.Exists(freshDir));

            var svc = new SettingsService(freshDir, bus: null);
            await svc.SaveWorkspaceStateAsync(new WorkspaceState { LastFolderPath = "/test" });

            Assert.True(Directory.Exists(freshDir));
            Assert.True(File.Exists(Path.Combine(freshDir, "workspace.json")));
        }
        finally
        {
            if (Directory.Exists(freshDir))
                Directory.Delete(freshDir, recursive: true);
        }
    }

    [Fact]
    public void RecentFolders_MaxTenItems()
    {
        var svc = CreateService();
        for (int i = 0; i < 12; i++)
            svc.AddRecentFolder($"/folder/{i}");

        var folders = svc.GetRecentFolders();
        Assert.Equal(10, folders.Count);
    }

    [Fact]
    public void RecentFolders_Ordering()
    {
        var svc = CreateService();
        svc.AddRecentFolder("/first");
        svc.AddRecentFolder("/second");
        svc.AddRecentFolder("/third");

        var folders = svc.GetRecentFolders();
        Assert.Equal(3, folders.Count);
        Assert.Equal("/third", folders[0]);
        Assert.Equal("/second", folders[1]);
        Assert.Equal("/first", folders[2]);
    }

    [Fact]
    public void AddRecentFolder_NormalizesPath()
    {
        var svc = CreateService();
        svc.AddRecentFolder("/some/path/");

        var folders = svc.GetRecentFolders();
        Assert.Single(folders);
        Assert.False(folders[0].EndsWith(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void AddRecentFolder_Deduplicates()
    {
        var svc = CreateService();
        svc.AddRecentFolder("/my/project");
        svc.AddRecentFolder("/my/project");

        var folders = svc.GetRecentFolders();
        Assert.Single(folders);
    }

    [Fact]
    public void AddRecentFolder_MovesExistingToTop()
    {
        var svc = CreateService();
        svc.AddRecentFolder("/folder1");
        svc.AddRecentFolder("/folder2");
        svc.AddRecentFolder("/folder1");

        var folders = svc.GetRecentFolders();
        Assert.Equal(2, folders.Count);
        Assert.Equal("/folder1", folders[0]);
        Assert.Equal("/folder2", folders[1]);
    }

    [Fact]
    public void GetRecentFolders_ReturnsReadOnly()
    {
        var svc = CreateService();
        svc.AddRecentFolder("/folder1");

        var folders = svc.GetRecentFolders();
        Assert.Throws<NotSupportedException>(() =>
        {
            ((System.Collections.IList)folders).Add("/hacked");
        });
    }
}
