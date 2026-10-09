# SWITCHCAST — PROJECT STATE

This is the authoritative progress, state, and environmental tracking document for SwitchCast. All AI agents must consult and update this file before and after executing tasks.

---

## 1. EXECUTIVE SUMMARY

- **Project**: SwitchCast
- **Current Phase**: Media Source Activation Behavior Fix (Active + Live & Active Only Activate Presentation Output)
- **Overall Status**: **Implemented & Verified**
- **Last Updated**: 2026-10-10T05:40:00+08:00 (UTC+8)

---

## 2. FUNCTIONALITY STATUS

### Implemented & Verified
- [x] **Media Source Activation Behavior Fix (Active + Live & Active Only Activate Presentation Output)**:
  - **Active + Live for Media**:
    - When an Image or Video media source is switched live in `ActiveAndLive` mode, SwitchCast starts rendering the image or playing the video and activates the existing `PresentationWindow` (restoring if minimized and bringing to the foreground via `IWindowActivationService.ActivateWindow`).
    - Does NOT launch external media applications (e.g. VLC or Photos). Reuses existing output window without recreation or recentering, preserving fullscreen/windowed geometry.
  - **Active Only for Media**:
    - When an Image or Video media source is navigated to in `ActiveOnly` mode, advances the selection cursor (`SetSelectedSource`), resolves `PresentationWindow`, restores if minimized, and brings it to the foreground.
    - Crucially preserves On-Air continuity: does NOT change `ActiveSource`, does NOT start media playback, does NOT seek or replace audience-facing frames. If closed, opens in Standby without taking media live.
  - **Live Only for Media**:
    - Preserved existing behavior: takes media live without requesting foreground focus or stealing focus from current active applications.
  - **Window and Monitor Source Semantics Preserved**:
    - Application window activation (`ActivateSource`) and monitor capture behavior remain completely untouched.
  - **Floating Dock Focus Handoff**:
    - Popup dropdown menu (`PresenterDockMenuWindow`) is closed and detached prior to executing the source switch command, preventing dropdown dismissal from reclaiming foreground focus away from the newly activated `PresentationWindow`.
  - **Native Application Icon Integration**:
    - Embedded multi-resolution `Assets/SwitchCast.ico` (16, 24, 32, 48, 64, 128, 256) generated from high-resolution `Assets/SwitchCast_1.png`.
    - Configured project deployment in `SwitchCast.csproj` (`<ApplicationIcon>Assets\SwitchCast.ico</ApplicationIcon>`, `<Content Include="Assets\SwitchCast.ico">`, and `<Content Include="Assets\SwitchCast_1.png">`).
    - Added automated branding unit tests in `ApplicationBrandingTests.cs` verifying multi-resolution ICO header, resolutions (16, 24, 32, 48, 64, 128, 256), file existence of `SwitchCast.ico` and `SwitchCast_1.png`, and csproj build configurations.
  - **Automated Regression Tests**:
    - Expanded test suite to **281 automated unit tests** (100% pass rate).
- [x] **Dashboard Start/Stop Presenting Dynamic Icon Correction**:
  - Replaced hardcoded play glyph `&#xE768;` in `DashboardPage.xaml` with compiled binding `{x:Bind ViewModel.PresentationButtonGlyph, Mode=OneWay}`.
  - Bound glyph dynamically in `DashboardViewModel.cs` to authoritative presentation state: `PresentationButtonGlyph => HasActivePresentation ? "\uE71A" : "\uE768"`.
  - Notifies on presentation status changes (`Idle`, `Starting`, `Active`, `Paused`, `Blackout`, `Stopping`, `Stopped`), ensuring label ("Start Presenting" / "Stop Presenting") and icon (Play `\uE768` / Stop square `\uE71A`) update in strict synchronization while preserving coral styling, typography, corner radii, and command bindings.
