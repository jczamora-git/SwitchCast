# SWITCHCAST — DEVELOPMENT CHANGELOG

All notable changes to the SwitchCast development harness and codebase will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), adhering to standard change types:
`feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`, `build`.

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
