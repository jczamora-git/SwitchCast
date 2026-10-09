# SWITCHCAST — DEVELOPMENT CHANGELOG

All notable changes to the SwitchCast development harness and codebase will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), adhering to standard change types:
`feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`, `build`.

---

## [1.2.1] - 2026-10-10

- **Media Source Presentation Output Activation**: Updated source switching behavior so that choosing an Image or Video media source under `ActiveAndLive` or `ActiveOnly` mode activates and brings the existing `PresentationWindow` to the foreground (restoring if minimized) instead of failing to activate non-existent external application windows. In `ActiveOnly` mode, the presentation output activates without taking media live, altering `ActiveSource`, or restarting playback; if closed, it opens in Standby mode. `LiveOnly` mode preserves background switching without stealing focus. Ordinary application window and monitor sources retain established Win32 foreground activation behavior.
- **Floating Dock Focus Handoff**: Dismissed and detached popup menu in `PresenterDockMenuWindow` prior to executing source transitions, preventing dock dropdown teardown from stealing foreground focus away from the activated presentation output.
- **Dashboard Start/Stop Presenting Dynamic Icon**: Corrected misleading static Play icon when presenting is live on `DashboardPage.xaml`. Dynamic compiled binding `{x:Bind ViewModel.PresentationButtonGlyph, Mode=OneWay}` updates the icon between Play (`\uE768`) and Stop square (`\uE71A`) in lockstep with the button label and authoritative presentation status (`HasActivePresentation`), while preserving coral button styling and layout.
- **Presenter Dock Close Dashboard Activation**: When clicking the Floating Presenter Dock X button, automatically restore and activate the existing `MainWindow` Control Dashboard (restoring if minimized, navigating to Dashboard, and requesting foreground) while preserving active live presentations (capture, video playback, and `PresentationWindow` remain live). Guarded against activation during application shutdown and preserved hotkey toggle semantics.
- **Floating Presenter Dock Double-Click Maximization**: Prevented caption double-click from maximizing or entering fullscreen on `PresenterDockWindow` by subclassing the dock HWND (`WM_NCLBUTTONDBLCLK` on `HTCAPTION`, `SC_MAXIMIZE` interception, `WM_GETMINMAXINFO` clamping, and removal of `WS_MAXIMIZEBOX`). Native hold-and-drag and Presentation Window fullscreen toggle remain fully functional.
- **Mixed-Source Next/Previous Navigation**: Repaired Next/Previous queue cycling between Window, Monitor, Image, and Video sources by maintaining a single authoritative sequence across all types, resolving `ActiveSource` clearing during capture stop, and supporting direct live transitions for media without failing on HWND activation.

### Added
- **Native Application Icon Branding**: Packaged multi-resolution Windows application icon `Assets/SwitchCast.ico` embedding 16x16, 24x24, 32x32, 48x48, 64x64, 128x128, and 256x256 resolutions derived from `Assets/SwitchCast_1.png`. Configured project deployment in `SwitchCast.csproj` and added automated tests in `ApplicationBrandingTests.cs`.
- **Presenter Dock Direct Source Unqueue**: Enabled presenters to remove individual sources directly from the Floating Dock "Queued Sources" dropdown via interactive checkmark checkboxes. Preserved On-Air presentation continuity, maintained open dropdown for multi-item unqueue, and synchronized state immediately with the Main Dashboard and Sources tab.

---

## [1.2.0] - 2026-10-10

### Added
- **Local Media Presentation Integration**: Import PNG, JPG, BMP, GIF images and MP4, MKV, MOV, WMV, AVI videos directly into the unified presentation queue.
- **Local Video Audio Playback & Synchronization**: Integrated audio output for video sources with volume slider (0–100%) and instant mute toggle controls.
- **Dock-Controlled Fullscreen Presentation Mode**: Native borderless fullscreen toggle (`AppWindowPresenterKind.FullScreen`) for `PresentationWindow` with title-bar auto-collapsing and aspect-ratio preservation.
- **Adaptive Video Transport Controls**: Added timeline scrubber, Play/Pause, Restart, Forward 10s, and Backward 10s controls to the Floating Presenter Dock when a video source is on-air.

### Improved
- **Floating Presenter Dock Caption Dragging**: Replaced custom pointer routing with genuine Windows App SDK `InputNonClientPointerSource` non-client regions for smooth, natural title-bar dragging without sticky click-to-move anomalies.
- **Dashboard Empty-State UX**: Replaced blank selector box with styled empty-state dropdown matching the Floating Dock, plus direct `+ Add Source` navigation button.
- **Preview Workspace Layout**: Fixed text clipping in empty workspace panel with automatic wrapping.

### Fixed
- **Floating Dock Playback Timeline Cross-Thread COMException (0x8001010E)**: Permanently resolved `RPC_E_WRONG_THREAD` crashes during video playback by eliminating background thread-pool timers, replacing them with a single UI-owned `Microsoft.UI.Dispatching.DispatcherQueueTimer`, and eliminating unsafe off-thread execution fallbacks.

---

## [Floating Dock Playback Slider Cross-Thread COMException Permanent Hotfix] - 2026-10-09

### Fixed (fix / threading / winui / test / docs)
- **Eliminate COMException 0x8001010E (RPC_E_WRONG_THREAD)**:
  - Eliminated the fallback `System.Threading.Timer` (`_playbackProgressTimer`), `OnFallbackTimerTick`, and unsafe synchronous execution in `RunOnUIThread`.
  - Replaced reflection/dynamic dispatch with strongly typed `Microsoft.UI.Dispatching.DispatcherQueue` and `Microsoft.UI.Dispatching.DispatcherQueueTimer`.
  - Configured `PresenterDockWindow` to pass its UI thread `DispatcherQueue` on window initialization.
  - Ensured all UI-bound `PropertyChanged` notifications occur strictly on the owning UI thread, with clean drop semantics on `TryEnqueue` failure rather than fallback to worker thread execution.
  - Implemented safe timer disposal on dock close to prevent orphan callbacks.
- **Automated Unit & Regression Tests**:
  - Added unit tests in [SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs) validating UI dispatcher ownership, timer ticks, scrubbing state, off-thread marshaling, and TryEnqueue failure handling.
  - **255 automated unit tests passing with 100% success rate**.

---

## [Dashboard Source Selector Empty-State UX Fix] - 2026-10-09

### Fixed / Added / Improved (fix / UI / UX / test / docs)
- **Main Dashboard Source Selector Empty-State UX**:
  - Replaced the blank gray selector rectangle when zero sources are queued with an informative `DropDownButton` and styled Flyout matching the Floating Presenter Dock empty state in [Views/DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml).
  - Surface displays `Queued Sources (0)`, icon `\uE7F4`, title `No queued sources`, description `Add application windows, displays, images, or videos from the Sources tab.`, and action button `+ Add Presentation Source`.
- **Primary Presentation Control Bar "+ Add Source" Button**:
  - Added a compact `+ Add Source` button to the main control bar navigating directly to `SourcesPage` via `INavigationService`.
  - Automatically highlights the Sources tab in the `NavigationView` sidebar.
- **Start Presenting Validation & Tooltip**:
  - Disabled Start Presenting when 0 presentation sources are queued and presentation is inactive.
  - Added dynamic tooltip: `Add a presentation source first.` when empty vs `Start Live Presentation` / `Stop Live Presentation`.
- **Preview Workspace Description Wrapping**:
  - Fixed clipped text in `EmptyWorkspacePanel` by adding `TextWrapping="Wrap"` and `MaxWidth="420"`.
  - Renamed action button to `Add Presentation Sources`.
- **Automated Unit & Regression Tests**:
  - Added tests in [SwitchCast.Tests/ViewModels/DashboardViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/DashboardViewModelTests.cs) verifying empty state properties, placeholder text, enablement rules, and queue count transitions.
  - **250 automated unit tests passing with 100% success rate**.

---

## [Floating Presenter Dock Finalization] - 2026-10-09

### Fixed / Added / Improved (fix / feat / UI / test / docs)
- **Native Windows Caption Dragging**:
  - Replaced legacy pointer routing and manual `SendMessage(WM_NCLBUTTONDOWN)` with genuine Windows App SDK `Microsoft.UI.Input.InputNonClientPointerSource` non-client regions in [Views/PresenterDockWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml.cs).
  - Configured dock surface as `NonClientRegionKind.Caption` and interactive controls as `NonClientRegionKind.Passthrough`.
  - Completely resolved sticky dragging / click-to-move bug without mouse-follow anomalies.
- **Unified Permanent Dock Layout**:
  - Removed dual Compact vs Expanded mode switching, toggle buttons, and legacy settings rows.
  - Established permanent single-row layout baseline (680 DIPs width) with full presenter actions, fullscreen toggle, source selector, and switching mode dropdown.