- [x] **Floating Presenter Dock X Dismissal -> Control Dashboard Activation**:
  - Clicking the X button on the Floating Presenter Dock (`CloseDockCommand`) now dismisses the dock and restores/activates the existing `MainWindow` Control Dashboard.
  - Reuses existing `IWindowActivationService` (`ActivateMainWindow()`) to restore `MainWindow` if minimized and bring to foreground, and navigates to Dashboard via `INavigationService.NavigateToDashboard()`.
  - **Presentation Continuity**: Live presentation, Direct3D 11 capture, video playback, and `PresentationWindow` output remain completely active and untouched when dock is closed.
  - **Application Exit Safety**: Guarded with `IApplicationLifecycleService.IsShuttingDown` and `IsShutdownApproved` in both `PresenterDockViewModel.CloseDock()` and `App.ActivateMainWindow()`, preventing dashboard reactivation during application shutdown.
  - **Hotkey Distinction**: Global hotkey `Ctrl+Shift+D` (`ToggleDock`) continues normal toggle semantics without unwanted dashboard focus shifts.
- [x] **Floating Presenter Dock Stability & Double-Click Maximize Prevention**:
  - **Confirmed Root Cause**: WinUI 3 `InputNonClientPointerSource` non-client caption regions (`HTCAPTION`) passed `WM_NCLBUTTONDBLCLK` to default window procedure, which interpreted double-clicks as caption double-click maximize commands despite `OverlappedPresenter.IsMaximizable = false`.
  - **Native Window Subclassing**: Subclassed dock HWND via `comctl32.dll` (`SetWindowSubclass`). Handled `WM_NCLBUTTONDBLCLK` on `HTCAPTION` (returns `IntPtr.Zero`), intercepted `WM_SYSCOMMAND` `SC_MAXIMIZE`, clamped `WM_GETMINMAXINFO` tracking bounds, stripped `WS_MAXIMIZEBOX` and `WS_THICKFRAME` from `GWL_STYLE`, removed `SC_MAXIMIZE` from system menu, and hooked `AppWindow.Changed` auto-restoration.
  - **Preserved Presentation Fullscreen**: The dock fullscreen button continues toggling fullscreen on `PresentationWindow` without dock maximization or window recreation.
- [x] **Next/Previous Mixed Source Navigation Fix**:
  - **Confirmed Root Cause**:
    1. `CaptureCoordinator.StopPreviewInternalAsync()` called `_presentationStateService.SetActiveSource(null)`, causing `ActiveSource` to drop to null midway through Window -> Media transitions and triggering `DashboardViewModel` to fire a redundant switch that canceled the transition as superseded.
    2. `PresentationCoordinator.SwitchToNextSourceAsync()` previously filtered by `.Where(s => s.IsAvailable)` instead of the authoritative queue `_presentationStateService.SelectedSources`, causing index divergence from the dock dropdown.
    3. `ActiveAndLive` mode attempted native HWND window activation on media sources (images/videos), which failed and blocked transition.
  - **Authoritative Single Ordered Queue**: Both Next and Previous traverse `_presentationStateService.SelectedSources` directly across all source types (Window, Monitor, Image, Video) in exact dropdown order.
  - **Switching Mode Semantics**: Media sources are taken live directly without window activation in `ActiveAndLive` mode. Paused and blackout states are respected upon video transitions.
- [x] **Presenter Dock Direct Source Unqueue**:
  - **Interactive Checkbox Toggle**: Replaced static glyph in `PresenterDockMenuWindow` with an interactive `CheckBox` control with tooltip "Remove from presentation queue".
  - **Popup Persistence**: Menu stays open upon unqueue, updates item count, shrinks window dimensions, and switches to empty state when queue count reaches 0.
  - **On-Air Continuity Policy**: Unqueueing an On-Air source removes it from the future navigation queue while preserving active presentation output until the presenter explicitly switches or stops.
  - **Deterministic Cursor Recalculation**: If the unqueued item was selected, cursor advances to the next queued item (or previous if removing final item; or null if empty).
  - **UI Synchronization**: Dock dropdown, Dashboard source selector, and Sources tab checkboxes stay synchronized immediately.
