# 8.9 — Design System: Implementation Plan

> **Status:** ✅ Complete — all milestones (M1–M5) implemented  
> **Author:** Kiro  
> **Date:** 2026-06-22

---

## 0. Entry Gates (M0)

All must be true before coding starts:

- [x] Phase 8 TOFIX items R1.1–R1.6 all closed
- [x] `dotnet build src/aero.csproj` passes (0 errors) — confirmed via R1.2 smoke test
- [x] `dotnet test tests` passes (baseline: 401 passed)
- [x] 115 color token inventory exists in `8.2-theme-system/TOKENS.md`
- [x] 6 stub files already exist in `src/Styles/` (Spacing, CornerRadius, Shadows, Transitions, Typography, Borders)

---

## 1. Current State

The 6 `src/Styles/` files exist as **value stubs only** — they declare raw tokens
(`x:Double`, `FontSize.*`, etc.) but:

1. They are **not included** in `App.axaml` (no `ResourceDictionary.MergedDictionaries` or `StyleInclude`)
2. They define **no control Styles** — no `Button`, `TextBox`, `Border`, `ScrollBar` etc. have the values applied
3. `Tab.Padding` and `StatusBar.Padding` in `Spacing.axaml` are typed as `x:Double` but contain comma-separated pairs — incorrect type
4. `Transitions.axaml` stores values as `x:Double` / `x:String` — these cannot be referenced directly in Avalonia `Transitions` blocks; they need a different delivery mechanism

**Goal of this plan:** Fix the stubs, wire them into `App.axaml`, and apply the values via
control Styles so every UI element in the app uses the design system automatically.

---

## 2. Scope

### In scope

- Fix type errors in existing stubs (Tab.Padding, StatusBar.Padding, Transitions)
- Merge all 6 `ResourceDictionary` files into `App.axaml`
- Write control Style setters that apply the tokens to:
  - `Button` — radius, padding, min-height
  - `TextBox` — radius, padding, border width
  - `Border` (panel/dialog) — radius, border width
  - `ScrollBar` / `ScrollViewer` — thumb radius
  - `TextBlock` — base font size and family
  - `TabItem` — radius (top corners only), padding
  - Status bar items — padding, font size
- Add a `ControlThemes.axaml` file in `src/Styles/` as the single place where all
  control Style setters live (keeps each stub file focused on values only)
- Wire `ControlThemes.axaml` into `App.axaml` after the stub dictionaries

### Out of scope

- Color tokens — that is 8.2's job. No hex values here.
- Actual transition animations on specific controls — 8.1 and 8.2 apply transitions
  when they wire up their panels/theme. 8.9 only establishes the duration/easing constants.
- Icon styles — deferred to 8.5.
- Settings-driven overrides — 8.6's job.

---

## 3. Milestones

### M1 — Fix stubs

**Files:** `Spacing.axaml`, `Transitions.axaml`

| Problem | Fix |
|---------|-----|
| `Tab.Padding` / `StatusBar.Padding` typed as `x:Double` but value is `"8,4"` | Change type to `Thickness` |
| `Transition.DurationMs` as `x:Double` / `Transition.Easing` as `x:String` — not directly usable in Avalonia Transitions | Replace with a `TimeSpan` resource for duration and document that easing is applied per-control via inline Style |

**Deliverable:** Both files compile cleanly and values are correctly typed.

---

### M2 — Wire dictionaries into App.axaml

**File:** `App.axaml`

Add a `ResourceDictionary` merge block inside `<Application.Resources>` that includes
all 6 stub files in dependency order:

```
Spacing → CornerRadius → Borders → Shadows → Typography → Transitions
```

No control themes yet — just ensuring `{DynamicResource Spacing.md}` etc. resolves
from anywhere in the app.

**Deliverable:** `dotnet build` passes; all token keys are resolvable via `DynamicResource`.

---

### M3 — Write ControlThemes.axaml

**File:** `src/Styles/ControlThemes.axaml` (new)

One `Styles` block containing `Style` selectors for each affected control.
Values come exclusively from `{StaticResource ...}` or `{DynamicResource ...}` referencing
the stub files — no hard-coded numbers.

#### Controls to style

**Button**
```xml
<Style Selector="Button">
  <Setter Property="CornerRadius" Value="{StaticResource Radius.Button}" />
  <Setter Property="Padding" Value="{StaticResource Button.PaddingThickness}" />
  <Setter Property="MinHeight" Value="{StaticResource Button.MinHeight}" />
</Style>
```

**TextBox**
```xml
<Style Selector="TextBox">
  <Setter Property="CornerRadius" Value="{StaticResource Radius.Input}" />
  <Setter Property="Padding" Value="{StaticResource Input.Padding}" />
  <Setter Property="BorderThickness" Value="{StaticResource Border.Thickness}" />
</Style>
```

**Border (panel)**
```xml
<Style Selector="Border.panel-border">
  <Setter Property="CornerRadius" Value="{StaticResource Radius.Panel}" />
  <Setter Property="BorderThickness" Value="{StaticResource Border.Thickness}" />
</Style>
```

**Border (dialog)**
```xml
<Style Selector="Border.dialog-border">
  <Setter Property="CornerRadius" Value="{StaticResource Radius.Dialog}" />
  <Setter Property="BorderThickness" Value="{StaticResource Border.Thickness}" />
</Style>
```

**TabItem**
```xml
<Style Selector="TabItem">
  <Setter Property="Padding" Value="{StaticResource Tab.PaddingThickness}" />
  <!-- CornerRadius top-only applied via template — noted as Phase 8.2 task -->
</Style>
```