- **Adaptive Video Playback & Timeline Controls**:
  - Added source-aware second row for video sources in [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml) with Restart (`00:00`), Seek Backward 10s (`\uEB9E`), Play/Pause (`\uE768`/`\uE769`), Seek Forward 10s (`\uEB9D`), timeline slider scrubber, and timecode progress (`00:00 / 00:00`).
  - Height automatically transitions between 52 DIPs (standard sources) and 86 DIPs (video sources) with dynamic hit-test region updates.
- **Automated Unit & Regression Tests**:
  - Updated [SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs) covering video transport commands, seeking, duration, and scrubbing state.
  - **247 automated unit tests passing with 100% success rate**.

---

## [Phase 6.3: Fullscreen Presentation Output] - 2026-10-09

### Added / Improved (feat / UI / test / docs)
- **Native AppWindow Fullscreen Presenter**:
  - Implemented `AppWindowPresenterKind.FullScreen` and `AppWindowPresenterKind.Default` in [Views/PresentationWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml.cs).
  - Strongly typed `PresentationDisplayMode` enum (`Windowed`, `Fullscreen`) in [Models/PresentationDisplayMode.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/PresentationDisplayMode.cs).
  - Integrated `DisplayMode`, `DisplayModeChanged`, `SetDisplayMode()`, and `ToggleDisplayMode()` in `IPresentationWindowService` and `PresentationWindowService`.
- **Custom Title Bar Collapsing & 100% Viewport Geometry**:
  - Dynamic collapsing of custom title bar (`TitleBarRow.Height = new GridLength(0)`, `AppTitleBar.Visibility = Visibility.Collapsed`, `SetTitleBar(null)`) in fullscreen mode.
  - Audience-facing presentation canvas (Capture frames, Static Images, Video `MediaPlayerElement`, Standby, Blackout) expands to occupy full monitor bounds with uniform aspect ratio preserved.
- **Floating Presenter Dock Fullscreen Controller**:
  - Added minimalist Fullscreen icon button (`\uE740` Enter / `\uE73F` Exit) in Expanded dock mode in [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml).
  - Added Fullscreen toggle action in `PresenterDockMenuWindow` More Options popup.
  - Authoritative ViewModel command `ToggleFullscreenCommand` synchronized with `IPresentationWindowService.DisplayModeChanged`.
  - Button dynamically enables/disables based on `IsOutputWindowOpen`.
- **Keyboard Recovery & Multi-Monitor Preservation**:
  - Added `Escape` key shortcut when `PresentationWindow` is focused to safely exit fullscreen without affecting global hotkeys.
  - Native multi-monitor positioning preserved when toggling fullscreen on secondary displays.
- **Continuous Capture & Media Continuity**:
  - Preserves the EXACT same Win32 HWND, capture session, and media playback stream across transitions.
- **Automated Unit & Regression Tests**:
  - Added [SwitchCast.Tests/Services/PresentationFullscreenTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresentationFullscreenTests.cs) verifying display mode state machine, command execution, and open/close synchronization.
  - Test suite expanded to **247 automated unit tests** (100% pass rate).

---

## [Phase 6.2: Media Audio Playback] - 2026-10-09

### Added / Fixed / Improved (feat / fix / audio / UI / test / docs)
- **Local Media Audio Playback**:
  - Resolved silent video playback by removing hardcoded `IsMuted = true` in `MediaPresentationService`.
  - Initialized `Windows.Media.Playback.MediaPlayer` with unmuted audio playback connected to the default Windows audio output device.
- **Authoritative Media Audio Controller**:
  - Implemented `Volume`, `IsMuted`, `SetVolume(double volume)`, `SetMuted(bool isMuted)`, and `ToggleMute()` on `IMediaPresentationService` and `MediaPresentationService`.
  - Added `MediaVolume` and `IsMediaMuted` properties to `UserSettings` with asynchronous local persistence through `IApplicationSettingsService`.
- **Dashboard Audio Controls**:
  - Added Mute/Unmute button (`\uE74F` / `\uE767`), volume slider (0..100%), and percentage readout in the Video Playback Controls bar in [Views/DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml).
- **Floating Presenter Dock Audio Integration**:
  - Added compact mute/unmute button directly on the dock toolbar in [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml) (visible conditionally when a video source is On Air).
  - Added video audio volume slider and mute toggle in [Views/PresenterDockMenuWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockMenuWindow.xaml) More Options popup without altering single-row dock dimensions.
- **Audio Lifecycle, Blackout Privacy & Source Switching Safety**:
  - Synchronized video pause/resume with audio pause/resume.
  - Blackout suppresses video frames and mutes audio immediately; ending blackout resumes prior audio state.
  - Stop presenting stops video/audio and disposes active media player.
  - Multi-source switching (`Video -> Image`, `Video -> Window`, `Video -> Video`) terminates previous audio playback immediately to prevent dual audio streams or orphaned background playback.
- **Automated Unit & Regression Tests**:
  - Added [SwitchCast.Tests/Services/MediaAudioPlaybackTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/MediaAudioPlaybackTests.cs) verifying audio initialization, volume clamping, mute toggling, ViewModel synchronization, lifecycle pause/resume/blackout/stop, and source transitions.
  - Test suite expanded to **246 automated tests** with 100% pass rate.

---

## [Phase 6.1: Complete Media Presentation Integration] - 2026-10-09

### Added / Improved (feat / fix / test / docs)
- **Unified Media Presentation Queue & Navigation**:
  - Validated and streamlined authoritative single presentation queue across all 4 source types: `WindowSource`, `MonitorSource`, `ImageMediaSource`, and `VideoMediaSource`.
  - Dynamic source icon glyph (`ActiveSourceGlyph`) on Floating Presenter Dock button (`\uEB9F` for Image, `\uE714` for Video, `\uE7F4` for Window, `\uE790` for Display).
  - Proper category labels and glyph bindings on `PresenterDockMenuWindow` ListView items.
  - Multi-source cycling verified across `NextSource` (`Ctrl+Shift+Right`), `PreviousSource` (`Ctrl+Shift+Left`), and direct index selection (`Ctrl+Shift+1..5`) without race conditions.
- **Three Switching Modes Media Support**:
  - `Active + Live`: Takes media live immediately. Window activation gracefully skips file-backed sources without errors or focusing unrelated windows.
  - `Live Only`: Takes media live without changing application window focus.
  - `Active Only`: Updates selected queue cursor only without changing On-Air presentation output.
- **Fail-Closed & Video Playback Controls**:
  - Video Play, Pause, Resume, Restart, Loop toggle, and timecode position integrated with on-air state in Control Dashboard.
  - Fail-closed error handling for non-existent, moved, or corrupted files without application crashing or presentation HWND disruption.
- **Automated Regression Test Suite**:
  - Added [SwitchCast.Tests/Services/PresentationCoordinatorMediaTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresentationCoordinatorMediaTests.cs) coverage for 4-source mixed navigation, previous navigation, direct index switching, and switching modes.
  - Expanded test suite to **239 automated unit tests** (100% pass rate).

---

## [Floating Presenter Dock Native Dragging Hotfix] - 2026-10-09

### Fixed / Improved (fix / UI / test / docs)
- **Floating Presenter Dock Surface & Gesture Dragging**:
  - Implemented routed pointer event handlers (`AddHandler` with `handledEventsToo: true`) on `DockCardBorder` in [Views/PresenterDockWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml.cs).
  - Instant native dragging (`ReleaseCapture` + `WM_NCLBUTTONDOWN` / `HTCAPTION`) when pressing on non-interactive surfaces (background, padding, status badge, status dot, dividers).
  - Smooth click-versus-drag detection on interactive controls (buttons, dropdown triggers, action icons) using physical pixel movement threshold (5px / `DragThresholdSquared = 25`).
  - Normal clicks execute intended actions (opening menus, switching sources, pause, blackout, stop) without moving the window.
  - Intentional hold-and-drag gestures across the dock toolbar seamlessly initiate native Windows OS dragging without firing button clicks upon release.
  - Zero modification to `MainWindow` or `PresentationWindow` title-bar implementations.

---

## [Global Text Truncation & Layout Overflow Fix] - 2026-10-09

### Fixed / Improved (fix / UI / test / docs)
- **Dashboard Source Dropdown Constraints & Ellipsis**:
  - Replaced unconstrained string display in `ComboBox` with custom `ItemTemplate` across Target Source Selector, Live Preview switcher, and Ready-to-Preview switcher in [Views/DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml).
  - Applied bounded max-widths (`MaxWidth="300"`, `MaxWidth="260"`, `MaxWidth="280"`), 2-column item grid layouts (`Auto, *`), `TextTrimming="CharacterEllipsis"`, `TextWrapping="NoWrap"`, `MaxLines="1"`, and complete title tooltips via `ToolTipService.ToolTip="{x:Bind Title}"`.
  - Replaced unbounded horizontal `StackPanel` in Status Strip Active Source with a 2-column `Grid` (`ColumnDefinitions="Auto, *"`) ensuring proper single-line ellipsis and full title tooltip.
  - Added tooltip and single-line trimming to Queued Presentation Sources mini-strip.
