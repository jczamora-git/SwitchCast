# SWITCHCAST — TESTING & VERIFICATION STRATEGY

---

## 1. TESTING PHILOSOPHY & TEST PYRAMID

SwitchCast enforces a multi-layered verification strategy designed to maximize test speed, stability, and reliability while rigorously isolating hardware-dependent Windows APIs from core domain logic.

```
       / \
      /   \      Level 5: Manual Desktop & Meeting App Validation (Targeted)
     /-----\
    /       \    Level 4: Component Integration & Lifecycle Tests
   /---------\
  /           \  Level 3: Domain & Presentation Unit Tests (Fast, In-Memory)
 /-------------\
/               \ Level 1 & 2: Compiler Diagnostics & Static Analyzers
-----------------
```

---

## 2. AUTOMATED TESTING LAYERS

### Layer 1 & 2: Static Analysis & Compilation Gates
- Every touched project must compile with zero errors and zero unaddressed warnings.
- Roslyn analyzers enforce nullability, disposable handling, and async best practices.

### Layer 3: Unit Tests (`SwitchCast.Tests.Unit`)
- **Scope**: Pure business logic, state machines, view models, and configuration.
- **Execution Target**: < 1.0 second per test suite run. Zero OS or hardware dependencies.
- **Key Test Suites**:
  - `PresentationStartupAndThreadingTests`: Validates first queued source auto-start, authoritative source resolution, idempotent start requests, and UI thread dispatcher marshaling under off-thread worker callbacks.
  - `PresentationCoordinatorTests`: Validates state transitions (`Idle` -> `Active` -> `Paused` -> `Blackout` -> `Disposed`).
  - `SourceSelectionManagerTests`: Validates queuing, duplicate filtering, order re-arrangement, and active selection index bounds.
  - `HotkeyBindingTests`: Validates serialization, key-combo parsing, and conflict detection.

### Layer 4: Integration Tests (`SwitchCast.Tests.Integration`)
- **Scope**: Service boundary interactions, mock capture pipelines, and lifecycle teardown.
- **Key Test Suites**:
  - `CaptureLifecycleTests`: Verifies that switching sources triggers clean session termination, frame pool disposal, and new session initialization without deadlocks.
  - `DiscoveryServiceTests`: Verifies that invalid window handles and cloaked windows are rejected by filtering algorithms.

---

## 3. MOCKING PLATFORM BOUNDARIES

Because `Windows.Graphics.Capture` and Win32 `HWND` handles require an interactive Windows desktop session, infrastructure interfaces are mocked during automated CI testing:

```csharp
public interface ICaptureService : IAsyncDisposable
{
    bool IsCapturing { get; }
    event EventHandler<CaptureFrameEventArgs> FrameArrived;
    event EventHandler<CaptureErrorEventArgs> CaptureFailed;

    Task StartCaptureAsync(CaptureSource source, CancellationToken cancellationToken = default);
    Task StopCaptureAsync();
    Task SwitchSourceAsync(CaptureSource newSource, CancellationToken cancellationToken = default);
}
```

In unit tests, `Mock<ICaptureService>` allows verification of error propagation, state transitions, and cancellation handling without requiring an actual display.

---

## 4. EDGE CASE TEST MATRIX

| Scenario | Expected System Behavior | Verification Method |
| :--- | :--- | :--- |
| **Captured Window Closed by User** | Cleanly transition to `SourceClosed` state; display "Source Closed" overlay on presentation output; no crash. | Unit & Integration Test |
| **Captured Window Minimized** | Transition to `SourcePaused` or render last good frame / paused banner; no crash. | Integration Test |
| **Monitor Disconnected** | Detect display removal via message loop; fallback to default primary monitor or paused screen. | Manual & Contract Test |
| **Direct3D Device Loss (`DXGI_ERROR_DEVICE_REMOVED`)** | Release stale DirectX textures; re-create device context and swapchain; resume frame rendering. | Unit Mock / Stress Test |
| **Rapid Hotkey Switching (Stress)** | Debounce rapid inputs; cleanly cancel in-flight transitions without orphaned capture sessions. | Unit Concurrency Test |

---

## 5. MANUAL DESKTOP VALIDATION PROTOCOL (Level 5)

When manual validation is requested or required before major milestone releases:
1. **Interactive Multi-Window Switch**: Open 3 distinct applications (e.g., Notepad, VS Code, Browser) and 2 monitors. Queue all 5 sources. Switch between them sequentially and verify transition latency < 150ms.
2. **Third-Party Meeting App Test**:
   - Launch Zoom / MS Teams / Google Meet.
   - Share the SwitchCast Presentation Output Window.
   - Switch active sources in SwitchCast.
   - Verify on receiving client device that video feed remains continuous without stutter or black flash.

---

## 6. REGRESSION BEHAVIOR CONTRACTS

