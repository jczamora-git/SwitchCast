# SWITCHCAST — SYSTEM ARCHITECTURE SPECIFICATION

---

## 1. ARCHITECTURAL OVERVIEW

SwitchCast is engineered using **Clean Architecture** and **MVVM (Model-View-ViewModel)** principles. The architecture enforces unidirectional dependency flow and strict decoupling between native Windows graphics subsystems, application coordination logic, and XAML presentation layers.

```mermaid
graph TD
    subgraph UI ["SwitchCast.Desktop (WinUI 3 / XAML)"]
        DashboardView[Control Dashboard Window]
        PresentationView[Presentation Output Window]
        DashboardVM[DashboardViewModel]
        PresentationVM[PresentationViewModel]
    end

    subgraph App ["SwitchCast.Application"]
        PresCoord[IPresentationCoordinator]
        SourceMgr[ISourceSelectionManager]
        HotkeyCoord[IHotkeyCoordinator]
    end

    subgraph Core ["SwitchCast.Core (Domain)"]
        SourceModels[CaptureSource / WindowSource / MonitorSource]
        PresState[PresentationSessionState]
        Interfaces[Service Interfaces: ICaptureService, etc.]
    end

    subgraph Infra ["SwitchCast.Infrastructure.Windows"]
        WinCap[GraphicsCaptureEngine (WGC)]
        D3D11[Direct3D11 Swapchain Renderer]
        WinDisc[Win32 Window & Monitor Discovery]
        WinHot[Win32 Global Hotkey Service]
    end

    UI --> App
    UI --> Core
    App --> Core
    Infra --> Core
    Infra -.-> App
```

> [!IMPORTANT]
> **Logical Boundaries vs Physical Assemblies**: The boundaries described above represent logical namespaces and design separation. During early phases, code may live within a unified, well-organized project structure to minimize build complexity until multi-project splitting is strictly justified.

---

## 2. LOGICAL MODULES

### 1. `SwitchCast.Desktop` (Presentation Layer)
- **Role**: WinUI 3 desktop user interface, window lifecycle, and MVVM bindings.
- **Components**:
  - `MainWindow.xaml` / `DashboardViewModel.cs`: Control dashboard for window/monitor/media selection, preview grids, hotkey configuration, video playback controls, and session controls.
  - `PresentationWindow.xaml` / `PresentationViewModel.cs`: Clean, isolated output window hosting mutually exclusive visual presentation layers (Screen Capture `Image`, Direct Static `Image`, Video `MediaPlayerElement`, Standby overlay, and topmost Blackout overlay) for screen sharing in Zoom/Teams/Meet. Supports seamless toggle between `PresentationDisplayMode.Windowed` (with custom title bar) and `PresentationDisplayMode.Fullscreen` (true borderless `AppWindowPresenterKind.FullScreen` filling 100% of viewport with same HWND).
  - `PresenterDockWindow.xaml` / `PresenterDockViewModel.cs`: Lightweight always-on-top floating toolbar with native non-client dragging (`InputNonClientPointerSource`), maximized prevention via Win32 window subclassing (`WM_NCLBUTTONDBLCLK` / `SC_MAXIMIZE`), transport controls, and fullscreen output triggering.
  - `PresenterDockMenuWindow.xaml`: Borderless popup window hosting unconstrained dropdown menus (Queued Sources with direct interactive unqueue, Switching Mode, More Options).
- **Dependencies**: Depends on `SwitchCast.Application` and `SwitchCast.Core`. Has zero direct coupling to native Win32/Direct3D implementation classes.

### 2. `SwitchCast.Application` (Orchestration Layer)
- **Role**: Application use cases and state coordination.
- **Components**:
  - `PresentationCoordinator`: Coordinates transitions across all 4 source types (`Window`, `Display`, `Image`, `Video`), manages pause freeze/resume, blackout overlay, and error recovery. Maintains single authoritative queue traversal order across mixed media and capture sources.
  - `MediaPresentationService`: Manages static image decoding (`BitmapImage`) and native video playback (`Windows.Media.Playback.MediaPlayer`, synchronized audio/video with volume/mute controls).
  - `MediaDiscoveryService`: Asynchronous media file validation and metadata discovery (dimensions, durations, file sizes).
  - `Win32MediaPickerService`: Native WinUI 3 `FileOpenPicker` integration with HWND desktop interop.
  - `PresentationStateService`: Authoritative central state store managing `SelectedSources`, `SelectedSource` cursor, `ActiveSource`, `ForegroundSource`, `SwitchMode`, and `Status`. Enforces On-Air presentation continuity on unqueue and deterministic selection cursor recalculation.
  - `HotkeyCoordinator`: Maps keyboard shortcuts to presentation actions.
