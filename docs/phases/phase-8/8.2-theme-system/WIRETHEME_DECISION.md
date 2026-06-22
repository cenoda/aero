# WireThemeDictionaries — Implementation Decision

> **Date:** 2026-06-22  
> **Context:** M1 blocker — `WireThemeDictionaries` method missing in `App.axaml.cs` (CS0103)  
> **Status:** ✅ RESOLVED — Approach 2 (Sentinel Key) implemented

---

## Problem Statement

`App.axaml.cs` calls `WireThemeDictionaries(themeService)` on line 34, but the method doesn't exist. This method must:

1. Scan `Application.Current.Resources.MergedDictionaries`
2. Find the `ResourceDictionary` instances loaded from `ThemeLight.axaml` and `ThemeDark.axaml`
3. Assign them to `ThemeService.LightTheme` and `ThemeService.DarkTheme` properties

---

## Brainstormed Approaches

### Approach 1: Heuristic Detection (Color Analysis)

**Concept:** Identify Light vs Dark by analyzing the `global.background` color value.

```csharp
private static void WireThemeDictionaries(ThemeService themeService)
{
    var merged = Application.Current.Resources.MergedDictionaries;
    
    foreach (var dict in merged.OfType<ResourceDictionary>())
    {
        if (dict.ContainsKey("global.background") && 
            dict["global.background"] is SolidColorBrush bg)
        {
            // Heuristic: Light themes have light backgrounds (R, G, B > 200)
            if (bg.Color.R > 200 && bg.Color.G > 200 && bg.Color.B > 200)
                themeService.LightTheme = dict;
            else
                themeService.DarkTheme = dict;
        }
    }
}
```

**Pros:**
- ✅ No changes to theme files
- ✅ Works with current 115-token setup

**Cons:**
- ❌ Fragile heuristic (background color could change)
- ❌ Requires color analysis logic
- ❌ Could misidentify themes

**Verdict:** ⚠️ Risky — heuristics are brittle

---

### Approach 2: Sentinel Key (Explicit Identification)

**Concept:** Add hidden sentinel keys to each theme file for explicit identification.

**Modify `ThemeLight.axaml`** (add at end):
```xml
<!-- Theme identification sentinel -->
<sys:Boolean x:Key="_themePreset">True</sys:Boolean>
<sys:String x:Key="_themeVariant">Light</sys:String>
```

**Modify `ThemeDark.axaml`** (add at end):
```xml
<!-- Theme identification sentinel -->
<sys:Boolean x:Key="_themePreset">True</sys:Boolean>
<sys:String x:Key="_themeVariant">Dark</sys:String>
```

**Implementation:**
```csharp
private static void WireThemeDictionaries(ThemeService themeService)
{
    var merged = Application.Current.Resources.MergedDictionaries;
    
    foreach (var dict in merged.OfType<ResourceDictionary>())
    {
        if (dict.ContainsKey("_themePreset") && 
            dict["_themeVariant"] is string variant)
        {
            if (variant == "Light")
                themeService.LightTheme = dict;
            else if (variant == "Dark")
                themeService.DarkTheme = dict;
        }
    }
}
```

**Pros:**
- ✅ 100% reliable — explicit identification
- ✅ Simple, no heuristics
- ✅ Easy to debug

**Cons:**
- ❌ Modifies theme files (adds 2 keys → 117 tokens)
- ❌ Sentinel keys appear in resource dictionary

**Verdict:** ✅ Recommended — most reliable

---

### Approach 3: Source URI Matching (Reflection-Based)

**Concept:** Access the `Source` property of loaded `ResourceDictionary` to identify origin AXAML file.

```csharp
private static void WireThemeDictionaries(ThemeService themeService)
{
    var merged = Application.Current.Resources.MergedDictionaries;
    
    foreach (var dict in merged.OfType<ResourceDictionary>())
    {
        var source = GetResourceDictionarySource(dict);
        
        if (source?.AbsoluteUri.Contains("ThemeLight.axaml") == true)
            themeService.LightTheme = dict;
        else if (source?.AbsoluteUri.Contains("ThemeDark.axaml") == true)
            themeService.DarkTheme = dict;
    }
}

private static Uri? GetResourceDictionarySource(ResourceDictionary dict)
{
    var sourceProperty = dict.GetType().GetProperty("Source");
    return sourceProperty?.GetValue(dict) as Uri;
}
```

