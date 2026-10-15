# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Critical PowerPoint & Presenter Dock Regression Hotfix
- **Date**: 2026-10-15T18:55:00+08:00 (UTC+8)
- **Status**: Completed, Validated & Ready for Local Commit (342 Tests Passing)

---

## 1. Objectives & Confirmed Root Causes Resolved

1. **PowerPoint COM Switching Crash (Camera -> PowerPoint Transition)**:
   - **Root Cause**:
     - When switching from Camera to PowerPoint, `CameraCaptureService.StopCaptureInternalAsync` called `_frameReader.Dispose()` and `_mediaCapture.Dispose()` on the deactivation path outside guarded try/catch blocks; native WinRT/COM shutdown exceptions escaped into `SwitchPresentationSourceAsync`.
     - `PowerPointPresentationService.LoadPresentationAsync` lacked slide image availability fallback: if slides had not been pre-rendered, it did not render them before going live, leaving null image paths.
     - `PowerPointPresentationService.UpdateSlideImageAsync` did not check if a DispatcherQueue was configured before attempting operations, potentially creating `BitmapImage` on a worker thread.
     - `PresentationCoordinator.SwitchPresentationSourceAsync` caught exceptions and logged only `error={ex.Message}` without hex HRESULT or stack trace, then rethrew `throw;`.
     - `PresenterDockViewModel.SwitchSourceAsync` lacked try-catch around `await _presentationCoordinator.ExecuteSourceSwitchAsync(source)`, allowing the unhandled task exception to bubble into the message loop and break in `App.xaml.cs`.
   - **Resolution**:
     - Hardened `CameraCaptureService.StopCaptureInternalAsync` with guarded disposal try/catch blocks logging any WinRT/COM shutdown exceptions via `SwitchCastDiagnostics.LogException`.
     - Updated `PowerPointPresentationService.LoadPresentationAsync` to automatically trigger `ImportPresentationAsync(source.FilePath)` if `SlideImagePaths` is empty.
     - Hardened `UpdateSlideImageAsync` in `PowerPointPresentationService` to verify UI `DispatcherQueue` before attempting `BitmapImage` instantiation.
     - Added rich diagnostic logging to `PresentationCoordinator.StartPresentationAsync` and `SwitchPresentationSourceAsync` with hex HRESULT (`0x{ex.HResult:X8}`), managed thread ID, source ID, source type, and stack trace.
     - Guarded all async commands in `PresenterDockViewModel` with `try / catch` logging via `SwitchCastDiagnostics.LogException`.

2. **Centralized Automatic Presenter Dock Startup**:
   - **Root Cause**:
     - `_dockService?.ShowDock()` was called only in `DashboardViewModel.StartPresentationAsync` after an asynchronous `ConfigureAwait(false)` resumption, causing cross-thread window creation exceptions.
     - Other entry points (direct coordinator calls, hotkeys, non-window sources) lacked automatic dock startup.
   - **Resolution**:
     - Injected `IPresenterDockService` and `IApplicationSettingsService` into `PresentationCoordinator`.
     - Centralized dock opening in `PresentationCoordinator.StartPresentationAsync`: opens the existing Dock once when presentation starts for all 6 source types (Window, Display, Image, Video, PowerPoint, Camera) whenever `AutoOpenPresenterDock != false`.
     - Added `SetDispatcherQueue` to `IPresenterDockService` and `PresenterDockService`, wired from `_mainWindow.DispatcherQueue` in `App.xaml.cs`.
     - Implemented UI thread dispatch marshaling in `PresenterDockService.ShowDock()`, `CloseDock()`, and `ToggleDock()`.
     - Preserved manual close behavior: source switches do not reopen a manually closed dock; stopping and starting a new presentation reopens the dock.

3. **Floating Dock PowerPoint Controls & Empty Black Secondary Row**:
   - **Root Cause**:
     - In `PresenterDockWindow.xaml.cs`, `ViewModel` was assigned *after* `InitializeComponent()`. When `InitializeComponent()` executed `this.Bindings.Initialize()`, `this.ViewModel` was `null`, so WinUI compiled bindings (`x:Bind`) never attached child listeners to `ViewModel.PropertyChanged`.
     - In `PresentationCoordinator.cs`, property changed handler on `PresentationStateService` notified media properties but omitted `IsActiveSourcePowerPoint`, `IsActiveSourceCamera`, and `PowerPointProgressText`.
     - Code-behind in `PresenterDockWindow.xaml.cs` listened to `HasSecondaryRow` and resized `_appWindow` to 86 DIPs, but `PowerPointRowGrid.Visibility` remained `Collapsed` because `x:Bind` was disconnected, leaving a black empty gap.
   - **Resolution**:
     - Moved `ViewModel` assignment and `ViewModel.SetDispatcherQueue(DispatcherQueue)` before `InitializeComponent()` in `PresenterDockWindow.xaml.cs`, and called `Bindings.Update()` immediately after.
     - Added direct typed properties `PowerPointRowVisibility` and `VideoPlaybackRowVisibility` to `PresenterDockViewModel` and bound them directly in `PresenterDockWindow.xaml`.
     - Updated `PresenterDockWindow.OnViewModelPropertyChanged` to trigger `Bindings.Update()` and dynamic window sizing upon changes to `HasSecondaryRow`, `IsActiveSourcePowerPoint`, `IsActiveSourceVideo`, `PowerPointRowVisibility`, or `VideoPlaybackRowVisibility`.
     - Raised `IsActiveSourcePowerPoint`, `IsActiveSourceCamera`, `PowerPointProgressText`, and `DirectImageSource` in `PresentationCoordinator` on `ActiveSource` change.
     - Updated `PresentationViewModel.NotifyAllLayers` to notify `DirectImageSource`, `PresentationImageSource`, and `MediaPlayer`.

---

## 2. Validation Results

- **Level 1 (Compilation - Debug)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> **PASS** (0 errors, 0 warnings).
- **Level 1 (Compilation - Release)**: `dotnet build SwitchCast.csproj -c Release -p:Platform=x64` -> **PASS** (0 errors, 0 warnings).
- **Level 2 (Static Analysis)**: Roslyn compiler diagnostics -> **PASS** (0 errors, 0 warnings).
- **Level 3 (Unit & Integration Tests)**: `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Debug` -> **PASS** (342 passed, 0 failed, 0 skipped, 16 new tests in `PowerPointDockRegressionTests.cs`).
- **Level 4 (Native Verification)**: Automated mocked unit suite passed completely. Interactive hardware validation with physical webcam, PowerPoint Live / COM, and display outputs is marked `IMPLEMENTED — RUNTIME UNVERIFIED` until physically exercised on target Windows host.

---

## 3. Recommended Next Steps

1. Queue sources: Window, PowerPoint, Camera, Video.
2. Click "Start Presenting". Verify that Presenter Dock opens automatically with the active source.
3. Switch from Camera to PowerPoint: verify no COMException, slide renders cleanly, and PowerPoint slide controls ([Previous Slide] [Next Slide] Slide 1 of N) appear without an empty black gap.
4. Close Presenter Dock with the X button: verify Dashboard activates, presentation remains live.
5. Switch sources from Dashboard: verify Presenter Dock remains closed.
6. Stop presentation and start again: verify Presenter Dock opens automatically.
