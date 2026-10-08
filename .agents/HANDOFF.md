# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Phase 5 — Global Hotkeys & Floating Presenter Dock
- **Date**: 2026-10-08T22:45:00+08:00 (UTC+8)
- **Status**: Completed

---

## 1. Objective
Add a presenter-friendly floating control dock and global hotkeys so the user can switch presentation sources instantly while presenting, without needing to return to the main dashboard window, while preserving the existing single capture pipeline, stable presentation output window, and Phase 4.7 rapid switching safety protections.

---

## 2. Architecture & Design Decisions
1. **Win32 Message-Only Window for Global Hotkeys**:
   - `Win32HotkeyService` creates an invisible Win32 message-only window (`HWND_MESSAGE` = `-3`) with a dedicated `WndProc` delegate pinned in memory to intercept `WM_HOTKEY` (0x0312).
   - This ensures background shortcut handling without polling, without low-level keyboard hooks (`WH_KEYBOARD_LL`), and without dependency on whether the main window is focused, minimized, or in background.
   - Cleanly registers on startup/reconfiguration and unregisters all hotkeys upon shutdown or disable.
2. **Compact Always-On-Top Presenter Dock**:
   - `PresenterDockWindow.xaml` is a native WinUI 3 top-level window configured with `OverlappedPresenter.IsAlwaysOnTop = true`, fixed compact dimensions (440x88), custom title bar drag handle (`AppTitleBar`), and borderless styling.
   - Provides quick switching buttons (Previous, Next, Quick Switcher flyout with direct source list), status pill (Live/Paused/Blackout/Idle), Pause/Resume toggle, Blackout toggle, Stop Presenting, and Show Presentation Output.
3. **Presenter Dock Window Service**:
   - `PresenterDockService` provides single-instance lifecycle management (`OpenDock()`, `CloseDock()`, `ToggleDock()`, `BringToFront()`), preventing duplicate dock windows and handling window closure gracefully.
4. **Authoritative Source Switching Integration**:
   - Added `SwitchToNextSourceAsync()`, `SwitchToPreviousSourceAsync()`, and `SwitchToSourceIndexAsync(int index)` directly to `IPresentationCoordinator`.
   - All dock actions and global hotkeys route through `PresentationCoordinator` and `CaptureCoordinator`, strictly preserving the Latest-Request-Wins request coalescing, transition serialization, and generation filtering established in Phase 4.7.
5. **Presenter Settings & Persistence**:
   - Added Section C in `SettingsPage.xaml` / `SettingsViewModel.cs` for toggling global hotkeys, auto-opening dock on presentation start, keeping dock always-on-top, and displaying the active hotkey binding map.
   - Persisted in `%LOCALAPPDATA%\SwitchCast\settings.json` via `ApplicationSettingsService`.

---

## 3. Files Modified / Created

### Models & Services
- [Models/HotkeyModels.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/HotkeyModels.cs) *(New)*
- [Models/UserSettings.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/UserSettings.cs)
- [Services/IHotkeyService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IHotkeyService.cs) *(New)*
- [Services/Win32HotkeyService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32HotkeyService.cs) *(New)*
- [Services/IPresenterDockService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresenterDockService.cs) *(New)*
- [Services/PresenterDockService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresenterDockService.cs) *(New)*
- [Services/IPresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresentationCoordinator.cs)
- [Services/PresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationCoordinator.cs)

### UI & ViewModels
- [ViewModels/PresenterDockViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/PresenterDockViewModel.cs) *(New)*
- [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml) *(New)*
- [Views/PresenterDockWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml.cs) *(New)*
- [ViewModels/DashboardViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/DashboardViewModel.cs)
- [Views/DashboardPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/DashboardPage.xaml)
- [ViewModels/SettingsViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SettingsViewModel.cs)
- [Views/SettingsPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SettingsPage.xaml)
- [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs)

### Automated Test Suite
- [SwitchCast.Tests/Services/HotkeyServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/HotkeyServiceTests.cs) *(New)*
- [SwitchCast.Tests/Services/PresenterDockServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresenterDockServiceTests.cs) *(New)*
- [SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs) *(New)*
- [SwitchCast.Tests/Services/PresentationCoordinatorTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresentationCoordinatorTests.cs)
- [SwitchCast.Tests/SwitchCast.Tests.csproj](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/SwitchCast.Tests.csproj)

---

## 4. Validation Performed
- **Level 1 (Build)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 4.0s).
- **Level 2 (Static Analysis)**: Analyzers and nullable reference checks -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (109 passed, 0 failed, 0 skipped in 380ms).
- **Level 4 (Deterministic Lifecycle & Concurrency Hardening)**: Verified hotkey registration/unregistration, message-only window lifecycle, dock single-instance management, presenter dock commands, and sequential source transitions through the coordinator.

---

## 5. Next Steps
- **Next Task**: **Phase 6 — Stability & Performance Optimization**
- Implement Direct3D 11 device loss resilience and recovery hooks, dynamic DPI multi-monitor scaling, and extended load verification.