- **Dependencies**: Depends purely on `SwitchCast.Core` interfaces.

### 3. `SwitchCast.Core` (Domain Layer)
- **Role**: Enterprise domain models, core value objects, application state enums, and service contracts.
- **Components**:
  - Models: `CaptureSource`, `WindowSource`, `MonitorSource`, `MediaFileSource`, `ImageMediaSource`, `VideoMediaSource`, `PresentationSessionState`, `HotkeyBinding`.
  - Abstractions: `ICaptureService`, `IWindowDiscoveryService`, `IMonitorDiscoveryService`, `IMediaDiscoveryService`, `IMediaPickerService`, `IMediaPresentationService`, `IOutputRenderer`, `IHotkeyService`.
- **Dependencies**: Pure C# standard library with zero external or UI dependencies.

### 4. `SwitchCast.Infrastructure.Windows` (Platform Infrastructure)
- **Role**: Low-level Windows OS interop, DirectX graphics pipeline, media playback, and hardware capture.
- **Components**:
  - `WindowsGraphicsCaptureEngine`: Implements `ICaptureService` using `Windows.Graphics.Capture` (`GraphicsCaptureItem`, `Direct3D11CaptureFramePool`).
  - `Direct3D11Renderer`: Manages Direct3D 11 device creation, swapchain composition, aspect-ratio scaling, and frame presentation.
  - `Win32WindowDiscovery`: Uses `EnumWindows`, `GetWindowText`, `DwmGetWindowAttribute` to identify capture-eligible desktop windows.
  - `Win32MonitorDiscovery`: Uses `EnumDisplayMonitors` to identify physical displays.
  - `Win32HotkeyService`: Implements global keyboard hooks via `RegisterHotKey`.

### 5. `SwitchCast.Tests` (Test Suite)
- **Role**: Comprehensive automated unit, integration, and contract test suites verifying domain models, state machines, media discovery, media presentation, and mocked capture coordination.

---

## 3. SOURCE SWITCHING LIFECYCLE & PIPELINE

When a presenter switches between presentation sources (e.g., Application Window, Monitor, Static Image, Video File), `PresentationCoordinator` orchestrates the transition deterministically across the unified pipeline:

```
[ User Action ]
  ├── Clicks target source in Dashboard UI OR
  ├── Selects source from Floating Presenter Dock OR
  └── Presses configured Global Hotkey (e.g. Next/Prev / Ctrl+Shift+1..5)
        │
        ▼
[ PresentationCoordinator ]
  ├── Validates target source availability & session sequence
  ├── Routes source based on type:
  │     ├─ Window / Monitor: Coordinates CaptureCoordinator (Direct3D 11 / WGC)
  │     ├─ Image: Loads via MediaPresentationService (BitmapImage)
  │     └─ Video: Plays via MediaPresentationService (MediaPlayer, synchronized A/V, local audio with volume/mute controls)
  │
  ▼
[ Presentation Output Window (Single Stable HWND) ]
  ├── Mutually exclusive visual layers:
  │     ├─ Layer 1: Standby Canvas (Idle / Error)
  │     ├─ Layer 2: Screen Capture Canvas (Window / Display)
  │     ├─ Layer 3: Direct Image Canvas (PNG / JPG / BMP / WEBP)
  │     ├─ Layer 4: Direct Video Canvas (MediaPlayerElement)
  │     └─ Layer 5: Blackout Overlay (Solid 100% Opaque Black Canvas - Topmost)
  └── Delivers zero-latency visual output to meeting attendees (Meeting audio transmitted separately via conferencing app "Also share system audio")
```

---

## 4. THREADING & RESOURCE LIFETIME GOVERNANCE

| Subsystem | Thread Affinity | Lifecycle / Disposal Rules |
| :--- | :--- | :--- |
| **WinUI 3 UI / ViewModels** | UI Thread (Explicit DispatcherQueue per Window) | Explicitly dispatched via `IUIThreadDispatcher` (`DispatcherQueueAdapter`). Each window (`MainWindow`, `PresentationWindow`, `PresenterDockWindow`) owns its respective `DispatcherQueue`. ViewModels marshal domain state changes to UI-bound XAML bindings strictly on the owning UI thread. |
| **Camera Capture Pipeline** | Background Worker / WinRT Thread (`MediaFrameReader`) | Acquires BGRA8 frames on camera thread. Never fires direct XAML-bound notifications synchronously from camera worker thread. State transitions (e.g. `Starting` -> `Active`) update domain state and dispatch UI notifications safely. |
| **Video Playback Pipeline** | Windows Media Playback Thread | Player state changes (`MediaStateChanged`) are isolated from UI error handlers; UI state propagation uses `IUIThreadDispatcher`. |
| **Direct3D 11 Device & Context** | Rendering Thread / UI Thread Interop | Shared device lifetime tied to Presentation Window; handled via `IDisposable` with device-loss recovery. |
| **Win32 Hotkeys** | Message Loop Thread | Registered on dedicated hidden window handle or main loop; cleaned up on shutdown. |

