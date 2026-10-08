# SWITCHCAST — PROJECT STATE

This is the authoritative progress, state, and environmental tracking document for SwitchCast. All AI agents must consult and update this file before and after executing tasks.

---

## 1. EXECUTIVE SUMMARY

- **Project**: SwitchCast
- **Current Phase**: Phase 4 — Dedicated Presentation Output Window
- **Overall Status**: **Completed (Ready for Phase 5)**
- **Last Updated**: 2026-10-08T20:25:00+08:00 (UTC+8)

---

## 2. FUNCTIONALITY STATUS

### Implemented & Verified
- [x] **Strict AI Development Harness (Phase 0)**: Standardized rules ([AGENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/AGENTS.md)), 6 domain skills, architecture specifications, coding standards, and [.editorconfig](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.editorconfig).
- [x] **WinUI 3 Desktop Application Shell (Phase 1)**: Modern native Windows 11 Fluent interface targeting `net8.0-windows10.0.19041.0` with Windows App SDK 1.5, x64 architecture, and unpackaged execution support.
- [x] **Dependency Injection & Architecture**: Full DI container configured via `Microsoft.Extensions.DependencyInjection` in [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs) registering all core services, discovery engines, capture pipelines, presentation coordinators, and ViewModels.
- [x] **MVVM Pattern**: ViewModels and commands powered by `CommunityToolkit.Mvvm` ([MainViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/MainViewModel.cs), [DashboardViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/DashboardViewModel.cs), [PresentationViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/PresentationViewModel.cs), [SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs), [SettingsViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SettingsViewModel.cs), [SelectableSourceItem.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SelectableSourceItem.cs)).
- [x] **Centralized Application State & Selection Management**: [IPresentationStateService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresentationStateService.cs) & [PresentationStateService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationStateService.cs) managing session status (`Idle`, `Active`, `Paused`, `Blackout`), active capture source, queued sources list, multi-source toggle selection, and availability reconciliation.
- [x] **Real Window Discovery Engine (Phase 2)**: [IWindowDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IWindowDiscoveryService.cs) & [Win32WindowDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowDiscoveryService.cs) enumerating active top-level application windows using `EnumWindows`, `IsWindowVisible`, `GetWindowTextW`, `DwmGetWindowAttribute` (`DWMWA_CLOAKED`), `WS_EX_TOOLWINDOW` filtering, process name resolution, and SwitchCast self-exclusion.
- [x] **Real Monitor Discovery Engine (Phase 2)**: [IMonitorDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IMonitorDiscoveryService.cs) & [Win32MonitorDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32MonitorDiscoveryService.cs) enumerating connected displays via `EnumDisplayMonitors` and `GetMonitorInfo`, calculating resolutions, virtual coordinates, and primary/secondary flags.
- [x] **Native Graphics Capture Pipeline (Phase 3)**:
  - COM interop bridge `IGraphicsCaptureItemInterop` creating `GraphicsCaptureItem` for window (`HWND`) and monitor (`HMONITOR`) sources with owning PID cross-validation.
  - Direct3D 11 device provider [Direct3D11DeviceProvider.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11DeviceProvider.cs) managing hardware-accelerated D3D11 device and WinRT `IDirect3DDevice` wrappers.
  - Frame pool & session manager [CaptureSessionManager.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/CaptureSessionManager.cs) acquiring `Direct3D11CaptureFramePool` streams, dynamic resize handling, cursor capture, and fail-closed teardown.
  - Live preview renderer [Direct3D11PreviewRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PreviewRenderer.cs) converting GPU surfaces to `SoftwareBitmapSource` with frame pacing.
  - Central orchestrator [CaptureCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/CaptureCoordinator.cs) managing serialized state transitions (`Idle` -> `Starting` -> `Capturing` -> `Stopping` -> `Failed`).
- [x] **Dashboard Live Preview UI (Phase 3)**: [DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml) featuring live preview video surface, source switcher dropdown, "Start Live Preview" and "Stop Preview" buttons, live status pill, progress indicators, and error InfoBars.
- [x] **Dedicated Presentation Output Window (Phase 4)**:
  - Native WinUI 3 top-level window [PresentationWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml) titled `"SwitchCast Presentation Output"`, 1280x720 default aspect ratio, capturable by Google Meet, Zoom, and Teams (no `WDA_EXCLUDEFROMCAPTURE` on output window).
  - Standby screen, letterbox/pillarbox live video canvas, paused indicator pill, and 100% opaque blackout layer.
  - Single-instance window service [PresentationWindowService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationWindowService.cs) preventing duplicate output windows.
  - Unified frame distribution pipeline and output renderer [Direct3D11PresentationRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PresentationRenderer.cs).
  - Presentation coordinator [PresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationCoordinator.cs) supporting Start Presenting, Stop Presenting, Freeze/Pause, Resume, Blackout, and on-the-fly source switching without closing or recreating the output window.
  - Dashboard presentation controls in [DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml) and [DashboardViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/DashboardViewModel.cs).
- [x] **Automated Unit Test Suite**: 57 comprehensive unit tests in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) verifying presentation coordinator, presentation window states, capture transitions, discovery orchestration, search filtering, selection sync, and reconciliation (100% pass rate).

### Planned (Upcoming)
- [ ] **Phase 5**: Switching System (Global hotkeys, instant source switching, shortcut customization).
- [ ] **Phase 6**: Stability & Performance Optimization (Device loss recovery, leak audits, DPI adaptation).
- [ ] **Phase 7**: Advanced Presenter Features (Live thumbnail previews, smooth transitions).
- [ ] **Phase 8**: Packaging and Release (MSIX packaging, release readiness).

---

## 3. INVESTIGATION & KNOWN ISSUES

1. **SplitView Diagnostic (Phase 1 Investigation)**:
   - **Diagnostic**: `Converter failed to convert value of type Windows.Foundation.IReference<Microsoft.UI.Xaml.GridLength> to type Double` on `SplitView.TemplateSettings.CompactPaneGridLength` -> `SplineDoubleKeyFrame.Value`.
   - **Root Cause**: Upstream bug in Microsoft.WindowsAppSDK 1.5.240802000 package file `Microsoft.WinUI\Themes\generic.xaml:35014`.
   - **Impact**: Non-fatal upstream framework diagnostic. Navigation operates cleanly.
2. **Minimized Window OS Policy**: As per standard Windows Graphics Capture design, minimized application windows do not produce new Direct3D frames until restored.

---

## 4. ARCHITECTURE DECISION RECORDS

- [ADR-0001: Technology Stack & Clean Architecture Core](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/decisions/ADR-0001-architecture.md) — Implemented in Phase 1, 2, 3, and 4.

---

## 5. DETECTED ENVIRONMENT & TOOLING

- **Host OS**: Windows 11 Pro (OS Build 10.0.22631, win-x64)
- **.NET SDK**: 8.0.403 (`C:\Program Files\dotnet\sdk\8.0.403\`)
- **Target Framework**: `net8.0-windows10.0.19041.0`
- **Architecture**: `x64` (`win-x64`)
- **Windows App SDK**: 1.5.240802000

---

## 6. VERIFICATION RECORD

- **Level 1 (Compilation)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 21.1s).
- **Level 2 (Static Analysis)**: Nullable reference checks and analyzer validation -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (57 passed, 0 failed, 0 skipped in 216ms).

---

## 7. NEXT RECOMMENDED TASK

**Task**: **Phase 5 — Global Hotkeys & Presentation Switching Controls**
- **Objective**: Implement native Win32 global hotkeys (`RegisterHotKey`) for instant one-key source switching, pause, and blackout shortcuts.