- **Sources Page Row Trimming**:
  - Added `TextWrapping="NoWrap"`, `TextTrimming="CharacterEllipsis"`, `MaxLines="1"`, and tooltips to Title and Subtitle in [Views/SourcesPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SourcesPage.xaml).
  - Ensured flexible star-column layout never pushes the Queue checkbox outside the row or screen.
- **Floating Presenter Dock Title Trimming**:
  - Constrained `ExpandedSourceButton` text block with `MaxWidth="135"` and `CompactSourceButton` text block with `MaxWidth="85"` in [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml).
  - Added `ToolTipService.ToolTip="{x:Bind Title}"` and `ToolTipService.ToolTip="{x:Bind Type}"` to `ListView` items in [Views/PresenterDockMenuWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockMenuWindow.xaml).
- **Source Identity & Model Integrity**:
  - Preserved 100% of underlying full window titles, HWNDs, process IDs, and source queue identifiers in models (`WindowSource`, `MonitorSource`, `ImageMediaSource`, `VideoMediaSource`) and ViewModels.
- **Automated Unit & Regression Tests**:
  - Added [SwitchCast.Tests/ViewModels/SourceTitleTruncationTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/SourceTitleTruncationTests.cs) verifying short, long, extremely long, empty, and unicode/emoji titles, model preservation, and ViewModel tooltip integrity.
  - Expanded test suite to **234 automated unit tests** (100% pass rate).

---

## [Floating Presenter Dock Dragging & Stop Presentation Workflow] - 2026-10-09

### Added / Improved (feat / UI / test / docs)
- **Floating Presenter Dock Dragging Refinement**:
  - Removed dedicated drag handle icon (`\uE76F`) and its layout column/spacing from both Expanded (7 columns) and Compact (6 columns) dock modes in [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml).
  - Implemented native Windows non-client window dragging (`ReleaseCapture` + `WM_NCLBUTTONDOWN` / `HTCAPTION`) from non-interactive toolbar surfaces (card border, status badge pill, status dot, and padding).
  - Implemented visual tree hit-testing (`IsInteractiveControl`) that strictly protects all interactive controls (`ButtonBase`, `ComboBox`, `TextBox`, `Slider`, `ToggleSwitch`, `ListViewItem`, `MenuFlyoutItem`) from dragging triggers, preserving normal button clicks, hovers, and flyout interactions.
- **Unified Stop Presenting Workflow & Automatic Dashboard Activation**:
  - Unified Stop Presenting across Control Dashboard, Floating Presenter Dock, Presenter Dock Menu, and Global Hotkey (`Ctrl+Shift+S`).
  - Authoritative Stop sequence in `PresentationCoordinator.StopPresentationAsync()` safely terminates active capture or media playback, sets presentation status to `Idle`, closes the `PresentationWindow` (it no longer remains visible on Standby), restores `MainWindow` if minimized, brings the Control Dashboard to the foreground via `IWindowActivationService.ActivateMainWindow()`, and navigates to `DashboardPage`.
  - Suppressed MainWindow activation during application exit confirmation (`ApplicationLifecycleService.ExecuteShutdownAsync` calls `StopPresentationAsync(isShuttingDown: true)`).
- **Automated Unit & Regression Tests**:
  - Added [SwitchCast.Tests/Services/StopPresentationWorkflowTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/StopPresentationWorkflowTests.cs) covering all Stop triggers, source types (Window, Monitor, Image, Video), paused/blackout states, idempotency, and application shutdown suppression.
  - Expanded test suite to **223 automated unit tests** (100% pass rate).

---

## [Phase 6 — Direct Media Sources & Settings UI Refinement] - 2026-10-09

### Added / Improved (feat / UI / test / docs)
- **Windows 11 Settings-Style Theme Selector & Appearance Layout**:
  - Replaced cramped vertical theme RadioButtons in [Views/SettingsPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SettingsPage.xaml) with a compact right-aligned WinUI 3 `ComboBox` (140 DIP width) matching Windows 11 Settings.
  - Corrected Grid column geometry to allocate flexible Star width to the title and description, preventing vertical word wrapping.
  - Linked two-way to `SettingsViewModel.SelectedThemeIndex` for immediate theme switching across `System`, `Light`, and `Dark` with local persistence.
- **Direct Local Image & Video Presentation Sources**:
  - Extended domain models with `ImageMediaSource` and `VideoMediaSource` in [Models/MediaFileSource.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/MediaFileSource.cs) supporting dimensions, duration, and file size formatting.
  - Implemented [Services/Media/MediaDiscoveryService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Media/MediaDiscoveryService.cs) for asynchronous metadata extraction via Windows imaging and storage APIs.
  - Implemented [Services/Media/Win32MediaPickerService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Media/Win32MediaPickerService.cs) using native WinUI 3 `FileOpenPicker` with HWND desktop interop.
  - Implemented [Services/Media/MediaPresentationService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Media/MediaPresentationService.cs) managing `BitmapImage` decoding and native `Windows.Media.Playback.MediaPlayer` (muted by default).
  - Extended [Views/PresentationWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml) with mutually exclusive visual layers: Screen Capture `Image`, Direct Static `Image`, `MediaPlayerElement`, Standby Overlay, and topmost Blackout Overlay.
  - Added video playback controls to [Views/DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml) (Play/Pause, Restart, Loop, timecode position, and progress).
  - Preserved single stable Presentation Output HWND across mixed source switching (`Window` <-> `Image` <-> `Video` <-> `Monitor`).
  - Preserved Blackout and Pause privacy rules: pausing video playback and preventing rendering/audio leaks during blackout.
- **Sources Page Media Category & Persistence**:
  - Added "Media Files" category tab to [Views/SourcesPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SourcesPage.xaml) alongside Application Windows and Displays.
  - Added "Add Media" import button, compact file cards, queue toggles, and remove actions.
  - Persisted imported media file paths in `UserSettings.ImportedMediaPaths` for automatic reloading on startup.
- **Automated Unit & Regression Tests**:
  - Added [SwitchCast.Tests/Models/MediaFileSourceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Models/MediaFileSourceTests.cs).
  - Added [SwitchCast.Tests/Services/MediaDiscoveryServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/MediaDiscoveryServiceTests.cs).
  - Added [SwitchCast.Tests/Services/PresentationCoordinatorMediaTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresentationCoordinatorMediaTests.cs).
  - Added [SwitchCast.Tests/ViewModels/SourcesViewModelMediaTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/SourcesViewModelMediaTests.cs).
  - Expanded test suite from 170 to **209 automated unit tests** (100% pass rate).

---

## [v1.0.0 — Final UI Polish, Creator Attribution & First GitHub Release] - 2026-10-09

### Added / Improved (feat / UI / test / docs / release)
- **Settings Page UX & Visual Refinement**:
  - Replaced bulky radio-button circles in the Settings sidebar with a compact `ListView` navigation list (38 DIP row height, 14 DIP icons, clean hover/selected states).
  - Aligned Appearance color mode options (System | Light | Dark) with clean horizontal spacing.
  - Aligned Window and Presenter preference toggles with responsive text wrapping to prevent horizontal clipping.
  - Streamlined Keyboard Shortcuts list (~46 DIP row height) with action descriptions, filter search, and monospace key badges.
