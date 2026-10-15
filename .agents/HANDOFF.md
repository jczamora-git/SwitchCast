# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: PowerPoint and Camera Runtime Hotfix (Issues A, B, C, D)
- **Date**: 2026-10-15T15:45:00+08:00 (UTC+8)
- **Status**: Completed, Validated & Committed Locally (307 Tests Passing)

---

## 1. Objectives & Confirmed Root Causes Resolved

1. **Issue A — PowerPoint Slide Counter Synchronization**:
   - **Root Cause**: `PresentationCoordinator.PowerPointProgressText` previously read `SlideProgressText` from `CurrentPresentationSource` (the static record assigned at presentation start), which was never updated when slides advanced. Furthermore, `PowerPointLiveService` spawned ad-hoc STA threads on every method call, causing COM apartment violations (`RPC_E_WRONG_THREAD` 0x8001010E) and failing to track external slide advances (mouse clicks, keyboard arrows, remote clickers).
   - **Resolution**:
     - `PresentationCoordinator.PowerPointProgressText` now reads directly from the authoritative service (`_powerPointService.CurrentSlideIndex` and `_powerPointService.TotalSlideCount`, or `_powerPointLiveService`).
     - `_powerPointService.SlideChanged` now updates `_presentationStateService.ActiveSource` with the new `CurrentSlideIndex` on every step.
     - `PowerPointLiveService` now maintains a single persistent STA worker thread with an action pump, polling `_slideShowWindow.View.CurrentShowPosition` (which changes only on slide transitions, not intra-slide animation clicks) and raising `SlidePositionChanged`.

2. **Issue B — PowerPoint Source Disappearance**:
   - **Root Cause**: `SourcesViewModel` was registered as `Transient` in DI, causing every tab switch away from Sources to destroy its collections. Decks were not saved to disk settings, and when `RefreshSourcesAsync()` executed, the missing PowerPoint IDs caused `ReconcileAvailability` to mark them unavailable. Unqueueing also risked conflating queue removal with source deletion.
   - **Resolution**:
     - Added `ImportedPowerPointPaths` to `UserSettings`.
     - Registered `SourcesViewModel` as `Singleton` in `App.xaml.cs`.
     - Rehydrates imported PowerPoint decks on startup and refresh from `UserSettings.ImportedPowerPointPaths`.
     - Strictly separates queue state from source library state: unqueueing unchecks `IsSelected = false` and removes from `SelectedSources`, keeping the deck in `_allPowerPoint`. Missing files are shown as `IsAvailable = false` rather than deleted.

3. **Issue C — Camera Display & UI Lag**:
   - **Root Cause**: `CameraCaptureService.OnFrameArrived` used `using var frame = sender.TryAcquireLatestFrame();`, disposing the underlying Direct3D native frame before asynchronous WinUI rendering executed. Webcams delivered frames in NV12 or YUY2, which threw exceptions in `SoftwareBitmapSource.SetBitmapAsync` (which strictly requires `Bgra8` + `Premultiplied`). In addition, unthrottled frame arrivals flooded `DispatcherQueue.TryEnqueue`, lagging the UI.
   - **Resolution**:
     - Introduced an atomic backpressure gate (`Interlocked.CompareExchange(ref _isProcessingFrame, 1, 0)`) in `CameraCaptureService` that drops stale frames when the downstream renderer is busy (latest-frame-wins).
     - Converted raw webcam frames into independent `BitmapPixelFormat.Bgra8` + `BitmapAlphaMode.Premultiplied` `SoftwareBitmap` instances on background threads before passing to the UI thread.
     - Added deterministic disposal of `SoftwareBitmap` in `Direct3D11PresentationRenderer.RenderBitmapAsync` and wired backpressure release (`FrameRenderCompleted`).
     - Added format optimization (~720p/1080p @ 30fps) and resource teardown when stopping or switching.

4. **Issue D — COM Exception Diagnostics & Threading**:
   - **Root Cause**: `PowerPointPresentationService.UpdateSlideImageAsync()` created `new BitmapImage(new Uri(imagePath))` directly on background threads without UI thread dispatch, throwing `COMException` `0x8001010E`. `MediaPresentationService.ClearVideoInternal()` called `_player.Pause()` and `_player.Source = null` off-thread. Exception handlers logged only `ex.Message`, which was frequently empty.
   - **Resolution**:
     - Implemented `SwitchCastDiagnostics.LogException` formatting exception type, hex HRESULT (`0x{ex.HResult:X8}`), thread ID, message, and stack trace.
     - Dispatched `BitmapImage` creation to the UI thread via `DispatcherQueue` in `PowerPointPresentationService`.
     - Dispatched player cleanup in `MediaPresentationService.ClearVideoInternal` via `RunOnUIThread`.

5. **Hotkey Warning (0x00000581)**:
   - Confirmed Win32 error 1409 (`ERROR_HOTKEY_ALREADY_REGISTERED`) is safely recorded in `binding.RegistrationError` without throwing unhandled exceptions or killing other applications.

---

## 2. Validation Results

- **Level 1 (Compilation - Debug)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> **PASS** (0 errors, 0 warnings).
- **Level 1 (Compilation - Release)**: `dotnet build SwitchCast.csproj -c Release -p:Platform=x64` -> **PASS** (0 errors, 0 warnings).
- **Level 2 (Static Analysis)**: Roslyn compiler diagnostics -> **PASS** (0 errors, 0 warnings).
- **Level 3 (Unit & Integration Tests)**: `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Debug` -> **PASS** (307 passed, 0 failed, 0 skipped).
- **Level 4 (Native Verification)**: Automated mocked unit suite passed completely. Interactive hardware (physical webcam, physical multi-monitor, installed PowerPoint application) is categorized as `IMPLEMENTED — RUNTIME UNVERIFIED` until physically exercised on target Windows machines.

---

## 3. Recommended Next Steps

1. Connect physical USB camera (e.g. A4tech FHD 1080P) and verify real-time video feed in Presentation Output.
2. Open live 14-slide PowerPoint deck and advance slides using both Floating Dock and PowerPoint native window to verify real-time counter synchronization.
