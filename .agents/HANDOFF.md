# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: P0 Critical Native Crash Recovery — AccessViolationException After Stop Presenting Fixed
- **Date**: 2026-10-16T00:15:00+08:00 (UTC+8)
- **Status**: Completed, Validated & Ready for Local Commit (372 Tests Passing, 0 Build Warnings/Errors)

---

## 1. Objectives & Confirmed Root Causes Resolved

1. **AccessViolationException in Microsoft.WinUI.dll After Stop Presenting**:
   - **Confirmed Root Cause**:
     - `Direct3D11PresentationRenderer.Clear()` previously enqueued an asynchronous delegate `await _softwareBitmapSource.SetBitmapAsync(emptyBitmap)` onto the UI thread.
     - Meanwhile, `PresentationCoordinator.StopPresentationAsync()` called `PresentationWindowService.ClosePresentationWindow()` -> `target.Close()`.
     - `PresentationWindow.xaml` had `Image Source="{x:Bind ViewModel.PresentationImageSource}"` bound to the singleton `_softwareBitmapSource`.
     - Because `PresentationOutputImage.Source` was never cleared before window close, when `SetBitmapAsync` updated the DirectX composition surface on the destroyed native XAML island, `Microsoft.WinUI.dll` crashed with native `AccessViolationException` (`0xC0000005: Attempted to read or write protected memory`).
     - Furthermore, `PresentationVideoPlayer.SetMediaPlayer(null)` was never called, leaving native MediaPlayer swapchains bound to a destroyed control, and `PresentationViewModel` was never disposed on window close.
   - **Remediation**:
     - **Explicit Window Teardown**: Added `Cleanup()` to `PresentationWindow.xaml.cs` (called idempotently on `ClosePresentationWindow` and `Closed`). Detaches `PresentationOutputImage.Source = null`, `PresentationDirectImage.Source = null`, and `PresentationVideoPlayer.SetMediaPlayer(null)`. Unsubscribes `ViewModel.PropertyChanged` and `_settingsService.ThemeChanged`, calls `Bindings.StopTracking()`, and disposes `ViewModel`.
     - **Safe Atomic Generation Clear**: Rewrote `Direct3D11PresentationRenderer.Clear()` to atomically increment `_currentGeneration` and reset `_isProcessingFrame = 0` without queuing asynchronous `SetBitmapAsync(emptyBitmap)` calls into tearing-down visual trees.
     - **Reordered Stop Lifecycle**: In `PresentationCoordinator.ExecuteStopPresentationAsync`:
       1. Cancel camera startup timeout and drain pending camera frames -> stop Camera capture.
       2. Stop Windows Graphics Capture (`StopPreviewAsync`).
       3. Stop PowerPoint live slideshow and clear presentation.
       4. Stop Video and clear Image.
       5. Invalidate render generations (`Clear()`).
       6. Transition presentation status to `Idle`.
       7. Close and clean up `PresentationWindow`.
       8. Activate MainWindow and navigate to Dashboard.
     - **Stop Idempotency**: Guarded `StopPresentationAsync` with `_stopLock` and `_activeStopTask` deduplication. Concurrent calls from Dashboard, Dock, hotkeys, or manual window close await the same single task.
     - **Expected TaskCanceledException Classification**: Explicitly caught `OperationCanceledException` (including `TaskCanceledException`) in `Direct3D11PresentationRenderer` and `Direct3D11PreviewRenderer`, logging as normal lifecycle cancellation without generating error incidents.
     - **Eliminated Duplicate Dashboard Activation**: Fixed Presenter Dock close to only invoke secondary `ShowDashboard()` when closing while idle; when closing while presenting, `StopPresentationAsync` acts as the single authoritative dashboard activator.

---

## 2. Verification Performed

- **Unit & Contract Tests**: 372 passing tests (0 failures, 0 skipped) across all test fixtures.
- **Compiler Gates**:
  - `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> 0 Errors, 0 Warnings.
  - `dotnet build SwitchCast.csproj -c Release -p:Platform=x64` -> 0 Errors, 0 Warnings.
  - `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Debug` -> 372 Passed, 0 Failed.
- **Runtime Verification Note**: Unit and integration test harnesses verified all lifecycle state transitions, event unsubscriptions, and concurrency deduplications. Interactive Windows desktop session was not independently exercised in this headless CLI environment.

---

## 3. Important Files Modified

- `SwitchCast/Views/PresentationWindow.xaml`: Added `x:Name="PresentationOutputImage"` and `x:Name="PresentationDirectImage"` for explicit detachment.
- `SwitchCast/Views/PresentationWindow.xaml.cs`: Added idempotent `Cleanup()` method detaching Image sources, MediaPlayer, compiled bindings, event handlers, and disposing ViewModel.
- `SwitchCast/Services/PresentationWindowService.cs`: Invokes `target.Cleanup()` on `ClosePresentationWindow()` and in `OnWindowClosed()`.
- `SwitchCast/Services/Capture/Direct3D11PresentationRenderer.cs`: Updated `Clear()` to atomically increment generation without racing `SetBitmapAsync`, and handled `OperationCanceledException`.
- `SwitchCast/Services/Capture/Direct3D11PreviewRenderer.cs`: Added `OperationCanceledException` handling across render methods.
- `SwitchCast/Services/PresentationCoordinator.cs`: Reordered stop lifecycle sequence and implemented `_activeStopTask` idempotency deduplication.
- `SwitchCast.Tests/Services/StopLifecycleNativeCrashRecoveryTests.cs` *(New)*: Comprehensive regression test suite covering Stop sequence, source drain order, and concurrency deduplication.

---

## 4. Next Steps for Next Agent

1. Run native stress test scenarios across mixed sources on actual Windows hardware session.
2. Confirm WER / LocalDumps remain clean with 0 AccessViolations during rapid presentation stop cycles.
3. Keep the 0-error and 0-warning compiler gate intact.