- **Creator Attribution & Authoritative Metadata**:
  - Added full creator attribution: **John Christopher King Zamora**.
  - Added repository link: [https://github.com/jczamora-git/SwitchCast](https://github.com/jczamora-git/SwitchCast).
  - Added `OpenRepositoryCommand` launching the GitHub repository via `Windows.System.Launcher.LaunchUriAsync`.
  - Configured single authoritative version metadata in `SwitchCast.csproj` (`Version 1.0.0`, `AssemblyVersion 1.0.0.0`, `InformationalVersion 1.0.0`).
  - Added factual privacy architecture statement (`100% Offline & Local • Zero Telemetry • No Network Access`).
- **Release Packaging (win-x64)**:
  - Published self-contained Release package via `dotnet publish SwitchCast.csproj -c Release -r win-x64 --self-contained true`.
  - Generated distribution archive `releases/SwitchCast-v1.0.0-win-x64.zip` (SHA-256: `6C416C1A274A931EB9B487CCD9A0B5ADAFB8E423A54559386591A67850E495DE`).
- **Documentation & Remote Configuration**:
  - Created comprehensive [README.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/README.md) with overview, features, technology stack, keyboard shortcuts, usage guide, and creator attribution.
  - Created [docs/RELEASE_NOTES_v1.0.0.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/RELEASE_NOTES_v1.0.0.md).
  - Configured `origin` remote: `https://github.com/jczamora-git/SwitchCast.git`.
- **Automated Unit Tests**:
  - Added [SwitchCast.Tests/ViewModels/SettingsViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/SettingsViewModelTests.cs) verifying About metadata, Creator, RepositoryUrl, Subtitle, and Privacy statement.
  - Total test suite: 170 automated unit and regression tests passing (100% pass rate).

---

## [Dynamic Application Icon Pipeline Repair] - 2026-10-09

### Fixed / Improved (fix / UI / test / docs)
- **Resolved DispatcherQueue Resolution & UI Thread Marshalling**:
  - Fixed root-cause defect where `Win32WindowIconService` was initialized in the `App` constructor before `DispatcherQueue.GetForCurrentThread()` became available, causing `_dispatcherQueue` to remain `null` and falling back to direct background thread `SoftwareBitmapSource.SetBitmapAsync` calls that threw `RPC_E_WRONG_THREAD` and silently returned `null`.
  - Added `SetDispatcherQueue` and lazy fallback resolution in [Services/Win32WindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowIconService.cs).
  - Wired `_mainWindow.DispatcherQueue` binding in [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs) upon window activation.
  - Implemented explicit UI-thread marshalling in [ViewModels/SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs) `LoadIconsAsync` when setting `item.IconSource`, ensuring WinUI 3 XAML compiled binding (`x:Bind`) property changed events execute strictly on the UI thread.
- **Model Property Synchronization**:
  - Implemented `OnIconSourceChanged` in [ViewModels/SelectableSourceItem.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SelectableSourceItem.cs) to automatically synchronize `HasIconSource` and `HasNoIconSource` boolean flags and notify XAML bindings whenever `IconSource` is assigned.
- **Dual-Tier Thread-Safe Caching**:
  - Added immutable raw `byte[]` pixel cache (`_rawPixelCache`) in `Win32WindowIconService` alongside `_iconSourceCache`, allowing raw BGRA32 icon data to be cached with zero thread affinity across all workers and windows.
- **Automated Unit & Regression Tests**:
  - Added [SwitchCast.Tests/Services/WindowIconExtractionTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/WindowIconExtractionTests.cs) and updated [SwitchCast.Tests/ViewModels/SelectableSourceItemTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/SelectableSourceItemTests.cs) to verify automatic `HasIconSource` and `HasNoIconSource` updates.
  - All 169 unit and regression tests passing (100% pass rate).

---

## [Window Positioning & Native Application Icon] - 2026-10-09

### Added / Improved (feat / UI / test / docs)
- **Centered Presentation Output Window Positioning**:
  - Implemented automatic, DPI-aware initial centering for `PresentationWindow` in [Views/PresentationWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml.cs) using [Services/WindowPositioningHelper.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/WindowPositioningHelper.cs).
  - Determines target monitor from `MainWindow` (if active) with fallback to primary display (`MonitorFromWindow`), retrieving usable work area (`MONITORINFO.rcWork`) excluding taskbar offsets.
  - Supports multi-monitor configurations with arbitrary or negative virtual coordinates, arbitrary DPI scale factors (100%, 125%, 150%), and work-area boundary clamping if requested dimensions exceed the screen.
  - Preserved single-instance lifecycle: centering applies strictly on initial window open; existing open windows are brought forward via `IWindowActivationService` without recentering or interrupting ongoing screen shares.
- **Native Windows Application Icon & Consistent Branding**:
  - Created high-quality multi-resolution Windows icon asset at [Assets/SwitchCast.ico](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Assets/SwitchCast.ico) matching the custom title-bar coral badge (`#FF7A59`) and screen-share glyph (`\uE7F4`). Contains 7 standard resolutions (16x16, 24x24, 32x32, 48x48, 64x64, 128x128, 256x256) with 32-bit ARGB alpha transparency.
  - Configured `<ApplicationIcon>Assets\SwitchCast.ico</ApplicationIcon>` and `<Content Include="Assets\SwitchCast.ico"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>` in [SwitchCast.csproj](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.csproj) to embed the icon in `SwitchCast.exe` for Windows Explorer, Taskbar, and Task Manager.
  - Configured native window icons on `MainWindow` and `PresentationWindow` using `_appWindow.SetIcon(...)`, ensuring crisp branded identities in Alt+Tab, Taskbar entries, and window titles.
- **Automated Unit & Regression Tests**:
  - Created [SwitchCast.Tests/Services/WindowPositioningHelperTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/WindowPositioningHelperTests.cs) verifying centering math across 1080p, 1440p, 1366x768, negative coordinates, DPI scaling, and work-area clamping.
  - Created [SwitchCast.Tests/Services/ApplicationBrandingTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/ApplicationBrandingTests.cs) verifying `Assets/SwitchCast.ico` file header integrity, 7 embedded resolution frames, and `.csproj` build configuration.
  - All 167 unit and regression tests passing (100% pass rate).

---

## [Dynamic Application Icons Runtime Fix] - 2026-10-09

### Fixed / Added (fix / UI / test / docs)
- **WinUI 3 UI Thread Marshalling for SoftwareBitmapSource**:
  - Fixed thread affinity fault (`RPC_E_WRONG_THREAD`) in [Services/Win32WindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowIconService.cs) where `SoftwareBitmapSource` and `SetBitmapAsync` were called on a ThreadPool worker thread following asynchronous pixel extraction, which threw a COM exception and defaulted all items to fallback glyphs.
  - Marshalled `SoftwareBitmapSource` creation and bitmap initialization to the UI thread via `_dispatcherQueue.TryEnqueue(...)`.
- **QueryFullProcessImageNameW for Cross-Architecture Process Paths**:
  - Upgraded [Services/Win32WindowDiscoveryService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowDiscoveryService.cs) and `Win32WindowIconService` with `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` and `QueryFullProcessImageNameW`, resolving Access Denied failures on 32-bit and non-elevated user applications.
- **Multi-Tier Win32 Icon Resolution & Alpha Handling**:
  - Implemented multi-tier icon resolution (`WM_GETICON` -> `GetClassLongPtr` -> `ExtractIconExW` -> `SHGetFileInfoW`).
  - Added `GetIconInfo` bit-depth validation to preserve true alpha channel transparency for modern 32-bit ARGB icons (Chrome, Visual Studio, Explorer, Antigravity) while synthesizing opaque alpha for legacy masked icons.
  - Ensured safe native handle ownership (`DestroyIcon` on owned shell/executable handles, never on borrowed window/class handles).
- **Dual-Key Caching & Bounded Concurrent Loading**:
  - Dual-keyed cache (`windowSource.Id` and `exe_{ProcessPath}`) reusing extracted process icons across multiple windows in 0ms.
  - Upgraded [ViewModels/SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs) `LoadIconsAsync` with `SemaphoreSlim(8)` to load window icons concurrently without blocking the UI.
- **Automated Unit Tests**:
  - Expanded [SwitchCast.Tests/Services/WindowIconServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/WindowIconServiceTests.cs) with real `explorer.exe` icon extraction, cache reuse, and cancellation handling, expanding the test suite to 156 passing tests (100% pass rate).

---

## [Presentation Output Custom Title Bar UI Hotfix] - 2026-10-09

### Fixed / Added (fix / UI / test / docs)
- **Modern Integrated Custom Title Bar on Presentation Output**:
  - Replaced the default bright white Windows native caption title bar on [Views/PresentationWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml) with a sleek integrated Fluent Design title bar matching `MainWindow`.
  - Configured `ExtendsContentIntoTitleBar = true` and `SetTitleBar(AppTitleBar)` in [Views/PresentationWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml.cs).
  - Styled native Windows caption buttons via `AppWindow.TitleBar` (transparent background, theme-synchronized glyph foregrounds and hover/pressed states, double-click maximize/restore, window dragging, and Windows 11 Snap Layouts).
- **Theme Awareness & Dynamic Theme Switching**:
  - Subscribed `PresentationWindow` to `IApplicationSettingsService.ThemeChanged` to dynamically adapt the title bar surface, title text, and caption button glyphs when toggling between Dark and Light modes.
  - Linked root grid and header brushes to semantic design system tokens (`AppBackgroundBrush`, `AppTextPrimaryBrush`, `AppSubtleDividerBrush`, `AppAccentBrush`).
- **Audience Presentation Canvas & Geometry Integrity**:
  - Maintained clear separation between title bar (`Grid.Row="0"`, 38 DIPs) and the presentation canvas (`Grid.Row="1"`, `*`).
  - Preserved all 3 presentation layers: Standby Screen, Live Video Canvas (`Image` with `Stretch="Uniform"` and frozen paused indicator pill), and Blackout Canvas.
  - Preserved native window title `"SwitchCast Presentation Output"` without applying `WDA_EXCLUDEFROMCAPTURE`, guaranteeing continued instant discovery and capture in Zoom, Microsoft Teams, and Google Meet.
- **Automated Unit Tests**:
  - Expanded [SwitchCast.Tests/ViewModels/PresentationViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/PresentationViewModelTests.cs) with dynamic property change tests for presentation state and coordinator events, bringing total passing tests to 154 (100% pass rate).

---

## [Desktop UX Hotfix — Presenter Actions Bring-to-Front, Centered Startup & Dynamic Application Icons] - 2026-10-09

### Added (feat / UI / test / docs)
- **Presenter Actions Bring-to-Front & Window Focus Handoff**:
  - Fixed Presenter Actions ("Control Dashboard" and "Presentation Output") in [Views/PresenterDockMenuWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockMenuWindow.xaml.cs) by detaching owner HWND parent (`GWLP_HWNDPARENT = IntPtr.Zero`) prior to menu closure, preventing Windows from automatically reclaiming focus to the floating dock.
  - Enhanced [Services/Win32WindowActivationService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowActivationService.cs) and [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs) `ActivateMainWindow()` to restore minimized windows (`SW_RESTORE`) and request foreground activation (`SetForegroundWindow`) without creating duplicate `MainWindow` instances.
  - Enhanced [Services/PresentationWindowService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationWindowService.cs) with injected `IWindowActivationService`, ensuring existing `PresentationWindow` instances are restored and brought forward without recreating the window or disrupting capture.
- **Centered MainWindow Startup & DPI-Aware Saved Placement**:
  - Implemented DPI-aware startup window placement in [MainWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/MainWindow.xaml.cs).
  - Calculates target monitor usable work area center coordinates via `MonitorFromWindow` and `GetMonitorInfo` (`centerX = workArea.Left + (workArea.Width - windowWidth) / 2`, `centerY = workArea.Top + (workArea.Height - windowHeight) / 2`), supporting secondary displays with negative coordinates and taskbar offsets.
  - Added `WindowPositionX`, `WindowPositionY`, and `RememberWindowPosition` in [Models/UserSettings.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/UserSettings.cs) and [Services/ApplicationSettingsService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/ApplicationSettingsService.cs), restoring valid positions with automatic work-area boundary clamping or recovering to center when a saved display is disconnected.
- **Dynamic Native Application Icons Engine**:
  - Implemented [Services/IWindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IWindowIconService.cs) and [Services/Win32WindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowIconService.cs) to extract authentic Windows application icons via local Win32 / Shell APIs:
    1. Window icon: `SendMessageTimeout` with `WM_GETICON` (`ICON_SMALL2` -> `ICON_SMALL` -> `ICON_BIG`) with 200ms timeout.
    2. Class icon: `GetClassLongPtr` (`GCLP_HICONSM` -> `GCLP_HICON`).
    3. Shell icon: `SHGetFileInfo` / process executable icon extraction.
    4. Safe native handle ownership: `DestroyIcon` strictly released on owned shell handles, never on borrowed window/class icons.
    5. GDI 32-bit DIB section extraction with full alpha-channel validation and conversion to WinUI `SoftwareBitmapSource`.
    6. In-memory thread-safe caching (`ConcurrentDictionary`) keyed by source ID.
  - Updated [ViewModels/SelectableSourceItem.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SelectableSourceItem.cs) and [ViewModels/SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs) to load icons asynchronously upon discovery refresh.
  - Updated [Views/SourcesPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SourcesPage.xaml) to display true-color native 20x20 application icons with theme-adaptive fallback glyphs for displays or unresolved windows.
- **Automated Unit & Regression Tests**:
  - Added [SwitchCast.Tests/Services/WindowIconServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/WindowIconServiceTests.cs), [SwitchCast.Tests/Services/MainWindowPositioningMathTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/MainWindowPositioningMathTests.cs), [SwitchCast.Tests/Services/PresenterDockActionsActivationTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresenterDockActionsActivationTests.cs), and [SwitchCast.Tests/ViewModels/SourcesViewModelIconTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/SourcesViewModelIconTests.cs).
  - Expanded the test suite from 135 to 152 automated passing tests (100% pass rate).

---

## [Window Hierarchy & Safe Application Shutdown] - 2026-10-09

### Added (feat / UI / test / docs)
- **Application Window Hierarchy & Main Window Exit Authority**:
  - Implemented synchronous close interception (`AppWindow.Closing` with `args.Cancel = true`) on `MainWindow`.
  - Added native WinUI 3 `ContentDialog` confirmation:
    - Presenting prompt: `"Your live presentation will stop, and all SwitchCast windows will close. Are you sure you want to exit?"`
    - Idle prompt: `"Are you sure you want to exit SwitchCast?"`
    - Safe "Cancel" default action vs. "Exit SwitchCast" primary action.
    - Protected against re-entrant confirmation dialogs during rapid `X` clicks or `Alt+F4`.
- **Centralized Application Lifecycle Coordinator**:
  - Implemented [Services/IApplicationLifecycleService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IApplicationLifecycleService.cs) and [Services/ApplicationLifecycleService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/ApplicationLifecycleService.cs) registered as a singleton in DI container.
  - Governs deterministic, idempotent teardown: stopping presentation, stopping capture, closing secondary windows, unregistering hotkeys, and persisting user settings.
- **Secondary Window Close Isolation**:
  - **Presenter Dock**: Closing the dock closes only the dock window and any open popups; leaves active capture, presentation output, and MainWindow untouched.
  - **Presentation Output**: Closing the output window safely stops active presentations, clears the renderer, sets status to `Idle`, and leaves `MainWindow` open.
- **Automated Unit Tests**:
  - Added [SwitchCast.Tests/Services/ApplicationLifecycleServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/ApplicationLifecycleServiceTests.cs) verifying 6 lifecycle scenarios (initial state, approval, confirmation tracking, teardown coordination, idempotency, exception resilience), bringing total passing tests to 135 (100% pass rate).

---

## [Phase 5.3 Hotfix — Floating Dock Dropdown Overflow & External Menu Positioning] - 2026-10-09

### Fixed (fix / UI / test / docs)
- **Windows App SDK Root Bounds Workaround & Native Window Host**:
  - Resolved popup clipping defect on Floating Presenter Dock where dropdowns were restricted to the 46–52 DIP window height due to `IsConstrainedToRootBounds` being true in Windows App SDK.
  - Implemented [Views/PresenterDockMenuWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockMenuWindow.xaml) and [Views/PresenterDockMenuWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockMenuWindow.xaml.cs) as a lightweight borderless topmost WinUI 3 Window with `WS_EX_TOOLWINDOW` and dock HWND ownership (`GWLP_HWNDPARENT`).
- **DPI-Aware Positioning & Monitor Bounds Clamping**:
  - Implemented [Services/PresenterDockMenuPositioner.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresenterDockMenuPositioner.cs) supporting DPI scaling (`GetDpiForWindow`), monitor work area calculation (`GetMonitorInfo`), right-edge shift-in, left-edge clamp, and automatic flip-above when the dock is positioned near the bottom of the screen.
- **Unconstrained Presenter Dropdown Menus**:
  - **Queued Sources**: Scrollable `ListView` displaying queued capture sources with title, type, and active checkmarks, with empty state when 0 sources are queued.
  - **Switching Mode**: 3-mode selector (`ActiveAndLive`, `ActiveOnly`, `LiveOnly`) with titles, descriptions, and active checkmarks.
  - **More Options**: Direct actions for Stop Live Presentation, Control Dashboard, and Presentation Output Window.
- **Lifecycle & Dismissal Safety**:
  - Integrated automatic dismissal on `WindowActivationState.Deactivated`, `Escape` key press, item selection, dock dragging, dock collapse/expand, and dock closure.
- **Automated Unit Tests**:
  - Added [SwitchCast.Tests/Services/PresenterDockMenuPositionerTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresenterDockMenuPositionerTests.cs) verifying 5 positioning and boundary clamping scenarios, expanding the test suite to 129 passing tests (100% pass rate).

---

## [Main Application UI/UX Refinement] - 2026-10-09

### Added (feat / UI / test / docs)
- **Modern Windows Desktop Shell & Custom Integrated Title Bar**:
  - Implemented custom integrated application top title bar in [MainWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/MainWindow.xaml) and [MainWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/MainWindow.xaml.cs) using `ExtendsContentIntoTitleBar = true` and `SetTitleBar(AppTitleBar)`.
  - Configured native caption buttons via `AppWindow.TitleBar` with transparent backgrounds and dynamic theme-synchronized foreground and hover colors.
  - Retained standard Windows behavior: window dragging, double-click to maximize/restore, minimize, maximize, close, and Windows 11 Snap Layouts.
- **Centralized Fluent Design System Tokens (`App.xaml`)**:
  - Added dark (`#101010` - `#141414`) and light theme dictionaries in [App.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml) providing semantic brushes: `AppBackgroundBrush`, `AppSidebarBrush`, `AppSurfaceBrush`, `AppSurfaceElevatedBrush`, `AppHoverBrush`, `AppBorderBrush`, `AppSubtleDividerBrush`, `AppAccentBrush` (coral `#FF7A59`), `AppBadgeBackgroundBrush`, and `AppPreviewCanvasBrush`.
  - Reusable styles for `SubtleButtonStyle`, `PrimaryAccentButtonStyle`, `DestructiveButtonStyle`, and `KeyBadgeBorderStyle`.
- **Workflow-First Presenter Dashboard**:
  - Redesigned [Views/DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml) with compact top status summary strip (Live/Paused/Blackout/Standby status, active source, queued count, output window status).
  - Enlarged focal 16:9 aspect ratio preview canvas with overlay controls, live status badge, and clear empty/ready states.
  - Prominent primary action bar with coral "Start Presenting" / red "Stop Presenting", target source picker, and pause/blackout controls.
  - Lightweight queued sources mini-strip.
- **Compact Desktop Source Picker**:
  - Redesigned [Views/SourcesPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SourcesPage.xaml) with unified filter toolbar (category selector for Windows vs Displays + instant search text box).
  - Compact ~52px list rows with single-line truncated titles, process metadata, and queue checkboxes.
- **Two-Pane Categorized Settings (OpenCode Inspired)**:
  - Redesigned [Views/SettingsPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SettingsPage.xaml) into a two-pane layout with category navigation panel (`General`, `Appearance`, `Window`, `Presenter Controls`, `Keyboard Shortcuts`).
  - Added compact setting rows with right-aligned toggles and subtle horizontal dividers.
  - Implemented searchable keyboard shortcuts table with key badge pills and "Reset to Defaults" action.
  - Updated [ViewModels/SettingsViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SettingsViewModel.cs) with category navigation and search filtering.
- **Automated Unit Tests**:
  - Added tests in [SwitchCast.Tests/ViewModels/SettingsViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/SettingsViewModelTests.cs) for category navigation visibility and shortcut filtering, bringing total passing tests to 124 (100% pass rate).

---

## [Phase 5.3 — Minimal Presenter Dock & Three-Mode Source Switching] - 2026-10-09

### Added (feat / UI / test / docs)
- **Three-Mode Source Switching Architecture (`PresenterSwitchMode`)**:
  - Implemented [Models/PresenterSwitchMode.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/PresenterSwitchMode.cs) supporting `LiveOnly` (default), `ActiveAndLive`, and `ActiveOnly`.
  - Implemented [Services/IWindowActivationService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IWindowActivationService.cs) and [Services/Win32WindowActivationService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowActivationService.cs) handling native `SetForegroundWindow` and `ShowWindowAsync` (`SW_RESTORE`).
  - Added navigation cursor (`SelectedSource`), foreground tracking (`ForegroundSource`), and live on-air (`ActiveSource`) separation in [IPresentationStateService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresentationStateService.cs) and [PresentationStateService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationStateService.cs).
  - Integrated three-mode routing in `PresentationCoordinator.ExecuteSourceSwitchAsync`, ensuring Dock buttons, quick switcher flyout, and Global Hotkeys (`Ctrl+Shift+Right`, `Ctrl+Shift+Left`, `Ctrl+Shift+1..5`) follow the active mode.
- **Minimal Single-Row Presenter Toolbar (Both Modes)**:
  - Redesigned [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml) into an ultra-compact single-row toolbar in both Expanded (660×52 DIP) and Compact (460×46 DIP) modes.
  - Enforced single-line source titles with `TextTrimming="CharacterEllipsis"`, `TextWrapping="NoWrap"`, and `MaxLines="1"` with rich multi-line tooltips.
  - Added mode dropdown selector buttons with concise badges ("A+L", "A", "L"), informative tooltips, and checkmarked selection flyouts on both modes.
  - Added icon-first buttons for Pause/Resume, Blackout, Stop Presenting, and More actions (Dashboard, Output window) with full accessibility tooltips.
- **Automated Unit & Regression Tests**:
  - Added [SwitchCast.Tests/Services/WindowActivationServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/WindowActivationServiceTests.cs).
  - Expanded [SwitchCast.Tests/Services/PresentationCoordinatorTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresentationCoordinatorTests.cs) with tests for all three switching modes, window activation error resilience, and monitor source fallbacks.
  - Expanded [SwitchCast.Tests/Services/PresentationStateServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresentationStateServiceTests.cs) and [SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs), expanding the automated test suite to 122 passing tests (100% pass rate).

---

## [Phase 5.1 — Floating Presenter Dock UI & Window Chrome Fix] - 2026-10-08

### Fixed (fix / UI / test)
- **Window Chrome & Native Title Bar Removal**:
  - Configured `presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false)`, removing the duplicate OS caption title bar that was previously consuming ~32px and vertically compressing the dock content.
  - Implemented custom drag region on the top header bar delegating directly to the Windows window manager via `ReleaseCapture()` and `SendMessage(WindowHandle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero)`.
- **DPI-Aware Window Scaling & Work-Area Centering**:
  - Implemented `GetDpiForWindow` scaling (`scale = dpi / 96.0`) ensuring physical pixel allocations (`AppWindow.Resize`) accurately reflect DIP targets across 100%, 125%, 150%, and 200% displays.
  - Centered default opening position at top of current monitor work area via `MonitorFromWindow` and `GetMonitorInfo`.
- **2-Row Expanded & 1-Row Compact Presenter Modes**:
  - **Expanded Mode** (620×110 DIP): 2-row layout with top drag header (SwitchCast branding, live status pill, mode toggle, close button) and bottom action row (source quick switcher flyout, next/previous buttons, pause, blackout, stop, show output, show dashboard).
  - **Compact Mode** (480×54 DIP): Sleek 1-row mini toolbar with essential switching, playback controls, and expand toggle.
  - Added setting persistence for compact mode preference via `UserSettings.StartDockInCompactMode`.
- **Decoupled Dashboard Focus Navigation**:
  - Added `ShowDashboard()` / `RequestShowDashboard` on `IPresenterDockService` and `PresenterDockService`, cleanly routing dashboard activation to `_mainWindow?.Activate()` without direct UI dependencies.
- **XAML Resource Resolution & SubtleButtonStyle Fix**:
  - Resolved `Microsoft.UI.Xaml.Markup.XamlParseException` (HRESULT `0x802B000A`) caused by referencing non-existent `{ThemeResource SubtleButtonStyle}`.
  - Defined explicit local `DockSubtleButtonStyle` inside `<Grid.Resources>` of [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml) and added `SubtleButtonStyle` in [App.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml) `<Application.Resources>` as an application-wide fallback.
  - Audited and verified all theme resources and brush keys across the entire Presenter Dock view.
- **Automated Unit Tests**:
  - Added `ShowDashboardCommand_CallsDockServiceShowDashboard` to [SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs), bringing total passing tests to 110 (100% pass rate).

---

## [Phase 5 — Global Hotkeys & Floating Presenter Dock] - 2026-10-08

### Added (feat / test / docs)
- **Native Win32 Global Hotkeys Engine**:
  - Implemented [Models/HotkeyModels.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/HotkeyModels.cs), [Services/IHotkeyService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IHotkeyService.cs), and [Services/Win32HotkeyService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32HotkeyService.cs) registering system-wide hotkeys via Win32 `RegisterHotKey` / `UnregisterHotKey`.
  - Hosted on a lightweight message-only window (`HWND_MESSAGE` = `-3`) with pinned `WndProc` delegate, capturing `WM_HOTKEY` (0x0312) messages without polling, hooks, or window focus dependencies.
  - Actions supported: Next Source (`Ctrl+Shift+Right`), Previous Source (`Ctrl+Shift+Left`), Pause/Resume (`Ctrl+Shift+P`), Blackout (`Ctrl+Shift+B`), Stop Presenting (`Ctrl+Shift+S`), Toggle Presenter Dock (`Ctrl+Shift+D`), Focus Dashboard (`Ctrl+Shift+M`), and Direct Source Switching (`Ctrl+Shift+1..5`).
- **Floating Presenter Companion Dock**:
  - Implemented [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml) and [Views/PresenterDockWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml.cs) as a top-level native WinUI 3 Window with `OverlappedPresenter.IsAlwaysOnTop = true`, fixed compact size (440x88), custom title bar drag handle (`AppTitleBar`), and borderless styling.
  - Built [ViewModels/PresenterDockViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/PresenterDockViewModel.cs) with status badge (Live/Paused/Blackout/Idle), direct switch flyout with live queued sources, next/previous buttons, pause toggle, blackout toggle, stop presenting, and show presentation output.
- **Single-Instance Presenter Dock Window Service**:
  - Implemented [Services/IPresenterDockService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresenterDockService.cs) and [Services/PresenterDockService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresenterDockService.cs) to manage opening, closing, and toggling dock visibility cleanly.
- **Presenter Controls & Hotkey Configuration in Settings**:
  - Added Section C in [Views/SettingsPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SettingsPage.xaml) and [ViewModels/SettingsViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SettingsViewModel.cs) for enabling global hotkeys, auto-opening dock on presentation start, keeping dock always-on-top, and displaying the active hotkey binding table.
  - Added persistence properties to [Models/UserSettings.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/UserSettings.cs).
- **Sequential Source Switching in Presentation Coordinator**:
  - Added `SwitchToNextSourceAsync()`, `SwitchToPreviousSourceAsync()`, and `SwitchToSourceIndexAsync(int index)` to [IPresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresentationCoordinator.cs) and [PresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationCoordinator.cs), routing directly through the serialized Latest-Request-Wins pipeline.
- **Automated Unit Test Suites**:
  - Added [SwitchCast.Tests/Services/HotkeyServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/HotkeyServiceTests.cs) (hotkey lifecycle, binding models, event triggers).
  - Added [SwitchCast.Tests/Services/PresenterDockServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresenterDockServiceTests.cs) (dock service lifecycle).
  - Added [SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs) (presenter dock commands and state sync).
  - Expanded [SwitchCast.Tests/Services/PresentationCoordinatorTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresentationCoordinatorTests.cs) for quick next/previous switching, expanding the total test suite to 109 tests (100% pass rate).

---

## [Phase 4.7 — Rapid Source Switching Crash Fix & Concurrency Hardening] - 2026-10-08

### Fixed (fix / test / docs)
- **WinRT 0xC000027B Stowed Exception & Native Lifetime Hardening**:
  - Eliminated premature disposal of `emptyBitmap` in `Clear()` across `Direct3D11PreviewRenderer` and `Direct3D11PresentationRenderer`, which was disposing the underlying COM `SoftwareBitmap` while `SoftwareBitmapSource.SetBitmapAsync` was in-flight on the compositor.
  - Ensured `SoftwareBitmapSource.Dispose()` is strictly scheduled on the UI thread's `DispatcherQueue`.
- **Latest-Request-Wins Source Switching Serialization**:
  - Implemented transition sequence numbers (`_transitionSequenceNumber`, `_presentationSequenceNumber`) and requested target sources in `CaptureCoordinator` and `PresentationCoordinator`.
  - Coalesces rapid sequential requests (A -> B -> C) by dropping superseded intermediate switches immediately upon acquiring the transition semaphore, preventing duplicate/overlapping native capture session creation and frame pool destruction.
- **Synchronous Exception Boundaries in Event Handlers**:
  - Converted `OnFrameArrived` and `OnCaptureFrameArrived` from `async void` to synchronous `void` with top-level try/catch blocks, eliminating unobserved asynchronous exception escapes to the UI SynchronizationContext.
- **Session Generation Filtering in Renderers**:
  - Enhanced `ICapturePreviewRenderer` and `IPresentationOutputRenderer` with session generation checks (`long generation = 0`), discarding stale in-flight UI frame renders arriving after a session transition.
- **Automated Concurrency Regression Tests**:
  - Added [SwitchCast.Tests/Services/RapidSwitchingConcurrencyTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/RapidSwitchingConcurrencyTests.cs) with 16 automated concurrency test scenarios, expanding the test suite to 86 tests (100% pass rate).

---

## [Phase 4.6 — Performance & Stability Optimization] - 2026-10-08

### Added (perf / test / docs)
- **RefCountedSoftwareBitmap Memory Management**:
  - Implemented [RefCountedSoftwareBitmap.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/RefCountedSoftwareBitmap.cs) providing thread-safe reference-counted lifetime management around WinRT `SoftwareBitmap` instances.
  - Guarantees deterministic disposal across multiple asynchronous UI and capture consumers without GC finalizer delays or unmanaged memory growth (eliminated up to 250 MB/s allocation leak).
- **Decoupled Non-Blocking UI Delivery Pipeline**:
  - Refactored [Direct3D11PreviewRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PreviewRenderer.cs) and [Direct3D11PresentationRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PresentationRenderer.cs) to eliminate `TaskCompletionSource` blocking waits from capture worker threads.
  - Implemented atomic presentation gates via `Interlocked.CompareExchange`, completely eliminating worker threadpool starvation and `0xC000027B` stowed exception crash vectors.
- **Priority Separation & Preview Rate-Limiting**:
  - Implemented ~15 FPS (66ms interval) rate limiter for Dashboard Preview monitoring, reducing preview GPU-to-CPU and UI thread workload by 75%.
  - Preserved unthrottled (~30-60 FPS) delivery for dedicated Presentation Output.
- **Session Generation Tracking**:
  - Added incrementing session generation counters in [CaptureSessionManager.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/CaptureSessionManager.cs) to discard stale frames arriving across rapid source switching boundaries.
- **Direct3D Device Recovery**:
  - Added `ResetDevice()` in [Direct3D11DeviceProvider.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11DeviceProvider.cs) for resilient handling of DXGI device removal/reset events.
- **Performance Baseline Documentation**:
  - Documented authoritative before/after audit report in [docs/PERFORMANCE_BASELINE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/PERFORMANCE_BASELINE.md).
- **Automated Regression Tests**:
  - Added [SwitchCast.Tests/Services/RefCountedSoftwareBitmapTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/RefCountedSoftwareBitmapTests.cs) and expanded `CaptureCoordinatorTests.cs` and `PresentationCoordinatorTests.cs`, bringing total passing tests to 70 (100% pass rate).

---

## [Stabilization & Capture Recovery] - 2026-10-08

### Fixed (fix / perf / test)
- **Direct3D11CaptureFrame Premature Disposal & Exception Storms**:
  - Eliminated synchronous `using var frame` disposal in `CaptureSessionManager.cs` that previously destroyed native WinRT `IDirect3DSurface` COM objects during asynchronous `SoftwareBitmap.CreateCopyFromSurfaceAsync` surface extraction.
  - Bound frame lifetime to complete surface copies, eliminating downstream `ObjectDisposedException`, `ArgumentException`, and `TaskCanceledException` storms.
  - Implemented `Interlocked.CompareExchange` backpressure pacing with frame pool draining to prevent threadpool starvation and real-time lag.
- **Unified Owned SoftwareBitmap Distribution**:
  - Upgraded `FrameArrivedEventArgs` to deliver independently owned `SoftwareBitmap` instances.
  - Added `RenderBitmapAsync(SoftwareBitmap)` to `ICapturePreviewRenderer` and `IPresentationOutputRenderer`, allowing both dashboard preview and presentation output to share a single GPU-to-CPU copy without redundant concurrent surface reads.
- **Accurate Presentation Status**:
  - Ensured `PresentationCoordinator` and `DashboardViewModel` transition cleanly from `Starting` to `Active` only when capture frames are confirmed.

### Added (feat / test)
- **Win32 GDI Diagnostic Capture Service**:
  - Implemented [IWin32DiagnosticCaptureService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/IWin32DiagnosticCaptureService.cs) and [Win32DiagnosticCaptureService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Win32DiagnosticCaptureService.cs) based on reference patterns from AutoSnap (`PrintWindow` with `PW_RENDERFULLCONTENT (0x02)` and `BitBlt` fallbacks) with direct conversion of 32-bit DIB sections into WinUI 3 `SoftwareBitmap`.
- **Automated Unit Tests**:
  - Added [SwitchCast.Tests/Services/Win32DiagnosticCaptureServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/Win32DiagnosticCaptureServiceTests.cs) and expanded `CaptureCoordinatorTests.cs`, bringing the test suite to 61 tests (100% pass rate).

---

## [Phase 4] - 2026-10-08

### Added (feat / test / docs)
- **Dedicated Presentation Output Window**:
  - Implemented [Views/PresentationWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml) and [Views/PresentationWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml.cs) as a separate, shareable native WinUI 3 Window titled `"SwitchCast Presentation Output"`.
  - Configured 1280x720 initial client dimensions (16:9), resizable, moveable across monitors, without `WDA_EXCLUDEFROMCAPTURE` to ensure direct discovery in Google Meet, Zoom, and Microsoft Teams.
  - Implemented visual states: Standby Screen ("Ready to Present"), Live Video Canvas (`Stretch="Uniform"` letterbox/pillarbox), Paused indicator pill, and 100% opaque Blackout overlay.
- **Single-Instance Presentation Window Service**:
  - Implemented [IPresentationWindowService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresentationWindowService.cs) and [PresentationWindowService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationWindowService.cs) to ensure idempotent window opening, activation/focus, and clean teardown on closure.
- **Unified Frame Delivery Architecture & Presentation Renderer**:
  - Enhanced [ICaptureCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/ICaptureCoordinator.cs) with `FrameArrived` event distribution, enabling a single underlying capture session to supply both local preview and shareable output renderers simultaneously without redundant captures.
  - Implemented [IPresentationOutputRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/IPresentationOutputRenderer.cs) and [Direct3D11PresentationRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PresentationRenderer.cs) providing real-time GPU frame conversion, frame pacing, freeze-frame pause retention, and blackout clearing.
- **Presentation Coordinator & Presenter Controls**:
  - Implemented [IPresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresentationCoordinator.cs) and [PresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationCoordinator.cs) managing presentation lifecycle (`Idle`, `Starting`, `Active`, `Paused`, `Blackout`, `Error`), continuous source switching, and synchronized state transitions.
- **Dashboard Presentation UI & XAML MVVM Binding Fix**:
  - Updated [DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml) with Section B (Presentation Output Controls), output window status indicator, Start/Stop presentation actions, Pause/Resume toggle, and Blackout button.
  - Resolved reported XLS0432 diagnostics and verified all XAML bindings against `DashboardViewModel.cs`.
- **Automated Unit Tests**:
  - Expanded test suite from 38 to 57 unit tests in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) verifying presentation coordinator, presentation window states, and dashboard commands (100% pass rate).

