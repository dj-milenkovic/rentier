---
name: rentier-ui-design
description: >
  Authoritative Rentier Desktop UI design system: semantic color tokens, dark/light
  theme rules, icon authoring, sidebar layout, control classes, and asset pipeline.
  Use this skill whenever adding or modifying any AXAML view, style, icon, brush,
  color, image asset, layout, or appearance setting in Rentier.Desktop — even if
  the user just says "make it look nicer", "add an icon", "fix the color", or
  "add a new page". Also use it when a new view is created to ensure it follows
  the token system from the start, and proactively flag any hardcoded color you
  spot during code review.
tags:
  - avalonia
  - ui
  - design-system
  - theming
  - rentier
---

# Rentier UI Design System

## Golden rules (memorize these before touching any AXAML)

1. **Never hardcode colors.** No hex literals, no named colors (`Red`, `Gray`, `White`…)
   in AXAML. Always `{DynamicResource RentierXxxBrush}`.
2. **`DynamicResource`, not `StaticResource`**, for every brush — so theme switches
   at runtime without restart.
3. **PathIcon geometry must be closed/filled.** Open-path geometry (pure lines, no `Z`)
   renders as invisible because `PathIcon` fills, it does not stroke. Use `Z` to close
   shapes or build filled rectangles explicitly.
4. **Image assets must have transparent backgrounds.** Bitmaps with white/solid
   backgrounds look broken on the dark theme. See the Asset Pipeline section.
5. **Theme-aware converters via `TryGetResource`.** C# converters that return brushes
   must not use static `SolidColorBrush` fields. Read from app resources at call time
   so the correct theme variant is always used.

---

## Design token reference

All brushes live in `src/Rentier.Desktop/Assets/Styles/Theme.axaml` inside
`ThemeDictionaries` keyed `"Dark"` and `"Light"`. The file is loaded as a
`StyleInclude` in `App.axaml` so tokens respond to `RequestedThemeVariant` changes.

### Surface brushes

| Key | Dark | Light | Usage |
|-----|------|-------|-------|
| `RentierRegionBrush` | `#0F172A` | `#F8FAFC` | Window / page background |
| `RentierSidebarBrush` | `#1E293B` | `#FFFFFF` | Sidebar panel |
| `RentierCardBrush` | `#1E293B` | `#FFFFFF` | Card / panel background |
| `RentierCardElevatedBrush` | `#243349` | `#F1F5F9` | Raised card surface |
| `RentierBorderBrush` | `#334155` | `#E2E8F0` | Borders, dividers |
| `RentierNavHoverBrush` | `#263348` | `#EFF6FF` | Sidebar item hover |

### Typography brushes

| Key | Dark | Light | Usage |
|-----|------|-------|-------|
| `RentierTextPrimaryBrush` | `#F1F5F9` | `#0F172A` | Main labels, headings |
| `RentierTextSecondaryBrush` | `#94A3B8` | `#64748B` | Captions, metadata |
| `RentierMutedBrush` | `#64748B` | `#94A3B8` | Timestamps, placeholders |

### Accent

| Key | Dark & Light | Usage |
|-----|-------------|-------|
| `RentierAccentBrush` | `#3B82F6` | Buttons, active pipe, scrollbar |
| `RentierNavActivePipeBrush` | `#3B82F6` | 4 px left selection indicator |

### Status brushes

| Key | Dark Fg | Light Fg | Dark Bg | Light Bg |
|-----|---------|----------|---------|----------|
| `RentierDangerForegroundBrush` | `#EF4444` | `#DC2626` | — | — |
| `RentierDangerBackgroundBrush` | `#2D0A0A` | `#FEF2F2` | — | — |
| `RentierWarningForegroundBrush` | `#F59E0B` | `#D97706` | — | — |
| `RentierWarningBackgroundBrush` | `#2D1800` | `#FFFBEB` | — | — |
| `RentierSuccessForegroundBrush` | `#22C55E` | `#16A34A` | — | — |
| `RentierSuccessBackgroundBrush` | `#0A2D14` | `#F0FDF4` | — | — |

### Filing status badge brushes (theme-invariant — always saturated)

| Key | Value | Meaning |
|-----|-------|---------|
| `RentierStatusInitBrush` | `#D4A017` | Init (gold) |
| `RentierStatusFiledBrush` | `#0063B1` | Filed (blue) |
| `RentierStatusPaidBrush` | `#107C10` | Paid (green) |
| `RentierStatusBadgeTextBrush` | `#FFFFFF` | Text on all badge backgrounds |

These badge backgrounds are intentionally the same in both themes — they are semantic
meaning colors, not surface colors, and all are dark/saturated enough that white text
always passes contrast requirements.

### Theme-invariant tokens (use `StaticResource`)

```xml
<CornerRadius x:Key="RentierCardCornerRadius">8</CornerRadius>
<CornerRadius x:Key="RentierControlCornerRadius">6</CornerRadius>
<Thickness   x:Key="RentierCardPadding">16</Thickness>
<BoxShadows  x:Key="RentierCardShadow">0 2 8 0 #20000000, 0 1 2 0 #10000000</BoxShadows>
```

