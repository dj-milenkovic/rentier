# Plan: Rentier UI Modernization

## Problem
Rentier Desktop uses bare `FluentTheme` with no custom design system. The result is:
- Hardcoded hex colors and named colors everywhere (`#F5F5F5`, `Red`, `Gray`, `Orange`, `#FFEEEE`, `#CC0000`)
- No dark mode support (Inter font configured but not used in theme overrides)
- Sidebar is a plain `ListBox` with `TextBlock` — no icons, no visual selection indicator
- Dashboard metrics are in one flat `Border Background="#F5F5F5"`
- No user-selectable theme preference

## Approach

Build a thin design system layer **on top of** the existing `FluentTheme` — same pattern WalletWasabi uses. No WalletWasabi packages are pulled in; we adapt their patterns.

### Color Palette

**Dark** (default system-dark):
- `RegionColor`: `#0F172A` — window bg (Slate-900)
- `SidebarBackground`: `#1E293B` — sidebar (Slate-800)
- `CardBackground`: `#243349` — cards/panels
- `Accent`: `#3B82F6` — Blue-500 (scrollbar, buttons, active nav, progress)
- `TextPrimary`: `#F1F5F9` — Slate-100
- `TextSecondary`: `#94A3B8` — Slate-400
- `Border`: `#334155` — Slate-700
- `DangerFg`: `#EF4444`, `DangerBg`: `#1F0A0A`
- `WarningFg`: `#F59E0B`, `WarningBg`: `#1F1200`
- `SuccessFg`: `#22C55E`, `SuccessBg`: `#0A1F0D`

**Light** (system-light):
- `RegionColor`: `#F8FAFC` — Slate-50
- `SidebarBackground`: `#FFFFFF`
- `CardBackground`: `#FFFFFF`
- `Accent`: `#3B82F6` — same
- `TextPrimary`: `#0F172A` — Slate-900
- `TextSecondary`: `#64748B` — Slate-500
- `Border`: `#E2E8F0` — Slate-200
- `DangerFg`: `#DC2626`, `DangerBg`: `#FEF2F2`
- `WarningFg`: `#D97706`, `WarningBg`: `#FFFBEB`
- `SuccessFg`: `#16A34A`, `SuccessBg`: `#F0FDF4`

### Theme Switching
- `ThemePreference` enum: `System | Dark | Light`
- `IThemeService` (Desktop/Services) — reads/writes `%LOCALAPPDATA%\\Rentier\\ui.json`
- Applied via `Application.Current!.RequestedThemeVariant` in App.axaml.cs and AppearanceSettingsViewModel
- New "Appearance" tab in Settings

### Sidebar Design
```
┌─────────────────────────────┐
│  ⚡ Rentier        [220px]  │  ← header with logo icon + title
│ ─────────────────────────── │
│ ▌ 🏠 Dashboard              │  ← active: 4px accent pipe on left + subtle hover bg
│   📋 Filings                │
│   📊 Reports                │
│   🔄 Sync                   │
│ ─────────────────────────── │
│   ⚙  Settings              │
└─────────────────────────────┘
```
Each ListBoxItem has: `[4px pipe] [PathIcon 18px] [Label]`

## Tasks (14 total)

### Phase 1 — Design System
1. `theme-axaml` — `Assets/Styles/Theme.axaml` — ThemeDictionaries for Dark + Light
2. `controls-axaml` — `Assets/Styles/Controls.axaml` — Button, ProgressBar, DataGrid, ScrollBar, ListBox overrides
3. `theme-service` — `IThemeService` + JSON implementation + CompositionRoot registration
4. `app-axaml-update` — Wire Theme.axaml + Controls.axaml into App.axaml; `RequestedThemeVariant="Default"`

### Phase 2 — Appearance Settings
5. `appearance-vm` — `AppearanceSettingsViewModel` — reactive ThemePreference + save
6. `appearance-view` — `AppearanceSettingsView.axaml` — RadioButtons for System/Dark/Light
7. `settings-add-tab` — Add Appearance TabItem to SettingsView + SettingsViewModel

### Phase 3 — Shell
8. `icons-axaml` — 5 nav icons in Icons.axaml (Lucide MIT)
9. `nav-entry-icon` — Add `Icon` to `NavigationEntry`; update MainWindowViewModel
10. `main-window` — Rewrite MainWindow.axaml — styled sidebar with pipe+icon+label

### Phase 4 — Content Polish
11. `dashboard-polish` — Replace hardcoded colors with DynamicResource tokens + card elevation
12. `filings-polish` — Semantic tokens throughout
13. `reports-polish` — Semantic tokens throughout
14. `sync-polish` — Semantic tokens + color-coded log entries

## Status (updated 2026-04-22 15:57)

Completed:
- Theme + Controls files created: `Assets/Styles/Theme.axaml`, `Assets/Styles/Controls.axaml`
- `IThemeService` (`ThemeService`) implemented; persists to `%LOCALAPPDATA%\\Rentier\\ui.json`
- `AppearanceSettingsViewModel` + `AppearanceSettingsView` created; Settings view updated with Appearance tab
- Icons added to `Assets/Icons.axaml` and NavigationEntry extended to hold `Icon`
- `MainWindow` sidebar rewritten with modern design (pipe, icons, Inter font applied)
- `CompositionRoot` updated: `IThemeService` registered as singleton; `AppearanceSettingsViewModel` registered transient
- Dashboard, Filings, Reports, Sync views updated to use semantic tokens (card brushes, danger/warning tokens)
- `SyncSeverityBrushConverter` added and wired to Sync log entries
- `MainWindowViewModel.NavIcon()` fixed to use Avalonia `TryGetResource`
- Solution built successfully (0 errors)

Remaining:
- Manual review of other Views to ensure no remaining hardcoded colors (quick grep suggested most replacements done)
- Optional: refine FilingStatusBrushConverter badge colors for better contrast
- Verify theme persistence across restarts on user's machine and minor polishing (margins, consistent paddings)

## Notes
- DynamicResource must be used everywhere theme-sensitive values are required.
- Follow Clean Architecture: UI-only services remain in Rentier.Desktop; no domain or application leaks.

---

(Original plan was authored in session workspace. This file is a copy moved to the repository docs folder.)