### Fixed (fix)
- **RoGetActivationFactory HSTRING Marshaling**: Replaced invalid `[MarshalAs(UnmanagedType.HString)] string` P/Invoke parameter with native `IntPtr` handle and safe allocation/deletion lifecycle (`WindowsCreateString` and `WindowsDeleteString`), resolving runtime `MarshalDirectiveException` (0x80131535).

---

## [Phase 3] - 2026-10-08

### Added (feat / test / docs)
- **Native Windows Graphics Capture Engine**:
  - Implemented COM interop [IGraphicsCaptureItemInterop.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Interop/IGraphicsCaptureItemInterop.cs) for `GraphicsCaptureItem` creation from `HWND` and `HMONITOR`.
  - Implemented [GraphicsCaptureItemFactory.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/GraphicsCaptureItemFactory.cs) validating window validity and owning PID cross-checks to prevent HWND reuse security hazards.
  - Implemented [Direct3D11DeviceProvider.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11DeviceProvider.cs) providing hardware-accelerated Direct3D 11 devices with WARP fallback and WinRT `IDirect3DDevice` projections.
  - Implemented [CaptureSessionManager.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/CaptureSessionManager.cs) managing `Direct3D11CaptureFramePool`, cursor capture toggles, dynamic surface resizing, and fail-closed disposal on source close.
  - Implemented [Direct3D11PreviewRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PreviewRenderer.cs) converting GPU surfaces into `SoftwareBitmapSource` with real-time frame pacing.
  - Implemented [CaptureCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/CaptureCoordinator.cs) governing serialized state machine transitions (`Idle`, `Starting`, `Capturing`, `Stopping`, `Failed`).
