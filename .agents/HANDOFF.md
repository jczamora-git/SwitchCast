# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Capture Engine Recovery, Frame Ownership Repair & Architecture Stabilization
- **Date**: 2026-10-08T21:05:00+08:00 (UTC+8)
- **Status**: Completed

---

## 1. Objective
Eliminate the black-screen preview/output and application-freezing defects in SwitchCast's capture pipeline. Study two working reference C# WinForms capture applications (AutoSnap and Jeizi OCR) to adapt proven frame buffering, synchronization, and Win32 GDI fallback patterns while strictly maintaining SwitchCast's native WinUI 3 architecture.

---

## 2. Root Cause Analysis
- **Premature Native Frame Disposal**: In `CaptureSessionManager.cs`, `OnFrameArrived` acquired the frame in a synchronous `using var frame = sender.TryGetNextFrame();` block. Because event handlers in `CaptureCoordinator` and `PresentationCoordinator` initiate asynchronous operations (`SoftwareBitmap.CreateCopyFromSurfaceAsync`), the `FrameArrived` handler returned immediately at the first `await`.
- The synchronous block then disposed the `Direct3D11CaptureFrame`, instantly destroying the underlying `IDirect3DSurface` COM object while the asynchronous GPU surface copy was in progress.
- This triggered repeated `ObjectDisposedException`, `ArgumentException`, and `TaskCanceledException` storms (30-60/sec) in `WinRT.Runtime.dll`, starved the UI thread dispatcher, and left `SoftwareBitmapSource` empty/black.
- **Premature Status Reporting**: `PresentationCoordinator` was setting `PresentationStatus.Active` ("Presenting Live") before the first frame was ever acquired or rendered.

---

## 3. Reference Architecture Analysis & Solutions Applied
1. **AutoSnap (`WindowCaptureSource.cs`)**:
   - Analyzed GDI `PrintWindow` with `PW_RENDERFULLCONTENT` (0x02) and `BitBlt` fallbacks.
   - Created `IWin32DiagnosticCaptureService` / `Win32DiagnosticCaptureService` providing fail-safe standalone single-frame capture converting GDI DIB sections directly to `SoftwareBitmap`.
2. **Jeizi OCR (`WindowCaptureService.cs`)**:
   - Analyzed synchronized latest-frame buffer and producer/consumer ownership isolation.
   - Refactored `CaptureSessionManager` to hold `Direct3D11CaptureFrame` alive during `SoftwareBitmap.CreateCopyFromSurfaceAsync(surface)` and dispose it deterministically only after the surface copy completes.
   - Added `Interlocked.CompareExchange` backpressure pacing with frame draining to prevent threadpool starvation.
   - Updated `ICapturePreviewRenderer` and `IPresentationOutputRenderer` with `RenderBitmapAsync(SoftwareBitmap)` so both renderers receive owned, valid bitmaps without competing GPU reads.

---

## 4. Files Modified / Created

### Interfaces & Pipeline Services
- [Services/Capture/IWin32DiagnosticCaptureService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/IWin32DiagnosticCaptureService.cs) *(New)*
- [Services/Capture/Win32DiagnosticCaptureService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Win32DiagnosticCaptureService.cs) *(New)*
- [Services/Capture/Interop/NativeCaptureMethods.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Interop/NativeCaptureMethods.cs)
- [Services/Capture/CaptureSessionManager.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/CaptureSessionManager.cs)
- [Services/Capture/FrameArrivedEventArgs.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/FrameArrivedEventArgs.cs)
- [Services/Capture/ICapturePreviewRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/ICapturePreviewRenderer.cs)
- [Services/Capture/Direct3D11PreviewRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PreviewRenderer.cs)
- [Services/Capture/IPresentationOutputRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/IPresentationOutputRenderer.cs)
- [Services/Capture/Direct3D11PresentationRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PresentationRenderer.cs)
- [Services/Capture/CaptureCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/CaptureCoordinator.cs)
- [Services/PresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationCoordinator.cs)
- [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs)

### Automated Test Suite
- [SwitchCast.Tests/Services/Win32DiagnosticCaptureServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/Win32DiagnosticCaptureServiceTests.cs) *(New)*
- [SwitchCast.Tests/Services/CaptureCoordinatorTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/CaptureCoordinatorTests.cs)
- [SwitchCast.Tests/SwitchCast.Tests.csproj](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/SwitchCast.Tests.csproj)

---

## 5. Validation Performed
- **Level 1 (Build)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 2.8s).
- **Level 2 (Static Analysis)**: Analyzers and nullable reference checks -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (61 passed, 0 failed, 0 skipped in 246ms).
- **Level 4 (Native Interop & Capture Lifecycle)**: Deterministic frame disposal, fail-safe Win32 diagnostic capture, and thread-safe UI bitmap distribution verified.

---

## 6. Next Steps
- **Next Task**: **Phase 5 — Global Hotkeys & Presentation Switching Controls**
- Implement native Win32 `RegisterHotKey` hooks for background keyboard shortcuts (switching between queued sources 1-9, toggle pause, and instant blackout) when SwitchCast is in the background.