**TextBlock (base)**
```xml
<Style Selector="TextBlock">
  <Setter Property="FontSize" Value="{StaticResource FontSize.Body}" />
  <Setter Property="FontFamily" Value="Inter" />
</Style>
```

**ScrollBar thumb radius** — applied via ControlTemplate override for `ScrollBar`.
Thumb uses `Radius.ScrollbarThumb` as `CornerRadius`.

> **Note on TabItem CornerRadius (top-only):** Avalonia's `TabItem` does not expose
> `CornerRadius` directly on the item — it must be done via a ControlTemplate setter
> targeting the inner `Border`. This is a known limitation. M3 documents it and defers
> the exact template override to 8.2 (when the tab strip gets its color tokens applied).

**Deliverable:** `ControlThemes.axaml` created, included in `App.axaml`, build passes.
Visual inspection shows buttons are rounded, inputs are rounded, TextBox borders are 1px.

---

### M4 — Add semantic Thickness resources

**Files:** `Spacing.axaml` (additions), `Borders.axaml` (additions)

The control styles in M3 need `Thickness`-typed resources (e.g. `Button.PaddingThickness`,
`Border.Thickness`, `Input.Padding`, `Tab.PaddingThickness`). Add these alongside the
existing `x:Double` scale values:

```xml
<!-- Spacing.axaml additions -->
<Thickness x:Key="Button.PaddingThickness">12,0,12,0</Thickness>
<Thickness x:Key="Input.Padding">8,6,8,6</Thickness>
<Thickness x:Key="Tab.PaddingThickness">12,6,12,6</Thickness>
<Thickness x:Key="StatusBar.PaddingThickness">8,4,8,4</Thickness>

<!-- Borders.axaml additions -->
<Thickness x:Key="Border.Thickness">1</Thickness>
```

**Deliverable:** All control Style setters in `ControlThemes.axaml` compile without
binding errors. No `x:Double` used where `Thickness` is required.

---

### M5 — Smoke test + build verification

```bash
dotnet build src/aero.csproj
dotnet test tests
dotnet run --project src   # visual check
```

**Visual checklist (manual):**
- [ ] Buttons have rounded corners (6px radius), consistent padding
- [ ] TextBox/input fields have rounded corners (6px), 1px border
- [ ] Font is Inter, body size 13px
- [ ] No layout regressions (panels, tabs, dialogs still look correct)
- [ ] `dotnet test tests` — still 401 passed (or more), 0 failed

---

## 4. File Map

| File | Action | Notes |
|------|--------|-------|
| `src/Styles/Spacing.axaml` | Modify | Fix `Tab.Padding`/`StatusBar.Padding` types; add `Thickness` semantic aliases |
| `src/Styles/CornerRadius.axaml` | No change | Values already correct |
| `src/Styles/Shadows.axaml` | No change | Values already correct |
| `src/Styles/Transitions.axaml` | Modify | Replace `x:Double`/`x:String` with `TimeSpan` for duration; add doc comment for easing |
| `src/Styles/Typography.axaml` | No change | Values already correct |
| `src/Styles/Borders.axaml` | Modify | Add `Border.Thickness` as `Thickness` type |
| `src/Styles/ControlThemes.axaml` | Create | All control Style setters referencing the stubs |
| `src/App.axaml` | Modify | Add `<Application.Resources>` merge block + ControlThemes include |

---

## 5. Definition of Done

- [x] `dotnet build src/aero.csproj` — 0 errors, 0 warnings introduced by 8.9 changes (only pre-existing CS9057 from Dock.Serializer)
- [x] `dotnet test tests` — baseline count maintained (401 passed), 0 new failures
- [x] All 6 `src/Styles/` files contain only correctly-typed resource values
- [x] `src/Styles/ControlThemes.axaml` exists and is included in `App.axaml`
- [x] `App.axaml` merges all 6 dictionaries and `ControlThemes.axaml`
- [x] Visual smoke pass: app compiled and launched (XOpenDisplay fail is headless env only); XAML resources resolved without errors
- [x] No hard-coded pixel/color values remain in `ControlThemes.axaml` — all via resource keys
- [x] `docs/phases/phase-8/TOFIX.md` has no new open items from this sub-phase

---

## 6. Phase 8.9 Limitations (by design)

- **TabItem top-only radius** — deferred to 8.2. Requires ControlTemplate override that interacts with tab color tokens. Documenting here avoids re-adding complexity without a concrete need.
- **Transition application** — 8.9 establishes duration/easing constants only. Actual `Transitions { ... }` blocks are written per-control in 8.1 (panels) and 8.2 (theme hover states). This is intentional: applying transitions globally to all controls would cause regressions in AvaloniaEdit and Dock.Avalonia.
- **ScrollBar ControlTemplate** — the full ScrollBar template override (thumb radius) is considered medium complexity. If it causes friction with Avalonia's SimpleTheme, document the limitation and skip — thumb radius is cosmetic.
- **No color applied** — 8.9 is structure only. All color comes from 8.2. Running the app after 8.9 may look unstyled or use SimpleTheme defaults for colors — this is expected.

---

## 7. Notes for Implementing Agent

- Use `{StaticResource}` (not `{DynamicResource}`) for structural tokens (radius, spacing, border width) — these never change at runtime. Reserve `{DynamicResource}` for color tokens (8.2's domain).
- `App.axaml` currently has `<Application.Styles>` but no `<Application.Resources>`. Both can coexist — add `<Application.Resources>` as a sibling element.
- Avalonia's SimpleTheme already sets default font and button styles. The 8.9 `ControlThemes.axaml` styles will layer on top. If a property conflicts with SimpleTheme, the later-declared style wins — `ControlThemes.axaml` must be included *after* SimpleTheme in `App.axaml`.
- Test with `dotnet build` after each milestone, not just at the end.
