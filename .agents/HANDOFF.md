# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Floating Presenter Dock Stability, Mixed-Source Navigation & Direct Unqueue
- **Date**: 2026-10-10T05:00:00+08:00 (UTC+8)
- **Status**: Completed, Verified & Committed Locally

---

## 1. Objectives Implemented

1. **Bug 1 — Dock Double-Click Maximization Prevention**:
   - **Confirmed Root Cause**: WinUI 3 `InputNonClientPointerSource` non-client caption regions (`HTCAPTION`) passed `WM_NCLBUTTONDBLCLK` to default window procedure, which interpreted double-clicks as caption double-click maximize commands despite `OverlappedPresenter.IsMaximizable = false`.
   - **Native Window Subclassing**: Subclassed dock HWND via `comctl32.dll` (`SetWindowSubclass`). Handled `WM_NCLBUTTONDBLCLK` on `HTCAPTION` (returns `IntPtr.Zero`), intercepted `WM_SYSCOMMAND` `SC_MAXIMIZE`, clamped `WM_GETMINMAXINFO` tracking bounds, stripped `WS_MAXIMIZEBOX` and `WS_THICKFRAME` from `GWL_STYLE`, removed `SC_MAXIMIZE` from system menu, and hooked `AppWindow.Changed` auto-restoration.
   - **Preserved Presentation Fullscreen**: The dock fullscreen button continues toggling fullscreen on `PresentationWindow` without dock maximization or window recreation. Native hold-and-drag, DPI scaling, and always-on-top remain intact.

2. **Bug 2 — Next/Previous Mixed Source Navigation Fix**:
   - **Confirmed Root Cause**:
     1. `CaptureCoordinator.StopPreviewInternalAsync()` called `_presentationStateService.SetActiveSource(null)`, causing `ActiveSource` to drop to null midway through Window -> Media transitions, triggering `DashboardViewModel` to fire a redundant switch that canceled the transition as superseded.
     2. `PresentationCoordinator.SwitchToNextSourceAsync()` previously filtered by `.Where(s => s.IsAvailable)` instead of the authoritative queue `_presentationStateService.SelectedSources`, causing index divergence from the dock dropdown.
     3. `ActiveAndLive` mode attempted native HWND window activation on media sources (images/videos), which failed and blocked transition.
   - **Authoritative Single Ordered Queue**: Both Next and Previous traverse `_presentationStateService.SelectedSources` directly across all source types (Window, Monitor, Image, Video) in exact dropdown order.
   - **Switching Mode Semantics**: Media sources are taken live directly without window activation in `ActiveAndLive` mode. Paused and blackout states are respected upon video transitions.

3. **Bug 3 — Presenter Dock Direct Source Unqueue**:
   - **Interactive Checkbox Control**: Replaced static glyph in `PresenterDockMenuWindow` with an interactive `CheckBox` control with tooltip "Remove from presentation queue".
   - **Popup Persistence**: Menu stays open upon unqueue, updates item count, shrinks window dimensions, and switches to empty state when queue count reaches 0.
   - **On-Air Continuity Policy**: Unqueueing an On-Air source removes it from the future navigation queue while preserving active presentation output until the presenter explicitly switches or stops.
   - **Deterministic Cursor Recalculation**: If the unqueued item was selected, cursor advances to the next queued item (or previous if removing final item; or null if empty).
   - **UI Synchronization**: Dock dropdown, Dashboard source selector, and Sources tab checkboxes stay synchronized immediately.

---

## 2. Key Commits & Files Changed

- **Commit 1 (`0bf9cc0`)**: `fix: prevent floating dock maximization`
  - `Views/PresenterDockWindow.xaml.cs`
- **Commit 2 (`9e676a3`)**: `fix: repair mixed-source queue navigation`
  - `Services/Capture/CaptureCoordinator.cs`
  - `Services/PresentationCoordinator.cs`
  - `ViewModels/PresenterDockViewModel.cs`
  - `ViewModels/DashboardViewModel.cs`
  - `SwitchCast.Tests/Services/PresentationCoordinatorMediaTests.cs`
- **Commit 3 (`8d0cf84`)**: `feat: allow source unqueue from presenter dock`
  - `Services/PresentationStateService.cs`
  - `Services/PresentationCoordinator.cs`
  - `ViewModels/PresenterDockViewModel.cs`
  - `ViewModels/SourcesViewModel.cs`
  - `ViewModels/DashboardViewModel.cs`
  - `Views/PresenterDockMenuWindow.xaml`
  - `Views/PresenterDockMenuWindow.xaml.cs`
  - `SwitchCast.Tests/Services/PresentationStateServiceTests.cs`
  - `SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs`

---

## 3. Validation Results

- **Level 1 (Compilation)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> **PASS** (0 errors, 0 warnings).
- **Level 2 (Static Analysis)**: Roslyn compiler diagnostics -> **PASS** (0 errors, 0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Debug` -> **PASS** (263 passed, 0 failed, 0 skipped).
