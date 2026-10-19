# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: SWITCHCAST v1.5.0 — Lightweight Performance & Adaptive Rendering (Phased Implementation)
- **Date**: 2026-10-20T02:05:00+08:00 (UTC+8)
- **Status**: Completed, Validated & Committed Locally (463 Tests Passing, 0 Build Warnings/Errors, Clean Architecture)

---

## 1. Objectives & Outcomes

1. **Phase A — Capability Probing & Performance Telemetry**:
   - `IGraphicsCapabilityService` & `GraphicsCapabilityService`: Zero-allocation native DXGI 1.1 adapter enumeration (`CreateDXGIFactory1`, `EnumAdapters1`, `DXGI_ADAPTER_DESC1`) extracting adapter name, dedicated VRAM, shared system memory, vendor ID, and software flags.
   - Probes Direct3D 11 hardware capability and exact feature levels (11.1, 11.0, 10.1, 10.0, 9.3) as well as WARP software rasterizer device creation.
   - Accurately detects integrated GPUs (Intel UHD/Iris Xe, AMD Vega) to adapt queue pacing and memory footprint without forcing software rendering. Caches results and auto-refreshes on device change.
   - `IPerformanceMetricsService` & `PerformanceMetricsService`: Real-time thread-safe performance counter aggregation (`Interlocked.Increment`, `Interlocked.Exchange`, `Stopwatch.GetTimestamp()`) for cold startup duration, presentation startup latency, source switch latency, preview FPS, output FPS, frame drops, queue depth, device reset count, and process memory with zero per-frame disk writes.

2. **Phase B — Adaptive Rendering Backend Selection**:
   - Implemented user-selectable `RenderingMode`: `Auto (Recommended)`, `Prefer Hardware`, and `Compatibility Mode`.
   - Enhanced `IDirect3D11DeviceProvider` and `Direct3D11DeviceProvider` to initialize Direct3D 11 devices based on selected mode:
     - `Auto`: Attempts hardware acceleration; seamlessly falls back to WARP software rasterizer if hardware creation fails.
     - `Prefer Hardware`: Enforces hardware D3D11 device creation; reports exact HRESULT without silent software fallback.
     - `Compatibility Mode`: Directly requests WARP software rasterizer (`D3D_DRIVER_TYPE_WARP`) for troubleshooting problematic or crashing GPU drivers.
   - Corrected `D3D_DRIVER_TYPE_WARP = 5` in `NativeCaptureMethods.cs` (previously misdeclared as 2).
   - Added `SetRenderingMode(mode)` supporting live backend switching with safe device recreation.

3. **Phase C — GPU-Backed Frame Processing Improvements**:
   - Optimized `CaptureSessionManager.cs` to copy surfaces directly with `BitmapAlphaMode.Premultiplied` in a single native pass via `SoftwareBitmap.CreateCopyFromSurfaceAsync(surface, BitmapAlphaMode.Premultiplied)`.
   - Completely bypassed the secondary `SoftwareBitmap.Convert(...)` allocation and copy loop, saving ~8.3 MB per frame at 1080p and preventing unified memory bus thrashing on integrated GPUs.
   - Enforced strict rate-limiting on dashboard preview (~15 FPS) in `Direct3D11PreviewRenderer.cs`, protecting audience-facing output priority.

4. **Phase D & E — Software Fallback & Self-Healing Device Recovery**:
   - Added automatic device-loss recovery in `CaptureSessionManager.cs` handling `DXGI_ERROR_DEVICE_REMOVED` (`0x887A0005`), `DXGI_ERROR_DEVICE_RESET` (`0x887A0007`), and `DXGI_ERROR_DEVICE_HUNG` (`0x887A0006`). Automatically resets `_deviceProvider`, increments metrics, and recreates `Direct3D11CaptureFramePool` without closing or recreating `MainWindow` or `SwitchDeck`.
   - In `CaptureCoordinator.cs`, wired device reset and single recovery retry if capture start fails in `Auto` mode.

5. **Phase F — Performance Settings UI & Documentation**:
   - Added dedicated **Performance & Adaptive Rendering** category to `Views/SettingsPage.xaml` and `ViewModels/SettingsViewModel.cs` (Sidebar index 6, glyph `\uE9E9`).
   - Rendering mode selection `ComboBox` (`Auto (Recommended)`, `Prefer Hardware`, `Compatibility Mode`).
   - 3-card summary strip displaying Active Backend (`ActiveBackendText` & `HardwareAccelerationStatus`), Primary Graphics Adapter (Name & VRAM), and Direct3D Feature Level & Vendor.
   - Adaptive Resource Profile notification banner informing presenter of current GPU profile.
   - In-memory performance diagnostics toggle switch (`EnablePerformanceDiagnostics`).
   - "Refresh Hardware Info" button bound to `RefreshPerformanceDataCommand`.

---

## 2. Verification Evidence

1. **Automated Unit & Contract Test Suite**:
   - Total Tests: **463 Passed, 0 Failed, 0 Skipped (100% Pass Rate)**.
   - New test suites:
     - `SwitchCast.Tests/Services/GraphicsCapabilityServiceTests.cs` (4 tests)
     - `SwitchCast.Tests/Services/PerformanceMetricsServiceTests.cs` (7 tests)
     - `SwitchCast.Tests/Services/Direct3D11DeviceProviderTests.cs` (6 tests)
     - `SwitchCast.Tests/ViewModels/SettingsViewModelTests.cs` (+3 tests)

2. **Compiler Quality Gate**:
   - `Debug x64`: **0 Warnings, 0 Errors**
   - `Release x64`: **0 Warnings, 0 Errors**

3. **Functionality Invariants Maintained**:
   - Background Music playback independent from Video `MediaPlayer`.
   - PowerPoint static slide caching and live COM slideshow unaffected.
   - Camera `MediaCapture` exclusive device lifecycle preserved.
   - SwitchDeck transport and song picker controls operating as intended.
   - Window lifecycle invariant: closing presentation or dock never terminates `MainWindow`.

---

## 3. Local Git Commits

- Commit `3f1211d`: `feat(perf): implement Phase A graphics capability detection and performance telemetry`
- Commit `ab0110b`: `feat(perf): implement Phase B adaptive rendering backend selection with Auto, PreferHardware, and CompatibilityMode (WARP)`
- Commit `b24e355`: `feat(perf): implement Phases C-E GPU-backed frame pipeline optimization and device loss recovery`
- Commit `43788f2`: `feat(perf): implement Phase F Performance Settings UI and adaptive rendering controls`
