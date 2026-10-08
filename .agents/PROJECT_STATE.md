# SWITCHCAST — PROJECT STATE

This is the authoritative progress, state, and environmental tracking document for SwitchCast. All AI agents must consult and update this file before and after executing tasks.

---

## 1. EXECUTIVE SUMMARY

- **Project**: SwitchCast
- **Current Phase**: Desktop UX Hotfix (Presenter Actions Bring-to-Front, Centered Startup, Dynamic Application Icons)
- **Overall Status**: **Completed (Ready for Phase 6)**
- **Last Updated**: 2026-10-09T03:00:00+08:00 (UTC+8)

---

## 2. FUNCTIONALITY STATUS

### Implemented & Verified
- [x] **Strict AI Development Harness (Phase 0)**: Standardized rules ([AGENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/AGENTS.md)), 6 domain skills, architecture specifications, coding standards, and [.editorconfig](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.editorconfig).
- [x] **WinUI 3 Desktop Application Shell (Phase 1 & UI Refinement)**:
  - Custom integrated application top title bar (`ExtendsContentIntoTitleBar = true`, `SetTitleBar(AppTitleBar)`), replacing the disconnected white title bar with a seamless, theme-aware header.
  - Native caption buttons styled dynamically via `AppWindow.TitleBar` (transparent backgrounds, theme-adaptive foreground and hover states, double-click to maximize, Windows 11 Snap Layouts).
  - Modern, near-black dark theme design system (`App.xaml`) with centralized semantic brush tokens (`AppBackgroundBrush`, `AppSidebarBrush`, `AppSurfaceBrush`, `AppSurfaceElevatedBrush`, `AppHoverBrush`, `AppBorderBrush`, `AppAccentBrush` coral `#FF7A59`, `AppBadgeBackgroundBrush`, `AppPreviewCanvasBrush`) and complete light theme fidelity.
  - Compact, responsive left navigation sidebar integrated smoothly with the shell.
- [x] **Presenter Actions Bring-to-Front & Window Focus Handoff (Hotfix)**:
  - Fixed Presenter Actions ("Control Dashboard" and "Presentation Output") in `PresenterDockMenuWindow` by detaching owner HWND before closing, preventing Windows from reclaiming focus to the dock window.
  - `ActivateMainWindow()` in `App.xaml.cs` and `IWindowActivationService` restores minimized windows (`ShowWindowAsync(SW_RESTORE)`) and brings them to the foreground without recreating `MainWindow`.
  - `PresentationWindowService` integrates `IWindowActivationService` to restore and activate existing `PresentationWindow` instances without tearing down or recreating the output window or capture stream.
- [x] **Centered MainWindow Startup & DPI-Aware Saved Placement (Hotfix)**:
  - Calculates target monitor usable work area center coordinates on cold/first launch (`centerX = workArea.Left + (workArea.Width - windowWidth) / 2`, `centerY = workArea.Top + (workArea.Height - windowHeight) / 2`).
  - Supports multi-monitor setups with negative coordinates, differing DPI scales, and taskbar offsets.
  - Restores valid saved positions with work-area bounding and recovers safely to screen center if a monitor is disconnected.
- [x] **Dynamic Native Application Icons Engine (Hotfix)**:
  - [IWindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IWindowIconService.cs) & [Win32WindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowIconService.cs) extracting native icons via `SendMessageTimeout` (`WM_GETICON`), `GetClassLongPtr` (`GCLP_HICONSM`/`GCLP_HICON`), and `SHGetFileInfo` with safe native `DestroyIcon` lifecycle management.
  - In-memory thread-safe icon caching with non-blocking async background resolution on discovery refresh.
  - Sources page displays crisp true-color 20x20 app icons (Chrome, Visual Studio, Explorer, etc.) with theme-adaptive fallback glyphs for displays or unresolved windows.