---

## Adding a new token

When a color is needed that no existing token covers:

1. Add a `SolidColorBrush` entry to **both** `Dark` and `Light` sections of
   `Theme.axaml` with a `Rentier` prefix.
2. Choose a value that passes WCAG AA contrast against the surface it sits on.
3. Reference it everywhere with `{DynamicResource YourNewBrush}`.
4. If the color is the same in both themes (e.g., a saturated status color), add it to
   both sections with the same value and document the reason in a comment.

---

## Control class conventions (`Controls.axaml`)

Always prefer class-scoped styles over global overrides.

| Selector / Class | Purpose |
|------------------|---------|
| `Button.accent` | Blue filled call-to-action button |
| `Border.card` | Card surface — applies background, shadow, corner radius, padding |
| `ListBox.sidebar-nav` | Sidebar navigation list — handles hover/selected states |

### Using cards

```xml
<Border Classes="card">
  <!-- content -->
</Border>
```

The `card` style applies `RentierCardBrush`, `RentierCardShadow`,
`RentierCardCornerRadius`, and `RentierCardPadding` automatically.

### Using the accent button

```xml
<Button Classes="accent" Command="{Binding SaveCommand}" Content="Save" />
```

---

## Icon authoring rules

Icons live in `src/Rentier.Desktop/Assets/Icons.axaml` as `StreamGeometry` resources
included in `App.axaml` `MergedDictionaries`.

### Critical: closed paths only

`PathIcon` renders geometry with `Fill`, not `Stroke`. An icon made entirely of open
line segments (`M … L … M … L …` with no `Z`) will be **completely invisible**.

✅ Good — closed shape:
```xml
<StreamGeometry x:Key="NavFilingsIcon">M13 2H6V22H18V9L13 2Z M13 2V9H18</StreamGeometry>
```

❌ Bad — open lines only (renders as nothing):
```xml
<StreamGeometry x:Key="NavReportsIcon">M18 20V10 M12 20V4 M6 20v-6</StreamGeometry>
```

For bar charts, use filled rectangles:
```xml
<StreamGeometry x:Key="NavReportsIcon">M3 22V15H7V22H3Z M10 22V9H14V22H10Z M17 22V4H21V22H17Z</StreamGeometry>
```

### Arc commands — use explicit spacing

Avalonia's StreamGeometry parser can silently fail on compactly written arc flags
(`00-2`, `002`). Use explicit spaces around arc parameters:

✅ `a2 2 0 0 1 -2 2`  
❌ `a2 2 0 01-2 2`

When in doubt, replace arcs with Bezier curves or sharp-corner equivalents.

### Nav icon loading

Icons are loaded from app resources via `TryGetResource` in `MainWindowViewModel`:

```csharp
private static StreamGeometry? NavIcon(string key)
{
    if (Avalonia.Application.Current?.TryGetResource(
            key, Avalonia.Styling.ThemeVariant.Default, out var resource) == true)
        return resource as StreamGeometry;
    return null;
}
```

All `NavigationEntry` objects are built in the constructor — the resource dictionary
must be loaded before the ViewModel is constructed. Icons are 18×18 in the sidebar
template (`PathIcon Width="18" Height="18"`).

---

## Sidebar structure

```
MainWindow.axaml
└── DockPanel
    └── Border (Left, 220px) — RentierSidebarBrush
        └── DockPanel
            ├── Border (Top) — Logo header
            │   └── StackPanel (Center, Spacing=6)
            │       ├── Image (80×80, app-icon-alpha.png)
            │       └── TextBlock "Rentier" (SemiBold 15, LetterSpacing 1.5)
            └── ListBox (Classes="sidebar-nav")
                └── DataTemplate<NavigationEntry>
                    └── Grid (Cols: 4, 44, *)
                        ├── Border  ← 4 px accent pipe (visible when IsSelected)
                        ├── PathIcon ← 18×18, RentierTextPrimaryBrush
                        └── TextBlock ← label
```

### NavigationEntry record

```csharp
public record NavigationEntry(
    string Label,
    ReactiveObject ViewModel,
    StreamGeometry? Icon = null,
    bool IsVisible = true);
```

### Window startup

```xml
<Window WindowStartupLocation="CenterScreen"
        Width="1000" Height="640"
        MinWidth="800" MinHeight="520">
```

---

## Asset pipeline for bitmap icons

App icon files live at `Assets/Icons/` in the repo root and are linked into the
Desktop project as Avalonia resources:

```xml
<!-- Rentier.Desktop.csproj -->
<AvaloniaResource Include="..\..\Assets\**">
  <Link>Assets\%(RecursiveDir)%(Filename)%(Extension)</Link>
</AvaloniaResource>
```

This makes them available as `avares://Rentier.Desktop/Assets/Icons/…`.

### White-background removal

The app icon PNGs ship with a solid `#FEFEFE` background. For display on dark
surfaces, generate a transparent variant using this PowerShell snippet:

