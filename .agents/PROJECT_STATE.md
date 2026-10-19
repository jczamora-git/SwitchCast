# SWITCHCAST — PROJECT STATE

This is the authoritative progress, state, and environmental tracking document for SwitchCast. All AI agents must consult and update this file before and after executing tasks.

---

## 1. EXECUTIVE SUMMARY

- **Project**: SwitchCast
- **Current Phase**: OBS-Inspired Lightweight Performance Audit (Architecture Audit, Fallback Hierarchy & Empirical Benchmark Plan Defined in `docs/PERFORMANCE_OPTIMIZATION_PLAN.md`)
- **Overall Status**: **Audited & Validated (411 Automated Unit & Contract Tests Passing, Debug & Release x64 Builds Clean [0 warnings, 0 errors], No Invasive Code Changes Applied)**
- **Last Updated**: 2026-10-19T21:10:00+08:00 (UTC+8)

---

## 2. FUNCTIONALITY STATUS

### Implemented & Verified
- [x] **Mandatory Dual-Player Isolation Guards (Video MediaPlayer vs Background Music MediaPlayer)**:
  - **Strict Service Isolation**: `MediaPresentationService` exclusively owns its dedicated `MediaPlayer` for presentation output video; `BackgroundMusicService` exclusively owns its dedicated `MediaPlayer` for background music. Neither service shares instances, mutable state, or disposal.
  - **Command Ownership**: SwitchDeck and Dashboard video commands (`ToggleVideoPlaybackCommand`, `RestartVideoCommand`, `SeekBackward10Command`, `SeekForward10Command`, `ToggleMediaMuteCommand`) exclusively control `MediaPresentationService`. SwitchDeck music commands (`ToggleBackgroundMusicPlayPauseCommand`, `NextBackgroundMusicTrackCommand`, `PreviousBackgroundMusicTrackCommand`, `ToggleBackgroundMusicShuffleCommand`, `CycleBackgroundMusicRepeatModeCommand`, `ToggleBackgroundMusicMuteCommand`) exclusively control `BackgroundMusicService`. PowerPoint and source switching commands remain strictly separate.
  - **Independent State and Settings**: Video properties (`IsVideoPlaying`, `IsVideoPaused`, `IsVideoEnded`, `Position`, `Duration`, `Volume`, `IsMuted`) and Music properties (`IsPlaying`, `IsPaused`, `CurrentTrack`, `CurrentPlaylist`, `RepeatMode`, `IsShuffleEnabled`, `Position`, `Duration`, `Volume`, `IsMuted`) use independent events and notifications. Video volume/mute persists via `settings.json`; music volume/mute/playlist persists via `playlists.json`. Added explicit isolated helper methods to `IApplicationSettingsService`.
  - **Independent Playback Lifecycle**: Source switching (`Window -> Video -> PowerPoint -> Camera -> Video`) never stops, resets, or recreates the music player; music track changes never reset On Air presentation. Video ended never advances music; music ended never advances presentation sources. The only intentional coordination is the explicit `StopPresentationAsync` session-stop policy.
  - **Player Lifetime Safety**: Video player and Music player dispose independently with zero shared `MediaSource`, no stale callbacks, no cross-thread XAML notifications, and no double disposal.
  - **SwitchDeck UI Isolation**: Background Music controls open in their own contextual popup available across all sources, preserving the compact main Dock and source-specific secondary control rows (Video playback row, PowerPoint slide row).
  - **Mandatory 16-Test Regression Matrix**: Implemented and verified in `SwitchCast.Tests/Services/DualPlayerIsolationTests.cs` (TEST 1 through TEST 16 all passing). Total test count increased from 395 to 411.
