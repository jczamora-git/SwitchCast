# SWITCHCAST — DEVELOPMENT ROADMAP

---

## ROADMAP OVERVIEW

This roadmap outlines the structured, phased development plan for SwitchCast. All AI agents must adhere to the sequence of phases and satisfy all acceptance criteria and quality gates before transitioning across phases.

---

## PHASE 0 — Architecture & AI Development Harness
- **Status**: **COMPLETED**
- **Objective**: Establish a strict, production-oriented AI development harness, complete system architecture specifications, coding standards, quality gates, and agent skills without modifying product code prematurely.
- **Scope**:
  - Comprehensive repository assessment.
  - Root [AGENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/AGENTS.md) and 12 core rules.
  - 6 domain agent skills under [.agents/skills/](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/skills/).
  - System architecture, PRD, coding standards, testing strategy, and security model docs.
  - Baseline [ADR-0001 Architecture](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/decisions/ADR-0001-architecture.md).
  - [.editorconfig](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.editorconfig) configuration.
- **Dependencies**: None.
- **Acceptance Criteria**:
  - [x] All harness documents and skills created with complete, non-placeholder content.
  - [x] Clear traceability from requirements to architectural components.
  - [x] No unauthorized changes to existing code.
- **Validation**:
  - File presence and cross-link validation.

---

## PHASE 1 — Core Application Foundation
- **Status**: **COMPLETED**
- **Objective**: Establish the modern WinUI 3 (Windows App SDK / .NET 8) desktop project structure, configure dependency injection, establish application state models, and build the basic Control Dashboard window shell.
- **Scope**:
  - Initialize WinUI 3 Desktop application targeting `net8.0-windows10.0.19041.0`.
  - Configure `Microsoft.Extensions.DependencyInjection` and `CommunityToolkit.Mvvm`.
  - Establish core domain entities (`CaptureSource`, `PresentationSessionState`).
  - Create the main `MainWindow.xaml` Control Dashboard window layout.
- **Dependencies**: Phase 0 completion.
- **Acceptance Criteria**:
  - [x] Application builds and launches cleanly displaying the Control Dashboard window.
  - [x] Clean Architecture folder boundaries established.
  - [x] Dependency injection container boots successfully.
- **Validation**: Level 1 (Build), Level 2 (Static Analysis), Level 3 (DI unit test).

---

## PHASE 2 — Window & Monitor Discovery
- **Status**: **COMPLETED (Ready for Phase 3)**
- **Objective**: Implement robust, native Win32 discovery of running application windows and connected physical/virtual monitors.
- **Scope**:
  - Implement `IWindowDiscoveryService` (`EnumWindows`, title/icon extraction, filtering).
  - Implement `IMonitorDiscoveryService` (`EnumDisplayMonitors`, bounds, DPI).
  - Bind discovery lists into `SourcesViewModel` and `DashboardViewModel` with real-time refresh.
  - Multi-source selection and stale-source reconciliation.
- **Dependencies**: Phase 1 foundation.
- **Acceptance Criteria**:
  - [x] Sources page lists all valid running desktop application windows.
  - [x] Sources page lists all connected monitors with accurate resolutions.
  - [x] Filtered windows (toolbars, invisible, minimized stubs, SwitchCast itself) excluded cleanly.
  - [x] Multi-source selection survives navigation.
  - [x] Disconnected displays / closed windows handled safely with caution badges.
- **Validation**: 30 automated unit tests; Level 1 compilation clean (0 warnings, 0 errors).

---

## PHASE 3 — Capture Engine
- **Status**: **COMPLETED (Ready for Phase 4)**
- **Objective**: Implement the hardware-accelerated screen capture pipeline using `Windows.Graphics.Capture` and Direct3D 11.
- **Scope**:
  - Interop bridge for `IGraphicsCaptureItemInterop` with process re-validation.
  - `Direct3D11CaptureFramePool` management and `FrameArrived` handler with dynamic surface resizing.
  - Deterministic frame disposal and resource lifecycle management.
  - Hardware Direct3D 11 device provider with WARP fallback.
  - WinUI 3 dashboard live preview renderer with aspect-ratio preservation and source switcher.