```powershell
Add-Type -AssemblyName System.Drawing
$src  = "Assets\Icons\png\rentier_1024x1024.png"   # largest source
$dest = "src\Rentier.Desktop\Assets\Icons\app-icon-alpha.png"
$bmp = [System.Drawing.Bitmap]::new($src)
$out = [System.Drawing.Bitmap]::new($bmp.Width, $bmp.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $bmp.Height; $y++) {
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        $px = $bmp.GetPixel($x, $y)
        if ($px.R -gt 240 -and $px.G -gt 240 -and $px.B -gt 240) {
            $out.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
        } else { $out.SetPixel($x, $y, $px) }
    }
}
$out.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); $out.Dispose()
```

The output at `src/Rentier.Desktop/Assets/Icons/app-icon-alpha.png` is part of the
Desktop project's own `Assets\**` glob and referenced as:

```xml
<Image Source="avares://Rentier.Desktop/Assets/Icons/app-icon-alpha.png"
       Width="80" Height="80"
       HorizontalAlignment="Center"
       RenderOptions.BitmapInterpolationMode="HighQuality" />
```

Always use the largest available source PNG (1024×1024) as input for best quality
when scaled down.

---

## Theme switching architecture

| File | Responsibility |
|------|---------------|
| `Services/IThemeService.cs` | `ThemePreference { System, Light, Dark }` enum + interface |
| `Services/ThemeService.cs` | Persists to `%LOCALAPPDATA%\Rentier\ui.json`; applies `Application.Current!.RequestedThemeVariant` via `Dispatcher.UIThread` |
| `App.axaml.cs` | Calls `ThemeService.ApplyOnStartup()` before the window opens |
| `ViewModels/AppearanceSettingsViewModel.cs` | Reactive `SelectedTheme` — auto-saves on change, no explicit save command needed |
| `Views/AppearanceSettingsView.axaml` | RadioButtons for System / Light / Dark, `GroupName="ThemeGroup"` |
| `Views/SettingsView.axaml` | `TabItem Header="Appearance"` with `AppearanceSettingsView` as content |
| `CompositionRoot.cs` | `IThemeService` → singleton; `AppearanceSettingsViewModel` → transient |

---

## Theme-aware converters

Converters that return brushes must NOT cache brushes as static fields — those fields
are set once at class load time and ignore theme changes:

```csharp
// ❌ Wrong — static field ignores theme switching
private static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#EF4444"));

// ✅ Correct — read the live theme-resolved brush at call time
private static SolidColorBrush GetBrush(string key)
{
    var app = global::Avalonia.Application.Current!;
    if (app.TryGetResource(key, app.ActualThemeVariant, out var res) &&
        res is IBrush b)
        return (SolidColorBrush)b;
    return Brushes.Transparent;
}
```

Always use `global::Avalonia.Application.Current` to avoid namespace collision with
`Rentier.Application`.

### Existing converters

| Converter | Token mapping |
|-----------|--------------|
| `FilingStatusBrushConverter` | Init→`RentierStatusInitBrush`, Filed→`RentierStatusFiledBrush`, Paid→`RentierStatusPaidBrush` |
| `SyncSeverityBrushConverter` | Error→`RentierDangerForegroundBrush`, Warning→`RentierWarningForegroundBrush`, Info→`RentierTextSecondaryBrush` |

---

## Hardcoded color audit checklist

Before committing any AXAML, run:

```powershell
# Hex colors
Select-String -Path src\Rentier.Desktop\Views\*.axaml -Pattern '#[0-9A-Fa-f]{3,8}'
# Named colors in property attributes
Select-String -Path src\Rentier.Desktop\Views\*.axaml `
  -Pattern '(Background|Foreground|BorderBrush|Fill)="(Red|Gray|Orange|Blue|Green|White|Black|Yellow)"'
```

Any hit that is NOT a token reference or a theme-invariant structural color (e.g.
`Foreground="White"` on a badge with a saturated background) must be replaced with
a `{DynamicResource …}`.

---

## Correct token usage patterns

```xml
<!-- Card surface -->
<Border Classes="card">…</Border>

<!-- Danger inline message -->
<Border Background="{DynamicResource RentierDangerBackgroundBrush}"
        BorderBrush="{DynamicResource RentierDangerForegroundBrush}"
        BorderThickness="1" CornerRadius="{StaticResource RentierControlCornerRadius}"
        Padding="12,8">
  <TextBlock Foreground="{DynamicResource RentierDangerForegroundBrush}"
             Text="{Binding ErrorMessage}" />
</Border>

<!-- Secondary label -->
<TextBlock Foreground="{DynamicResource RentierTextSecondaryBrush}"
           FontSize="12" Text="{Binding Caption}" />

<!-- Accent call-to-action -->
<Button Classes="accent" Content="Save" Command="{Binding SaveCommand}" />

<!-- Filing status badge -->
<Border Background="{Binding Status, Converter={StaticResource FilingStatusBrushConverter}}"
        CornerRadius="4" Padding="8,3">
  <TextBlock Foreground="{DynamicResource RentierStatusBadgeTextBrush}"
             Text="{Binding StatusDisplayText}" FontSize="11" FontWeight="SemiBold" />
</Border>
```