- [x] **Presenter Dock Playback Progress Cross-Thread COMException Permanent Hotfix (0x8001010E)**:
  - **Confirmed Root Cause**:
    - Previous attempt retained a `System.Threading.Timer` fallback (`_playbackProgressTimer`) when dynamic reflection or delegate casting failed.
    - `RunOnUIThread` contained an unsafe fallback: when `TryEnqueue` was unavailable, failed, or threw, it executed `action()` directly on the calling ThreadPool thread.
    - Firing `OnPropertyChanged(nameof(VideoPositionSeconds))` from the thread pool forced compiled WinUI 3 XAML bindings (`RangeBase.set_Value`) in `PresenterDockWindow` to execute off the owning UI thread, throwing WinRT `COMException` `0x8001010E` (`RPC_E_WRONG_THREAD`).
  - **Authoritative Single UI-Owned DispatcherQueueTimer**:
    - Completely eliminated `System.Threading.Timer`, `_playbackProgressTimer`, and `OnFallbackTimerTick`.
    - Strongly typed `Microsoft.UI.Dispatching.DispatcherQueue` and `Microsoft.UI.Dispatching.DispatcherQueueTimer` in `PresenterDockViewModel`.
    - `PresenterDockWindow` passes its UI thread `DispatcherQueue` directly to `PresenterDockViewModel.SetDispatcherQueue(DispatcherQueue)`.
    - A single repeating `DispatcherQueueTimer` (250ms interval) executes exclusively on the UI thread to update `VideoPositionSeconds` and `VideoPositionText`.
  - **Unsafe Fallback Execution Eliminated**:
    - Refactored `RunOnUIThread(Action)`: if called off-thread, invokes `dispatcher.TryEnqueue(callback)`. If `TryEnqueue` fails (returns `false`), the update is safely dropped and NEVER executed synchronously on the worker thread.
  - **Scrubbing & Slider Seek Loop Protection**:
    - While user is actively dragging the slider (`_isScrubbing = true`), automatic timer updates to `VideoPositionSeconds` are suppressed to prevent slider jitter.
    - User commit via `CompleteScrubbing` seeks to the clamped target position once.
  - **Clean Timer & ViewModel Lifecycle**:
    - `PresenterDockWindow.Closed` unhooks `PropertyChanged` and calls `ViewModel.Dispose()`.
    - `Dispose()` stops the `DispatcherQueueTimer`, detaches event handlers, unhooks coordinator and service events, and sets `_disposed = true`.
  - **Automated Unit Tests**:
    - Expanded test suite to **255 automated tests** (100% pass rate) validating UI dispatcher ownership, timer ticks, scrubbing state, off-thread event marshaling, and TryEnqueue failure safety.
- [x] **Main Dashboard Source Selector Empty-State UX Fix**:
  - **Informative Empty State Surface**:
    - Replaced blank gray selector box with an informative `DropDownButton` and styled Flyout matching the Floating Presenter Dock's empty state.
    - Displayed "No queued sources", `\uE7F4` icon, descriptive guidance ("Add application windows, displays, images, or videos from the Sources tab."), and a primary coral action button `+ Add Presentation Source`.
  - **Dashboard "+ Add Source" Button**:
    - Added a visible compact `+ Add Source` button to the primary presentation control bar that navigates directly to `SourcesPage` via `INavigationService`.
    - Automatically updates `NavigationView` sidebar selection to highlight the Sources tab.
  - **Start Presenting Validation & Tooltip**:
    - Bound `IsEnabled` to `CanStartPresentation` (`false` when 0 sources are queued and presentation is not active).
    - Added dynamic tooltip: "Add a presentation source first." when empty vs "Start Live Presentation" / "Stop Live Presentation".
  - **Preview Workspace Text Wrapping Fix**:
    - Fixed clipping on descriptive text block in `EmptyWorkspacePanel` with `TextWrapping="Wrap"` and `MaxWidth="420"`.
    - Renamed action button to "Add Presentation Sources" with shared navigation command.
  - **Automated Unit Tests**:
    - Expanded test suite to **250 automated tests** (100% pass rate) validating empty state properties, placeholder text, enablement rules, and queue count transitions (0 -> 1 -> 0).
