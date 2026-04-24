# Feature Specification: Velopack Auto-Update

**Feature Branch**: `040-velopack-auto-update`  
**Created**: 2026-04-06  
**Status**: Draft  
**Input**: User description: "Implement auto-update functionality using the Velopack framework (MIT license, cross-platform). This involves installing the Velopack NuGet package, configuring VelopackApp in Program.cs, creating an IUpdateService interface, implementing VelopackUpdateService, adding update notification UI, and registering services in the DI container."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Background Update Check on App Start (Priority: P1)

When the user launches Rentier, the application silently checks for available updates in the background without interrupting the user's workflow. If a newer version is available, a non-intrusive notification bar appears at the top of the main window informing the user. The notification includes the new version number and two clear actions: update now or dismiss for later.

**Why this priority**: This is the foundational user-facing experience. Without the background check and notification, no other update functionality is visible to the user. It delivers immediate value by keeping users informed about new versions without requiring manual checks.

**Independent Test**: Can be fully tested by launching the app when a newer release exists on GitHub. The notification bar should appear within a few seconds of startup, showing the correct version number, and the user can dismiss it without affecting their current workflow.

**Acceptance Scenarios**:

1. **Given** the app starts and a newer version is available on GitHub, **When** the main window loads, **Then** a notification bar appears showing "Update vX.Y.Z available" with [Update Now] and [Later] buttons.
2. **Given** the app starts and no newer version is available, **When** the main window loads, **Then** no update notification is shown and the user experience is unchanged.
3. **Given** the app starts and the device has no internet connectivity, **When** the update check fails, **Then** no notification or error is shown — the failure is silent and the app functions normally.
4. **Given** the update notification is visible, **When** the user clicks [Later], **Then** the notification bar is dismissed and does not reappear during the current session.

---

### User Story 2 - Download and Apply Update (Priority: P2)

When the user chooses to update, the application downloads the new version in the background while displaying a progress bar. The user can continue working during the download. Once the download completes, the user is prompted to restart the application to apply the update.

**Why this priority**: This is the core update delivery mechanism. Without it, the notification from P1 would be informational only. It converts awareness into action — the actual update installation.

**Independent Test**: Can be tested by clicking [Update Now] on the notification bar when an update is available. The progress bar should advance smoothly, and upon completion, a restart prompt should appear. After restart, the app should be running the new version.

**Acceptance Scenarios**:

1. **Given** the user clicks [Update Now] on the notification bar, **When** the download begins, **Then** a progress bar replaces the notification bar showing download progress as a percentage.
2. **Given** the download is in progress, **When** the download completes successfully, **Then** a prompt appears asking the user to restart the application with [Restart Now] and [Later] buttons.
3. **Given** the user clicks [Restart Now] after download completes, **When** the application restarts, **Then** the new version is running and no update notification is shown.
4. **Given** the user clicks [Later] on the restart prompt, **When** the prompt is dismissed, **Then** the update is applied on the next manual restart of the application.
5. **Given** the download is in progress, **When** a network failure occurs, **Then** the progress bar is replaced with an error message and the user can retry or dismiss.

---

### User Story 3 - Seamless Install/Uninstall Lifecycle Hooks (Priority: P3)

When the application is installed, updated, or uninstalled, the auto-update framework's lifecycle hooks execute before any UI initialization. This ensures clean installation, proper shortcut creation, registry cleanup on uninstall, and smooth version transitions — all without user interaction.

**Why this priority**: This is a behind-the-scenes infrastructure concern. Users never directly interact with lifecycle hooks, but they are essential for correct installer behavior. Without them, fresh installs and uninstalls may leave orphaned files or broken shortcuts.

**Independent Test**: Can be tested by performing a fresh install of the application package, then an update, then an uninstall. After install: app launches correctly and shortcuts exist. After update: new version runs. After uninstall: application files and shortcuts are removed cleanly.

**Acceptance Scenarios**:

1. **Given** the user runs the installer for the first time, **When** the application launches, **Then** the auto-update lifecycle hooks execute before the UI initializes and the app starts normally.
2. **Given** the user uninstalls the application, **When** the uninstaller runs, **Then** the auto-update lifecycle hooks execute cleanup logic and the application is fully removed.

---

### Edge Cases

