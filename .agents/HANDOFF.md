# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: SWITCHCAST v1.5.0 — Native Stability, Sources Synchronization & D3D11 Reset Hotfix
- **Date**: 2026-10-20T02:35:00+08:00 (UTC+8)
- **Status**: Completed, Validated & Committed Locally (471 Tests Passing, 0 Build Warnings/Errors, Clean Architecture)

---

## 1. Objectives & Outcomes

1. **Sources Default Category & Synchronized Dropdown**:
   - Introduced `SourceCategoryOption` model (`Models/SourceCategoryOption.cs`) with stable category identity, observable count, and observable `DisplayText`.
   - `SourcesViewModel` maintains an authoritative, permanent `ObservableCollection<SourceCategoryOption>` collection initialized with 6 categories.
   - Initialized `SelectedCategoryIndex = 1` (`Application Windows`) by default on fresh application launches, automatically displaying discovered application windows without manual presenter interaction.
   - Bound `CategoryComboBox` in `Views/SourcesPage.xaml` to `CategoryOptions` with `SelectedItem="{x:Bind ViewModel.SelectedCategoryOption, Mode=TwoWay}"` and `DisplayMemberPath="DisplayText"`.
   - In `Views/SourcesPage.xaml.cs`, moved `ViewModel` initialization before `InitializeComponent()`, ensuring the ComboBox binds to populated category options during page load.
   - Preserves category selection across session navigations (Dashboard <-> Sources) via singleton `SourcesViewModel` lifetime.
   - Category counts update in-place via `INotifyPropertyChanged` without resetting ComboBox selection or blanking the collapsed label.
   - Hardened `ApplyFilter()` fallback switch to default to Application Windows (`_ => _allWindows`), preventing invalid or unselected states from defaulting to Cameras.
   - Added guard in `OnSelectedCategoryIndexChanged` preventing out-of-range or negative indices (-1) from de-synchronizing selection.

2. **P0 Invalid Window Handle (`0x80070578`) Teardown Guard**:
   - Discovered root cause: during `StopPresentationAsync`, clearing active source triggered `OnViewModelPropertyChanged` for `IsActiveSourcePowerPoint` and `HasSecondaryRow`, enqueueing `ApplyWindowSizingAndPosition` via `DispatcherQueue.TryEnqueue`. Immediately afterward, `_dockService.CloseDock()` destroyed the window handle (`target.Close()`). When the dispatcher processed the queued lambda, `_appWindow.Resize(...)` ran on the destroyed HWND, throwing `COMException (0x80070578): Invalid window handle`.
   - Added `_isClosed` flag to `Views/PresenterDockWindow.xaml.cs`, unsubscribing size/load/property/appwindow handlers in `OnWindowClosed`, setting `_appWindow = null`, and zeroing `WindowHandle = IntPtr.Zero`.
   - Added `IsWindow(WindowHandle)` and `_isClosed` guards to `ApplyWindowSizingAndPosition`, `UpdateNonClientRegions`, `OnViewModelPropertyChanged`, and `OnAppWindowChanged` with defensive exception logging.
   - Hardened `PresenterDockConfirmationWindow.xaml.cs` and `PresentationWindow.xaml.cs` to zero `WindowHandle` and null `_appWindow` on window closure.

3. **Direct3D 11 Reset Diagnostics & Device Recovery**:
   - Discovered root cause of 6 false device reset warnings: `SetRenderingMode()` in `Direct3D11DeviceProvider.cs` was unconditionally calling `ResetDevice()`, which logged a warning via `_metricsService?.RecordDeviceReset()`. Additionally, if `_winrtDevice` was null at startup, it called `ResetDevice()` even when setting the default Auto mode.
   - Refactored `SetRenderingMode(mode)`: if mode is unchanged, returns immediately; if mode changed, safely recreates the device without calling `ResetDevice()` or recording false device loss events.
   - In `CaptureSessionManager.cs`, added comprehensive diagnostics logging on genuine device loss (`0x887A0005`, `0x887A0007`, `0x887A0006`): HRESULT, Operation (`CaptureFrameProcessing`), Mode, Backend, Generation, RecoveryAttempt, and RecoveryResult. Removed duplicate `RecordDeviceReset()` call.
   - Preserved complete Auto, PreferHardware, and CompatibilityMode adaptive rendering behavior.

---

## 2. Verification Evidence

1. **Automated Unit & Contract Test Suite**:
   - Total Tests: **471 Passed, 0 Failed, 0 Skipped (100% Pass Rate)**.
   - Added regression test suite:
     - `SwitchCast.Tests/Services/NativeStabilityAndSourcesHotfixTests.cs` (8 new tests)
   - Discrepancy Note: Earlier report of 438 tests was taken prior to v1.4.0 playlist fixes (+5 tests = 443) and v1.5.0 performance telemetry/capability tests (+20 tests = 463). With the 8 new regression tests, test count is now 471.

2. **Compiler Quality Gate**:
   - `Debug x64`: **0 Warnings, 0 Errors**
   - `Release x64`: **0 Warnings, 0 Errors**

3. **Native Runtime Verification**:
   - Unit and contract tests exercise ViewModel category options, in-place header updating, guard logic, device mode switching, and metric recording.
   - Physical WinUI 3 XAML desktop rendering, modal popups, and native window closure teardown in interactive display environments: NATIVE VERIFICATION PENDING runtime GUI session.

---

## 3. Local Commits

- `c30281f` — fix(sources): synchronize default category and dropdown
- `a94005f` — fix(lifecycle): prevent invalid hwnd after switchdeck close
- `0bf413a` — fix(perf): distinguish device initialization from reset

---

## 4. Modified Files Map

- `Models/SourceCategoryOption.cs` (New model for stable category options)
- `ViewModels/SourcesViewModel.cs` (CategoryOptions observable collection, SelectedCategoryOption synchronization, header in-place updates, safe fallback)
- `Views/SourcesPage.xaml` (CategoryComboBox ItemsSource and SelectedItem binding with DisplayMemberPath)
- `Views/SourcesPage.xaml.cs` (ViewModel initialized before InitializeComponent)
- `Views/PresenterDockWindow.xaml.cs` (_isClosed flag, nulling _appWindow, zeroing WindowHandle, IsWindow checks, try-catch guards)
- `Views/PresenterDockConfirmationWindow.xaml.cs` (Nulling _appWindow and zeroing WindowHandle on closure)
- `Views/PresentationWindow.xaml.cs` (Nulling _appWindow, zeroing WindowHandle, IsWindow check before RemoveWindowSubclass)
- `Services/Capture/Direct3D11DeviceProvider.cs` (SetRenderingMode recreates device without calling ResetDevice or logging false warnings)
- `Services/Capture/CaptureSessionManager.cs` (Comprehensive diagnostic logging on genuine device loss, removed duplicate reset metric call)
- `SwitchCast.Tests/Services/NativeStabilityAndSourcesHotfixTests.cs` (8 regression tests)
