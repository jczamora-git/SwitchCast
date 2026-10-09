# ADR-0001: Core Desktop Architecture & Technology Stack

- **Status**: **Accepted (for Phase 0 / Target Architecture)**
- **Date**: 2026-10-08
- **Deciders**: Principal Software Architect, Senior Desktop Engineer
- **Consulted**: Security, QA, Release Engineers

---

## 1. CONTEXT & PROBLEM STATEMENT

SwitchCast is a high-performance Windows desktop application designed for real-time presentation source switching across running windows and connected monitors during virtual meetings (Zoom, Microsoft Teams, Google Meet).

The solution requires:
1. Low-latency, hardware-accelerated screen capture on modern Windows 10/11.
2. High-performance rendering pipeline capable of 60 FPS swapchain presentation with minimal CPU/GPU overhead.
3. Decoupled two-window presentation model (Private Control Dashboard vs. Shareable Presentation Output Window).
4. Modern, accessible, DPI-aware desktop user interface with light/dark theme support.
5. Strict local-only, zero-telemetry, privacy-first guarantees.

---

## 2. DECISION

We adopt the following technology stack and architectural patterns:
1. **Primary Language & Runtime**: C# on **.NET 8.0+** (`net8.0-windows10.0.19041.0`).
2. **UI Framework**: **WinUI 3** via the **Windows App SDK**.
3. **Capture Subsystem**: **`Windows.Graphics.Capture` (WGC)** utilizing WinRT interop (`IGraphicsCaptureItemInterop`) for window and monitor capture.
4. **Rendering Subsystem**: **Direct3D 11** swapchain presentation rendered directly into a WinUI 3 `SwapChainPanel` control.
5. **Presentation Pattern**: **MVVM (Model-View-ViewModel)** powered by `CommunityToolkit.Mvvm`.
6. **Application Architecture**: **Clean Architecture** with interface-isolated platform dependencies (`SwitchCast.Core`, `SwitchCast.Application`, `SwitchCast.Infrastructure.Windows`, `SwitchCast.Desktop`).

---

## 3. ALTERNATIVES CONSIDERED

| Alternative | Evaluation & Tradeoffs | Verdict |
| :--- | :--- | :--- |
| **Electron / Web Tech** | High memory usage (>200MB idle), heavy CPU overhead for 60 FPS video canvas rendering, poor native Win32/D3D11 swapchain interop. | **Rejected** |
| **WPF (.NET 8)** | Mature and stable, but lacks native modern composition, requires legacy Win32 interop wrappers for D3D11 swapchains (`D3DImage`), and lacks modern Fluent controls. | **Rejected** |
| **Windows Forms (.NET 4.7.2)** | Legacy framework present in initial repo stub. Inefficient for Direct3D swapchain composition, poor multi-monitor DPI handling, outdated UI paradigm. | **Rejected (Migrating in Phase 1)** |
| **WinUI 3 / Windows App SDK** | Native DirectX composition, official modern Windows UI stack, native WinRT `Windows.Graphics.Capture` integration, excellent high-DPI handling, active Microsoft support. | **Accepted** |

---

## 4. ADVANTAGES & DISADVANTAGES

### Advantages:
- **Maximum Performance**: Direct GPU texture sharing from `Direct3D11CaptureFramePool` directly to Direct3D 11 swapchain backbuffers eliminates CPU memory copies.
- **Modern UX**: Native Windows 11 Fluent Design, system dark/light mode responsiveness, and dynamic multi-monitor DPI awareness.
- **Privacy & Security**: Built-in support for `SetWindowDisplayAffinity` and OS-level capture exclusion.
- **Testability**: Clean architecture separation allows domain state machines and coordinators to be unit-tested without launching WinUI or GPU hardware.

### Disadvantages & Tradeoffs:
- Requires Windows 10 version 1903 (build 18362) or later (acceptable for modern enterprise desktop targets).
- Direct3D 11 interop and WinRT interop require careful native resource lifetime and COM cleanup.

---

## 5. REPOSITORY STATUS VS TARGET ARCHITECTURE

- **Confirmed Repository Fact**: The repository currently contains a legacy .NET Framework 4.7.2 Windows Forms template (`SwitchCast.csproj`, `Form1.cs`).
- **Architectural Migration Plan**: In Phase 1, this legacy stub will be cleanly migrated/upgraded to a modern .NET 8 WinUI 3 Windows App SDK solution without affecting the established Phase 0 harness.

---

## 6. UNRESOLVED CHOICES & OPEN QUESTIONS

1. **Packaging Strategy**: MSIX packaging vs. Unpackaged / Self-contained desktop executable. Both will be evaluated in Phase 8 based on ease of distribution.
2. **Virtual Camera Driver**: Future consideration for Phase 7+ (whether to include DirectShow virtual camera filters alongside the output window).