- What happens when the GitHub releases API is rate-limited? The update check should fail silently, same as no-internet.
- What happens when the user starts an update and closes the app mid-download? The partial download should not corrupt the installation; the update is retried on next launch or next manual trigger.
- What happens when the current version is ahead of the latest release (development/pre-release builds)? No update notification should be shown.
- What happens when the GitHub release exists but the asset binary is missing or corrupt? The download should fail gracefully with an error message and retry option.
- What happens if the user is on a metered or slow connection? The download progress bar provides visual feedback; no timeout occurs for large downloads within reasonable network conditions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The application MUST check for updates automatically in the background within 10 seconds of the main window loading.
- **FR-002**: The update check MUST be non-blocking — the user MUST be able to interact with the application immediately regardless of update check status.
- **FR-003**: When an update is available, the application MUST display a notification bar at the top of the main window showing the available version number.
- **FR-004**: The notification bar MUST include an [Update Now] action to begin the download and a [Later] action to dismiss the notification for the current session.
- **FR-005**: When the user initiates an update, the application MUST show download progress as a percentage in a progress bar.
- **FR-006**: After a successful download, the application MUST prompt the user to restart with [Restart Now] and [Later] options.
- **FR-007**: If the user chooses [Restart Now], the application MUST close and relaunch with the updated version applied.
- **FR-008**: If the user chooses [Later] on the restart prompt, the update MUST be applied on the next manual application restart.
- **FR-009**: All update-related network failures MUST be handled silently during the background check (no error shown to the user).
- **FR-010**: Network failures during an active user-initiated download MUST display an error message with a retry option.
- **FR-011**: The Velopack lifecycle handler MUST execute as the very first operation in the application entry point, before any UI framework initialization.
- **FR-012**: The update service MUST use GitHub Releases as the update source, pointing to the Rentier repository.
- **FR-013**: The update service interface MUST be defined in the Application layer, and the implementation MUST reside in the Infrastructure layer.
- **FR-014**: The update service and related components MUST be registered in the dependency injection container following existing project patterns.
- **FR-015**: The update notification MUST be localized using the existing localization system (resource strings).

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature touches three layers — Application (IUpdateService interface), Infrastructure (VelopackUpdateService implementation), and Desktop (Program.cs hooks, notification UI, DI registration). The Clean Architecture dependency rule is maintained: Desktop depends on Application interface; Infrastructure implements Application interface. No layer boundary violations.
- **CA-002 (Money and Dates)**: No monetary or date fields are involved in this feature. Version strings and progress percentages are the only data types. Not applicable.
- **CA-003 (Privacy and Security)**: No user data is transmitted. The only outbound call is to the public GitHub Releases API to check for new versions and download release assets. No credentials, tax data, or personal information leaves the device. This adds a new allowed outbound endpoint (GitHub API) beyond the currently approved IMAP and NBS endpoints.
- **CA-004 (Network Scope)**: Adds one new outbound endpoint: GitHub Releases API (`api.github.com` and `github.com` for asset downloads). This is a read-only, public API call. The constitution's network scope (currently IMAP + NBS) must be updated to include GitHub Releases for auto-update.
- **CA-005 (Async and UI)**: All update operations (check, download, apply) MUST be async. The background check MUST NOT block UI startup. Progress updates MUST be scheduled on the UI thread via `RxApp.MainThreadScheduler`. ReactiveCommand.CreateFromTask MUST be used for update actions.
- **CA-006 (Testing Impact)**: Application layer: unit tests for any thin wrappers or DTOs. Infrastructure layer: integration tests for VelopackUpdateService (mocked UpdateManager). Desktop layer: ViewModel tests for update notification state machine (checking, available, downloading, downloaded, error states).

### Key Entities *(include if feature involves data)*

- **UpdateInfo**: Represents an available update — contains the target version identifier and any release metadata needed for display. This is a read-only data transfer object flowing from Infrastructure to Desktop through the Application layer.
- **UpdateState**: Represents the current state of the update workflow — one of: Idle, Checking, UpdateAvailable, Downloading (with progress percentage), Downloaded, Error (with message). Drives the notification bar visibility and content.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users are notified of available updates within 10 seconds of app launch without any interruption to their workflow.
- **SC-002**: Users can download and apply an update in 3 clicks or fewer (Update Now → Restart Now, or Update Now → Later → manual restart).
- **SC-003**: The update check and notification add no perceptible delay to application startup (main window is interactive immediately).
- **SC-004**: 100% of network failures during background checks are handled silently — zero unexpected error dialogs shown to users when offline.
- **SC-005**: The download progress bar updates smoothly and accurately reflects actual download progress.
- **SC-006**: After applying an update and restarting, the application runs the new version with no data loss or configuration reset.
- **SC-007**: Install, update, and uninstall lifecycle operations complete cleanly with no orphaned files, broken shortcuts, or registry artifacts.

## Assumptions

- The Rentier repository uses GitHub Releases to publish versioned application packages (this may need to be configured as part of the CI/CD pipeline, but the release publishing itself is out of scope for this feature).
- Velopack's `UpdateManager` with `GithubSource` supports the current GitHub repository structure and release naming conventions.
- The application is distributed as a Velopack-packaged installer (the packaging/build pipeline is a prerequisite but outside the scope of this specification).
- Users have periodic internet access; the app does not need to queue or schedule update checks — a single check at startup is sufficient.
- The update check uses the GitHub public API, which has rate limits (60 requests/hour for unauthenticated requests). For a desktop app with one check per launch, this is more than sufficient.
- The constitution's approved outbound network endpoints will be amended to include GitHub Releases API access for auto-update functionality.
- Session-scoped dismissal (clicking [Later] hides the notification for the current session only) is the desired behavior — the notification will reappear on the next app launch if the update is still available.