- [x] **P0 Startup Termination Resolution — COMException (0x80004005) in WinRT.Runtime.dll Fixed**:
  - **Runtime Evidence & Forensic Investigation**:
    - `MainWindow` successfully initialized (`HWND=0x70DDC | PID=12136 | Thread=1`).
    - Immediately after, `MainWindow.Activate()` and `OnNavViewLoaded` navigated to `DashboardPage`.
    - WinRT threw unhandled `System.Runtime.InteropServices.COMException` (`HResult=0x80004005 (E_FAIL / Unspecified error)`), crashing `SwitchCast.exe` with exit code `0xFFFFFFFF (-1)`.
    - Stack trace captured via newly hardened synchronous incident flush:
      ```
      at ABI.System.Collections.Generic.IDictionaryMethods`2.Indexer_Get(IObjectReference obj, Dictionary`2 __lookupCache, K key)
      at Microsoft.UI.Xaml.ResourceDictionary.get_Item(Object key)
      at SwitchCast.Views.DashboardPage.DashboardPage_obj1_Bindings.LookupConverter(String key)
      at SwitchCast.Views.DashboardPage.DashboardPage_obj1_Bindings.Update_ViewModel_IsMusicShuffleEnabled(Boolean obj, Int32 phase)
      at SwitchCast.Views.DashboardPage.DashboardPage_obj1_Bindings.Initialize()
      at SwitchCast.Views.DashboardPage.DashboardPage_obj1_Bindings.Loading(FrameworkElement src, Object data)
      ```
  - **Confirmed Root Cause**:
    - In commit `8af982d`, `Views/DashboardPage.xaml` referenced `{StaticResource BoolToAccentBrushConverter}` and `{StaticResource InvertedBoolToVisibilityConverter}` without registering them in `App.xaml` or `Page.Resources`.
    - When WinUI 3 XAML bindings initialized on page loading on the ASTA UI thread, `DashboardPage_obj1_Bindings.LookupConverter` queried native COM `ResourceDictionary::Lookup` for `"BoolToAccentBrushConverter"`. Missing resource lookups throw native COM `E_FAIL` (`0x80004005`).
    - Because WinUI `App.OnUnhandledException` previously logged using asynchronous background tasks (`LogException`), fast process termination prevented the exception details from being written to disk.
  - **Remediation**:
    - **Eliminated Missing Dynamic Converters**: Replaced dynamic string-based resource lookups in `DashboardPage.xaml` with direct ViewModel properties:
      - Added `EmptyPlaylistVisibility` and `PlaylistTracksVisibility` to `DashboardViewModel`.
      - Added `MusicShuffleOpacity` and `MusicRepeatOpacity` to `DashboardViewModel` with `ThemeResource AppAccentBrush`.
      - Replaced `InvertedBoolToVisibilityConverter` in `DataTemplate` with a statically-qualified compile-time method binding `{x:Bind views:DashboardPage.InvertBoolToVisibility(IsAvailable), Mode=OneWay}`.
    - **Synchronous Critical Incident Log Flushing**:
      - Updated `App.OnUnhandledException` to call `SwitchCastDiagnostics.LogCritical(...)` instead of `LogException`.
      - Hardened `ErrorReportingService.FlushCriticalIncident(...)` to synchronously append the full exception type, hex HRESULT, message, thread ID, and stack trace to the daily log file before returning, guaranteeing diagnostic capture during unhandled terminating crashes.
  - **Validation & Native Testing**:
    - 3-cycle automated native launch, verification, and clean close loop passed without errors.
    - Native Release x64 binary execution verified: `PID=24940, HWND=462294, Responding=True`.
    - Full automated test suite: **395 tests passing (0 failures, 0 skipped)**.
- [x] **SwitchDeck Independent Background Music Playlists & Cursor Controls**:
  - Independent audio playlist management (Create, Rename, Delete, Reorder, Import audio files) via `BackgroundMusicService`.
  - Floating SwitchDeck mini music player and expanded menu popup.
  - Presentation cursor visibility toggle (`ShowPresentationCursor`) synchronized between settings, capture sessions (`ICaptureSessionManager.IsCursorCaptureEnabled`), and audience-facing presentation output window (`WM_SETCURSOR` client subclassing).
- [x] **Critical Dock Close & Application Exit Hotfix (MainWindow Preservation & AccessViolationException Fix)**:
  - **Confirmed Root Causes**:
    1. **Synchronous Task Continuation Reentrancy & Window Destruction Order Inversion**: In `PresenterDockConfirmationWindow.xaml.cs`, `Dismiss(bool confirmed)` called `_tcs?.TrySetResult(confirmed)` before `Close()`. Because `_tcs` was instantiated without `TaskCreationOptions.RunContinuationsAsynchronously`, setting the result executed continuations inline synchronously on the UI thread. The awaiting method `CloseDockAsync` immediately called `StopPresentationAsync` and then `PresenterDockService.CloseDock()`, which destroyed `PresenterDockWindow`.
    2. **Win32 GWLP_HWNDPARENT Cross-Window Ownership Tearing**: `PresenterDockConfirmationWindow` was parented/owned by `PresenterDockWindow` via `SetWindowLongPtr(GWLP_HWNDPARENT, ownerHwnd)`. In Win32, destroying an owner window causes Windows to automatically destroy all owned child windows. When the call stack unwound back to `Dismiss()` in `PresenterDockConfirmationWindow` and called `Close()`, WinUI 3 attempted to dismantle an already-destroyed XAML Island / HWND, throwing native `AccessViolationException` (`0xC0000005`) in `Microsoft.WinUI.dll` on `Window.Close()`.
    3. **Process Crash Appeared as Unintended Application Exit**: Because `AccessViolationException` in .NET 8 is a corrupted process state exception, it bypassed standard `try { Close(); } catch {}` blocks and immediately crashed `SwitchCast.exe`, creating the perception that clicking "Stop & Close" closed the entire application.
    4. **Duplicate MainWindow Activations**: `StopPresentationAsync` authoritatively activated `MainWindow` and navigated to Dashboard. Right after, `PresenterDockViewModel.CloseDockAsync` invoked `_dockService.ShowDashboard()`, which triggered a second, redundant `ActivateMainWindow()` call on `App.xaml.cs`.
  - **Remediation**:
    - **Removed Win32 `GWLP_HWNDPARENT`**: Decoupled `PresenterDockConfirmationWindow` from native window ownership coupling. Topmost z-order is safely and independently maintained via `OverlappedPresenter.IsAlwaysOnTop = true` and `WS_EX_TOOLWINDOW`.
    - **Clean Dialog Self-Closing Before Task Resolution**: In `Dismiss(bool confirmed)`, the confirmation window immediately logs the request and calls `Close()`. On the `Closed` event, `_tcs.TrySetResult(_userConfirmed)` is signaled with `TaskCreationOptions.RunContinuationsAsynchronously`, guaranteeing the confirmation window is completely closed and dismantled before `StopPresentationAsync` or `CloseDock` commences.
    - **Single Authoritative Dashboard Activation**: Removed the redundant `_dockService.ShowDashboard()` call from `PresenterDockViewModel.CloseDockAsync` when an active presentation is being stopped. `StopPresentationAsync` authoritatively activates `MainWindow` and navigates to the Dashboard. When closing an idle dock, `ShowDashboard()` is preserved.
    - **Programmatic Close Guard**: Added `IsClosingProgrammatically` flag to `PresenterDockWindow` and set it in `PresenterDockService.CloseDock()` to prevent `AppWindow.Closing` from intercepting programmatic dock closures.
    - **Window Lifecycle Diagnostics**: Added detailed logging with HWND, PID, event name, and stop sequence ID across `MainWindow`, `PresentationWindow`, `PresenterDockWindow`, `PresenterDockConfirmationWindow`, `PresentationCoordinator`, and `PresentationWindowService`.
    - **Regression Test Coverage**: Added `DockCloseLifecycleHotfixTests.cs` (7 tests covering active cancel, stop & close, dashboard stop, idle dock close, 25-cycle repeated stress, native HWND validity checks, and shutdown bypass). Total test suite expanded to 379 passing tests.
- [x] **P0 Native Crash Recovery — AccessViolationException After Stop Presenting Fixed**:
  - **Confirmed Root Cause**:
    1. **Asynchronous `SetBitmapAsync` Racing Window Destruction**: In `PresentationCoordinator.StopPresentationAsync`, `_outputRenderer.Clear()` was enqueuing an asynchronous delegate to the WinUI UI thread that created a 1x1 `SoftwareBitmap` and called `await _softwareBitmapSource.SetBitmapAsync(emptyBitmap)`. Meanwhile, `_presentationWindowService.ClosePresentationWindow()` invoked `target.Close()`, destroying the `PresentationWindow`'s native HWND and XAML visual tree. `SoftwareBitmapSource` is a singleton COM object whose underlying WinUI DComp visual connection held references into the destroyed `Image` element. When `SetBitmapAsync` completed its composition update on freed visual memory, `Microsoft.WinUI.dll` crashed with `AccessViolationException` (`0xC0000005: Attempted to read or write protected memory`).
    2. **Missing Native Control Teardown**: `PresentationWindow.xaml.cs` never set `PresentationOutputImage.Source = null`, `PresentationDirectImage.Source = null`, or `PresentationVideoPlayer.SetMediaPlayer(null)` prior to `Close()`. Native DirectX composition and MediaPlayer references remained bound into destroyed XAML controls.
    3. **PresentationViewModel Leak & Dangling Subscriptions**: `PresentationViewModel` was never disposed on window close, leaking singleton subscriptions to `_presentationStateService` and `_presentationCoordinator`.
    4. **Inverted Stop Cleanup Sequence**: `_outputRenderer.Clear()` was invoked before `_captureCoordinator.StopPreviewAsync()` and `_cameraCaptureService.StopCaptureAsync()`, allowing lingering capture frames to queue into the renderer during teardown.
    5. **Non-Idempotent Stop Invocations**: Simultaneous stop calls from Dashboard, Presenter Dock, hotkeys, or window close would execute duplicate destructive cleanup passes.
  - **Remediation**:
    - **Reordered Stop Lifecycle**: In `PresentationCoordinator.ExecuteStopPresentationAsync`, stopped Camera frame acquisition first -> stopped WGC capture second -> stopped PowerPoint slideshow/slides third -> stopped Media Video & cleared Image fourth -> cleared output renderer generations fifth -> transitioned status to `Idle` sixth -> closed and cleaned up `PresentationWindow` seventh -> activated MainWindow and navigated to Dashboard eighth.
    - **Stop Idempotency**: Implemented `_activeStopTask` deduplication guarded by `_stopLock`. Concurrent Stop calls await the single in-flight Stop task without executing duplicate cleanup.
    - **PresentationWindow Explicit Teardown**: Added `Cleanup()` to `PresentationWindow.xaml.cs` (called idempotently on `ClosePresentationWindow` and `Closed`). Detaches `PresentationOutputImage.Source = null`, `PresentationDirectImage.Source = null`, `PresentationVideoPlayer.SetMediaPlayer(null)`, unsubscribes `ViewModel.PropertyChanged` and `ThemeChanged`, calls `Bindings.StopTracking()`, and disposes `ViewModel`.
    - **Safe Atomic Generation Clear**: Updated `Direct3D11PresentationRenderer.Clear()` to atomically increment `_currentGeneration` and reset `_isProcessingFrame = 0` without queuing dangerous, unnecessary 1x1 `SetBitmapAsync` calls into tearing-down XAML trees.
    - **Expected Cancellation Handling**: Explicitly caught `OperationCanceledException` (including `TaskCanceledException`) in `Direct3D11PresentationRenderer` and `Direct3D11PreviewRenderer`, classifying them as expected source-switching cancellations rather than logging false render error incidents.
    - **Eliminated Duplicate Dashboard Activation**: Fixed Presenter Dock close to only invoke secondary `ShowDashboard()` when closing while idle; when closing while presenting, `StopPresentationAsync` acts as the single authoritative dashboard activator.
- [x] **Dock Modal & UI Consistency Hotfix (Centered Confirmation Dialog, Responsive Layout, and Official Website Link)**:
  - **Decoupled Centered Presenter Dock Close Modal**:
    - **Confirmed Root Cause**: `ShowCloseConfirmationDialogAsync` previously showed a `ContentDialog` inside `PresenterDockWindow`'s visual tree, attempting to resize the dock window via a 210 DIP height hack. Because `PresenterDockWindow` is an always-on-top, borderless floating window with non-client drag regions configured via `InputNonClientPointerSource`, clicks were intercepted as window drags or swallowed, causing focus freezes, visual clipping, and occasional deadlocks.
    - **Remediation**: Created `PresenterDockConfirmationWindow` as a dedicated, standalone modal Window with `OverlappedPresenter` (`IsAlwaysOnTop = true`, borderless, ToolWindow style). Sized at 480x210 DIPs, centered on the monitor work area where the Presenter Dock is located. Temporarily disables the dock via Win32 `EnableWindow(dockHwnd, false)` while open, and re-enables and restores foreground upon closure. Supports full keyboard navigation (Escape to Cancel, Enter, Tab). Restored `PresenterDockWindow`'s clean fixed height (52 DIPs single row / 86 DIPs secondary row).
  - **Standardized Maximized MainWindow Layout**:
    - **Confirmed Root Cause**: `DashboardPage.xaml` and `SourcesPage.xaml` had `HorizontalAlignment="Left"` with `MaxWidth="1040"`, while `SettingsPage.xaml` had `HorizontalAlignment="Stretch"` with `MaxWidth="1080"`. On maximized 1080p/1440p displays, Dashboard and Sources were pinned to the left edge with large empty space on the right, while Settings was centered. In Settings, categories with fewer controls caused horizontal shifting.
    - **Remediation**: Introduced a unified responsive container pattern across `DashboardPage`, `SourcesPage`, and `SettingsPage`: `Grid HorizontalAlignment="Stretch"` hosting a `StackPanel MaxWidth="1160" HorizontalAlignment="Stretch" Padding="28,20,28,28"`. Fixed `SettingsPage` category navigation column to 200 DIPs with flexible `Width="*"` detail panel and explicit `HorizontalAlignment="Stretch"` on all category borders, ensuring a locked, stable position with zero horizontal shifting across all 6 categories.
  - **Official SwitchCast Website Link**:
    - Replaced the General & About "Repository" row with "Website" pointing to `https://switchcast.wasmer.app/`.
    - Added `WebsiteUrl` and `OpenWebsiteCommand` to `SettingsViewModel`, opening the system default browser via `Windows.System.Launcher.LaunchUriAsync` with graceful error handling.
    - Updated `SettingsViewModelTests` to assert the website URL. Internal GitHub Issues diagnostic reporting remains untouched.
- [x] **Full Stability & Regression Recovery Pass (Stop/Dock Lifecycle Contract, Diagnostic Crash/Hang Watchdog, Camera Frame Task Observation, PowerPoint Slide Preservation, Video Stutter Fix)**:
  - **Authoritative Lifecycle Contract (Actions A - D)**:
    - **Action A (Stop Presenting)**: Fully terminates active capture/media/slideshow/camera, clears and closes Presentation Output window, restores and activates MainWindow, navigates to Control Dashboard, preserves the application process, and leaves source library and queue data intact without creating duplicate windows.
    - **Action B (Dock X Confirmation)**:
      - *Active presentation*: Displays custom modal confirmation dialog ("Stop presentation and close Dock?", "A presentation is currently running. Closing the Presenter Dock will also stop the presentation and close Presentation Output.", Actions: "Cancel", "Stop & Close"). Dynamically expands window bounds to 210 DIPs to prevent dialog clipping. "Cancel" preserves presentation and dock; "Stop & Close" calls authoritative `StopPresentationAsync`, closes output window, closes dock, and activates Dashboard without terminating application.
      - *Idle*: Closes Floating Dock immediately with zero confirmation prompt, activating Dashboard.
    - **Action C (Application Shutdown)**: MainWindow exit confirmation coordinates clean termination; secondary dock confirmation is bypassed, Dashboard is never reactivated, and all resources are properly disposed.
    - **Action D (Show/Hide Hotkey)**: Non-destructive visibility toggle via `ToggleDock()`; never triggers stop presentation or close confirmation.
  - **Crash and UI Hang Diagnostics & Startup Previous-Crash Banner**:
    - **Synchronous Critical Flush**: Added `FlushCriticalIncident` to `IErrorReportingService`/`ErrorReportingService` and `LogCritical` to `SwitchCastDiagnostics`. Fatal unhandled exceptions (`AppDomain.UnhandledException` where `e.IsTerminating` is true) bypass thread-pool asynchronous queues and write JSON incident files synchronously before process exit.
    - **Proactive UI Hang Watchdog**: Added `UIHangWatchdog` running an independent background timer monitoring WinUI `DispatcherQueue` message loop latency. Dispatches heartbeats every 2s; logs warning incident at 5s unresponsive and critical deadlock incident at 15s.
    - **Startup Crash Detection**: Added `GetPreviousCrashReports()` to inspect previous sessions. `DashboardViewModel` checks for previous crashes on initialization and displays a dismissible warning InfoBar on the Dashboard with a direct "View Crash Report" action invoking `ErrorReportDialog`.
  - **Camera Frame Task Observation & Bounded Latest-Frame-Wins Loop**:
    - Replaced fire-and-forget `_ = _outputRenderer.RenderBitmapAsync(bitmap)` in `PresentationCoordinator.OnCameraFrameArrived`.
    - Added sequential asynchronous rendering loop with backpressure signaling via `_cameraCaptureService.NotifyFrameCompleted()`.
    - Dropped/superseded frames are immediately disposed via `bitmap.Dispose()`.
    - Render exceptions are caught and reported to diagnostics.
    - Coordinator marks camera source status as `Active` only after the first frame render succeeds.
  - **PowerPoint Slide State Preservation & Switch Race Elimination**:
    - Added `UpdateSelectedSource(CaptureSource source)` in `IPresentationStateService` / `PresentationStateService`.
    - Slide advance/retreat actions in `PresentationCoordinator` synchronize slide index in `SelectedSources` so returning to PowerPoint from Video or Camera restores the exact slide index (e.g. Slide 4).
    - Camera-to-PowerPoint switch race eliminated: pending camera frames are discarded and `_targetRequestedPresentationSource` is validated before rendering camera bitmaps.
  - **Video Playback Performance & Stutter Prevention**:
    - Throttled high-frequency `MediaPlaybackSession.PositionChanged` events in `MediaPresentationService` to at most once per 250ms, eliminating UI message loop starvation caused by 60fps position callbacks.
  - **Quality Gates & Test Coverage**:
    - Added `FullStabilityRegressionContractTests.cs` (10 executable contract tests across all 5 requirement domains A-E).
    - Total test suite expanded to **366 automated tests** (100% pass rate).
    - Debug x64 and Release x64 builds compile with **0 errors and 0 warnings**.
- [x] **PowerPoint Source-Switching Hotfix & Production Error Diagnostics System**:
  - **PowerPoint Threading Hotfix (0x8001010E RPC_E_WRONG_THREAD)**:
    - **Confirmed Root Cause**: `PresentationStateService.ActiveSource` changes on worker threads triggered `DashboardViewModel.OnPresentationStatePropertyChanged`, which synchronously set `SelectedPresentationSource = _presentationStateService.ActiveSource;`. Because `SelectedPresentationSource` is two-way bound to `ComboBox.SelectedItem` in `DashboardPage.xaml`, WinUI threw COMException `0x8001010E (RPC_E_WRONG_THREAD)`. This escaped out of `SetActiveSource` into `PresentationCoordinator.SwitchPresentationSourceAsync`, setting `Status = Error` and showing `ERROR` on the Floating Dock.
    - **Remediation**: Wrapped all `SelectedPresentationSource` and `SelectedPreviewSource` updates in `RunOnUIThread(...)` with reentrancy protection (`_isSyncingPresentationSource`). Removed premature return in `SwitchPresentationSourceAsync` so presentation switches proceed reliably.
  - **Production-Ready Local Diagnostics & Reporting Subsystem**:
    - Central architecture: `IErrorReportingService`, `ErrorReportingService`, `IDiagnosticRedactionService`, `DiagnosticRedactionService`, `IDiagnosticReportBuilder`, `DiagnosticReportBuilder`.
    - Structured incident telemetry: `DiagnosticIncident` with unique ID (`SC-YYYYMMDD-XXXXXXXX`), hex HRESULT, safe message, stack trace, timeline, repeat count, OS/app versions.
    - Privacy-first redaction: Automatic sanitization of Windows usernames (`[USER]`), user profiles (`%USERPROFILE%`), email addresses (`[REDACTED_EMAIL]`), auth tokens, passwords, and API keys (`[REDACTED_SECRET]`).
    - Local storage: Bounded log storage at `%LOCALAPPDATA%\SwitchCast\Logs` with 10-incident file rotation and 30-day log purging. Zero remote telemetry.
    - Unhandled exception hooks: Attached to `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, and `App.UnhandledException`.
    - UI integration: `Views/Dialogs/ErrorReportDialog.xaml` (WinUI 3 ContentDialog with friendly message, badge, collapsible sanitized technical details, Copy Report, Save Report, Report on GitHub, Report to Developer). Audience-facing Presentation Output is isolated and never interrupted by error modals.
    - Settings integration: Added Category 5 ("Diagnostics & Reports") in `SettingsPage.xaml` and `SettingsViewModel.cs` with incident review, log location, and "Open Logs Folder".
  - **Automated Regression Suite**: Expanded test suite to **353 automated unit tests** (100% pass rate). Both Debug and Release builds compile with 0 errors and 0 warnings.
- [x] **Critical PowerPoint & Presenter Dock Regression Hotfix (COM Crash, Auto-Open Dock & Broken Slide Controls)**:
  - **PowerPoint COM Switching Crash Resolution**:
    - **Confirmed Root Cause**: When transitioning Camera -> PowerPoint, `CameraCaptureService.StopCaptureInternalAsync` invoked `_frameReader.Dispose()` and `_mediaCapture.Dispose()` on the deactivation path outside guarded try/catch blocks; native WinRT/COM shutdown exceptions escaped into `SwitchPresentationSourceAsync`. Simultaneously, if PowerPoint slides had not yet been cached or had missing slide image paths, `PowerPointPresentationService.LoadPresentationAsync` called `UpdateSlideImageAsync` with null/empty paths or attempted background `BitmapImage` creation without dispatcher validation. When `SwitchPresentationSourceAsync` caught the failure, it logged only `error={ex.Message}` (often empty for COMExceptions) without hex HRESULT or stack trace, then rethrew `throw;`, hitting `PresenterDockViewModel.SwitchSourceAsync` which had no try-catch, causing an unhandled task exception breaking in `App.xaml.cs`.
    - **Remediation**:
      - Hardened `CameraCaptureService.StopCaptureInternalAsync`: guarded `_frameReader.Dispose()` and `_mediaCapture.Dispose()` with dedicated try/catch blocks logging any disposal exceptions via `SwitchCastDiagnostics.LogException` without breaking transition flow.
      - Updated `PowerPointPresentationService.LoadPresentationAsync`: added automatic fallback import (`ImportPresentationAsync(source.FilePath)`) if `SlideImagePaths` is empty before going live; strictly guarded `UpdateSlideImageAsync` to verify UI `DispatcherQueue` before attempting `BitmapImage` instantiation.
      - Added high-fidelity structured diagnostics with hex HRESULT (`0x{ex.HResult:X8}`), managed thread ID, source ID, source type, and stack trace in `PresentationCoordinator.StartPresentationAsync` and `SwitchPresentationSourceAsync`.
      - Guarded all async commands in `PresenterDockViewModel` (`SwitchSourceAsync`, `NextSourceAsync`, `PreviousSourceAsync`, `PowerPointNextSlideAsync`, `PowerPointPreviousSlideAsync`, `TogglePauseAsync`, `ToggleBlackoutAsync`, `StopPresentationAsync`) with `try / catch` logging via `SwitchCastDiagnostics.LogException` to prevent unhandled exceptions from terminating UI tasks.
  - **Centralized Automatic Presenter Dock Startup**:
    - **Confirmed Root Cause**: `_dockService?.ShowDock()` was previously called only in `DashboardViewModel.StartPresentationAsync` after an asynchronous `ConfigureAwait(false)` resumption, causing cross-thread window creation exceptions. Direct coordinator starts, hotkeys, and non-window sources lacked a centralized startup guarantee.
    - **Remediation**:
      - Injected `IPresenterDockService` and `IApplicationSettingsService` directly into `PresentationCoordinator`.
      - Centralized automatic dock startup in `PresentationCoordinator.StartPresentationAsync`: opens the existing Dock instance once when presentation starts for all 6 source types (Window, Display, Image, Video, PowerPoint, Camera) whenever `AutoOpenPresenterDock != false`.
      - Added `SetDispatcherQueue` to `IPresenterDockService` and `PresenterDockService`, wired from `_mainWindow.DispatcherQueue` in `App.xaml.cs`.
      - Implemented UI thread dispatch marshaling in `PresenterDockService.ShowDock()`, `CloseDock()`, and `ToggleDock()`, guaranteeing thread-safe window instantiation and activation from any thread.
      - Preserved user close behavior: source switches do not reopen a manually closed dock; stopping and starting a new presentation reopens the dock.
  - **Floating Dock PowerPoint Controls & Empty Black Secondary Row Fix**:
    - **Confirmed Root Cause**:
      1. In `PresenterDockWindow.xaml.cs`, `ViewModel` was assigned *after* `InitializeComponent()`. When `InitializeComponent()` executed `this.Bindings.Initialize()`, `this.ViewModel` was `null`, so WinUI compiled bindings (`x:Bind`) never attached child listeners to `ViewModel.PropertyChanged`.
      2. In `PresentationCoordinator.cs`, property changed handler on `PresentationStateService` notified media properties but omitted `IsActiveSourcePowerPoint`, `IsActiveSourceCamera`, and `PowerPointProgressText`.
      3. Code-behind in `PresenterDockWindow.xaml.cs` listened to `HasSecondaryRow` and resized `_appWindow` to 86 DIPs, but `PowerPointRowGrid.Visibility` remained `Collapsed` because `x:Bind` was disconnected, leaving a black empty gap.
    - **Remediation**:
      - Moved `ViewModel` assignment and `ViewModel.SetDispatcherQueue(DispatcherQueue)` before `InitializeComponent()` in `PresenterDockWindow.xaml.cs`, and called `Bindings.Update()` immediately after.
      - Added direct typed properties `PowerPointRowVisibility` and `VideoPlaybackRowVisibility` to `PresenterDockViewModel` and bound them in `PresenterDockWindow.xaml`.
      - Updated `PresenterDockWindow.OnViewModelPropertyChanged` to trigger `Bindings.Update()` and dynamic window sizing upon changes to `HasSecondaryRow`, `IsActiveSourcePowerPoint`, `IsActiveSourceVideo`, `PowerPointRowVisibility`, or `VideoPlaybackRowVisibility`.
      - Raised `IsActiveSourcePowerPoint`, `IsActiveSourceCamera`, `PowerPointProgressText`, and `DirectImageSource` in `PresentationCoordinator` on `ActiveSource` change.
  - **Automated Regression Suite**:
    - Added `PowerPointDockRegressionTests.cs` (16 new automated tests), expanding the total test suite to **342 automated unit tests** (100% pass rate). Both Debug and Release builds compile with 0 errors and 0 warnings.
- [x] **Presentation Pipeline Hotfix (First Queued Source Auto-Start & UI State Dispatch Stabilization)**:
  - **COMException 0x8001010E (RPC_E_WRONG_THREAD) Resolution**:
    - Created explicit UI dispatcher abstraction: `IUIThreadDispatcher` with `DispatcherQueueAdapter`, `ImmediateUIThreadDispatcher`, and thread-safe registration via `UIThreadDispatcherProvider`.
    - Established explicit per-window dispatcher ownership: `MainWindow` and `DashboardPage` configure `DashboardViewModel` and `SourcesViewModel`; `PresentationWindow` configures `PresentationViewModel`; `PresenterDockWindow` configures `PresenterDockViewModel`.
    - Dispatched off-thread `PropertyChanged` notifications from `DashboardViewModel`, `PresentationViewModel`, `PresenterDockViewModel`, and `SourcesViewModel` strictly onto their owning UI threads, preventing worker-thread invocation of WinUI compiled bindings (`x:Bind`) on `TextBlock.Text`, `Visibility`, `Slider.Value`, etc.
    - Repaired `MediaPresentationService.PlayVideoAsync` to isolate worker-thread `MediaStateChanged` invocations outside the player initialization try/catch block, eliminating false-positive "Failed to play video (0x8001010E)" alerts on the Dashboard when UI subscribers execute off-thread.
    - Strictly enforced that if `TryEnqueue` fails or the dispatcher is unavailable, actions are dropped or logged rather than executing unsafely on the background worker thread.
  - **First Queued Source Auto-Start & Authoritative Source Resolution**:
    - Resolved the presentation startup bug where clicking "Start Presenting" with queued sources opened Presentation Output in standby without taking the first source live.
    - Implemented `DashboardViewModel.ResolveAuthoritativeStartSource()`: resolves target source with strict precedence (explicit queued selection -> first queued source in `SelectedSources` -> null).
    - Guarded `SelectedPresentationSource` against spurious `null` pushes from WinUI `ComboBox` when templates or views re-evaluate visibility.
    - Updated `PresentationCoordinator.StartPresentationAsync`: targets the first queued source atomically when no source parameter is passed and current active source is not already in the presentation queue.
    - Enforced idempotency on `StartPresentationCommand`: disabled when `IsStarting` is true; transitions safely without duplicate transition storms.
  - **Automated Regression Suite**: Added `PresentationStartupAndThreadingTests.cs` (16 new tests), expanding the test suite to **326 automated unit tests** (100% pass rate). Both Debug and Release builds compile with 0 errors and 0 warnings.
- [x] **Critical Camera Runtime Hotfix (Camera Starts but Does Not Render / COMException / Stuck STARTING)**:
  - **Catch-22 Frame Rejection Deadlock Resolved**:
    - `PresentationCoordinator.OnCameraFrameArrived` previously checked `if (Status != PresentationStatus.Active || !IsActiveSourceCamera)` and discarded all incoming camera frames while `Status` was `Starting`. Because frames were discarded, no frame was ever rendered; because no frame was ever rendered, `Status` never became `Active`.
    - Fixed `OnCameraFrameArrived` to accept frames during `Starting` and `Active`: `if ((Status != PresentationStatus.Active && Status != PresentationStatus.Starting) || !IsActiveSourceCamera)`.
    - Defined clear transition rule: when the first camera frame is delivered to the renderer, `Status` transitions from `Starting` to `Active` (LIVE), and the floating dock immediately updates to `LIVE`.
    - Added an 8-second bounded startup timeout (`ArmCameraStartupTimeout`) using `CancellationTokenSource`: if camera hardware fails or freezes without delivering frames, status transitions to `Error` with user-friendly feedback instead of remaining stuck at `STARTING...`.
  - **NV12 Pixel Format & COMException Fix**:
    - In `CameraCaptureService`, `CreateFrameReaderAsync(frameSource, MediaEncodingSubtypes.Bgra8)` is now explicitly requested, instructing Windows Media Foundation's native topology to perform hardware-accelerated color conversion to `Bgra8` directly.
    - Eliminated invalid `SoftwareBitmap.Convert(rawBitmap, Bgra8, Premultiplied)` calls on planar `BitmapPixelFormat.Nv12` frames (which threw 30 first-chance `COMException` events/sec in `WinRT.Runtime.dll`).
    - Implemented managed CPU fallbacks (`ConvertNv12ToBgra8`, `ConvertYuy2ToBgra8`) using `CopyToBuffer` and ITU-R BT.601 math, guaranteeing 100% exception-free conversion even if native hardware conversion is unavailable.
    - Added Direct3D surface fallback: if `SoftwareBitmap` is null on GPU-backed frames, extracts bitmap via `SoftwareBitmap.CreateCopyFromSurfaceAsync(surface, BitmapAlphaMode.Premultiplied)`.
    - In `Direct3D11PresentationRenderer.RenderBitmapAsync`, ensured format check guards prevent `SoftwareBitmap.Convert` from being invoked on non-BGRA8 formats on the UI thread.
  - **Duplicate Transition Storm & Idempotency**:
    - Guarded `StartPresentationAsync`, `SwitchPresentationSourceAsync`, and `ExecuteSourceSwitchAsync`: if the presentation is already `Starting` or `Active` with the requested target source ID, duplicate start requests are ignored as redundant while ensuring the presentation output window remains visible.
    - Unconditionally stopped camera capture and cancelled pending timeout watchers when switching away to Window, Monitor, Media, or PowerPoint sources.
  - **Structured Unhandled Exception Diagnostics**:
    - Wired `UnhandledException` in `App.xaml.cs` to log exact hex HRESULT (`0x{ex.HResult:X8}`), exception type, message, and stack trace via `SwitchCastDiagnostics.LogException`.
  - **Automated Regression Suite**: Expanded test suite to **310 automated unit tests** (100% pass rate). Both Debug and Release builds compile with 0 errors and 0 warnings.
- [x] **PowerPoint & Camera Runtime Hotfix (Issues A–D Resolved)**:
  - **Issue A — PowerPoint Slide Counter Synchronization**:
    - `PresentationCoordinator.PowerPointProgressText` dynamically reads the authoritative current slide index and total slide count from `IPowerPointPresentationService` and `IPowerPointLiveService`.
    - Wired `_powerPointService.SlideChanged` to update `_presentationStateService.ActiveSource` with the latest `CurrentSlideIndex` on every slide advance (`NextSlideAsync`, `PreviousSlideAsync`, `GoToSlideAsync`).
    - Fixed `PowerPointLiveService` to maintain a single persistent STA worker thread and poll `SlideShowWindow.View.CurrentShowPosition`, capturing external slide transitions (keyboard, mouse clicks, remote controls) without advancing on intra-slide animations.
  - **Issue B — PowerPoint Source Persistence**:
    - Added `ImportedPowerPointPaths` to `UserSettings` to store imported `.pptx` decks persistently across application sessions.
    - Registered `SourcesViewModel` as `Singleton` in `App.xaml.cs` to prevent destruction of imported collections upon tab navigation.
    - Rehydrates imported PowerPoint files on startup and refresh; marks non-existent decks as `IsAvailable = false` rather than silently deleting them.
    - Preserved separation of states: unqueueing removes a deck from the presentation queue while strictly keeping it in the imported source library.
  - **Issue C — Camera Display & Pipeline Stabilization**:
    - Eliminated blank output and application freezing by introducing an atomic backpressure gate (`Interlocked.CompareExchange`) in `CameraCaptureService`, preventing queue flooding and UI dispatcher saturation.
    - Moved pixel format conversion off the UI thread: converted webcam frames (NV12, YUY2) into independent `BitmapPixelFormat.Bgra8` + `BitmapAlphaMode.Premultiplied` `SoftwareBitmap` instances on background threads before passing to `Direct3D11PresentationRenderer`.
    - Enforced deterministic disposal of `SoftwareBitmap` in `Direct3D11PresentationRenderer.RenderBitmapAsync` and wired backpressure release (`FrameRenderCompleted`).
    - Handled camera format negotiation (preferred ~720p/1080p @ 30fps) and resource teardown when switching away or stopping.
  - **Issue D — COM Diagnostics & Threading Corrections**:
    - Created `SwitchCastDiagnostics` providing structured logging of exception type, exact hex HRESULT (`0x{ex.HResult:X8}`), thread ID, message, and stack trace across all COM and hardware operations.
    - Resolved `RPC_E_WRONG_THREAD` (0x8001010E) in `PowerPointPresentationService` by dispatching `BitmapImage` creation onto the WinUI UI thread via `DispatcherQueue`.
    - Resolved `RPC_E_WRONG_THREAD` in `MediaPresentationService.ClearVideoInternal` by executing player pause and source cleanup via `RunOnUIThread`.
  - **Automated Regression Suite**: Expanded test suite to **307 automated unit tests** (100% pass rate). Both Debug and Release builds compile with 0 errors and 0 warnings.
- [x] **Local Presentation Expansion (Phases P0 - P5 Complete)**:
  - **P1 — Smart External Display Mode & Configurable Automation**:
    - Multi-monitor topology discovery via Win32 `MONITORINFOEX` geometry.
    - Automatic detection and positioning of existing `PresentationWindow` on secondary display without HWND recreation.
    - Configurable automatic fullscreen (`AutoFullscreenOnExternalDisplay`) and preferred monitor persistence.
    - Safe recovery to primary display in `Windowed` mode upon external display disconnect.
  - **P2 — PowerPoint Static Slide Integration (.pptx)**:
    - First-class `PowerPointSource` model tracking `FilePath`, `SlideCount`, `CurrentSlideIndex`, `SlideImagePaths`.
    - Local disk MD5-hashed slide cache preventing redundant renders.
    - `PowerPointComSlideRenderer` export engine with COM lifecycle isolation.
    - Bidirectional slide navigation (`NextSlideAsync`, `PreviousSlideAsync`, `GoToSlideAsync`) with boundary clamping.
  - **P3 — PowerPoint Live Slideshow Integration**:
    - Attended COM automation via `IPowerPointLiveService` / `PowerPointLiveService`.
    - Enforces windowed slideshow mode (`ppShowTypeWindow = 2`) to ensure PowerPoint never takes over the audience display/projector.
    - Direct slideshow navigation (`NextAsync`, `PreviousAsync`, `GoToSlideAsync`) for animations and slides.
  - **P4 — Camera Source Integration (Webcam / USB Capture Devices)**:
    - `CameraSource` model tracking `DeviceId`, resolution, and `IsInternal` (built-in webcam vs external USB).
    - Camera discovery and hotplug watching via `ICameraDiscoveryService` / `DeviceInformation.CreateWatcher`.
    - Native video frame acquisition via `ICameraCaptureService` using `MediaCapture` and `MediaFrameReader`, rendering directly into `IPresentationOutputRenderer.RenderBitmapAsync`.
    - Privacy compliance: cameras are only activated when selected On Air, with graceful error handling when access is denied or device is busy.
  - **P5 — Unified Source Queue & Adaptive Presenter Dock Controls**:
    - Unified 6-source queue: Window, Monitor, Image, Video, PowerPoint, Camera.
    - Adaptive Floating Presenter Dock: contextual secondary row for PowerPoint slide advance and progress indicator (`Slide X of Y`), maintaining clear separation between queued source switching and slide advance.
    - Expanded unit test suite to **299 automated unit tests** (100% pass rate). Both Debug and Release builds compile with 0 errors and 0 warnings.
- [x] **Phase P1 — Smart External Display Mode & Configurable Automation**:
  - **Multi-Monitor Topology Discovery**: `IExternalDisplayService` and `ExternalDisplayService` automatically analyze physical desktop layout, identifying primary display, extended non-primary displays, and distinguishing clone/mirrored configurations (shared geometry).
  - **Single Presentation Output Window HWND Preservation**: When an eligible extended TV/projector is detected, automatically moves the existing `PresentationWindow` to the external display's work area bounds without destroying, recreating, or flickering the HWND.
  - **Configurable Automatic Fullscreen**: Enters borderless fullscreen mode automatically when `AutoFullscreenOnExternalDisplay` is enabled; respects user override when disabled or when presentation output is windowed.
  - **Projector Disconnect Recovery Policy**: If the assigned external monitor is unplugged or disconnected, recovers the `PresentationWindow` back to the primary monitor in safe `Windowed` mode. Never emergency-fullscreens on the presenter's private laptop monitor.
  - **Presenter Workspace Isolation**: The `MainWindow` Control Dashboard and `PresenterDockWindow` floating toolbar stay on the presenter's primary workspace, never moving unexpectedly to the audience screen.
  - **Persistent Settings Integration**: Added `AutoDetectExternalDisplay` (default: true), `AutoFullscreenOnExternalDisplay` (default: true), and `PreferredDisplayDeviceName` (default: null/Auto) in `UserSettings`, with interactive UI controls in `SettingsPage.xaml` under Presenter Controls.
  - **Automated Regression Suite**: Expanded test suite to **289 automated unit tests** (100% pass rate) with full multi-monitor topology matrix in `ExternalDisplayServiceTests.cs`.
- [x] **Media Source Activation Behavior Fix (Active + Live & Active Only Activate Presentation Output)**:
  - **Active + Live for Media**:
    - When an Image or Video media source is switched live in `ActiveAndLive` mode, SwitchCast starts rendering the image or playing the video and activates the existing `PresentationWindow` (restoring if minimized and bringing to the foreground via `IWindowActivationService.ActivateWindow`).
    - Does NOT launch external media applications (e.g. VLC or Photos). Reuses existing output window without recreation or recentering, preserving fullscreen/windowed geometry.
  - **Active Only for Media**:
    - When an Image or Video media source is navigated to in `ActiveOnly` mode, advances the selection cursor (`SetSelectedSource`), resolves `PresentationWindow`, restores if minimized, and brings it to the foreground.
    - Crucially preserves On-Air continuity: does NOT change `ActiveSource`, does NOT start media playback, does NOT seek or replace audience-facing frames. If closed, opens in Standby without taking media live.
  - **Live Only for Media**:
    - Preserved existing behavior: takes media live without requesting foreground focus or stealing focus from current active applications.
  - **Window and Monitor Source Semantics Preserved**:
    - Application window activation (`ActivateSource`) and monitor capture behavior remain completely untouched.
  - **Floating Dock Focus Handoff**:
    - Popup dropdown menu (`PresenterDockMenuWindow`) is closed and detached prior to executing the source switch command, preventing dropdown dismissal from reclaiming foreground focus away from the newly activated `PresentationWindow`.
  - **Native Application Icon Integration**:
    - Embedded multi-resolution `Assets/SwitchCast.ico` (16, 24, 32, 48, 64, 128, 256) generated from high-resolution `Assets/SwitchCast_1.png`.
    - Configured project deployment in `SwitchCast.csproj` (`<ApplicationIcon>Assets\SwitchCast.ico</ApplicationIcon>`, `<Content Include="Assets\SwitchCast.ico">`, and `<Content Include="Assets\SwitchCast_1.png">`).
    - Added automated branding unit tests in `ApplicationBrandingTests.cs` verifying multi-resolution ICO header, resolutions (16, 24, 32, 48, 64, 128, 256), file existence of `SwitchCast.ico` and `SwitchCast_1.png`, and csproj build configurations.
  - **Automated Regression Tests**:
    - Expanded test suite to **281 automated unit tests** (100% pass rate).
- [x] **Dashboard Start/Stop Presenting Dynamic Icon Correction**:
  - Replaced hardcoded play glyph `&#xE768;` in `DashboardPage.xaml` with compiled binding `{x:Bind ViewModel.PresentationButtonGlyph, Mode=OneWay}`.
  - Bound glyph dynamically in `DashboardViewModel.cs` to authoritative presentation state: `PresentationButtonGlyph => HasActivePresentation ? "\uE71A" : "\uE768"`.
  - Notifies on presentation status changes (`Idle`, `Starting`, `Active`, `Paused`, `Blackout`, `Stopping`, `Stopped`), ensuring label ("Start Presenting" / "Stop Presenting") and icon (Play `\uE768` / Stop square `\uE71A`) update in strict synchronization while preserving coral styling, typography, corner radii, and command bindings.
- [x] **Floating Presenter Dock X Dismissal -> Control Dashboard Activation**:
  - Clicking the X button on the Floating Presenter Dock (`CloseDockCommand`) now dismisses the dock and restores/activates the existing `MainWindow` Control Dashboard.
  - Reuses existing `IWindowActivationService` (`ActivateMainWindow()`) to restore `MainWindow` if minimized and bring to foreground, and navigates to Dashboard via `INavigationService.NavigateToDashboard()`.
  - **Presentation Continuity**: Live presentation, Direct3D 11 capture, video playback, and `PresentationWindow` output remain completely active and untouched when dock is closed.
  - **Application Exit Safety**: Guarded with `IApplicationLifecycleService.IsShuttingDown` and `IsShutdownApproved` in both `PresenterDockViewModel.CloseDock()` and `App.ActivateMainWindow()`, preventing dashboard reactivation during application shutdown.
  - **Hotkey Distinction**: Global hotkey `Ctrl+Shift+D` (`ToggleDock`) continues normal toggle semantics without unwanted dashboard focus shifts.
- [x] **Floating Presenter Dock Stability & Double-Click Maximize Prevention**:
  - **Confirmed Root Cause**: WinUI 3 `InputNonClientPointerSource` non-client caption regions (`HTCAPTION`) passed `WM_NCLBUTTONDBLCLK` to default window procedure, which interpreted double-clicks as caption double-click maximize commands despite `OverlappedPresenter.IsMaximizable = false`.
  - **Native Window Subclassing**: Subclassed dock HWND via `comctl32.dll` (`SetWindowSubclass`). Handled `WM_NCLBUTTONDBLCLK` on `HTCAPTION` (returns `IntPtr.Zero`), intercepted `WM_SYSCOMMAND` `SC_MAXIMIZE`, clamped `WM_GETMINMAXINFO` tracking bounds, stripped `WS_MAXIMIZEBOX` and `WS_THICKFRAME` from `GWL_STYLE`, removed `SC_MAXIMIZE` from system menu, and hooked `AppWindow.Changed` auto-restoration.
  - **Preserved Presentation Fullscreen**: The dock fullscreen button continues toggling fullscreen on `PresentationWindow` without dock maximization or window recreation.
- [x] **Next/Previous Mixed Source Navigation Fix**:
  - **Confirmed Root Cause**:
    1. `CaptureCoordinator.StopPreviewInternalAsync()` called `_presentationStateService.SetActiveSource(null)`, causing `ActiveSource` to drop to null midway through Window -> Media transitions and triggering `DashboardViewModel` to fire a redundant switch that canceled the transition as superseded.
    2. `PresentationCoordinator.SwitchToNextSourceAsync()` previously filtered by `.Where(s => s.IsAvailable)` instead of the authoritative queue `_presentationStateService.SelectedSources`, causing index divergence from the dock dropdown.
    3. `ActiveAndLive` mode attempted native HWND window activation on media sources (images/videos), which failed and blocked transition.
  - **Authoritative Single Ordered Queue**: Both Next and Previous traverse `_presentationStateService.SelectedSources` directly across all source types (Window, Monitor, Image, Video) in exact dropdown order.
  - **Switching Mode Semantics**: Media sources are taken live directly without window activation in `ActiveAndLive` mode. Paused and blackout states are respected upon video transitions.
- [x] **Presenter Dock Direct Source Unqueue**:
  - **Interactive Checkbox Toggle**: Replaced static glyph in `PresenterDockMenuWindow` with an interactive `CheckBox` control with tooltip "Remove from presentation queue".
  - **Popup Persistence**: Menu stays open upon unqueue, updates item count, shrinks window dimensions, and switches to empty state when queue count reaches 0.
  - **On-Air Continuity Policy**: Unqueueing an On-Air source removes it from the future navigation queue while preserving active presentation output until the presenter explicitly switches or stops.
  - **Deterministic Cursor Recalculation**: If the unqueued item was selected, cursor advances to the next queued item (or previous if removing final item; or null if empty).
  - **UI Synchronization**: Dock dropdown, Dashboard source selector, and Sources tab checkboxes stay synchronized immediately.
- [x] **Presenter Dock Playback Progress Cross-Thread COMException Permanent Hotfix (0x8001010E)**:
  - **Confirmed Root Cause**:
    - Previous attempt retained a `System.Threading.Timer` fallback (`_playbackProgressTimer`) when dynamic reflection or delegate casting failed.
    - `RunOnUIThread` contained an unsafe fallback: when `TryEnqueue` was unavailable, failed, or threw, it executed `action()` directly on the calling ThreadPool thread.
    - Firing `OnPropertyChanged(nameof(VideoPositionSeconds))` from the thread pool forced compiled WinUI 3 XAML bindings (`RangeBase.set_Value`) in `PresenterDockWindow` to execute off the owning UI thread, throwing WinRT `COMException` `0x8001010E` (`RPC_E_WRONG_THREAD`).
  - **Authoritative Single UI-Owned DispatcherQueueTimer**:
    - Completely eliminated `System.Threading.Timer`, `_playbackProgressTimer`, and `OnFallbackTimerTick`.
    - Strongly typed `Microsoft.UI.Dispatching.DispatcherQueue` and `Microsoft.UI.Dispatching.DispatcherQueueTimer` in `PresenterDockViewModel`.
    - `PresenterDockWindow` passes its UI thread `DispatcherQueue` directly to `PresenterDockViewModel.SetDispatcherQueue(DispatcherQueue)`.
    - A single repeating `DispatcherQueueTimer` (250ms interval) executes exclusively on the UI thread to update `VideoPositionSeconds` and `VideoPositionText`.
  - **Unsafe Fallback Execution Eliminated**:
    - Refactored `RunOnUIThread(Action)`: if called off-thread, invokes `dispatcher.TryEnqueue(callback)`. If `TryEnqueue` fails (returns `false`), the update is safely dropped and NEVER executed synchronously on the worker thread.
  - **Scrubbing & Slider Seek Loop Protection**:
    - While user is actively dragging the slider (`_isScrubbing = true`), automatic timer updates to `VideoPositionSeconds` are suppressed to prevent slider jitter.
    - User commit via `CompleteScrubbing` seeks to the clamped target position once.
  - **Clean Timer & ViewModel Lifecycle**:
    - `PresenterDockWindow.Closed` unhooks `PropertyChanged` and calls `ViewModel.Dispose()`.
    - `Dispose()` stops the `DispatcherQueueTimer`, detaches event handlers, unhooks coordinator and service events, and sets `_disposed = true`.
  - **Automated Unit Tests**:
    - Expanded test suite to **255 automated tests** (100% pass rate) validating UI dispatcher ownership, timer ticks, scrubbing state, off-thread event marshaling, and TryEnqueue failure safety.
- [x] **Main Dashboard Source Selector Empty-State UX Fix**:
  - **Informative Empty State Surface**:
    - Replaced blank gray selector box with an informative `DropDownButton` and styled Flyout matching the Floating Presenter Dock's empty state.
    - Displayed "No queued sources", `\uE7F4` icon, descriptive guidance ("Add application windows, displays, images, or videos from the Sources tab."), and a primary coral action button `+ Add Presentation Source`.
  - **Dashboard "+ Add Source" Button**:
    - Added a visible compact `+ Add Source` button to the primary presentation control bar that navigates directly to `SourcesPage` via `INavigationService`.
    - Automatically updates `NavigationView` sidebar selection to highlight the Sources tab.
  - **Start Presenting Validation & Tooltip**:
    - Bound `IsEnabled` to `CanStartPresentation` (`false` when 0 sources are queued and presentation is not active).
    - Added dynamic tooltip: "Add a presentation source first." when empty vs "Start Live Presentation" / "Stop Live Presentation".
  - **Preview Workspace Text Wrapping Fix**:
    - Fixed clipping on descriptive text block in `EmptyWorkspacePanel` with `TextWrapping="Wrap"` and `MaxWidth="420"`.
    - Renamed action button to "Add Presentation Sources" with shared navigation command.
  - **Automated Unit Tests**:
    - Expanded test suite to **250 automated tests** (100% pass rate) validating empty state properties, placeholder text, enablement rules, and queue count transitions (0 -> 1 -> 0).
- [x] **Floating Presenter Dock Finalization**:
  - **Native Windows Caption Dragging**:
    - Replaced pointer routing and manual `SendMessage(WM_NCLBUTTONDOWN)` implementation with genuine Windows App SDK `InputNonClientPointerSource` non-client caption regions.
    - Set the dock surface as `NonClientRegionKind.Caption` while setting interactive controls (buttons, dropdown triggers, sliders) as `NonClientRegionKind.Passthrough`.
    - Completely resolved sticky dragging / click-to-move anomaly; window drag initiates exclusively while holding left mouse button and terminates instantly on release.
  - **Permanent Unified Dock Layout**:
    - Removed compact vs expanded mode switching, obsolete toggle buttons, and legacy settings UI rows.
    - Baseline width fixed to 680 DIPs single row with standard presenter controls (Live status badge, Previous/Next source navigation, Active source dropdown, Mode selector, Mute, Pause/Resume, Blackout, Stop Presenting, Fullscreen output toggle, More Options menu, Close dock).
  - **Adaptive Video Playback & Timeline Controls**:
    - When a video source is On Air, the dock automatically reveals a second video transport row (height adapts from 52 DIPs to 86 DIPs).
    - Video controls include: Restart video (`00:00`), Seek backward 10s (`\uEB9E`), Play/Pause video (`\uE768`/`\uE769`), Seek forward 10s (`\uEB9D`), duration scrubber slider with smooth pause-during-scrubbing commitment, and live timecode text (`00:00 / 00:00`).
    - Non-video sources (Window, Display, Image) cleanly hide the video row and shrink dock height to 52 DIPs with dynamic hit-test region recalculation.
- [x] **Fullscreen Presentation Output (Phase 6.3)**:
  - **Native AppWindow Fullscreen Presenter**:
    - Leverages supported Windows App SDK `AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen)` for true borderless fullscreen on the active monitor without custom window recreation hacks.
    - Transitions smoothly between `PresentationDisplayMode.Windowed` and `PresentationDisplayMode.Fullscreen`.
  - **Same HWND Preservation & Share Continuity**:
    - Preserves the EXACT same `PresentationWindow` instance, native Win32 HWND, active capture/media session, and playback state across all fullscreen and windowed mode transitions.
    - Zero window recreation, zero capture session recreation, and zero audio stream interruption.
  - **Custom Title Bar & Viewport Geometry**:
    - Completely collapses and hides custom XAML title bar (`TitleBarRow.Height = 0` / `AppTitleBar.Visibility = Collapsed` / `SetTitleBar(null)`) in fullscreen mode so audience content fills 100% of the viewport with aspect-ratio preservation.
    - Restores custom title bar height (38px) and default window presenter upon exiting fullscreen.
  - **Floating Presenter Dock Control Surface**:
    - Added dedicated fullscreen icon toggle (`\uE740` Enter / `\uE73F` Exit) in Expanded dock mode and inside `PresenterDockMenuWindow` More Options popup.
    - Dynamic tooltip and state synchronization linked directly to `IPresentationWindowService.DisplayMode`.
    - Auto-disables toggle button when `PresentationWindow` is closed.
  - **Fallback Escape Key Recovery**:
    - Safe `Escape` key handler in `PresentationWindow` to exit fullscreen when output window has keyboard focus, without interfering with system-wide hotkeys.
  - **Lifecycle Integration & Multi-Monitor**:
    - Restores previous normal window size, coordinates, and monitor placement when exiting fullscreen.
    - Stop Presenting and window close events safely reset display mode state to Windowed.
- [x] **Media Audio Playback & Synchronization (Phase 6.2)**:
  - **Audible Video Playback**: Fixed silent video playback by removing hardcoded `IsMuted = true` and wiring native `Windows.Media.Playback.MediaPlayer` audio channels to default Windows audio endpoints.
  - **Single Authoritative Playback Session**: Synchronized single-session media playback governed by `MediaPresentationService` where audio and video seek, pause, resume, and loop as a single atomic stream.
  - **Mute & Volume Controls**:
    - Normalized volume range `[0.0, 1.0]` internally with 0%..100% display and two-way slider bindings.
    - Dashboard Video Controls Bar: Integrated speaker mute toggle button (`\uE74F` / `\uE767`), volume slider, and percentage text indicator.
    - Floating Presenter Dock: Compact mute button (`\uE74F` / `\uE767`) directly on the single-row dock toolbar (shown only when video is on air) and full volume slider + mute toggle inside the `PresenterDockMenuWindow` More Options popup without expanding dock dimensions.
  - **Playback Lifecycle & Privacy Integration**:
    - Video Play: Plays audio in accordance with volume and mute settings.
    - Video Pause: Pauses audio and video in sync.
    - Video Resume: Resumes audio and video together.
    - Video Restart: Resets playback position to 00:00 for both audio and video.
    - Presentation Blackout: Suspends video rendering and suppresses audio immediately. Restoring blackout resumes prior playback.
    - Stop Presenting: Safely stops and disposes active media player, unloads resources, closes Presentation Output, and foregrounds Control Dashboard.
    - Source Switching Safety: Instant audio termination when switching between `Video -> Image`, `Video -> Window`, and `Video -> Video` (no dual audio streams or orphaned background audio).
  - **Settings Persistence**: Persists `MediaVolume` and `IsMediaMuted` across application restarts using `IApplicationSettingsService`.
  - **Meeting Audio Guidance**: Documented separation between local playback and conferencing meeting transmission (e.g., Google Meet "Also share system audio").
- [x] **Complete Media Presentation Integration (Phase 6.1)**:
  - Full end-to-end presentation pipeline verified across all 4 source types (`WindowSource`, `MonitorSource`, `ImageMediaSource`, `VideoMediaSource`).
  - **Unified Presentation Queue**: Single authoritative queue managed by `IPresentationStateService` containing both desktop capture sources and file-backed media sources with stable type-safe identities.
  - **Dashboard Source Picker & Controls**: Target source picker dropdown, queued sources strip, and video playback controls bar (Play/Pause, Restart, Loop toggle, mm:ss timecode) dynamically displayed when video is on air.
  - **Floating Presenter Dock & Popups**: Seamless integration in both Expanded and Compact dock modes. Source button dynamically binds to `ActiveSourceGlyph` (`\uEB9F` for Image, `\uE714` for Video, `\uE7F4` for Window, `\uE790` for Monitor) and `ActiveSourceTitle`. `PresenterDockMenuWindow` ListView displays correct category labels and glyphs.
  - **Global Hotkey Navigation**: `NextSource` (`Ctrl+Shift+Right`), `PreviousSource` (`Ctrl+Shift+Left`), and direct index hotkeys (`Ctrl+Shift+1..5`) cycle through mixed queues (Window -> Image -> Video -> Monitor) with zero crashes or race conditions.
  - **Presentation Coordinator Routing**: `PresentationCoordinator` routes media files cleanly to `MediaPresentationService` without sending file paths to `Windows.Graphics.Capture`.
  - **Presentation Output Window (Single Stable HWND)**: Mutually exclusive visual layers in `PresentationWindow` (Screen Capture `Image`, Direct Static `Image` with `Uniform` centering, `MediaPlayerElement` bound to `MediaPlayer`, Standby canvas, and topmost Blackout overlay).
  - **Three Switching Modes with Media**:
    - `Active + Live`: Takes media live immediately. Window activation gracefully skips file-backed sources without errors or focusing unrelated windows.
    - `Live Only`: Takes media live without changing application window focus.
    - `Active Only`: Updates selected queue cursor only without changing On-Air presentation output.
  - **Transition & Resource Safety**: Stop/unload of obsolete video players and capture sessions during rapid source switching. Stable Presentation HWND preserved across all transitions.
  - **Fail-Closed & Media Error Handling**: Non-existent, corrupted, or unsupported media files cleanly set error state on presenter dashboard/dock without crashing or interrupting presentation output.
- [x] **Floating Presenter Dock Native Dragging Hotfix**:
  - Implemented routed pointer event handlers (`AddHandler` with `handledEventsToo: true`) on `DockCardBorder` in [Views/PresenterDockWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml.cs).
  - Instant native dragging (`ReleaseCapture` + `WM_NCLBUTTONDOWN` / `HTCAPTION`) when pressing on non-interactive surfaces.
  - Smooth click-versus-drag detection on interactive controls using physical pixel movement threshold (5px / `DragThresholdSquared = 25`).
- [x] **Global Text Truncation & Dropdown Width Constraints**:
  - Fixed horizontal expansion and clipping on Dashboard source selectors, Status Strip Active Source, Sources Page list rows, and Floating Presenter Dock active source buttons.
- [x] **Unified Stop Presenting Workflow & Automatic Dashboard Activation**:
  - Authoritative Stop sequence in `PresentationCoordinator.StopPresentationAsync()` safely terminates active capture/media, sets status to `Idle`, closes `PresentationWindow`, restores `MainWindow`, and brings Control Dashboard to foreground.
- [x] **Windows 11 Settings-Style Theme Selector & Appearance Layout (Phase 6)**:
  - Compact Windows Settings-style theme `ComboBox` with immediate theme application and persistence.
- [x] **Automated Unit & Regression Test Suite**:
  - **239 comprehensive automated unit and regression tests** in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) with 100% pass rate.

