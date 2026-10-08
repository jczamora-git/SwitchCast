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