- **Dependencies**: Phase 2 source discovery.
- **Acceptance Criteria**:
  - [x] Successfully captures raw frames from selected `HWND` or `HMONITOR`.
  - [x] Zero memory leaks across continuous frame acquisition via deterministic disposal.
  - [x] Clean shutdown and source switching of capture sessions.
  - [x] Live preview surface integrated in presenter dashboard.
- **Validation**: 38 automated unit tests; Level 1 compilation clean (0 warnings, 0 errors).

---

## PHASE 4 — Presentation Output Window
- **Status**: **COMPLETED (Ready for Phase 5)**
- **Objective**: Create the dedicated, shareable Presentation Output Window hosting live video frames, standby screen, pause freeze-frame retention, and blackout overlay.
- **Scope**:
  - `PresentationWindow.xaml` and `PresentationViewModel.cs` providing a native shareable WinUI 3 Window titled `"SwitchCast Presentation Output"`.
  - Aspect-ratio letterboxing / pillarboxing live video canvas (`Stretch="Uniform"`).
  - Fail-closed fallback standby screen ("Ready to Present") when capture is stopped or inactive.
  - Paused indicator pill and 100% opaque solid blackout overlay.
  - Single-instance `PresentationWindowService` and authoritative `PresentationCoordinator`.
  - Unified frame delivery architecture distributing capture frames from a single capture engine session to both local preview and presentation output surfaces.
  - Dashboard presentation control strip (Start/Stop Presenting, Pause/Resume, Blackout, target source selector).
- **Dependencies**: Phase 3 capture engine.
- **Acceptance Criteria**:
  - [x] Presentation Output Window opens independently of the Control Dashboard with stable HWND.
  - [x] Output window remains capturable by Google Meet, Zoom, and Teams (no `WDA_EXCLUDEFROMCAPTURE`).
  - [x] Source switching preserves the open presentation window without requiring re-sharing.
  - [x] Freeze/Pause, Resume, and Blackout states function reliably.
  - [x] 0 build warnings or errors across the entire solution.
- **Validation**: 57 automated unit tests; Level 1 compilation clean (0 warnings, 0 errors).

---

## PHASE 5 — Global Hotkeys & Floating Presenter Dock
- **Status**: **COMPLETED (Ready for Phase 6)**
- **Objective**: Implement instant source switching, global keyboard shortcuts, compact floating presenter companion dock, and persistent settings.
- **Scope**:
  - `Win32HotkeyService` (`RegisterHotKey`) with configurable hotkey bindings and message listener window.
  - Floating companion dock window `PresenterDockWindow.xaml` and `PresenterDockViewModel.cs` with always-on-top, compact mode, and quick source switcher.
  - Single-instance `PresenterDockService` with automatic opening on presentation start.
  - Quick sequential cycling (`SwitchToNextSourceAsync`, `SwitchToPreviousSourceAsync`) and direct slot switching.
  - Three strongly typed source switching modes (`ActiveAndLive`, `ActiveOnly`, `LiveOnly`) with native window activation focus management (`IWindowActivationService`).
  - Minimal single-row floating presenter dock in both Expanded (660×52 DIP) and Compact (460×46 DIP) modes with strict single-line ellipsis truncation.
  - Settings UI in `SettingsPage.xaml` and `SettingsViewModel.cs` for hotkeys and dock preferences.
- **Dependencies**: Phase 4 presentation output.
- **Acceptance Criteria**:
  - [x] Switching between queued sources occurs instantly with Latest-Request-Wins and zero session recreation.
  - [x] Global hotkeys work reliably system-wide even when SwitchCast is not focused.
  - [x] Minimal single-row floating presenter companion dock allows full presenter control without focusing the dashboard.
  - [x] Three switching modes (Active + Live, Active Only, Live Only) operate seamlessly across dock controls and global hotkeys.
  - [x] 100% offline, zero telemetry, local persistence in `settings.json`.
- **Validation**: 122 automated unit and concurrency tests; Level 1 compilation clean (0 warnings, 0 errors).

---

