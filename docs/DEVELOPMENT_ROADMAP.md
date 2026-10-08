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
  - Settings UI in `SettingsPage.xaml` and `SettingsViewModel.cs` for hotkeys and dock preferences.
- **Dependencies**: Phase 4 presentation output.
- **Acceptance Criteria**:
  - [x] Switching between queued sources occurs instantly with Latest-Request-Wins and zero session recreation.
  - [x] Global hotkeys work reliably system-wide even when SwitchCast is not focused.
  - [x] Compact floating presenter companion dock allows full presenter control without focusing the dashboard.
  - [x] 100% offline, zero telemetry, local persistence in `settings.json`.
- **Validation**: 109 automated unit and concurrency tests; Level 1 compilation clean (0 warnings, 0 errors).

---

## PHASE 6 — Stability, Performance & Multi-DPI Optimization
- **Status**: **PLANNED**
- **Objective**: Harden the entire application against edge cases, multi-monitor DPI scaling changes, GPU device reset, and long-running memory usage.
- **Scope**:
  - Handle window resizing, minimization, and unexpected process termination of captured targets.
  - Handle monitor disconnection and display topology changes (`WM_DISPLAYCHANGE`).
  - Eliminate all heap allocations in hot frame paths.
- **Dependencies**: Phase 5 switching system.
- **Acceptance Criteria**:
  - Zero crashes when target windows close abruptly.
  - Consistent memory footprint over 2+ hours of active presentation.
  - Seamless handling of GPU sleep/resume and display disconnects.
- **Validation**: Long-running soak tests; simulated device removal tests.

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
- **Status**: **PLANNED**
- **Objective**: Prepare reproducible release builds, MSIX packaging, application manifests, and distribution readiness.
- **Scope**:
  - MSIX package manifest configuration (`Package.appxmanifest`).
  - Unpackaged executable build pipeline verification.
  - Release readiness verification and user distribution documentation.
- **Dependencies**: Phase 7 completion.
- **Acceptance Criteria**:
  - Clean installation, execution, and uninstallation on vanilla Windows 10/11 machines.
  - Zero external prerequisites required beyond standard Windows Desktop runtime.
