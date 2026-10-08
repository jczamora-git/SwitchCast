# SWITCHCAST — PROJECT STATE

This is the authoritative progress, state, and environmental tracking document for SwitchCast. All AI agents must consult and update this file before and after executing tasks.

---

## 1. EXECUTIVE SUMMARY

- **Project**: SwitchCast
- **Current Phase**: Phase 2 — Real Window and Monitor Discovery & Phase 1 Stabilization
- **Overall Status**: **Completed (Ready for Phase 3)**
- **Last Updated**: 2026-10-08T19:44:00+08:00 (UTC+8)

---

## 2. FUNCTIONALITY STATUS

### Implemented & Verified
- [x] **Strict AI Development Harness (Phase 0)**: Standardized rules ([AGENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/AGENTS.md)), 6 domain skills, architecture specifications, coding standards, and [.editorconfig](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.editorconfig).
- [x] **WinUI 3 Desktop Application Shell (Phase 1)**: Modern native Windows 11 Fluent interface targeting `net8.0-windows10.0.19041.0` with Windows App SDK 1.5, x64 architecture, and unpackaged execution support.
- [x] **Dependency Injection & Architecture**: Full DI container configured via `Microsoft.Extensions.DependencyInjection` in [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs) registering all core services, discovery engines, and ViewModels.
- [x] **MVVM Pattern**: ViewModels and commands powered by `CommunityToolkit.Mvvm` ([MainViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/MainViewModel.cs), [DashboardViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/DashboardViewModel.cs), [SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs), [SettingsViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SettingsViewModel.cs), [SelectableSourceItem.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SelectableSourceItem.cs)).
- [x] **Centralized Application State & Selection Management**: [IPresentationStateService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresentationStateService.cs) & [PresentationStateService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationStateService.cs) managing session status (`Idle`, `Active`, `Paused`, `Blackout`), active capture source, queued sources list, multi-source toggle selection, and availability reconciliation.
- [x] **Real Window Discovery Engine (Phase 2)**: [IWindowDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IWindowDiscoveryService.cs) & [Win32WindowDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowDiscoveryService.cs) enumerating active top-level application windows using `EnumWindows`, `IsWindowVisible`, `GetWindowTextW`, `DwmGetWindowAttribute` (`DWMWA_CLOAKED`), `WS_EX_TOOLWINDOW` filtering, process name resolution, and SwitchCast self-exclusion.
- [x] **Real Monitor Discovery Engine (Phase 2)**: [IMonitorDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IMonitorDiscoveryService.cs) & [Win32MonitorDiscoveryService](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32MonitorDiscoveryService.cs) enumerating connected displays via `EnumDisplayMonitors` and `GetMonitorInfo`, calculating resolutions, virtual coordinates, and primary/secondary flags.
- [x] **Source Management UI (Phase 2)**: [SourcesPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SourcesPage.xaml) featuring live discovery cards, real-time title/process search filter, tabbed category selector (`Application Windows (N)` vs. `Displays & Monitors (M)`), manual refresh button with loading spinner, and queued summary footer.
- [x] **Authoritative Dashboard Sync (Phase 2)**: [DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml) displaying live queued source metrics, dynamic queued source card list, and navigation bindings.
- [x] **Closed / Disconnected Source Reconciliation (Phase 2)**: Stale or closed sources retain selection in the presenter queue while safely marked as `IsAvailable = false` ("Closed / Unavailable" badge) without crashing or redirecting.
- [x] **Automated Unit Test Suite**: 30 comprehensive unit tests in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) verifying discovery orchestration, search filtering, selection sync, and reconciliation (100% pass rate).

### Planned (Upcoming)
- [ ] **Phase 3**: Capture Engine (`Windows.Graphics.Capture`, Direct3D 11 swapchain, frame lifecycle).
- [ ] **Phase 4**: Presentation Output Window (Dedicated shareable window, aspect-ratio scaling, fail-closed rendering).
- [ ] **Phase 5**: Switching System (Global hotkeys, instant source switching, blackout/pause controls).
- [ ] **Phase 6**: Stability & Performance Optimization (Device loss recovery, leak audits, DPI adaptation).
- [ ] **Phase 7**: Advanced Presenter Features (Live thumbnail previews, smooth transitions).
- [ ] **Phase 8**: Packaging and Release (MSIX packaging, release readiness).

---

## 3. INVESTIGATION & KNOWN ISSUES

1. **SplitView Diagnostic (Objective A Investigation)**:
   - **Diagnostic**: `Converter failed to convert value of type Windows.Foundation.IReference<Microsoft.UI.Xaml.GridLength> to type Double` on `SplitView.TemplateSettings.CompactPaneGridLength` -> `SplineDoubleKeyFrame.Value`.
   - **Root Cause**: Located directly in Microsoft.WindowsAppSDK 1.5.240802000 package file `Microsoft.WinUI\Themes\generic.xaml` at line 35014. The internal framework `SplitView` animation template directly binds a `GridLength` property to a `Double` keyframe value without a converter.
   - **Impact**: Non-fatal upstream framework diagnostic. NavigationView/SplitView pane animations and navigation operate without issues. Documented as an upstream WinUI 3 framework bug.
2. **Git Repository Uninitialized**: The directory is currently uninitialized as a git repository.

---

## 4. ARCHITECTURE DECISION RECORDS

- [ADR-0001: Technology Stack & Clean Architecture Core](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/decisions/ADR-0001-architecture.md) — Implemented in Phase 1 & 2.

---

## 5. DETECTED ENVIRONMENT & TOOLING

- **Host OS**: Windows 11 Pro (OS Build 10.0.22631, win-x64)
- **.NET SDK**: 8.0.403 (`C:\Program Files\dotnet\sdk\8.0.403\`)
- **Target Framework**: `net8.0-windows10.0.19041.0`
- **Architecture**: `x64` (`win-x64`)
- **Windows App SDK**: 1.5.240802000

---

## 6. VERIFICATION RECORD

- **Level 1 (Compilation)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 23.9s).
- **Level 2 (Static Analysis)**: Nullable reference checks and analyzer validation -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (30 passed, 0 failed, 0 skipped in 285ms).

---

## 7. NEXT RECOMMENDED TASK

**Task**: **Phase 3 — Capture Engine**
- **Objective**: Implement hardware-accelerated screen capture pipeline using `Windows.Graphics.Capture`, `IGraphicsCaptureItemInterop`, and Direct3D 11 frame pool lifecycle.
