# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Phase 5.3 — Minimal Presenter Dock & Three-Mode Source Switching
- **Date**: 2026-10-09T00:30:00+08:00 (UTC+8)
- **Status**: Completed

---

## 1. Objective
Refine the SwitchCast Presenter Dock into a minimal, polished, single-row floating toolbar across both Expanded (660×52 DIP) and Compact (460×46 DIP) modes, and implement three strongly typed source switching modes:
1. `ActiveAndLive` (A+L): Focuses application window and switches audience-facing presentation output.
2. `ActiveOnly` (A): Focuses application window without altering the audience-facing presentation output.
3. `LiveOnly` (L, default): Switches audience-facing presentation output without altering user application focus.

---

## 2. Architecture & Solutions Applied
1. **Three-Mode Source Switching (`PresenterSwitchMode`)**:
   - Defined `Models/PresenterSwitchMode.cs` enum with `LiveOnly` (0), `ActiveAndLive` (1), and `ActiveOnly` (2).
   - Created `IWindowActivationService` and `Win32WindowActivationService` invoking native `SetForegroundWindow` and `ShowWindowAsync` (`SW_RESTORE`) for focused window activation.
   - Updated `IPresentationStateService` / `PresentationStateService` and `IPresentationCoordinator` / `PresentationCoordinator` to maintain separate `SelectedSource` (logical navigation cursor), `ForegroundSource` (last confirmed focus), and `ActiveSource` (live on-air presentation).
   - Unified all source-switching entry points (Dock Next/Prev, Dock quick-switch flyout, Global Hotkeys `Ctrl+Shift+Right`, `Ctrl+Shift+Left`, `Ctrl+Shift+1..5`) through `ExecuteSourceSwitchAsync` to strictly follow the active `SwitchMode`.
2. **Minimal Single-Row Presenter Toolbar**:
   - Redesigned `Views/PresenterDockWindow.xaml` into a truly minimal, 1-row layout in both Expanded and Compact modes.
   - Expanded Mode (660×52 DIP): `[Drag Grip] [Live Pill] [Prev] [Source Dropdown ▼] [Next] [Mode ▼ (A+L/A/L)] [Divider] [Pause] [Blackout] [Stop] [More ▼] [Collapse] [Close]`.
   - Compact Mode (460×46 DIP): `[Drag Grip] [Live Dot] [Prev] [Source Title] [Next] [Mode ▼] [Pause] [Blackout] [More ▼ (Stop/Dashboard/Output)] [Expand] [Close]`.
   - Enforced `TextTrimming="CharacterEllipsis"`, `TextWrapping="NoWrap"`, and `MaxLines="1"` with rich multi-line tooltips (`SourceFullTooltip`) to prevent multiline wrapping or layout shifting.
   - Mode selector dropdown available on both Expanded and Compact modes with checkmark flyouts and immediate settings persistence.
   - Icon-first action buttons with full tooltips and accessible names.
3. **Preserved Capture & Stability**:
   - Preserved single Direct3D 11 capture pipeline, stable Presentation Output HWND, and Latest-Request-Wins transition serialization.

---

## 3. Files Modified / Created

### New Models & Services
- [Models/PresenterSwitchMode.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/PresenterSwitchMode.cs)
- [Services/IWindowActivationService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IWindowActivationService.cs)
- [Services/Win32WindowActivationService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowActivationService.cs)

### Core Services & ViewModels
- [Models/UserSettings.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/UserSettings.cs)
- [Services/IPresentationStateService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresentationStateService.cs)
- [Services/PresentationStateService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationStateService.cs)
- [Services/IPresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IPresentationCoordinator.cs)
- [Services/PresentationCoordinator.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationCoordinator.cs)
- [ViewModels/PresenterDockViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/PresenterDockViewModel.cs)
- [ViewModels/SettingsViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SettingsViewModel.cs)
- [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs)

### Views & Custom Chrome
- [Views/PresenterDockWindow.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml)
- [Views/PresenterDockWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml.cs)

### Automated Tests
- [SwitchCast.Tests/Services/WindowActivationServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/WindowActivationServiceTests.cs)
- [SwitchCast.Tests/Services/PresentationCoordinatorTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresentationCoordinatorTests.cs)
- [SwitchCast.Tests/Services/PresentationStateServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresentationStateServiceTests.cs)
- [SwitchCast.Tests/Services/RapidSwitchingConcurrencyTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/RapidSwitchingConcurrencyTests.cs)
- [SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/PresenterDockViewModelTests.cs)
- [SwitchCast.Tests/SwitchCast.Tests.csproj](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/SwitchCast.Tests.csproj)

---

## 4. Validation Performed
- **Level 1 (Build)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 22.3s).
- **Level 2 (Static Analysis)**: Analyzers and nullable reference checks -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (122 passed, 0 failed, 0 skipped in 315ms).
- **Level 4 (Deterministic Lifecycle & Focus Hardening)**: Verified window activation fallback, three-mode switching execution, single-row minimal toolbar layout, DPI scaling, and Latest-Request-Wins transition serialization.

---

## 5. Next Steps
- **Next Task**: **Phase 6 — Stability & Performance Optimization**
- Implement Direct3D 11 device loss resilience, dynamic multi-monitor DPI scaling adaptation, and extended load verification.