**Pros:**
- ✅ No changes to theme files
- ✅ Matches by filename

**Cons:**
- ❌ Untested — Avalonia might not expose `Source` after loading
- ❌ Requires reflection (fragile)
- ❌ Might not work at all

**Verdict:** ⚠️ Unproven — needs investigation

---

### Approach 4: Index-Based (Order Assumption)

**Concept:** Assume `ResourceInclude` order in `App.axaml` matches `MergedDictionaries` order.

```csharp
private static void WireThemeDictionaries(ThemeService themeService)
{
    var merged = Application.Current.Resources.MergedDictionaries;
    
    var themeDicts = merged
        .OfType<ResourceDictionary>()
        .Where(d => d.ContainsKey("global.background"))
        .ToList();
    
    if (themeDicts.Count >= 2)
    {
        // Assume first is Light, second is Dark (based on App.axaml order)
        themeService.LightTheme = themeDicts[0];
        themeService.DarkTheme = themeDicts[1];
    }
}
```

**Pros:**
- ✅ Simple
- ✅ No file modifications

**Cons:**
- ❌ Assumes order is preserved (not guaranteed by Avalonia)
- ❌ Fragile if someone reorders includes

**Verdict:** ❌ Risky — order not guaranteed

---

### Approach 5: Token Value Analysis (Unique Value Matching)

**Concept:** Since both themes have identical token *names* but different *values*, find a token with a value unique to each theme.

Example: Check if `global.background` equals `#FFFFFFFF` (Light) or `#FF1E1E1E` (Dark).

**Problem:** This is essentially Approach 1 (heuristic) with a different check.

**Verdict:** ⚠️ Same issues as Approach 1

---

## Comparison Matrix

| Approach | Reliability | Complexity | File Changes | Token Count |
|-----------|-------------|-------------|-------------|-------------|
| 1: Heuristic | ⚠️ Low | Medium | None | 115 |
| 2: Sentinel | ✅ High | Low | Yes (+2) | 117 |
| 3: Source URI | ❓ Unknown | High | None | 115 |
| 4: Index-Based | ⚠️ Low | Low | None | 115 |

---

## Open Questions

1. **Token Count Requirement:** Do we need exactly 115 tokens, or can we add sentinel keys (117 total)?
2. **Avalonia Internals:** Does `ResourceDictionary` expose `Source` after loading via `ResourceInclude`?
3. **Order Guarantee:** Does Avalonia preserve `MergedDictionaries` order from AXAML?

---

## Recommendation

**Approach 2 (Sentinel Key)** is recommended for:
- Reliability (explicit identification)
- Simplicity (easy to implement and debug)
- Maintainability (clear intent)

If 115 tokens is a hard requirement, investigate **Approach 3 (Source URI)** first.

---

**Next Steps:**
1. ~~Decide on approach~~ → **Approach 2 chosen**
2. ~~Implement `WireThemeDictionaries`~~ → **Moved to `ThemeService.WireThemeDictionaries()`**
3. ~~Verify build passes~~ → **✅ Build passes, 495 tests green**
4. ~~Update `M1_STATUS.md`~~ → **✅ Updated**

---

**Final decision:** Approach 2 (Sentinel Key).

`WireThemeDictionaries()` was moved to `ThemeService` (not `App.axaml.cs`) to keep
theme wiring encapsulated within the service. `App.axaml.cs` simply calls
`themeService.WireThemeDictionaries()` before `ApplyThemeAsync()`. The sentinel keys
(`sys:String` with `_themeVariant`) add 2 tokens (117 total) for zero runtime cost
and 100% reliability.

> Note: Approach 6 (load themes directly, skip MergedDictionaries) was considered but
> rejected because Avalonia 11's `ResourceInclude` cannot be instantiated in C# code
> (no parameterless constructor, `Source` only works via XAML). The sentinel-key
> approach achieves the same goal with minimal changes.

---

*Document created during brainstorming session. Resolved 2026-06-22.*
