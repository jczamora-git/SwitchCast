---
name: switchcast-capture
description: Windows Graphics Capture and Direct3D 11 Specialist skill for SwitchCast. Governs window/monitor enumeration, frame acquisition, swapchain rendering, resource lifecycle, and capture error handling.
---

# SwitchCast Screen Capture & Graphics Specialist Skill

## Role: Windows Graphics Capture / Direct3D Specialist

The Screen Capture skill governs all native graphics capture pipelines, DirectX/Direct3D 11 rendering, frame acquisition, display enumeration, and low-latency switching in SwitchCast.

---

## 1. Core Responsibilities & Modular Separation

Capture functionality MUST be divided into specialized, decoupled components. **Do not combine these into a single monolithic class.**

1. **A. Source Discovery (`IWindowDiscoveryService`, `IMonitorDiscoveryService`)**:
   - Enumerates valid desktop windows (`EnumWindows`, filtering cloaked/tool/invisible windows).
   - Enumerates connected physical and virtual monitors (`EnumDisplayMonitors`).
   - Retrieves metadata (titles, window handles `HWND`, monitor handles `HMONITOR`, bounds, icons).
2. **B. Source Selection (`ISourceSelectionManager`)**:
   - Maintains the presenter's active queue/list of selected sources.
   - Provides thread-safe source switching triggers.
3. **C. Capture Session (`ICaptureSession`, `GraphicsCaptureSession`)**:
   - Manages `GraphicsCaptureItem` creation via interop (`IGraphicsCaptureItemInterop`).
   - Configures `Direct3D11CaptureFramePool` and controls session start/stop/pause.
   - Handles yellow border policy (`IsBorderRequired`) and cursor capture policy (`IsCursorCaptureEnabled`).
4. **D. Frame Processing (`IFrameProcessor`)**:
   - Acquires frames from `Direct3D11CaptureFramePool.FrameArrived`.
   - Handles frame sizing, aspect ratio calculation, and texture format validation.
5. **E. Output Renderer (`IOutputRenderer`, Direct3D 11 SwapChain)**:
   - Renders incoming frame textures to a WinUI 3 `SwapChainPanel` or Direct3D 11 swapchain.
   - Manages DirectX device creation (`D3D11CreateDevice`), context, and backbuffers.
6. **F. Presentation Session (`IPresentationCoordinator`)**:
   - High-level coordinator linking selection, capture, rendering, blackout, and pause states.
7. **G. Hotkey Service (`IHotkeyService`)**:
   - Registers and listens for global hotkeys (`RegisterHotKey` / low-level hooks) to trigger instant source switches.

---

## 2. Technical Requirements & Native Resource Safety

- **Deterministic Disposal**: All native WinRT objects (`GraphicsCaptureSession`, `Direct3D11CaptureFramePool`, `Direct3D11CaptureFrame`) and COM/Direct3D objects (`ID3D11Device`, `ID3D11Texture2D`, `IDXGISwapChain`) must be disposed or released deterministically.
- **Fail-Closed Strategy**: When a captured window is closed, minimized, moved off-screen, or protected:
  - Do NOT leak previous frame contents or fall back to an arbitrary window.
  - Transition cleanly to a fail-closed state (e.g., render an intentional "Source Paused" or clean blank frame).
- **DirectX Device Loss**: Handle `DXGI_ERROR_DEVICE_REMOVED` or `DXGI_ERROR_DEVICE_RESET` by gracefully recreating the Direct3D 11 device, frame pools, and swapchains.
- **Feedback Loop Prevention**:
  - The capture engine must exclude the SwitchCast Presentation Window and Control Dashboard from capture targets unless explicitly requested by monitor capture.
  - Use `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` where applicable for the SwitchCast dashboard to prevent infinite mirror recursion.
- **Zero Allocations in Hot Paths**: Avoid per-frame managed heap allocations in `FrameArrived` callbacks. Reuse DirectX staging buffers.

---

## 3. Skill Activation Conditions

Activate this skill when:
- Implementing or modifying window or monitor enumeration.
- Modifying `Windows.Graphics.Capture` interop or frame pool lifecycles.
- Working on Direct3D 11 devices, swapchains, or `SwapChainPanel` shaders/renderers.
- Implementing global hotkey switching or source transition logic.
- Troubleshooting GPU memory leaks, frame drops, or capture failure states.

---

## 4. Capture Quality & Stability Checklist

Before finalizing capture code, verify:
- [ ] Are all `Direct3D11CaptureFrame` instances closed/disposed after consumption?
- [ ] Is `GraphicsCaptureSession` disposed when switching sources?
- [ ] Are window destruction (`WM_DESTROY`), monitor disconnects, and resolution changes handled gracefully?
- [ ] Is device loss recovery implemented?
- [ ] Is there zero unintentional capture feedback loop?
- [ ] Does the presentation output fail closed on source loss?
