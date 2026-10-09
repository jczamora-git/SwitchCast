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
- **Dependencies**: Depends on `SwitchCast.Application` and `SwitchCast.Core`. Has zero direct coupling to native Win32/Direct3D implementation classes.

### 2. `SwitchCast.Application` (Orchestration Layer)
- **Role**: Application use cases and state coordination.
- **Components**:
  - `PresentationCoordinator`: Coordinates transitions across all 4 source types (`Window`, `Display`, `Image`, `Video`), manages pause freeze/resume, blackout overlay, and error recovery.
  - `MediaPresentationService`: Manages static image decoding (`BitmapImage`) and native video playback (`Windows.Media.Playback.MediaPlayer`, muted by default).
  - `MediaDiscoveryService`: Asynchronous media file validation and metadata discovery (dimensions, durations, file sizes).
  - `Win32MediaPickerService`: Native WinUI 3 `FileOpenPicker` integration with HWND desktop interop.
  - `SourceSelectionManager`: Manages the ordered queue of sources selected by the user.
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
| **WinUI 3 UI / ViewModels** | UI Thread (Main) | Managed by XAML dispatcher; updates from background use `DispatcherQueue`. |
| **Capture Frame Pool** | Background Worker / WinRT Thread | Frames must be closed immediately in `FrameArrived` callback. FramePool disposed on source change. |
| **Direct3D 11 Device & Context** | Rendering Thread / UI Thread Interop | Shared device lifetime tied to Presentation Window; handled via `IDisposable` with device-loss recovery. |
| **Win32 Hotkeys** | Message Loop Thread | Registered on dedicated hidden window handle or main loop; cleaned up on shutdown. |

---

## 5. CAPTURE FAIL-CLOSED GUARANTEES

To ensure presenter privacy and software stability:
1. **Window Destruction / Minimization**: If a captured window terminates or is minimized, `PresentationCoordinator` catches the event, stops frame acquisition, and signals the renderer to display a clean, solid theme background with an intentional "Source Paused" notification.
2. **Device Loss Recovery**: If the GPU resets (`DXGI_ERROR_DEVICE_REMOVED`), the renderer releases all D3D11 references, recreates the device, and rebinds the swapchain seamlessly.
3. **Capture Feedback Loop Prevention**: The presentation window is excluded from window enumeration lists and marked with capture exclusion attributes to eliminate recursive mirror feedback.