- **Dashboard Live Preview UI**:
  - Implemented live video surface in [Views/DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml).
  - Added source switcher ComboBox allowing instant preview switching between queued sources.
  - Added "Start Live Preview", "Stop Preview" buttons, live indicator pill, and error InfoBars.
  - Integrated [ViewModels/DashboardViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/DashboardViewModel.cs) with reactive visibility and error notifications.
- **Automated Unit Tests**:
  - Expanded test suite from 30 to 38 unit tests in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) covering preview commands, source switching, and coordinator failure transitions (100% pass rate).
- **Git Baseline Repository**:
  - Created `.gitignore` and established baseline commit (`651e1a1: chore: establish SwitchCast Phase 2 baseline`).

---

## [Phase 2] - 2026-10-08

### Added (feat / test / docs)
- **Native Window Discovery Engine**:
  - Implemented [IWindowDiscoveryService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IWindowDiscoveryService.cs) and [Win32WindowDiscoveryService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowDiscoveryService.cs) using `EnumWindows`, `IsWindowVisible`, `GetWindowTextW`, `DwmGetWindowAttribute` (`DWMWA_CLOAKED`), `GetWindowLongPtr` (`WS_EX_TOOLWINDOW`), and process name retrieval.
  - Excluded SwitchCast self-windows and empty title/system utility windows safely.
