# SWITCHCAST — PROJECT STATE

This is the authoritative progress, state, and environmental tracking document for SwitchCast. All AI agents must consult and update this file before and after executing tasks.

---

## 1. EXECUTIVE SUMMARY

- **Project**: SwitchCast
- **Current Phase**: Phase 5.3 — Minimal Presenter Dock + Three-Mode Source Switching
- **Overall Status**: **Completed (Ready for Phase 6)**
- **Last Updated**: 2026-10-09T00:30:00+08:00 (UTC+8)

---

## 2. FUNCTIONALITY STATUS

### Implemented & Verified
- [x] **Strict AI Development Harness (Phase 0)**: Standardized rules ([AGENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/AGENTS.md)), 6 domain skills, architecture specifications, coding standards, and [.editorconfig](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.editorconfig).
- [x] **WinUI 3 Desktop Application Shell (Phase 1)**: Modern native Windows 11 Fluent interface targeting `net8.0-windows10.0.19041.0` with Windows App SDK 1.5, x64 architecture, and unpackaged execution support.
- [x] **Dependency Injection & Architecture**: Full DI container configured via `Microsoft.Extensions.DependencyInjection` in [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs) registering all core services, discovery engines, capture pipelines, presentation coordinators, window activation service, dock services, hotkey services, and ViewModels.
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
  - Authoritative report documented in [docs/PERFORMANCE_BASELINE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/PERFORMANCE_BASELINE.md).
- [x] **Rapid Source Switching Hardening & Concurrency Serialization (Phase 4.7)**:
  - Root cause analysis and resolution of `0xC000027B` stowed exception crash during rapid switching.
  - Fixed premature disposal of `emptyBitmap` in `Clear()` while `SoftwareBitmapSource.SetBitmapAsync` was in-flight on the compositor.
  - Ensured `SoftwareBitmapSource.Dispose()` executes exclusively on UI thread `DispatcherQueue`.
  - Implemented Latest-Request-Wins request coalescing and transition sequence tracking in `CaptureCoordinator` and `PresentationCoordinator`, discarding obsolete intermediate transitions without redundant native session churn.
  - Converted `OnFrameArrived` and `OnCaptureFrameArrived` event handlers from `async void` to synchronous `void` with strict try/catch boundaries to prevent unobserved asynchronous exceptions from escaping to the UI SynchronizationContext.
  - Added session generation filtering in both `Direct3D11PreviewRenderer` and `Direct3D11PresentationRenderer` to drop stale in-flight UI frame renders across source switches.
- [x] **Dashboard Live Preview UI (Phase 3)**: [DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml) featuring live preview video surface, source switcher dropdown, "Start Live Preview" and "Stop Preview" buttons, live status pill, progress indicators, and error InfoBars.
- [x] **Dedicated Presentation Output Window (Phase 4)**:
  - Native WinUI 3 top-level window [PresentationWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml) titled `"SwitchCast Presentation Output"`, 1280x720 default aspect ratio, capturable by Google Meet, Zoom, and Teams (no `WDA_EXCLUDEFROMCAPTURE` on output window).
  - Standby screen, letterbox/pillarbox live video canvas, paused indicator pill, and 100% opaque blackout layer.
  - Single-instance window service [PresentationWindowService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationWindowService.cs) preventing duplicate output windows.
  - Unified frame distribution pipeline and output renderer [Direct3D11PresentationRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PresentationRenderer.cs).
  - Presentation coordinator [PresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationCoordinator.cs) supporting Start Presenting, Stop Presenting, Freeze/Pause, Resume, Blackout, and on-the-fly source switching without closing or recreating the output window.
- [x] **Global Hotkeys & Minimal Presenter Companion Dock (Phase 5, 5.1 & 5.3)**:
  - System-wide global hotkeys via native Win32 `RegisterHotKey` / `UnregisterHotKey` in [Win32HotkeyService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32HotkeyService.cs) hosted on a dedicated message-only window (`HWND_MESSAGE`).
  - Hotkey actions for Next Source (`Ctrl+Shift+Right`), Previous Source (`Ctrl+Shift+Left`), Pause/Resume (`Ctrl+Shift+P`), Blackout (`Ctrl+Shift+B`), Stop Presenting (`Ctrl+Shift+S`), Toggle Presenter Dock (`Ctrl+Shift+D`), Focus Dashboard (`Ctrl+Shift+M`), and Direct Source Switching (`Ctrl+Shift+1..5`).
  - Native Win32 window focus & activation service [IWindowActivationService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IWindowActivationService.cs) & [Win32WindowActivationService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowActivationService.cs) implementing `SetForegroundWindow` and `ShowWindowAsync` (with `SW_RESTORE`) for focus management.
  - **Three Source Switching Modes**:
    1. `ActiveAndLive` ("A+L"): Activates the selected application window into the foreground AND switches live presentation output.
    2. `ActiveOnly` ("A"): Activates the selected application window into the foreground WITHOUT changing the audience-facing presentation output.
    3. `LiveOnly` ("L", default): Switches the audience-facing presentation output without altering user application focus.
  - Minimal single-row floating presenter dock [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml) in both Expanded (660×52 DIP) and Compact (460×46 DIP) modes.
  - Strict single-line source titles with `TextTrimming="CharacterEllipsis"`, `TextWrapping="NoWrap"`, `MaxLines="1"`, and rich multi-line tooltips.
  - Mode selector dropdown on both Expanded and Compact modes displaying compact badges ("A+L", "A", "L") with checkmark Flyouts and immediate settings persistence.
  - Icon-first action buttons with full tooltips and accessible names.
  - Borderless window chrome (`SetBorderAndTitleBar(false, false)`), always-on-top mode, DPI-aware physical pixel resizing, and separate drag handle (`WM_NCLBUTTONDOWN`).
- [x] **Automated Unit & Concurrency Test Suite**: 122 comprehensive unit & regression tests in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) verifying window activation handling, three-mode switching, navigation cursors, hotkey lifecycle, dock lifecycle, rapid source switching (Latest-Request-Wins), ref-counted bitmaps, and state services (100% pass rate).

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

- **Level 1 (Compilation)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 22.3s).
- **Level 2 (Static Analysis)**: Nullable reference checks and analyzer validation -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (122 passed, 0 failed, 0 skipped in 315ms).
- **Level 4 (Deterministic Lifecycle & Focus Hardening)**: Window activation fallback, three-mode switching execution, single-row minimal toolbar layout, DPI scaling, and Latest-Request-Wins transition serialization verified.

---

## 7. NEXT RECOMMENDED TASK

**Task**: **Phase 6 — Stability & Performance Optimization**
- **Objective**: Direct3D 11 device loss resilience, DPI dynamic scaling across multi-monitor setups, and extended presentation load tests.