- [x] **Application Window Hierarchy & Safe Exit Confirmation**:
  - `MainWindow`: Primary management window. Intercepts `AppWindow.Closing` synchronously (`args.Cancel = true`) and displays a native WinUI 3 `ContentDialog` asking for confirmation before exiting.
  - Cancel keeps all windows and active presentation intact; Exit SwitchCast authorizes shutdown, stops capture, cleans up secondary windows, unregisters hotkeys, and completes clean process termination.
  - `PresentationWindow`: Independent audience-facing output. Closing it stops the presentation safely without terminating MainWindow or the application.
  - `PresenterDockWindow`: Independent presenter companion dock. Closing it closes only the dock without stopping active presentations or closing MainWindow.
  - Centralized lifecycle coordinator [ApplicationLifecycleService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/ApplicationLifecycleService.cs) governing safe multi-window teardown.
- [x] **Dependency Injection & Architecture**: Full DI container configured via `Microsoft.Extensions.DependencyInjection` in [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs) registering all core services, discovery engines, capture pipelines, presentation coordinators, window activation service, icon service, dock services, hotkey services, lifecycle service, and ViewModels.
- [x] **MVVM Pattern**: ViewModels and commands powered by `CommunityToolkit.Mvvm` ([MainViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/MainViewModel.cs), [DashboardViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/DashboardViewModel.cs), [PresentationViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/PresentationViewModel.cs), [PresenterDockViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/PresenterDockViewModel.cs), [SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs), [SettingsViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SettingsViewModel.cs), [SelectableSourceItem.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SelectableSourceItem.cs)).
- [x] **Centralized Application State & Selection Management**: [IPresentationStateService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresentationStateService.cs) & [PresentationStateService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationStateService.cs) managing session status (`Idle`, `Active`, `Paused`, `Blackout`), active capture source (`ActiveSource`), selected navigation cursor (`SelectedSource`), confirmed foreground focus (`ForegroundSource`), three-mode switching preferences (`SwitchMode`), queued sources list, and availability reconciliation.
- [x] **Real Window Discovery Engine (Phase 2)**: [IWindowDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IWindowDiscoveryService.cs) & [Win32WindowDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowDiscoveryService.cs) enumerating active top-level application windows using `EnumWindows`, `IsWindowVisible`, `GetWindowTextW`, `DwmGetWindowAttribute` (`DWMWA_CLOAKED`), `WS_EX_TOOLWINDOW` filtering, process name resolution, and SwitchCast self-exclusion.
- [x] **Real Monitor Discovery Engine (Phase 2)**: [IMonitorDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IMonitorDiscoveryService.cs) & [Win32MonitorDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32MonitorDiscoveryService.cs) enumerating connected displays via `EnumDisplayMonitors` and `GetMonitorInfo`, calculating resolutions, virtual coordinates, and primary/secondary flags.
- [x] **Native Graphics Capture Pipeline (Phase 3 & Stabilization)**:
  - COM interop bridge `IGraphicsCaptureItemInterop` creating `GraphicsCaptureItem` for window (`HWND`) and monitor (`HMONITOR`) sources with owning PID cross-validation.
  - Direct3D 11 device provider [Direct3D11DeviceProvider.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11DeviceProvider.cs) managing hardware-accelerated D3D11 device and WinRT `IDirect3DDevice` wrappers with `ResetDevice()` device loss recovery.
  - Frame pool & session manager [CaptureSessionManager.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/CaptureSessionManager.cs) acquiring `Direct3D11CaptureFramePool` streams, maintaining Direct3D frame lifetime throughout `CreateCopyFromSurfaceAsync`, session generation tracking, and backpressure frame draining.
  - Diagnostic Win32 capture service [Win32DiagnosticCaptureService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Win32DiagnosticCaptureService.cs) providing fail-safe GDI `PrintWindow` (with `PW_RENDERFULLCONTENT`) and `BitBlt` single-frame screenshot acquisition and conversion.
  - Live preview renderer [Direct3D11PreviewRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PreviewRenderer.cs) presenting converted `SoftwareBitmap` onto WinUI 3 `SoftwareBitmapSource` with decoupled non-blocking UI delivery and ~15 FPS pacing.
  - Central orchestrator [CaptureCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/CaptureCoordinator.cs) managing serialized state transitions (`Idle` -> `Starting` -> `Capturing` -> `Stopping` -> `Failed`).
- [x] **Performance Profiling & Deterministic Frame Lifecycle (Phase 4.6)**:
  - [RefCountedSoftwareBitmap.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/RefCountedSoftwareBitmap.cs) implementing zero-leak multi-consumer frame lifetime management.
  - Single-conversion frame distribution preventing duplicate GPU-to-CPU copies across preview and presentation renderers.
  - Non-blocking UI dispatcher integration eliminating `0xC000027B` stowed exception and deadlock vectors.
