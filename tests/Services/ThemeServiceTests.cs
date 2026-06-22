using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aero.Services;
using Avalonia.Media;
using NSubstitute;
using Xunit;

namespace Aero.Tests.Services;

public class ThemeServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _overridePath;

    public ThemeServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "aero-theme-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _overridePath = Path.Combine(_testDir, "theme-override.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private ThemeService CreateService()
    {
        var settings = Substitute.For<ISettingsService>();
        return new ThemeService(settings, _overridePath);
    }

    // ── LoadOverrideAsync ──────────────────────────────────────────────

    [Fact]
    public async Task LoadOverrideAsync_FileMissing_ReturnsEmptyDict()
    {
        var svc = CreateService();

        var result = await svc.LoadOverrideAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadOverrideAsync_EmptyFile_ReturnsEmptyDict()
    {
        await File.WriteAllTextAsync(_overridePath, "");
        var svc = CreateService();

        var result = await svc.LoadOverrideAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadOverrideAsync_WhitespaceOnly_ReturnsEmptyDict()
    {
        await File.WriteAllTextAsync(_overridePath, "   \n  \t  ");
        var svc = CreateService();

        var result = await svc.LoadOverrideAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadOverrideAsync_InvalidJson_ReturnsEmptyDict()
    {
        await File.WriteAllTextAsync(_overridePath, "{invalid json content");
        var svc = CreateService();

        var result = await svc.LoadOverrideAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadOverrideAsync_ValidJson_ReturnsCorrectDict()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["editor.background"] = "#FF0000",
            ["panel.foreground"] = "#00FF00"
        });
        await File.WriteAllTextAsync(_overridePath, json);
        var svc = CreateService();

        var result = await svc.LoadOverrideAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("#FF0000", result["editor.background"]);
        Assert.Equal("#00FF00", result["panel.foreground"]);
    }

    [Fact]
    public async Task LoadOverrideAsync_EmptyObject_ReturnsEmptyDict()
    {
        await File.WriteAllTextAsync(_overridePath, "{}");
        var svc = CreateService();

        var result = await svc.LoadOverrideAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadOverrideAsync_NullValues_StillReturnsDict()
    {
        var json = "{\"editor.background\": null}";
        await File.WriteAllTextAsync(_overridePath, json);
        var svc = CreateService();

        var result = await svc.LoadOverrideAsync();

        // null JSON values become empty strings via GetString() ?? ""
        Assert.Single(result);
        Assert.Equal("", result["editor.background"]);
    }

    // ── TryParseHexColor ───────────────────────────────────────────────

    [Theory]
    [InlineData("#FF0000", true)]       // #RRGGBB
    [InlineData("#00FF00AA", true)]     // #RRGGBBAA
    [InlineData("#FFF", true)]          // #RGB
    [InlineData("#000", true)]          // #RGB black
    [InlineData("FF0000", true)]        // no leading # — still valid
    [InlineData("#123456", true)]       // another #RRGGBB
    public void TryParseHexColor_ValidFormats_ReturnsTrue(string hex, bool expected)
    {
        var result = ThemeService.TryParseHexColor(hex, out var color);

        Assert.Equal(expected, result);
        Assert.NotEqual(default(Color), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-color")]
    [InlineData("ZZZZZZ")]
    [InlineData("#GGGGGG")]
    public void TryParseHexColor_InvalidFormats_ReturnsFalse(string hex)
    {
        var result = ThemeService.TryParseHexColor(hex, out _);

        Assert.False(result);
    }

    // ── ApplyOverride ──────────────────────────────────────────────────

    [Fact]
    public void ApplyOverride_KnownKeysAreSet_OnActiveDictionary()
    {
        var svc = CreateService();

        // Create minimal ResourceDictionary instances to act as Light/Dark
        var lightDict = new Avalonia.Controls.ResourceDictionary();
        var darkDict = new Avalonia.Controls.ResourceDictionary();

        // Use reflection to set the private-set properties for testing
        typeof(ThemeService)
            .GetProperty(nameof(ThemeService.LightTheme))!
            .SetValue(svc, lightDict);
        typeof(ThemeService)
            .GetProperty(nameof(ThemeService.DarkTheme))!
            .SetValue(svc, darkDict);

        // Default IsDark is false, so Light is active
        var overrides = new Dictionary<string, string>
        {
            ["editor.background"] = "#123456",
            ["panel.foreground"] = "#AABBCC"
        };

        svc.ApplyOverride(overrides);

        // Verify the brushes were set on the light dictionary
        Assert.True(lightDict.ContainsKey("editor.background"));
        Assert.True(lightDict.ContainsKey("panel.foreground"));
    }

    [Fact]
    public void ApplyOverride_UnknownKeysAreSilentlyIgnored()
    {
        var svc = CreateService();
        var lightDict = new Avalonia.Controls.ResourceDictionary();
        var darkDict = new Avalonia.Controls.ResourceDictionary();
        typeof(ThemeService).GetProperty(nameof(ThemeService.LightTheme))!.SetValue(svc, lightDict);
        typeof(ThemeService).GetProperty(nameof(ThemeService.DarkTheme))!.SetValue(svc, darkDict);

        var overrides = new Dictionary<string, string>
        {
            ["nonexistent.token"] = "#FF0000"
        };

        // Should not throw
        svc.ApplyOverride(overrides);

        // The unknown key should still be set (dictionary allows any key),
        // but the point is no exception is thrown
    }

    [Fact]
    public void ApplyOverride_InvalidHex_SilentlySkipped()
    {
        var svc = CreateService();
        var lightDict = new Avalonia.Controls.ResourceDictionary();
        var darkDict = new Avalonia.Controls.ResourceDictionary();
        typeof(ThemeService).GetProperty(nameof(ThemeService.LightTheme))!.SetValue(svc, lightDict);
        typeof(ThemeService).GetProperty(nameof(ThemeService.DarkTheme))!.SetValue(svc, darkDict);

        var overrides = new Dictionary<string, string>
        {
            ["editor.background"] = "not-a-color"
        };

        // Should not throw
        svc.ApplyOverride(overrides);

        // The key should not be in the dictionary since the hex was invalid
        Assert.False(lightDict.ContainsKey("editor.background"));
    }

    [Fact]
    public void ApplyOverride_EmptyHexValue_SilentlySkipped()
    {
        var svc = CreateService();
        var lightDict = new Avalonia.Controls.ResourceDictionary();
        var darkDict = new Avalonia.Controls.ResourceDictionary();
        typeof(ThemeService).GetProperty(nameof(ThemeService.LightTheme))!.SetValue(svc, lightDict);
        typeof(ThemeService).GetProperty(nameof(ThemeService.DarkTheme))!.SetValue(svc, darkDict);

        var overrides = new Dictionary<string, string>
        {
            ["editor.background"] = ""
        };

        svc.ApplyOverride(overrides);

        Assert.False(lightDict.ContainsKey("editor.background"));
    }

    [Fact]
    public void ApplyOverride_WhitespaceHexValue_SilentlySkipped()
    {
        var svc = CreateService();
        var lightDict = new Avalonia.Controls.ResourceDictionary();
        var darkDict = new Avalonia.Controls.ResourceDictionary();
        typeof(ThemeService).GetProperty(nameof(ThemeService.LightTheme))!.SetValue(svc, lightDict);
        typeof(ThemeService).GetProperty(nameof(ThemeService.DarkTheme))!.SetValue(svc, darkDict);

        var overrides = new Dictionary<string, string>
        {
            ["editor.background"] = "   "
        };

        svc.ApplyOverride(overrides);

        Assert.False(lightDict.ContainsKey("editor.background"));
    }

    // ── LoadOverrideAsync + ApplyOverride integration ───────────────────

    [Fact]
    public async Task LoadAndApply_OverrideFileWithValidHex_AppliesToDict()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["editor.background"] = "#FF0000",
            ["bad.key"] = "invalid"
        });
        await File.WriteAllTextAsync(_overridePath, json);

        var svc = CreateService();
        var lightDict = new Avalonia.Controls.ResourceDictionary();
        var darkDict = new Avalonia.Controls.ResourceDictionary();
        typeof(ThemeService).GetProperty(nameof(ThemeService.LightTheme))!.SetValue(svc, lightDict);
        typeof(ThemeService).GetProperty(nameof(ThemeService.DarkTheme))!.SetValue(svc, darkDict);

        var overrides = await svc.LoadOverrideAsync();
        svc.ApplyOverride(overrides);

        Assert.True(lightDict.ContainsKey("editor.background"));
        Assert.False(lightDict.ContainsKey("bad.key"));
    }
}