---

## 5. CAPTURE FAIL-CLOSED GUARANTEES

To ensure presenter privacy and software stability:
1. **Window Destruction / Minimization**: If a captured window terminates or is minimized, `PresentationCoordinator` catches the event, stops frame acquisition, and signals the renderer to display a clean, solid theme background with an intentional "Source Paused" notification.
2. **Device Loss Recovery**: If the GPU resets (`DXGI_ERROR_DEVICE_REMOVED`), the renderer releases all D3D11 references, recreates the device, and rebinds the swapchain seamlessly.
3. **Capture Feedback Loop Prevention**: The presentation window is excluded from window enumeration lists and marked with capture exclusion attributes to eliminate recursive mirror feedback.

---

## 6. DIAGNOSTICS & ERROR REPORTING ARCHITECTURE

SwitchCast incorporates a dedicated, production-grade local diagnostics and error reporting engine designed to capture actionable technical data while rigorously protecting user privacy during live presentations:

```
[ Error / Exception Occurs ]
  ├── AppDomain / TaskScheduler / App.UnhandledException OR
  ├── PresentationCoordinator / Capture / Camera / PowerPoint failure OR
  └── Explicit diagnostic instrumentation (SwitchCastDiagnostics.LogException)
        │
        ▼
[ IErrorReportingService (ErrorReportingService) ]
  ├── Deduplicates burst errors (5-second component/operation/HRESULT window)
  ├── Enriches incident with OS, App version, architecture, and 100-event timeline
  ├── Dispatches event to IDiagnosticRedactionService
        │
        ▼
[ IDiagnosticRedactionService (DiagnosticRedactionService) ]
  ├── Redacts Windows usernames (-> [USER])
  ├── Redacts user profile paths (-> %USERPROFILE%)
  ├── Redacts email addresses (-> [REDACTED_EMAIL])
  └── Redacts auth tokens, secrets, and API keys (-> [REDACTED_SECRET])
        │
        ├─────────────────────────────┬─────────────────────────────┐
        ▼                             ▼                             ▼
[ Local Bounded Logs ]      [ Presentation Safe UI ]      [ Community Export ]
  ├── %LOCALAPPDATA%\SwitchCast\Logs
  ├── switchcast-YYYYMMDD.log  ├── Presenter Dock / Dashboard  ├── GitHub Issue URL (sanitized)
  ├── incident-<id>.json       ├── Non-blocking InfoBar        ├── Copy Report (clipboard)
  ├── 10-incident rotation     └── ErrorReportDialog.xaml      └── Save Report (.txt export)
  └── 30-day log purge             (NEVER shown in Output)
```

### Architectural Guarantees:
1. **Zero Remote Telemetry**: All logs and incidents are written strictly to local storage. No network transmission occurs unless the user explicitly copies, saves, or posts to GitHub.
2. **Audience-Facing Isolation**: `PresentationWindow` never displays error dialogs, badges, or notification modals. Error reporting is confined to `MainWindow` and `PresenterDockWindow`.
3. **Reentrancy & Deadlock Prevention**: Error reporting logging operations are asynchronous and non-blocking, isolated from UI threads and frame-delivery callbacks. Failures in the logging subsystem fail silently without crashing the main application.
4. **Synchronous Critical Flush**: High-severity and terminating incidents (e.g. `AppDomain.UnhandledException` where `IsTerminating == true`) bypass asynchronous thread-pool queues and flush synchronously to disk before process death.
5. **UI Hang Watchdog**: An independent background timer monitors UI `DispatcherQueue` responsiveness and emits warnings (5s) and deadlock incidents (15s) if the message loop becomes unresponsive.

---

## 7. AUTHORITATIVE WINDOW & PRESENTATION LIFECYCLE CONTRACT

SwitchCast enforces an authoritative 4-action lifecycle contract governing presentation teardown and window management:

