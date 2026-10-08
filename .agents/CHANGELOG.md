# SWITCHCAST — DEVELOPMENT CHANGELOG

All notable changes to the SwitchCast development harness and codebase will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), adhering to standard change types:
`feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`, `build`.

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