- [x] **Floating Presenter Dock Finalization**:
  - **Native Windows Caption Dragging**:
    - Replaced pointer routing and manual `SendMessage(WM_NCLBUTTONDOWN)` implementation with genuine Windows App SDK `InputNonClientPointerSource` non-client caption regions.
    - Set the dock surface as `NonClientRegionKind.Caption` while setting interactive controls (buttons, dropdown triggers, sliders) as `NonClientRegionKind.Passthrough`.
    - Completely resolved sticky dragging / click-to-move anomaly; window drag initiates exclusively while holding left mouse button and terminates instantly on release.
  - **Permanent Unified Dock Layout**:
    - Removed compact vs expanded mode switching, obsolete toggle buttons, and legacy settings UI rows.
    - Baseline width fixed to 680 DIPs single row with standard presenter controls (Live status badge, Previous/Next source navigation, Active source dropdown, Mode selector, Mute, Pause/Resume, Blackout, Stop Presenting, Fullscreen output toggle, More Options menu, Close dock).
  - **Adaptive Video Playback & Timeline Controls**:
    - When a video source is On Air, the dock automatically reveals a second video transport row (height adapts from 52 DIPs to 86 DIPs).
    - Video controls include: Restart video (`00:00`), Seek backward 10s (`\uEB9E`), Play/Pause video (`\uE768`/`\uE769`), Seek forward 10s (`\uEB9D`), duration scrubber slider with smooth pause-during-scrubbing commitment, and live timecode text (`00:00 / 00:00`).
    - Non-video sources (Window, Display, Image) cleanly hide the video row and shrink dock height to 52 DIPs with dynamic hit-test region recalculation.
- [x] **Fullscreen Presentation Output (Phase 6.3)**:
  - **Native AppWindow Fullscreen Presenter**:
    - Leverages supported Windows App SDK `AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen)` for true borderless fullscreen on the active monitor without custom window recreation hacks.
    - Transitions smoothly between `PresentationDisplayMode.Windowed` and `PresentationDisplayMode.Fullscreen`.
  - **Same HWND Preservation & Share Continuity**:
    - Preserves the EXACT same `PresentationWindow` instance, native Win32 HWND, active capture/media session, and playback state across all fullscreen and windowed mode transitions.
    - Zero window recreation, zero capture session recreation, and zero audio stream interruption.
  - **Custom Title Bar & Viewport Geometry**:
    - Completely collapses and hides custom XAML title bar (`TitleBarRow.Height = 0` / `AppTitleBar.Visibility = Collapsed` / `SetTitleBar(null)`) in fullscreen mode so audience content fills 100% of the viewport with aspect-ratio preservation.
    - Restores custom title bar height (38px) and default window presenter upon exiting fullscreen.
  - **Floating Presenter Dock Control Surface**:
    - Added dedicated fullscreen icon toggle (`\uE740` Enter / `\uE73F` Exit) in Expanded dock mode and inside `PresenterDockMenuWindow` More Options popup.
    - Dynamic tooltip and state synchronization linked directly to `IPresentationWindowService.DisplayMode`.
    - Auto-disables toggle button when `PresentationWindow` is closed.
  - **Fallback Escape Key Recovery**:
    - Safe `Escape` key handler in `PresentationWindow` to exit fullscreen when output window has keyboard focus, without interfering with system-wide hotkeys.
  - **Lifecycle Integration & Multi-Monitor**:
    - Restores previous normal window size, coordinates, and monitor placement when exiting fullscreen.
    - Stop Presenting and window close events safely reset display mode state to Windowed.
