# Contract: Update Notification UX

**Layer**: Rentier.Desktop  
**Component**: MainWindow notification bar  
**Driven by**: MainWindowViewModel reactive properties

## Visual Layout

The update notification bar is a `Border` docked at the top of the MainWindow content area, above the `ContentControl` that hosts page views. It spans the full width of the content area (excluding the sidebar).

```text
┌─────────────┬──────────────────────────────────────────────────┐
│             │  [Update Notification Bar - conditional]          │
│   Sidebar   ├──────────────────────────────────────────────────┤
│             │                                                  │
│             │  ContentControl (current page)                   │
│             │                                                  │
└─────────────┴──────────────────────────────────────────────────┘
```

## States and Content

### State: Idle / Checking / Dismissed
- **Bar visibility**: Hidden (`IsVisible="False"`)
- No UI elements shown

### State: UpdateAvailable
- **Bar visibility**: Visible
- **Background**: Informational accent (DynamicResource `RentierInfoBrush` or similar)
- **Content**: `"Update v{version} available"` — [Update Now] — [Later]
- **Strings**: Localized via `Strings.resx`
  - `Update_Available_Message` = "Update v{0} available"
  - `Update_Now_Button` = "Update Now"
  - `Update_Later_Button` = "Later"

### State: Downloading
- **Bar visibility**: Visible
- **Content**: `"Downloading update... {percent}%"` — ProgressBar
- **Strings**:
  - `Update_Downloading_Message` = "Downloading update... {0}%"

### State: Downloaded
- **Bar visibility**: Visible
- **Content**: `"Update ready. Restart to apply."` — [Restart Now] — [Later]
- **Strings**:
  - `Update_Ready_Message` = "Update ready. Restart to apply."
  - `Update_Restart_Button` = "Restart Now"

### State: Error
- **Bar visibility**: Visible
- **Background**: Error accent (DynamicResource `RentierErrorBrush` or similar)
- **Content**: `"Update failed: {message}"` — [Retry] — [Dismiss]
- **Strings**:
  - `Update_Error_Message` = "Update failed: {0}"
  - `Update_Retry_Button` = "Retry"
  - `Update_Dismiss_Button` = "Dismiss"

## ViewModel Bindings

| Property | Type | Drives |
|---|---|---|
| `UpdateBarVisible` | `bool` (OAPH) | Bar `IsVisible` |
| `CurrentUpdateState` | `UpdateState` | Which bar variant to show |
| `AvailableVersion` | `string?` | Version text in notification |
| `DownloadProgress` | `int` | ProgressBar value (0-100) |
| `UpdateErrorMessage` | `string?` | Error text |

| Command | Enabled When | Action |
|---|---|---|
| `BeginUpdateCommand` | `UpdateAvailable` | Starts download |
| `DismissUpdateCommand` | `UpdateAvailable` or `Error` | Hides bar |
| `RestartNowCommand` | `Downloaded` | Applies update and restarts |
| `DismissRestartCommand` | `Downloaded` | Schedules update on exit, hides bar |
| `RetryDownloadCommand` | `Error` | Retries download |

## Accessibility

- Notification bar should have `AutomationProperties.Name` set for screen readers
- Buttons must be keyboard-navigable (default Avalonia behavior)
- Progress bar should have `AutomationProperties.Name` indicating download progress