- **Native Monitor Discovery Engine**:
  - Implemented [IMonitorDiscoveryService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IMonitorDiscoveryService.cs) and [Win32MonitorDiscoveryService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32MonitorDiscoveryService.cs) using `EnumDisplayMonitors` and `GetMonitorInfo`.
  - Accurately captures monitor bounds, dimensions, and primary/secondary flags across arbitrary virtual desktop coordinates.
- **Presenter Selection & Reconciliation Architecture**:
  - Enhanced [PresentationStateService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationStateService.cs) with `IsSourceSelected`, `ToggleSourceSelection`, and `ReconcileAvailability(IReadOnlySet<string> activeDiscoveredIds)` to retain selections when sources are closed or disconnected.
- **Sources Management UI & Filtering**:
  - Upgraded [Views/SourcesPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SourcesPage.xaml) with dynamic category counters (`Application Windows (N)`, `Displays & Monitors (M)`), title/process real-time search box, manual refresh button, progress indicator, and queued presentation footer.
  - Added [SelectableSourceItem.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SelectableSourceItem.cs) with interactive checkbox bindings.
- **Dashboard Source Queue Integration**:
  - Bound [Views/DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml) to authoritative queued sources in [DashboardViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/DashboardViewModel.cs).
- **Automated Unit Tests**:
  - Expanded unit test suite from 19 to 30 tests in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) with 100% pass rate.