- [x] **Media Audio Playback & Synchronization (Phase 6.2)**:
  - **Audible Video Playback**: Fixed silent video playback by removing hardcoded `IsMuted = true` and wiring native `Windows.Media.Playback.MediaPlayer` audio channels to default Windows audio endpoints.
  - **Single Authoritative Playback Session**: Synchronized single-session media playback governed by `MediaPresentationService` where audio and video seek, pause, resume, and loop as a single atomic stream.
  - **Mute & Volume Controls**:
    - Normalized volume range `[0.0, 1.0]` internally with 0%..100% display and two-way slider bindings.
    - Dashboard Video Controls Bar: Integrated speaker mute toggle button (`\uE74F` / `\uE767`), volume slider, and percentage text indicator.
    - Floating Presenter Dock: Compact mute button (`\uE74F` / `\uE767`) directly on the single-row dock toolbar (shown only when video is on air) and full volume slider + mute toggle inside the `PresenterDockMenuWindow` More Options popup without expanding dock dimensions.
  - **Playback Lifecycle & Privacy Integration**:
    - Video Play: Plays audio in accordance with volume and mute settings.
    - Video Pause: Pauses audio and video in sync.
    - Video Resume: Resumes audio and video together.
    - Video Restart: Resets playback position to 00:00 for both audio and video.
    - Presentation Blackout: Suspends video rendering and suppresses audio immediately. Restoring blackout resumes prior playback.
    - Stop Presenting: Safely stops and disposes active media player, unloads resources, closes Presentation Output, and foregrounds Control Dashboard.
    - Source Switching Safety: Instant audio termination when switching between `Video -> Image`, `Video -> Window`, and `Video -> Video` (no dual audio streams or orphaned background audio).
  - **Settings Persistence**: Persists `MediaVolume` and `IsMediaMuted` across application restarts using `IApplicationSettingsService`.
  - **Meeting Audio Guidance**: Documented separation between local playback and conferencing meeting transmission (e.g., Google Meet "Also share system audio").
- [x] **Complete Media Presentation Integration (Phase 6.1)**:
  - Full end-to-end presentation pipeline verified across all 4 source types (`WindowSource`, `MonitorSource`, `ImageMediaSource`, `VideoMediaSource`).
  - **Unified Presentation Queue**: Single authoritative queue managed by `IPresentationStateService` containing both desktop capture sources and file-backed media sources with stable type-safe identities.
  - **Dashboard Source Picker & Controls**: Target source picker dropdown, queued sources strip, and video playback controls bar (Play/Pause, Restart, Loop toggle, mm:ss timecode) dynamically displayed when video is on air.
  - **Floating Presenter Dock & Popups**: Seamless integration in both Expanded and Compact dock modes. Source button dynamically binds to `ActiveSourceGlyph` (`\uEB9F` for Image, `\uE714` for Video, `\uE7F4` for Window, `\uE790` for Monitor) and `ActiveSourceTitle`. `PresenterDockMenuWindow` ListView displays correct category labels and glyphs.
  - **Global Hotkey Navigation**: `NextSource` (`Ctrl+Shift+Right`), `PreviousSource` (`Ctrl+Shift+Left`), and direct index hotkeys (`Ctrl+Shift+1..5`) cycle through mixed queues (Window -> Image -> Video -> Monitor) with zero crashes or race conditions.
  - **Presentation Coordinator Routing**: `PresentationCoordinator` routes media files cleanly to `MediaPresentationService` without sending file paths to `Windows.Graphics.Capture`.
  - **Presentation Output Window (Single Stable HWND)**: Mutually exclusive visual layers in `PresentationWindow` (Screen Capture `Image`, Direct Static `Image` with `Uniform` centering, `MediaPlayerElement` bound to `MediaPlayer`, Standby canvas, and topmost Blackout overlay).
  - **Three Switching Modes with Media**:
    - `Active + Live`: Takes media live immediately. Window activation gracefully skips file-backed sources without errors or focusing unrelated windows.
    - `Live Only`: Takes media live without changing application window focus.
    - `Active Only`: Updates selected queue cursor only without changing On-Air presentation output.
  - **Transition & Resource Safety**: Stop/unload of obsolete video players and capture sessions during rapid source switching. Stable Presentation HWND preserved across all transitions.
  - **Fail-Closed & Media Error Handling**: Non-existent, corrupted, or unsupported media files cleanly set error state on presenter dashboard/dock without crashing or interrupting presentation output.
