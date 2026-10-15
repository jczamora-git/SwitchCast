# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Critical Camera Runtime Hotfix (Camera Starts but Does Not Render / COMException / Stuck STARTING)
- **Date**: 2026-10-15T16:05:00+08:00 (UTC+8)
- **Status**: Completed, Validated & Ready for Local Commit (310 Tests Passing)

---

## 1. Objectives & Confirmed Root Causes Resolved

1. **Catch-22 Deadlock in Camera Presentation Output**:
   - **Root Cause**: `PresentationCoordinator.OnCameraFrameArrived` contained `if (Status != PresentationStatus.Active || !IsActiveSourceCamera)`. When a camera source was started, `Status` was set to `Starting`. The arrival handler rejected and discarded all incoming camera frames because `Status != Active`. Because frames were discarded, no frame reached the renderer; because no frame reached the renderer, `Status` never became `Active`. Consequently, the dock remained permanently stuck at `STARTING...` and the presentation window showed the standby screen.
   - **Resolution**:
     - Updated `OnCameraFrameArrived` to permit frame processing during both `Starting` and `Active` states: `if ((Status != PresentationStatus.Active && Status != PresentationStatus.Starting) || !IsActiveSourceCamera)`.
     - Added transition milestone: when the first camera frame is successfully submitted to the output renderer, `Status` transitions from `Starting` to `Active` (LIVE), and the floating dock immediately updates to `LIVE`.
     - Added an 8-second bounded startup timeout (`ArmCameraStartupTimeout` via `CancellationTokenSource`). If camera hardware fails or freezes without delivering frames, status transitions to `Error` with a clear presenter message rather than hanging indefinitely.

2. **NV12 Pixel Format Conversion & COMException (0x80004005)**:
   - **Root Cause**: Physical webcams (such as the A4tech FHD 1080P PC Camera) stream in NV12 format. Calling WinRT's `SoftwareBitmap.Convert(rawBitmap, Bgra8, Premultiplied)` directly on planar NV12 CPU bitmaps is unsupported by WinRT and throws `COMException` (0x80004005 `E_FAIL` / invalid media type) on every frame (30 times/sec), flooding first-chance debugger exceptions and severely lagging the application.
   - **Resolution**:
     - `CameraCaptureService.StartCaptureAsync` now explicitly requests `MediaEncodingSubtypes.Bgra8` via `CreateFrameReaderAsync(frameSource, MediaEncodingSubtypes.Bgra8)`, allowing Windows Media Foundation's native hardware/pipeline color converter to convert NV12 to BGRA8 natively.
     - Implemented fully managed, exception-free CPU fallback converters (`ConvertNv12ToBgra8` and `ConvertYuy2ToBgra8`) using `CopyToBuffer` and ITU-R BT.601 color-space math, ensuring zero COMExceptions even if native conversion is unavailable.
     - Handled GPU-backed frames: if `SoftwareBitmap` is null and `Direct3DSurface` is present, safely copies via `SoftwareBitmap.CreateCopyFromSurfaceAsync(surface, BitmapAlphaMode.Premultiplied)`.
     - In `Direct3D11PresentationRenderer.RenderBitmapAsync`, added format check guards so `SoftwareBitmap.Convert` is never invoked on non-BGRA8 formats on the UI thread.

3. **Duplicate Transition Storm & Idempotency**:
   - **Root Cause**: Rapid UI actions (e.g. clicking Start Presentation, dock state changes, or queue updates) triggered multiple simultaneous transition requests (`StartPresentation seq=1`, `seq=2`, `SwitchPresentation seq=3`) targeting the same camera source. Earlier sequences were superseded mid-startup, aborting status updates and creating reader restart loops.
   - **Resolution**:
     - Made `StartPresentationAsync`, `SwitchPresentationSourceAsync`, and `ExecuteSourceSwitchAsync` idempotent: if a presentation is already `Starting` or `Active` with the requested target source ID, duplicate start calls are ignored while ensuring the presentation output window remains visible.
     - Unconditionally stop camera capture and cancel pending timeout watchers when switching away from camera to Window, Monitor, Media, or PowerPoint sources.

4. **Structured Unhandled Exception Diagnostics**:
   - **Resolution**: Wired `UnhandledException` in `App.xaml.cs` to log exact hex HRESULT (`0x{ex.HResult:X8}`), exception type, message, and stack trace via `SwitchCastDiagnostics.LogException`.

---

## 2. Validation Results

- **Level 1 (Compilation - Debug)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> **PASS** (0 errors, 0 warnings).
- **Level 1 (Compilation - Release)**: `dotnet build SwitchCast.csproj -c Release -p:Platform=x64` -> **PASS** (0 errors, 0 warnings).
- **Level 2 (Static Analysis)**: Roslyn compiler diagnostics -> **PASS** (0 errors, 0 warnings).
- **Level 3 (Unit & Integration Tests)**: `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Debug` -> **PASS** (310 passed, 0 failed, 0 skipped).
- **Level 4 (Native Verification)**: Automated mocked unit suite passed completely. Interactive hardware validation with physical webcam (A4tech FHD 1080P PC Camera) is marked `IMPLEMENTED — RUNTIME UNVERIFIED` until physically exercised on target Windows host.

---

## 3. Recommended Next Steps

1. Connect physical USB camera (A4tech FHD 1080P PC Camera).
2. Queue Camera and click Start Presenting.
3. Verify dock changes from `STARTING...` to `LIVE`.
4. Verify live video feed renders in Presentation Output.
5. Switch Camera -> Window -> Camera -> PowerPoint -> Stop Presenting.
