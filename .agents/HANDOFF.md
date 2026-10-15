# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Full Stability & Regression Recovery Pass
- **Date**: 2026-10-15T20:30:00+08:00 (UTC+8)
- **Status**: Completed, Validated & Ready for Local Commit (366 Tests Passing, 0 Build Warnings/Errors)

---

## 1. Objectives & Confirmed Root Causes Resolved

1. **Stop Presenting & Dock X Confirmation Lifecycle Contract (Actions A - D)**:
   - **Action A (Stop Presenting)**: Fully stops active capture, video, camera, and slideshows; clears and closes the Presentation Output window; restores and activates `MainWindow`; navigates to the Control Dashboard; preserves process and queue data.
   - **Action B (Dock X Confirmation)**:
     - *Active Presentation*: When the presenter clicks X on the Floating Dock while a presentation is running (or paused/blackout), a custom confirmation dialog is presented:
       - Title: "Stop presentation and close Dock?"
       - Message: "A presentation is currently running. Closing the Presenter Dock will also stop the presentation and close Presentation Output."
       - Actions: "Cancel" (keeps Dock and presentation running) and "Stop & Close" (stops presentation via `StopPresentationAsync`, closes output window, closes dock, activates Dashboard).
       - Window size dynamically expands to 210 DIPs during confirmation to prevent dialog clipping caused by `WM_GETMINMAXINFO` native window constraints.
     - *Idle*: When no presentation is active, clicking X immediately closes the Dock without confirmation and activates the Control Dashboard.
   - **Action C (Application Shutdown)**: MainWindow exit confirmation coordinates clean termination; secondary dock confirmation is bypassed, Dashboard is never reactivated, and all resources are properly disposed.
   - **Action D (Show/Hide Hotkey)**: Hotkey dock toggle remains strictly non-destructive; it never triggers presentation stop or close confirmation.

2. **Crash & Hang Diagnostics with Synchronous Critical Flush & UI Hang Watchdog**:
   - **Synchronous Critical Flush**: Added `FlushCriticalIncident` to `IErrorReportingService` and `ErrorReportingService` (and `LogCritical` to `SwitchCastDiagnostics`). When `AppDomain.UnhandledException` fires with `e.IsTerminating == true`, incident files are written synchronously directly to disk, preventing process termination before background thread-pool tasks execute.
   - **Proactive UI Hang Watchdog**: Added `UIHangWatchdog` monitoring WinUI `DispatcherQueue` message loop responsiveness on an independent thread-pool timer. Emits warning diagnostics at 5s unresponsive and critical deadlock incident reports at 15s.
   - **Startup Crash Banner**: Added `GetPreviousCrashReports()` in `IErrorReportingService` and checked upon Dashboard initialization. An InfoBar appears if an unacknowledged crash incident exists, allowing the user to inspect it in `ErrorReportDialog` or dismiss it.

3. **Camera Frame Task Observation & Bounded Latest-Frame-Wins Loop**:
   - Replaced unobserved `_ = _outputRenderer.RenderBitmapAsync(bitmap)` in `PresentationCoordinator.OnCameraFrameArrived`.
   - Implemented a bounded asynchronous rendering loop: if a frame is currently rendering, subsequent frames update `_pendingCameraFrame` while discarding and disposing older unrendered frames.
   - Dispatches `_cameraCaptureService.NotifyFrameCompleted()` on both success and discard paths, maintaining proper backpressure.
   - Transition to `PresentationStatus.Active` occurs only after the first camera frame is rendered successfully.

4. **PowerPoint Repeated-Switch Reliability & Slide Index Preservation**:
   - Added `UpdateSelectedSource(CaptureSource source)` in `IPresentationStateService` / `PresentationStateService`.
   - `PresentationCoordinator` synchronizes slide position changes to `SelectedSources` so returning to PowerPoint after switching to Video and Camera restores the exact slide index.
   - Eliminated race condition between lingering camera frames and PowerPoint source switches by discarding pending camera frames before beginning source transitions.

5. **Video Playback Performance & Stutter Prevention**:
   - Throttled high-frequency `MediaPlaybackSession.PositionChanged` events in `MediaPresentationService` to at most once per 250ms, eliminating UI message loop starvation caused by 60fps position callbacks.

---

## 2. Verification Performed

- **Unit & Contract Tests**: 366 passing tests (0 failures, 0 skipped) across all test fixtures, including new executable regression contracts in `FullStabilityRegressionContractTests.cs`.
- **Compiler Gates**:
  - `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> 0 Errors, 0 Warnings.
  - `dotnet build SwitchCast.csproj -c Release -p:Platform=x64` -> 0 Errors, 0 Warnings.
  - `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Debug` -> 366 Passed, 0 Failed.
- **Runtime Verification Note**: Desktop GUI testing executed via unit test harnesses; interactive Windows desktop session was not independently exercised in this headless CLI environment.

---

## 3. Important Files Modified

- `SwitchCast/ViewModels/PresenterDockViewModel.cs`: Lifecycle close confirmation logic, `ConfirmCloseAsync` delegate, `CloseDockAsync()`.
- `SwitchCast/Views/PresenterDockWindow.xaml.cs`: Native dialog sizing, inline confirmation dialog handling, `AppWindow.Closing` interception.
- `SwitchCast/Services/Diagnostics/IErrorReportingService.cs` & `ErrorReportingService.cs`: `FlushCriticalIncident`, `GetPreviousCrashReports`, critical synchronous persistence.
- `SwitchCast/Services/Diagnostics/SwitchCastDiagnostics.cs`: `LogCritical` method with immediate synchronous write.
- `SwitchCast/Services/Diagnostics/UIHangWatchdog.cs`: Independent background watchdog monitoring UI dispatcher queue heartbeats.
- `SwitchCast/ViewModels/DashboardViewModel.cs`: Startup previous-crash detection and banner management.
- `SwitchCast/Views/DashboardPage.xaml` & `DashboardPage.xaml.cs`: Crash notification InfoBar and error dialog invocation.
- `SwitchCast/Services/Capture/Direct3D11PresentationRenderer.cs`: Asynchronous `RenderBitmapAsync` task completion tracking.
- `SwitchCast/Services/IPresentationStateService.cs` & `PresentationStateService.cs`: `UpdateSelectedSource` for queue synchronization.
- `SwitchCast/Services/PresentationCoordinator.cs`: Bounded camera frame delivery loop, PowerPoint slide state retention, switch race prevention.
- `SwitchCast/Services/Media/MediaPresentationService.cs`: 250ms throttling on `PositionChanged` events.
- `SwitchCast/App.xaml.cs`: Synchronous critical crash flush on terminating unhandled exceptions, `UIHangWatchdog` instantiation.
- `SwitchCast.Tests/Services/FullStabilityRegressionContractTests.cs`: Executable behavior contracts covering Scenarios A-E.
- `SwitchCast.Tests/Services/ErrorReportingServiceTests.cs`: Verification of critical flush and crash report retrieval.
- `SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs`: Unit test coverage for Dock X confirmation vs idle close.

---

## 4. Next Steps for Next Agent

1. Execute manual / native interactive smoke tests on real Windows hardware with webcam, PowerPoint, and MP4 files when desktop GUI session is available.
2. Maintain the regression behavior contracts in `FullStabilityRegressionContractTests.cs` for any subsequent PRs.
3. Keep the 0-error and 0-warning compiler gate intact.