- [x] **Floating Presenter Dock Native Dragging Hotfix**:
  - Implemented routed pointer event handlers (`AddHandler` with `handledEventsToo: true`) on `DockCardBorder` in [Views/PresenterDockWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml.cs).
  - Instant native dragging (`ReleaseCapture` + `WM_NCLBUTTONDOWN` / `HTCAPTION`) when pressing on non-interactive surfaces.
  - Smooth click-versus-drag detection on interactive controls using physical pixel movement threshold (5px / `DragThresholdSquared = 25`).
- [x] **Global Text Truncation & Dropdown Width Constraints**:
  - Fixed horizontal expansion and clipping on Dashboard source selectors, Status Strip Active Source, Sources Page list rows, and Floating Presenter Dock active source buttons.
- [x] **Unified Stop Presenting Workflow & Automatic Dashboard Activation**:
  - Authoritative Stop sequence in `PresentationCoordinator.StopPresentationAsync()` safely terminates active capture/media, sets status to `Idle`, closes `PresentationWindow`, restores `MainWindow`, and brings Control Dashboard to foreground.
- [x] **Windows 11 Settings-Style Theme Selector & Appearance Layout (Phase 6)**:
  - Compact Windows Settings-style theme `ComboBox` with immediate theme application and persistence.
- [x] **Automated Unit & Regression Test Suite**:
  - **239 comprehensive automated unit and regression tests** in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) with 100% pass rate.

### Planned (Upcoming)
- [ ] **Phase 7**: Advanced Presenter Features (Live thumbnail previews, smooth transitions).
- [ ] **Phase 8**: Packaging and Distribution (MSIX store packaging option).

---

## 3. INVESTIGATION & KNOWN ISSUES

1. **Conferencing Audio Transmission**: Local media audio playback is distinct from meeting audio sharing. In applications like Google Meet, presenters must enable "Also share system audio" when selecting the SwitchCast Presentation Output window.
2. **Minimized Window OS Policy**: As per standard Windows Graphics Capture design, minimized application windows do not produce new Direct3D frames until restored.

---

## 4. ARCHITECTURE DECISION RECORDS

- [ADR-0001: Technology Stack & Clean Architecture Core](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/decisions/ADR-0001-architecture.md) — Implemented across all phases.

---

## 5. DETECTED ENVIRONMENT & TOOLING

- **Host OS**: Windows 11 Pro (OS Build 10.0.22631, win-x64)
- **.NET SDK**: 8.0.403 (`C:\Program Files\dotnet\sdk\8.0.403\`)
- **Target Framework**: `net8.0-windows10.0.19041.0`
- **Architecture**: `x64` (`win-x64`)
- **Windows App SDK**: 1.5.240802000

---

## 6. VERIFICATION RECORD

- **Level 1 (Compilation)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors).
- **Level 2 (Static Analysis)**: Nullable reference checks and analyzer validation -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (263 passed, 0 failed, 0 skipped).

---

## 7. NEXT RECOMMENDED TASK

**Task**: **Phase 7 — Advanced Presenter Features & Smoothing**
- **Objective**: Live thumbnail preview rendering on dashboard cards, configurable smooth transitions, and presentation profiles.
