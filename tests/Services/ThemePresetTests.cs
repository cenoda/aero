using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aero.Models.Settings;
using Aero.Services;
using NSubstitute;
using Xunit;

namespace Aero.Tests.Services;

/// <summary>
/// M2 tests: token key verification across presets + SettingsModel theme round-trip.
/// Parses the AXAML files directly (no Avalonia Application required).
/// </summary>
public class ThemePresetTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _settingsPath;

    public ThemePresetTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "aero-theme-preset-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _settingsPath = Path.Combine(_testDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    /// <summary>
    /// Extract all x:Key values from an AXAML ResourceDictionary file.
    /// Filters out the _themeVariant sentinel key.
    /// </summary>
    private static string[] GetTokenKeys(string axamlPath)
    {
        Assert.True(File.Exists(axamlPath), $"File not found: {axamlPath}");
        var lines = File.ReadAllLines(axamlPath);
        return lines
            .Select(l => l.Trim())
            .Where(l => l.Contains("x:Key=\""))
            .Select(l =>
            {
                var start = l.IndexOf("x:Key=\"") + 7;
                var end = l.IndexOf("\"", start);
                return l[start..end];
            })
            .Where(k => k != "_themeVariant")
            .ToArray();
    }

    private static string FindAxamlFile(string fileName)
    {
        // File is copied to test output dir via aero.Tests.csproj Content include
        var candidate = Path.Combine(AppContext.BaseDirectory, "Styles", fileName);
        if (File.Exists(candidate)) return candidate;

        // Fallback: walk up from test output to find src/Styles/
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            candidate = Path.Combine(dir, "src", "Styles", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir) ?? "";
        }
        throw new FileNotFoundException(
            $"Could not find {fileName}. Searched: {AppContext.BaseDirectory}/Styles/ and walked up to project root.");
    }

    [Fact]
    public void ThemeLightAxaml_ContainsExactly115TokenKeys()
    {
        var path = FindAxamlFile("ThemeLight.axaml");

        var keys = GetTokenKeys(path);

        Assert.Equal(115, keys.Length);
    }

    [Fact]
    public void ThemeDarkAxaml_ContainsExactly115TokenKeys()
    {
        var path = FindAxamlFile("ThemeDark.axaml");

        var keys = GetTokenKeys(path);

        Assert.Equal(115, keys.Length);
    }

    [Fact]
    public void LightAndDarkPresets_HaveIdenticalTokenKeys()
    {
        var lightPath = FindAxamlFile("ThemeLight.axaml");
        var darkPath = FindAxamlFile("ThemeDark.axaml");

        var lightKeys = GetTokenKeys(lightPath).OrderBy(k => k).ToArray();
        var darkKeys = GetTokenKeys(darkPath).OrderBy(k => k).ToArray();

        Assert.Equal(lightKeys.Length, darkKeys.Length);

        for (int i = 0; i < lightKeys.Length; i++)
        {
            Assert.Equal(lightKeys[i], darkKeys[i]);
        }
    }

    [Fact]
    public void LightPreset_ContainsAllExpectedGlobalTokens()
    {
        var keys = GetTokenKeys(FindAxamlFile("ThemeLight.axaml"));

        Assert.Contains("global.background", keys);
        Assert.Contains("global.foreground", keys);
        Assert.Contains("global.accent", keys);
        Assert.Contains("global.accentHover", keys);
        Assert.Contains("global.error", keys);
        Assert.Contains("global.warning", keys);
    }

    [Fact]
    public void LightPreset_ContainsAllExpectedEditorTokens()
    {
        var keys = GetTokenKeys(FindAxamlFile("ThemeLight.axaml"));

        Assert.Contains("editor.background", keys);
        Assert.Contains("editor.foreground", keys);
        Assert.Contains("editor.lineHighlightBackground", keys);
        Assert.Contains("editor.selectionBackground", keys);
        Assert.Contains("editor.findMatchBackground", keys);
        Assert.Contains("editor.bracketMatchBackground", keys);
    }

    [Fact]
    public void LightPreset_ContainsAllExpectedGitTokens()
    {
        var keys = GetTokenKeys(FindAxamlFile("ThemeLight.axaml"));

        Assert.Contains("diff.insertedGutter", keys);
        Assert.Contains("diff.removedGutter", keys);
        Assert.Contains("diff.insertedText", keys);
        Assert.Contains("diff.removedText", keys);
        Assert.Contains("graph.background", keys);
        Assert.Contains("graph.nodeBorder", keys);
        Assert.Contains("git.branchForeground", keys);
        Assert.Contains("git.modifiedForeground", keys);
        Assert.Contains("git.stagedForeground", keys);
    }

    // ── SettingsModel theme tests ─────────────────────────────────────

    [Fact]
    public void SettingsModel_Theme_DefaultsToLight()
    {
        var model = new SettingsModel();

        Assert.Equal("Light", model.Theme);
    }

    [Fact]
    public async Task SettingsModel_Theme_RoundTrips_ViaJson()
    {
        var darkSettings = new SettingsModel { Theme = "Dark" };
        var json = System.Text.Json.JsonSerializer.Serialize(darkSettings);
        await File.WriteAllTextAsync(_settingsPath, json);

        var loaded = System.Text.Json.JsonSerializer.Deserialize<SettingsModel>(
            await File.ReadAllTextAsync(_settingsPath));

        Assert.NotNull(loaded);
        Assert.Equal("Dark", loaded.Theme);
    }

    [Fact]
    public void SettingsModel_Theme_AllowsLightAndDarkValues()
    {
        var light = new SettingsModel { Theme = "Light" };
        var dark = new SettingsModel { Theme = "Dark" };

        Assert.Equal("Light", light.Theme);
        Assert.Equal("Dark", dark.Theme);
    }
}