- [x] **Rapid Source Switching Hardening & Concurrency Serialization (Phase 4.7)**:
  - Latest-Request-Wins request coalescing and transition sequence tracking in `CaptureCoordinator` and `PresentationCoordinator`.
  - Converted frame event handlers to synchronous `void` with top-level try/catch blocks.
  - Added session generation filtering in preview and presentation renderers.
- [x] **Redesigned Presenter Dashboard UI**:
  - Compact on-air status overview strip (Live/Paused/Blackout/Standby status, active source, queued count, output window status).
  - Focal 16:9 aspect ratio preview container with deep dark surface, clear empty states, and overlay controls.
  - Prominent primary action bar ("Start Presenting" / "Stop Presenting") with source target dropdown and secondary pause/blackout controls.
  - Lightweight queued sources list with status badges.
- [x] **Redesigned Sources Page (Desktop Source Picker)**:
  - Compact IDE-style list rows (~52px height) with single-line truncated titles, process metadata, and queue checkboxes.
  - Unified filter toolbar with category selector (Windows vs Displays) and instant search.
  - Clean discovery empty states and queued summary footer.
- [x] **Redesigned Two-Pane Settings Page (OpenCode Inspired)**:
  - Clean category navigation panel (`General`, `Appearance`, `Window`, `Presenter Controls`, `Keyboard Shortcuts`).
  - Compact setting rows with right-aligned toggles and subtle horizontal dividers.
  - Dedicated searchable keyboard shortcuts table with key badge pills and reset to default action.
- [x] **Dedicated Presentation Output Window (Phase 4)**:
  - Native WinUI 3 top-level window [PresentationWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml) titled `"SwitchCast Presentation Output"`, 1280x720 default aspect ratio, capturable by Google Meet, Zoom, and Teams.
  - Standby screen, letterbox/pillarbox live video canvas, paused indicator pill, and 100% opaque blackout layer.
  - Single-instance window service [PresentationWindowService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationWindowService.cs).
- [x] **Global Hotkeys & Minimal Presenter Companion Dock (Phase 5, 5.1 & 5.3 + Hotfix)**:
  - System-wide global hotkeys via native Win32 `RegisterHotKey` / `UnregisterHotKey` in [Win32HotkeyService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32HotkeyService.cs).
  - Three source switching modes: `ActiveAndLive` (A+L), `ActiveOnly` (A), `LiveOnly` (L, default).
  - Minimal single-row floating presenter dock in Expanded and Compact modes.
  - Unclipped external dropdown host [PresenterDockMenuWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockMenuWindow.xaml) with DPI-aware positioning.
- [x] **Automated Unit & Regression Test Suite**: 152 comprehensive unit & regression tests in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) with 100% pass rate.

### Planned (Upcoming)
- [ ] **Phase 6**: Stability & Performance Optimization (Device loss recovery, leak audits, DPI dynamic multi-monitor adaptation).
- [ ] **Phase 7**: Advanced Presenter Features (Live thumbnail previews, smooth transitions).
- [ ] **Phase 8**: Packaging and Release (MSIX packaging, release readiness).

---

## 3. INVESTIGATION & KNOWN ISSUES

1. **SplitView Diagnostic (Phase 1 Investigation)**: Non-fatal upstream WindowsAppSDK diagnostic in `Microsoft.WinUI\Themes\generic.xaml:35014`.
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

- **Level 1 (Compilation)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 3.00s).
- **Level 2 (Static Analysis)**: Nullable reference checks and analyzer validation -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (152 passed, 0 failed, 0 skipped in 690ms).
- **Level 4 (Presenter Actions, Startup Centering & Dynamic Icons)**: Window focus handoff, restoration of minimized windows, DPI work-area centering, and native Win32 icon extraction verified.

---

## 7. NEXT RECOMMENDED TASK

**Task**: **Phase 6 — Stability & Performance Optimization**
- **Objective**: Direct3D 11 device loss resilience, DPI dynamic scaling across multi-monitor setups, and extended presentation load tests.

