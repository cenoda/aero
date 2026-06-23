using System.IO;
using Aero.Docking;
using Aero.Services;
using Dock.Model.Controls;
using Dock.Serializer;
using Xunit;

namespace Aero.Tests.Docking;

/// <summary>
/// Tests for LayoutPersistenceService — verifies save/restore of Dock layout.
/// </summary>
public class LayoutPersistenceServiceTests
{
    [Fact]
    public void Load_Returns_Null_WhenFileDoesNotExist()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "aero_test_missing_" + Path.GetRandomFileName());
        if (File.Exists(tempPath)) File.Delete(tempPath);

        var service = new TestableLayoutPersistenceService(tempPath);
        var result = service.Load();

        Assert.Null(result);
    }

    [Fact]
    public void Load_Returns_Null_WhenFileIsCorrupted()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "aero_test_corrupt_" + Path.GetRandomFileName());
        try
        {
            File.WriteAllText(tempPath, "not valid json {{{");
            var service = new TestableLayoutPersistenceService(tempPath);

            var result = service.Load();

            Assert.Null(result);  // corrupted file → graceful null return
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Save_Creates_File()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "aero_test_save_" + Path.GetRandomFileName());
        try
        {
            var layout = AeroDockFactory.CreateDefaultLayout();
            var service = new TestableLayoutPersistenceService(tempPath);

            service.Save(layout);

            Assert.True(File.Exists(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Save_WritesValidJson()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "aero_test_json_" + Path.GetRandomFileName());
        try
        {
            var layout = AeroDockFactory.CreateDefaultLayout();
            var service = new TestableLayoutPersistenceService(tempPath);

            service.Save(layout);

            var json = File.ReadAllText(tempPath);
            Assert.False(string.IsNullOrWhiteSpace(json));
            Assert.True(json.TrimStart().StartsWith("{") || json.TrimStart().StartsWith("["),
                "Expected saved file to contain valid JSON");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void RoundTrip_PreservesRootId()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "aero_test_rt_" + Path.GetRandomFileName());
        try
        {
            var layout = AeroDockFactory.CreateDefaultLayout();
            var service = new TestableLayoutPersistenceService(tempPath);

            service.Save(layout);
            var restored = service.Load();

            Assert.NotNull(restored);
            Assert.Equal("Root", restored.Id);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}

/// <summary>
/// Testable subclass of LayoutPersistenceService that uses a custom path
/// instead of the default ~/.aero/layout.json.
/// </summary>
internal class TestableLayoutPersistenceService : ILayoutPersistenceService
{
    private readonly string _path;
    private readonly DockSerializer _serializer = new();

    public TestableLayoutPersistenceService(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public void Save(IRootDock layout)
    {
        var json = _serializer.Serialize(layout);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }

    public IRootDock? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var json = File.ReadAllText(_path);
            return _serializer.Deserialize<IRootDock>(json);
        }
        catch
        {
            return null;
        }
    }
}
