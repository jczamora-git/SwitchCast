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
  - `MainWindow.xaml` / `DashboardViewModel.cs`: Control dashboard for window/monitor selection, preview grids, hotkey configuration, and session controls.
  - `PresentationWindow.xaml` / `PresentationViewModel.cs`: Clean, isolated output window hosting a Direct3D 11 `SwapChainPanel` that renders the active capture feed for screen sharing in Zoom/Teams/Meet.
- **Dependencies**: Depends on `SwitchCast.Application` and `SwitchCast.Core`. Has zero direct coupling to native Win32/Direct3D implementation classes.

### 2. `SwitchCast.Application` (Orchestration Layer)
- **Role**: Application use cases and state coordination.
- **Components**:
  - `PresentationCoordinator`: Coordinates transitions between capture sources, handles pause, blackout, and error recovery.
  - `SourceSelectionManager`: Manages the ordered queue of sources selected by the user.
  - `HotkeyCoordinator`: Maps keyboard shortcuts to presentation actions.
- **Dependencies**: Depends purely on `SwitchCast.Core` interfaces.

### 3. `SwitchCast.Core` (Domain Layer)
- **Role**: Enterprise domain models, core value objects, application state enums, and service contracts.
- **Components**:
  - Models: `CaptureSource`, `WindowSource`, `MonitorSource`, `PresentationSessionState`, `HotkeyBinding`.
  - Abstractions: `ICaptureService`, `IWindowDiscoveryService`, `IMonitorDiscoveryService`, `IOutputRenderer`, `IHotkeyService`.
- **Dependencies**: Pure C# standard library with zero external or UI dependencies.

### 4. `SwitchCast.Infrastructure.Windows` (Platform Infrastructure)
- **Role**: Low-level Windows OS interop, DirectX graphics pipeline, and hardware capture.
- **Components**:
  - `WindowsGraphicsCaptureEngine`: Implements `ICaptureService` using `Windows.Graphics.Capture` (`GraphicsCaptureItem`, `Direct3D11CaptureFramePool`).
  - `Direct3D11Renderer`: Manages Direct3D 11 device creation, swapchain composition, aspect-ratio scaling, and frame presentation.
  - `Win32WindowDiscovery`: Uses `EnumWindows`, `GetWindowText`, `DwmGetWindowAttribute` to identify capture-eligible desktop windows.
  - `Win32MonitorDiscovery`: Uses `EnumDisplayMonitors` to identify physical displays.
  - `Win32HotkeyService`: Implements global keyboard hooks via `RegisterHotKey`.

### 5. `SwitchCast.Tests` (Test Suite)
- **Role**: Comprehensive automated unit, integration, and contract test suites verifying domain models, state machines, and mocked capture coordination.

---

## 3. SOURCE SWITCHING LIFECYCLE & PIPELINE

When a presenter switches from Source A (e.g., Slide Deck) to Source B (e.g., Code Editor), the execution flow follows this deterministic sequence:

```
[ User Action ]
  ├── Clicks source in Dashboard UI OR
  └── Presses configured Global Hotkey (e.g. Ctrl+Shift+2)
        │
        ▼
[ PresentationCoordinator ]
  ├── Validates target source availability
  ├── Signals PresentationSessionState -> Switching
  ├── Instructs ICaptureService to transition session
        │
        ▼
[ WindowsGraphicsCaptureEngine ]
  ├── Stops / Disposes existing Direct3D11CaptureFramePool
  ├── Acquires new GraphicsCaptureItem for Target HWND / HMONITOR
  ├── Creates new FramePool with target dimensions & format (B8G8R8A8UIntNormalized)
  └── Starts new GraphicsCaptureSession
        │
        ▼
[ Frame Pipeline ]
  ├── FrameArrived event fires on background capture thread
  ├── Acquires Direct3D11CaptureFrame safely
  ├── Extracts Direct3D 11 Surface Texture
        │
        ▼
[ Direct3D11Renderer ]
  ├── Renders texture to SwapChain backbuffer with aspect-ratio letterboxing
  ├── Disposes incoming capture frame immediately to prevent pool starvation
  └── Presents swapchain (`IDXGISwapChain.Present(1, 0)`)
        │
        ▼
[ Presentation Output Window ]
  └── Instantly displays new source to meeting attendees
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