## PHASE 6 — Direct Media Sources & Settings UI Refinement
- **Status**: **COMPLETED (Ready for Phase 7)**
- **Objective**: Implement Windows 11 Settings-style compact theme dropdown, correct Appearance grid layout geometry, and add local image and video files as native, first-class presentation sources without spawning external applications.
- **Scope**:
  - Windows 11 Settings-style `ComboBox` theme selector with immediate application and settings persistence.
  - Appearance card layout fix (two-column flexible star layout with ample description width).
  - First-class `ImageMediaSource` and `VideoMediaSource` domain models with formatted metadata (dimensions, durations, file sizes).
  - Native WinUI 3 desktop file picker (`Win32MediaPickerService`) and asynchronous metadata discovery (`MediaDiscoveryService`).
  - Media presentation service (`MediaPresentationService`) managing static `BitmapImage` decoding and native `Windows.Media.Playback.MediaPlayer` (muted by default).
  - Mutually exclusive presentation output layers (`PresentationWindow`) with stable HWND preservation.
  - Sources page "Media Files" category with Add Media import, queue controls, item removal, and settings persistence.
  - Essential dashboard video playback controls (Play, Pause, Resume, Restart, Loop toggle, timecode progress).
  - Privacy guarantees: Video pause on Blackout/Pause, zero media rendering or audio leaks during blackout.
- **Dependencies**: Phase 5 presenter controls.
- **Acceptance Criteria**:
  - [x] Theme dropdown replaces RadioButtons with right-aligned Windows 11 styling and ample text space.
  - [x] Direct image and video presentation inside Presentation Output without opening external software.
  - [x] Seamless mixed queue switching across Window, Display, Image, and Video sources.
  - [x] Zero audio leaks / virtual audio drivers (muted by default as per Phase 6 scope).
  - [x] 100% offline, zero telemetry, local persistence in `settings.json`.
- **Validation**: 209 automated unit and regression tests; Level 1 compilation clean (0 warnings, 0 errors).

---

## PHASE 6.3 — Fullscreen Presentation Output
- **Status**: **COMPLETED (Ready for Phase 7)**
- **Objective**: Implement a true fullscreen presentation mode for the audience-facing Presentation Output window, controlled directly from the Floating Presenter Dock.
- **Scope**:
  - `PresentationDisplayMode` enum (`Windowed`, `Fullscreen`).
  - Native Windows App SDK `AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen)` without window recreation.
  - Complete collapsing and hiding of custom XAML title bar in fullscreen mode so audience content fills 100% of the active display.
  - Same HWND, DirectX 11 capture pipeline, and media/audio playback preservation across mode switches.
  - Floating Presenter Dock toolbar fullscreen icon toggle button (`\uE740` Enter / `\uE73F` Exit) and More Options popup integration.
  - Multi-monitor preservation and previous normal window placement restoration.
  - Focused keyboard escape recovery and window close lifecycle integration.
- **Dependencies**: Phase 6.2 media playback.
- **Acceptance Criteria**:
  - [x] Presentation Output fills target monitor with zero borders, title bar, or taskbar overlay.
  - [x] Exact same HWND and active capture/media session maintained.
  - [x] Floating Presenter Dock provides seamless one-click enter/exit toggle.
  - [x] Restores previous size, position, and title bar when exiting fullscreen.
- **Validation**: 247 automated unit and regression tests; Level 1 compilation clean (0 warnings, 0 errors).

---

## PHASE 7 — Advanced Presenter Features
- **Status**: **PLANNED**
- **Objective**: Implement enhanced presenter capabilities (live dashboard preview thumbnails, smooth transitions, saved application profiles).
- **Scope**:
  - Live thumbnail preview rendering on dashboard cards.
  - Configurable cross-fade and cut transitions.
  - Presentation profile saving/loading to local configuration.
- **Dependencies**: Phase 6 stability.
- **Acceptance Criteria**:
  - Previews render at lightweight frame rates without impacting primary output performance.
  - Profiles accurately restore queued application setups.

---

## PHASE 8 — Packaging, Signing & Release
- **Status**: **COMPLETED (v1.2.0 Release)**
- **Objective**: Prepare reproducible release builds, self-contained unpackaged Windows desktop packaging, application manifests, and distribution readiness.
- **Scope**:
  - Self-contained Windows App SDK x64 deployment (`<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`).
  - Standalone portable distribution archive (`SwitchCast-v1.2.0-win-x64.zip`) with SHA-256 verification.
  - Release readiness verification and user distribution documentation.
- **Dependencies**: Phase 6 stability & hotfix completion.
- **Acceptance Criteria**:
  - [x] Clean execution on vanilla Windows 10/11 machines without requiring separate .NET runtime installation.
  - [x] Zero external prerequisites required beyond standard Windows Desktop runtime.
  - [x] Automated tests and build verification passing 100%.
