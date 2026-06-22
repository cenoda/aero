using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aero.Models.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Aero.Services;

/// <summary>
/// Manages theme lifecycle: preset loading, runtime switching, JSON override.
/// Resolves <see cref="ISettingsService"/> for persistence of Light/Dark choice.
///
/// Both <c>ThemeLight.axaml</c> and <c>ThemeDark.axaml</c> are loaded as
/// <see cref="ResourceInclude"/> entries in <c>App.axaml</c> MergedDictionaries.
/// This service identifies them via <c>_themeVariant</c> sentinel keys and manages
/// which one is active by adding/removing from the merged set.
/// </summary>
public sealed class ThemeService
{
    private readonly ISettingsService _settings;
    private readonly string _overridePath;

    /// <summary>Pre-loaded Light theme ResourceDictionary.</summary>
    public ResourceDictionary LightTheme { get; private set; } = null!;

    /// <summary>Pre-loaded Dark theme ResourceDictionary.</summary>
    public ResourceDictionary DarkTheme { get; private set; } = null!;

    /// <summary>True if the currently active theme is Dark.</summary>
    public bool IsDark { get; private set; }

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aero");
        _overridePath = Path.Combine(configDir, "theme-override.json");
    }

    /// <summary>
    /// Test-only constructor: inject a custom override path and mock settings.
    /// </summary>
    internal ThemeService(ISettingsService settings, string overridePath)
    {
        _settings = settings;
        _overridePath = overridePath;
    }

    /// <summary>
    /// Scan <see cref="Application.Current"/> MergedDictionaries for theme dictionaries
    /// identified by the <c>_themeVariant</c> sentinel key, and assign them to
    /// <see cref="LightTheme"/> and <see cref="DarkTheme"/>.
    /// Must be called from the UI thread after <see cref="AvaloniaXamlLoader.Load"/>
    /// has parsed the ResourceIncludes in <c>App.axaml</c>.
    /// </summary>
    public void WireThemeDictionaries()
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var merged = app.Resources.MergedDictionaries;

        foreach (var dict in merged.OfType<ResourceDictionary>())
        {
            if (!dict.ContainsKey("_themeVariant"))
            {
                continue;
            }

            var variant = dict["_themeVariant"] as string;
            if (variant == "Light")
            {
                LightTheme = dict;
            }
            else if (variant == "Dark")
            {
                DarkTheme = dict;
            }
        }
    }

    /// <summary>
    /// Apply persisted theme + JSON override at startup.
    /// Must be called from the UI thread after <see cref="Application.Current"/> is available.
    /// </summary>
    public async Task ApplyThemeAsync()
    {
        var settings = await _settings.LoadSettingsAsync();
        var themeName = settings.Theme ?? "Light";
        SetActiveTheme(themeName);

        var overrides = await LoadOverrideAsync();
        if (overrides.Count > 0)
        {
            ApplyOverride(overrides);
        }
    }

    /// <summary>
    /// Toggle Light ↔ Dark at runtime. Saves the new choice to settings,
    /// swaps the active ResourceDictionary, reapplies JSON override, and updates
    /// <see cref="Application.Current.RequestedThemeVariant"/>.
    /// </summary>
    public async Task ToggleThemeAsync()
    {
        var settings = await _settings.LoadSettingsAsync();
        var toggled = IsDark ? "Light" : "Dark";

        settings = settings with { Theme = toggled };
        await _settings.SaveSettingsAsync(settings);

        SetActiveTheme(toggled);

        var overrides = await LoadOverrideAsync();
        if (overrides.Count > 0)
        {
            ApplyOverride(overrides);
        }

        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = IsDark
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }
    }

    /// <summary>
    /// Read <c>~/.aero/theme-override.json</c>. Returns an empty dictionary if
    /// the file is missing or contains invalid JSON (silently skips errors per spec).
    /// Does NOT create the file on startup (R1.7).
    /// </summary>
    public async Task<Dictionary<string, string>> LoadOverrideAsync()
    {
        if (!File.Exists(_overridePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = await File.ReadAllTextAsync(_overridePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            return result;
        }
        catch (Exception)
        {
            // Silently return empty dict on any parse/read error per spec
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Parse hex color strings and set matching keys on the active theme ResourceDictionary.
    /// Unknown keys are silently ignored. Invalid hex values are silently skipped.
    /// Accepts <c>#RRGGBB</c> and <c>#RRGGBBAA</c> formats.
    /// </summary>
    public void ApplyOverride(Dictionary<string, string> overrides)
    {
        var activeDict = IsDark ? DarkTheme : LightTheme;

        foreach (var (key, hexValue) in overrides)
        {
            if (string.IsNullOrWhiteSpace(hexValue))
            {
                continue;
            }

            if (TryParseHexColor(hexValue, out var color))
            {
                var brush = new SolidColorBrush(color);
                activeDict[key] = brush;
            }
            // Invalid hex → silently skip
        }
    }

    // ── Private helpers ────────────────────────────────────────────────

    /// <summary>
    /// Remove the inactive theme from MergedDictionaries and ensure
    /// only the active one is present. Positions it at index 0 so
    /// theme tokens are available to all downstream lookups.
    /// </summary>
    private void SetActiveTheme(string themeName)
    {
        IsDark = string.Equals(themeName, "Dark", StringComparison.OrdinalIgnoreCase);

        if (Application.Current is not { } app)
        {
            return;
        }

        var merged = app.Resources.MergedDictionaries;

        // Remove both if present, then add back only the active one at position 0
        merged.Remove(LightTheme);
        merged.Remove(DarkTheme);

        var activeDict = IsDark ? DarkTheme : LightTheme;
        merged.Insert(0, activeDict);
    }

    /// <summary>
    /// Attempt to parse a hex color string into an <see cref="Color"/>.
    /// Supports <c>#RGB</c>, <c>#RRGGBB</c>, and <c>#RRGGBBAA</c> formats.
    /// </summary>
    internal static bool TryParseHexColor(string hex, out Color color)
    {
        color = default;

        if (string.IsNullOrEmpty(hex))
        {
            return false;
        }

        // Ensure leading # for Color.Parse
        var h = hex.StartsWith('#') ? hex : "#" + hex;

        try
        {
            color = Color.Parse(h);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