### Planned (Upcoming)
- [ ] **Phase 7**: Advanced Presenter Features (Live thumbnail previews, smooth transitions).
- [ ] **Phase 8**: Packaging and Distribution (MSIX store packaging option).

---

## 3. INVESTIGATION & KNOWN ISSUES

1. **Conferencing Audio Transmission**: Local media audio playback is distinct from meeting audio sharing. In applications like Google Meet, presenters must enable "Also share system audio" when selecting the SwitchCast Presentation Output window.
2. **Minimized Window OS Policy**: As per standard Windows Graphics Capture design, minimized application windows do not produce new Direct3D frames until restored.

---

## 4. ARCHITECTURE DECISION RECORDS

- [ADR-0001: Technology Stack & Clean Architecture Core](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/docs/decisions/ADR-0001-architecture.md) — Implemented across all phases.

---

## 5. DETECTED ENVIRONMENT & TOOLING

- **Host OS**: Windows 11 Pro (OS Build 10.0.22631, win-x64)
- **.NET SDK**: 8.0.403 (`C:\Program Files\dotnet\sdk\8.0.403\`)
- **Target Framework**: `net8.0-windows10.0.19041.0`
- **Architecture**: `x64` (`win-x64`)
- **Windows App SDK**: 1.5.240802000

---

## 6. VERIFICATION RECORD

- **Level 1 (Compilation - Debug)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors).
- **Level 1 (Compilation - Release)**: `dotnet build SwitchCast.csproj -c Release -p:Platform=x64` -> PASS (0 warnings, 0 errors).
- **Level 2 (Static Analysis)**: Nullable reference checks and analyzer validation -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (307 passed, 0 failed, 0 skipped).
- **Level 4 (Native Verification)**: Automated mocked unit suite passed completely. Interactive hardware (physical webcam, physical multi-monitor, installed PowerPoint application) is categorized as `IMPLEMENTED — RUNTIME UNVERIFIED` until physically exercised on target Windows machines.

---

## 7. NEXT RECOMMENDED TASK

**Task**: **Phase 7 — Advanced Presenter Features & Smoothing**
- **Objective**: Live thumbnail preview rendering on dashboard cards, configurable smooth transitions, and presentation profiles.