To ensure previously functioning features do not regress during future development, executable behavior contracts are maintained in `SwitchCast.Tests/Services/FullStabilityRegressionContractTests.cs`.

Every pull request and release gate MUST verify the following 5 contract domains:

### Domain A: Window & Session Lifecycle
- **Stop Presenting**: Closes `PresentationWindow`, activates `MainWindow` Control Dashboard, clears output renderers, and preserves the queued sources intact (`Contract_A1`).
- **Dock X with Active Presentation**: Displays custom modal confirmation ("Stop presentation and close Dock?"); clicking Cancel preserves presentation and dock (`Contract_A2`); clicking Stop & Close stops presentation, closes output, closes dock, and activates Dashboard (`Contract_A3`).
- **Dock X while Idle**: Closes Floating Dock immediately without confirmation prompt and activates Dashboard (`Contract_A4`).
- **Full Application Exit**: Closes all secondary windows without secondary dock confirmation prompts and terminates cleanly without Dashboard reactivation (`Contract_A5`).

### Domain B: PowerPoint Slide State Retention
- **Slide Index Retention Across Switches**: Advancing slides in PowerPoint and switching to Video or Camera preserves the current slide index in `SelectedSources`. When returning to PowerPoint, the presentation resumes at the exact slide (`Contract_B1`).

### Domain C: Video Playback Performance
- **Dispatcher Throttle Protection**: High-frequency playback position callbacks from `MediaPlaybackSession` are throttled to at most once per 250ms in `MediaPresentationService`, preventing UI message loop starvation (`Contract_C1`).

### Domain D: Camera Bounded Frame Delivery
- **Backpressure & Disposal**: Video frame arrival uses a bounded latest-frame-wins delivery loop with `NotifyFrameCompleted()` backpressure signaling and immediate disposal of dropped frames (`Contract_D1`).

### Domain E: Diagnostics & Hang Watchdog
- **Synchronous Critical Flush**: Critical and terminating exceptions (`IsTerminating == true`) bypass thread-pool queues and write incident files synchronously to disk prior to process exit (`Contract_E1`).
- **UI Hang Watchdog Lifecycle**: Background watchdog safely tracks UI message loop heartbeat and records diagnostic incidents upon detecting dispatcher starvation (`Contract_E2`).

### Domain F: Native Teardown & Stop Lifecycle Contract
- **Stop Order & Drainage**: When Stop Presenting is requested, Camera capture is stopped and pending frames drained, WGC capture is stopped, PowerPoint slideshow is closed, and Video/Image presentation is stopped prior to clearing renderer generations or destroying the window (`StopLifecycleNativeCrashRecoveryTests`).
- **Window Teardown Boundary**: `PresentationWindow.Cleanup()` explicitly severs `PresentationOutputImage.Source = null`, `PresentationDirectImage.Source = null`, and `PresentationVideoPlayer.SetMediaPlayer(null)` before native HWND destruction, preventing `AccessViolationException` (`0xC0000005`) in `Microsoft.WinUI.dll`.
- **Stop Idempotency**: Multiple concurrent Stop requests are deduplicated through `_activeStopTask` so that exactly one authoritative cleanup execution occurs.
- **Expected Cancellation Handling**: `OperationCanceledException` (and `TaskCanceledException`) during source switching or window teardown is caught and logged at debug level as normal lifecycle cancellation rather than reported as an error incident.

### Domain G: Presenter Dock Close & Application Exit Hotfix
- **Dock Confirmation Cancel (Test A)**: When an active presentation is running, clicking Dock X invokes confirmation; clicking Cancel keeps presentation live, keeps output and dock open, and never stops presentation (`DockCloseLifecycleHotfixTests.TestA`).
- **Dock Stop & Close (Test B)**: Clicking Stop & Close invokes authoritative `StopPresentationAsync`, closes output window, closes dock, activates MainWindow once on Dashboard, and keeps MainWindow and process alive (`DockCloseLifecycleHotfixTests.TestB`).
- **Stop from Dashboard (Test C)**: Clicking Stop Presenting closes output window, clears renderers, activates MainWindow once, and leaves application alive (`DockCloseLifecycleHotfixTests.TestC`).
- **Idle Dock Close (Test D)**: Closing dock while idle skips confirmation, closes dock, and restores Dashboard (`DockCloseLifecycleHotfixTests.TestD`).
- **Repeated Stop & Close Cycles (Test E)**: 25 rapid Start -> Stop & Close -> Start cycles across sources execute with zero exceptions and single MainWindow activation (`DockCloseLifecycleHotfixTests.TestE`).
- **Native Window Lifetime (Test F)**: `Win32WindowActivationService` verifies window validity via `IsWindow`; invalid or null HWNDs are rejected without crashing (`DockCloseLifecycleHotfixTests.TestF`).
- **Application Shutdown Bypass (Test G)**: Full application exit bypasses dock confirmation and avoids duplicate Dashboard reactivation (`DockCloseLifecycleHotfixTests.TestG`).
