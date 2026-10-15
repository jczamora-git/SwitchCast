# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Presentation Pipeline Hotfix (First Queued Source Auto-Start + Camera/Video UI Threading Stabilization)
- **Date**: 2026-10-15T17:25:00+08:00 (UTC+8)
- **Status**: Completed, Validated & Ready for Local Commit (326 Tests Passing)

---

## 1. Objectives & Confirmed Root Causes Resolved

1. **COMException 0x8001010E (RPC_E_WRONG_THREAD) in Camera and Video Pipelines**:
   - **Root Cause**:
     - `PresentationCoordinator.OnCameraFrameArrived` arrives on a WinRT background worker thread. When the first frame is accepted, it calls `_presentationStateService.SetStatus(PresentationStatus.Active)`.
     - `PresentationStateService.PropertyChanged` notified `DashboardViewModel` and `PresentationViewModel` synchronously on the background worker thread.
     - `DashboardViewModel` and `PresentationViewModel` previously had no dispatcher marshaling; they re-raised `PropertyChanged` on the background thread.
     - WinUI compiled bindings (`x:Bind`) in `DashboardPage.xaml` (`TextBlock.set_Text`) and `PresentationWindow.xaml` (`UIElement.set_Visibility`) executed on the worker thread, throwing `0x8001010E (RPC_E_WRONG_THREAD)`.
     - In `MediaPresentationService.PlayVideoAsync`, `MediaStateChanged?.Invoke(...)` was raised synchronously within `PlayVideoAsync`'s `try { ... } catch (Exception ex) { ... }` block. When UI subscribers to `MediaStateChanged` threw `RPC_E_WRONG_THREAD`, `PlayVideoAsync` caught the exception and erroneously reported a false "Failed to play video (0x8001010E)" capture alert.
   - **Resolution**:
     - Created `IUIThreadDispatcher` abstraction with `DispatcherQueueAdapter`, `ImmediateUIThreadDispatcher`, and thread-safe registration.
     - Established explicit per-window dispatcher ownership across `MainWindow` (`DashboardViewModel`, `SourcesViewModel`), `PresentationWindow` (`PresentationViewModel`), and `PresenterDockWindow` (`PresenterDockViewModel`).
     - Marshaled all UI-bound `PropertyChanged` notifications in ViewModels via `_dispatcher.TryEnqueue(...)` without unsafe background fallback execution (never running action when `TryEnqueue` fails).
     - Moved `MediaStateChanged` event firing outside of `PlayVideoAsync`'s player initialization `try-catch`, preventing UI-side subscriber exceptions from masquerading as video playback decoding failures.

2. **First Queued Source Auto-Start Bug**:
   - **Root Cause**:
     - When sources were queued, `DashboardPage` displayed the first queued source in its `ComboBox` via `x:Bind SelectedPresentationSource`.
     - However, when the ComboBox layout updated or items refreshed, WinUI ComboBox pushed a spurious `null` or unselected value into `SelectedPresentationSource`.
     - When the presenter clicked "Start Presenting", `StartPresentationAsync` checked `SelectedPresentationSource ?? SelectedPreviewSource`. If `SelectedPresentationSource` was null and preview was active, it bypassed source switching, or if null, it opened Presentation Output in Standby without activating the first queued item.
   - **Resolution**:
     - Created `DashboardViewModel.ResolveAuthoritativeStartSource()` with strict priority:
       1. Valid explicitly selected queued source from `SelectedPresentationSource`.
       2. First valid source from `PresentationStateService.SelectedSources`.
       3. Null (start disabled if queue is empty).
     - Guarded `SelectedPresentationSource` against spurious null pushes during UI re-binding.
     - Updated `PresentationCoordinator.StartPresentationAsync`: when invoked without an explicit parameter, resolves target source to the first queued item in `SelectedSources` if the current active source is not queued.
     - Added `IsStarting` guard to `DashboardViewModel` disabling `CanStartPresentation` while startup is in-flight to prevent duplicate start storms.

---

## 2. Validation Results

- **Level 1 (Compilation - Debug)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> **PASS** (0 errors, 0 warnings).
- **Level 1 (Compilation - Release)**: `dotnet build SwitchCast.csproj -c Release -p:Platform=x64` -> **PASS** (0 errors, 0 warnings).
- **Level 2 (Static Analysis)**: Roslyn compiler diagnostics -> **PASS** (0 errors, 0 warnings).
- **Level 3 (Unit & Integration Tests)**: `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Debug` -> **PASS** (326 passed, 0 failed, 0 skipped, 16 new tests in `PresentationStartupAndThreadingTests.cs`).
- **Level 4 (Native Verification)**: Automated mocked unit suite passed completely. Interactive hardware validation with physical webcam (A4tech FHD 1080P PC Camera) and display outputs is marked `IMPLEMENTED — RUNTIME UNVERIFIED` until physically exercised on target Windows host.

---

## 3. Recommended Next Steps

1. Queue sources (Window -> Video -> PowerPoint -> Camera).
2. Without manually selecting a dropdown item, click "Start Presenting" once.
3. Verify that the first queued item immediately goes On Air into Presentation Output.
4. Verify that camera and video transitions run smoothly without `COMException 0x8001010E` or false video alerts.
5. Exercise switching modes (Active + Live, Active Only, Live Only).
