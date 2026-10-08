# SWITCHCAST — AGENT HANDOFF RECORD
 
---
 
## Task Details
- **Task**: Phase 4.7 — Rapid Source Switching Crash Fix & Concurrency Hardening
- **Date**: 2026-10-08T22:30:00+08:00 (UTC+8)
- **Status**: Completed
 
---
 
## 1. Objective
Fix the reproducible WinRT stowed exception crash (`0xC000027B` / process exit code `3221226107`) triggered during rapid switching between capture sources in SwitchCast, while preserving the Phase 4.6 performance optimizations, keeping the Presentation Output window HWND stable, and ensuring the Latest-Request-Wins policy.

---

## 2. Root Cause Analysis
1. **Premature `emptyBitmap` Disposal in `Clear()`**:
   - `Clear()` in `Direct3D11PreviewRenderer` and `Direct3D11PresentationRenderer` had `using var emptyBitmap = new SoftwareBitmap(...); _ = _softwareBitmapSource.SetBitmapAsync(emptyBitmap);`.
   - The C# `using` statement immediately disposed the native COM `SoftwareBitmap` when leaving the block, while `SetBitmapAsync` was still asynchronously accessing the COM object on the compositor thread. This triggered an unhandled WinRT `RO_E_CLOSED` stowed exception (`0xC000027B`), crashing the process during rapid transitions.
2. **Cross-Thread WinRT Disposal in `Dispose()`**:
   - `SoftwareBitmapSource.Dispose()` was called directly on worker/disposal threads instead of dispatching to the WinUI `DispatcherQueue`, causing cross-apartment COM state violations.
3. **Async Void Exception Leakage in Capture Event Handlers**:
   - `CaptureCoordinator.OnFrameArrived` and `PresentationCoordinator.OnCaptureFrameArrived` were `async void` methods that could leak exceptions directly to the UI `SynchronizationContext`.
4. **Intermediate Native Session Churn & Resource Races**:
   - Rapidly switching A -> B -> C created and destroyed Direct3D capture frame pools and sessions in overlapping succession before previous GPU allocations and WinRT async tasks settled.

---

## 3. Solutions Applied
1. **Safe `Clear()` and `Dispose()` Lifetime Management**:
   - In both renderers, `Clear()` now properly awaits `SetBitmapAsync(emptyBitmap)` inside the UI delegate before disposing `emptyBitmap`.
   - `SoftwareBitmapSource.Dispose()` is strictly scheduled onto the UI thread via `_dispatcherQueue.TryEnqueue`.
2. **Latest-Request-Wins Coalescing**:
   - Implemented `_transitionSequenceNumber` and `_targetRequestedSource` coalescing in `CaptureCoordinator` and `PresentationCoordinator`.
   - Superseded intermediate switch requests immediately exit upon acquiring `_transitionSemaphore`, preventing redundant native session teardowns and creations.
3. **Synchronous Exception Boundaries in Event Handlers**:
   - Converted `OnFrameArrived` and `OnCaptureFrameArrived` to synchronous `void` methods with strict top-level try/catch boundaries to completely prevent unobserved async exceptions.
4. **Session Generation Filtering in Renderers**:
   - Added `long generation = 0` parameters to `RenderSharedBitmapAsync` in `ICapturePreviewRenderer` and `IPresentationOutputRenderer` to immediately drop stale in-flight UI frame renders arriving after a session transition.
5. **16 New Automated Concurrency Regression Tests**:
   - Added [SwitchCast.Tests/Services/RapidSwitchingConcurrencyTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/RapidSwitchingConcurrencyTests.cs) covering normal switching, rapid A->B->C coalescing, concurrent switch requests, rendering in-flight, queued dispatcher callbacks, pause/blackout preservation, and error handling.

---

## 4. Files Modified / Created

### Core Pipeline & Services
- [Services/Capture/ICapturePreviewRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/ICapturePreviewRenderer.cs)
- [Services/Capture/IPresentationOutputRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/IPresentationOutputRenderer.cs)
- [Services/Capture/Direct3D11PreviewRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PreviewRenderer.cs)
- [Services/Capture/Direct3D11PresentationRenderer.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/Direct3D11PresentationRenderer.cs)
- [Services/Capture/CaptureCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Capture/CaptureCoordinator.cs)
- [Services/PresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationCoordinator.cs)

### Automated Test Suite
- [SwitchCast.Tests/Services/RapidSwitchingConcurrencyTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/RapidSwitchingConcurrencyTests.cs) *(New - 16 tests)*
- [SwitchCast.Tests/Services/CaptureCoordinatorTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/CaptureCoordinatorTests.cs)
- [SwitchCast.Tests/Services/PresentationCoordinatorTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresentationCoordinatorTests.cs)

---

## 5. Validation Performed
- **Level 1 (Build)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 4.0s).
- **Level 2 (Static Analysis)**: Analyzers and nullable reference checks -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (86 passed, 0 failed, 0 skipped in 418ms).
- **Level 4 (Deterministic Lifecycle & Concurrency Hardening)**: Verified Latest-Request-Wins request coalescing, zero COM object premature disposal, and session generation validation.

---

## 6. Next Steps
- **Next Task**: **Phase 5 — Global Hotkeys & Presentation Switching Controls**
- Implement native Win32 `RegisterHotKey` hooks for background keyboard shortcuts (switching between queued sources 1-9, toggle pause, and instant blackout) when SwitchCast is in the background.