### Fixed (fix)
- Investigated Visual Studio SplitView XAML binding diagnostic and identified root cause in Windows App SDK 1.5 package (`Microsoft.WinUI\Themes\generic.xaml:35014`). Documented as upstream framework bug with zero runtime functional impact.
- Added `VCInstallPath` overrides in `Directory.Build.props` to ensure seamless .NET desktop builds in Visual Studio installations without C++ MSVC toolchains.

---

## [Phase 1] - 2026-10-08

### Added (feat / test / build)
- **WinUI 3 Modern Desktop Application Shell**:
  - Replaced legacy WinForms stub with .NET 8 WinUI 3 project targeting `net8.0-windows10.0.19041.0` with Windows App SDK 1.5.
  - Implemented [MainWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/MainWindow.xaml) with responsive `NavigationView` and theme application.
  - Added [app.manifest](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/app.manifest) with PerMonitorV2 DPI awareness.
- **Dependency Injection & MVVM**:
  - Configured DI container in [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs) using `Microsoft.Extensions.DependencyInjection`.
  - Implemented ViewModels using `CommunityToolkit.Mvvm`: [MainViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/MainViewModel.cs), [DashboardViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/DashboardViewModel.cs), [SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs), [SettingsViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SettingsViewModel.cs).
- **Core Domain & Presentation State Management**:
  - Added models: [PresentationStatus.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/PresentationStatus.cs), [SourceType.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/SourceType.cs), [CaptureSource.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/CaptureSource.cs), [WindowSource.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/WindowSource.cs), [MonitorSource.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/MonitorSource.cs), [UserSettings.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/UserSettings.cs).
  - Implemented [PresentationStateService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationStateService.cs) for centralized, thread-safe presentation state and source queuing.
- **Settings & Theme Infrastructure**:
  - Implemented [ApplicationSettingsService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/ApplicationSettingsService.cs) providing local JSON settings persistence under `%LOCALAPPDATA%\SwitchCast\settings.json`.
  - Added dynamic System / Light / Dark theme switching with immediate UI updating and restart persistence.
- **Fluent Desktop UI Views**:
  - [DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml): Presentation status cards, empty workspace banner, quick actions with phase tooltips.
  - [SourcesPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SourcesPage.xaml): Category selector tabs for Windows vs Displays with discovery empty state.
  - [SettingsPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SettingsPage.xaml): Theme selection, window dimension options, and runtime environment metadata diagnostics.
- **Automated Unit Tests**:
  - Created [SwitchCast.Tests.csproj](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/SwitchCast.Tests.csproj) containing 19 unit tests across 5 test classes verifying services, models, viewmodels, and persistence (100% pass rate).

---

## [Phase 0] - 2026-10-08

### Added (chore / docs)
- **Root Agent Rules**: Created [AGENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/AGENTS.md) defining the 12 mandatory rules and 10-step agent workflow.
- **Agent Operational Harness**:
  - Created [.agents/README.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/README.md)
  - Created [.agents/PROJECT_STATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/PROJECT_STATE.md)
  - Created [.agents/HANDOFF.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/HANDOFF.md)
  - Created [.agents/CHANGELOG.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/CHANGELOG.md)
  - Created [.agents/TASK_TEMPLATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/TASK_TEMPLATE.md)
- **Agent Skills**:
  - Created [switchcast-architect/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-architect/SKILL.md)
  - Created [switchcast-winui/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-winui/SKILL.md)
  - Created [switchcast-capture/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-capture/SKILL.md)
  - Created [switchcast-security/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-security/SKILL.md)
  - Created [switchcast-qa/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-qa/SKILL.md)
  - Created [switchcast-release/SKILL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/switchcast-release/SKILL.md)
- **Technical & Architecture Documentation**:
  - Created [docs/PRODUCT_REQUIREMENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/PRODUCT_REQUIREMENTS.md)
  - Created [docs/SYSTEM_ARCHITECTURE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/SYSTEM_ARCHITECTURE.md)
  - Created [docs/DEVELOPMENT_ROADMAP.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/DEVELOPMENT_ROADMAP.md)
  - Created [docs/CODING_STANDARDS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/CODING_STANDARDS.md)
  - Created [docs/TESTING_STRATEGY.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/TESTING_STRATEGY.md)
  - Created [docs/SECURITY_MODEL.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/SECURITY_MODEL.md)
  - Created [docs/decisions/ADR-0001-architecture.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/decisions/ADR-0001-architecture.md)
- **Static Analysis & Formatting**:
  - Created [.editorconfig](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.editorconfig) configured for .NET 8 / WinUI 3 conventions.