### Action A — Stop Presenting
Triggered from Dashboard, Floating Presenter Dock, global hotkey, or direct window closure:
- **Strict Teardown Sequence**:
  1. Invalidate camera startup & drain camera frames (`_cameraStartupCts?.Cancel()`, `_pendingCameraFrame?.Dispose()`, `_cameraCaptureService.StopCaptureAsync()`).
  2. Stop Windows Graphics Capture (`_captureCoordinator.StopPreviewAsync()`).
  3. Stop PowerPoint live slideshow & clear presentation (`_powerPointLiveService.StopSlideshowAsync()`, `_powerPointService.ClearPresentation()`).
  4. Stop Video and clear Image (`_mediaPresentationService.ClearImage()`, `_mediaPresentationService.StopVideoAsync()`).
  5. Invalidate render generations (`_outputRenderer.Clear()`).
  6. Transition presentation status to `Idle`.
  7. Close and clean up Presentation Output window (`PresentationWindowService.ClosePresentationWindow()` -> `PresentationWindow.Cleanup()`).
  8. Restore and activate `MainWindow` and navigate to the Control Dashboard.
- **Idempotency Guarantees**: `StopPresentationAsync` is synchronized with `_stopLock` and deduplicated via `_activeStopTask`, ensuring multiple concurrent stop triggers (e.g. Dashboard button + Dock close + hotkey) observe and await the exact same single execution without duplicate teardowns or double-close side effects.
- **Native Teardown Boundary**: `PresentationWindow.Cleanup()` explicitly detaches `PresentationOutputImage.Source = null`, `PresentationDirectImage.Source = null`, and `PresentationVideoPlayer.SetMediaPlayer(null)` before native HWND destruction, severing DirectX DirectComposition and MediaPlayer references to prevent `AccessViolationException` (`0xC0000005`) in `Microsoft.WinUI.dll`. Unsubscribes `ViewModel.PropertyChanged` and `ThemeChanged`, calls `Bindings.StopTracking()`, and disposes `PresentationViewModel`.
- Preserves the application process, source library, and queued source items intact without creating duplicate window instances.

### Action B — Presenter Dock X Button (User Close Request)
- **Active Presentation Running (Active, Paused, Blackout)**:
  - Intercepts close request and displays custom confirmation dialog:
    - Title: "Stop presentation and close Dock?"
    - Message: "A presentation is currently running. Closing the Presenter Dock will also stop the presentation and close Presentation Output."
    - Options: `Cancel` and `Stop & Close`.
  - Hosted in a dedicated modal window (`PresenterDockConfirmationWindow`) centered on the active presentation control monitor, completely decoupled from the Floating Dock's compact surface to prevent caption drag interference, clipping, and z-order conflicts.
  - Temporarily disables owner dock input via Win32 `EnableWindow(dockHwnd, false)` to prevent duplicate actions.
  - **Lifetime & Teardown Boundary**: `PresenterDockConfirmationWindow` operates as an independent top-level tool window (`OverlappedPresenter.IsAlwaysOnTop = true`, `WS_EX_TOOLWINDOW`) without Win32 `GWLP_HWNDPARENT` coupling. This decoupling prevents native Win32 window manager destruction of child HWNDs when the owner dock closes, which previously caused `AccessViolationException` (`0xC0000005`) in `Microsoft.WinUI.dll` during `Window.Close()`.
  - **Sequential Close Order**:
    1. User clicks `Stop & Close`.
    2. `PresenterDockConfirmationWindow` logs the click, closes itself cleanly via `Window.Close()`, and awaits its `Closed` event.
    3. `ShowConfirmationAsync` resolves with `true` asynchronously (`TaskCreationOptions.RunContinuationsAsynchronously`).
    4. Authoritative `StopPresentationAsync` runs: stops all sources, clears renderers, closes `PresentationWindow` (with full DComp/MediaPlayer cleanup), activates `MainWindow`, and navigates to the Control Dashboard.
    5. `PresenterDockService.CloseDock()` marks `IsClosingProgrammatically = true` and closes `PresenterDockWindow`.
    6. `MainWindow` and the SwitchCast process remain alive and active on the Control Dashboard with unchanged PID and HWND.
  - `Cancel` (or Escape key): Dismisses confirmation modal, re-enables dock, restores foreground to dock, keeps Floating Dock open, keeps presentation live, and leaves output unaffected.
  - `Stop & Close`: Executes the authoritative sequential stop and close contract above without terminating the application process.
- **No Active Presentation (Idle)**:
  - Closes Floating Dock immediately with zero confirmation prompt.
  - Activates Control Dashboard via `PresenterDockService.ShowDashboard()`.
  - Process remains running.

### Action C — Full Application Exit
- Initiated via `MainWindow` close button.
- Displays standard exit confirmation dialog.
- Upon confirmed exit: stops active presentation, coordinates teardown across all managed windows (`PresentationWindow`, `PresenterDockWindow`), disposes hotkeys and native resources, and exits the process without secondary dock confirmation prompts or Dashboard reactivation.

### Action D — Dock Show/Hide Hotkey
- Toggles dock visibility non-destructively via `IPresenterDockService.ToggleDock()`.
- Never triggers stop presentation or close confirmation dialogs.

