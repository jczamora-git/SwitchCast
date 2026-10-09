# SWITCHCAST — PROJECT STATE

This is the authoritative progress, state, and environmental tracking document for SwitchCast. All AI agents must consult and update this file before and after executing tasks.

---

## 1. EXECUTIVE SUMMARY

- **Project**: SwitchCast
- **Current Phase**: Global Text Truncation & Layout Overflow Fix (UI/UX Robustness)
- **Overall Status**: **Completed & Tested**
- **Last Updated**: 2026-10-09T15:00:00+08:00 (UTC+8)

---

## 2. FUNCTIONALITY STATUS

### Implemented & Verified
- [x] **Global Text Truncation & Dropdown Width Constraints**:
  - Fixed horizontal expansion and clipping on Dashboard source selectors (`ComboBox` in Primary Control Bar, Live Preview switcher, and Ready-to-Preview switcher).
  - Provided custom `ComboBox.ItemTemplate` with bounded max-widths, `TextTrimming="CharacterEllipsis"`, `TextWrapping="NoWrap"`, and `ToolTipService.ToolTip="{x:Bind Title}"`.
  - Fixed Status Strip Active Source by replacing unbounded horizontal `StackPanel` with a 2-column `Grid` (`ColumnDefinitions="Auto, *"`) ensuring clean responsive ellipsis when window titles are long.
  - Constrained `SourcesPage` source list rows with responsive star columns, `TextWrapping="NoWrap"`, single-line ellipsis, and tooltips on both Title and Subtitle, preventing long names from pushing Queue checkboxes off-screen.
  - Constrained Floating Presenter Dock active source buttons with `MaxWidth="135"` (Expanded) and `MaxWidth="85"` (Compact) on the `TextBlock` inside horizontal button toolbars, with full tooltip strings.
  - Added tooltips and single-line trimming on `PresenterDockMenuWindow` ListView items.
  - Preserved 100% of underlying full window titles, HWNDs, process IDs, and source queue identifiers in models and ViewModels.
- [x] **Floating Presenter Dock Dragging Refinement**:
  - Removed the dedicated visible drag handle icon (`\uE76F`) and its layout column/spacing from both Expanded (7 columns) and Compact (6 columns) dock modes.
  - Implemented native Windows window dragging (`ReleaseCapture` + `WM_NCLBUTTONDOWN` / `HTCAPTION`) from all non-interactive toolbar surfaces (card border, status badge pill, status dot, and padding).
  - Implemented visual tree hit-testing (`IsInteractiveControl`) that strictly protects all interactive controls (`ButtonBase`, `ComboBox`, `TextBox`, `Slider`, `ToggleSwitch`, `ListViewItem`, `MenuFlyoutItem`) from dragging triggers, preserving normal button clicks, hovers, and flyout interactions.
- [x] **Unified Stop Presenting Workflow & Automatic Dashboard Activation**:
  - Unified Stop Presenting across Control Dashboard, Floating Presenter Dock, Presenter Dock Menu, and Global Hotkey (`Ctrl+Shift+S`).
  - Authoritative Stop sequence in `PresentationCoordinator.StopPresentationAsync()` safely terminates active capture or media playback, sets presentation status to `Idle`, closes the `PresentationWindow` (it no longer remains visible on Standby), restores `MainWindow` if minimized, brings the Control Dashboard to the foreground via `IWindowActivationService.ActivateMainWindow()`, and navigates to `DashboardPage`.
  - Suppressed MainWindow activation during application exit confirmation (`ApplicationLifecycleService.ExecuteShutdownAsync` calls `StopPresentationAsync(isShuttingDown: true)`).
- [x] **Windows 11 Settings-Style Theme Selector & Appearance Layout (Phase 6)**:
  - Replaced cramped theme radio buttons with a compact Windows Settings-style `ComboBox` right-aligned with 140 DIP width.
  - Corrected Grid column geometry to give flexible Star width to setting title and description, preventing narrow vertical word wrapping.
  - Real-time immediate theme application (`System`, `Light`, `Dark`) and local persistence via `IApplicationSettingsService`.
- [x] **Direct Local Image & Video Presentation Sources (Phase 6)**:
  - Added first-class `ImageMediaSource` and `VideoMediaSource` domain models with dimension, duration, and file size formatting.
  - Native WinUI 3 desktop file picker (`Win32MediaPickerService`) supporting PNG, JPG, JPEG, BMP, GIF, WEBP, MP4, M4V, WMV, MOV, AVI, MKV.
  - Media metadata discovery engine (`MediaDiscoveryService`) with async Windows imaging and video property extraction.
  - Dedicated media presentation engine (`MediaPresentationService`) managing `BitmapImage` decoding and native `Windows.Media.Playback.MediaPlayer`.
  - Mutually exclusive presentation layers in `PresentationWindow` (Screen Capture, Direct Image, `MediaPlayerElement`, Standby, and topmost Blackout).
  - Essential video controls in Control Dashboard (Play/Pause, Restart, Loop toggle, timecode position, and playback progress).
  - Preserved existing stable presentation HWND across mixed source transitions (Window -> Image -> Video -> Monitor).
  - Preserved Blackout and Pause privacy rules: pausing video on Pause/Blackout and preventing audio/video rendering during blackout.
  - Video playback is muted by default (zero audio routing / virtual drivers as per Phase 6 scope).
- [x] **Sources Page Media Category & Queue Persistence (Phase 6)**:
  - 3-category tab selector: `Application Windows`, `Displays & Monitors`, `Media Files`.
  - "Add Media" file picker import, compact media source items, remove action, and persistence in `UserSettings.ImportedMediaPaths`.
- [x] **Dynamic Native Application Icons Pipeline**:
  - WinUI 3 `SoftwareBitmapSource` thread affinity marshalled to UI thread via `DispatcherQueue`.
  - Multi-tier native icon extraction (`WM_GETICON`, `GetClassLongPtr`, `ExtractIconExW`, `SHGetFileInfoW`) with safe `DestroyIcon` lifecycle.
  - Dual-tier thread-safe caching (`_rawPixelCache` and `_iconSourceCache`).
- [x] **Dedicated Presentation Output Window & Centering**:
  - 16:9 shareable presentation output window with DPI-aware initial centering helper.
  - Integrated custom Fluent title bar with theme synchronization.
- [x] **Global Hotkeys & Floating Companion Dock**:
  - System-wide hotkeys and floating presenter toolbar supporting 3 switching modes (`A+L`, `A`, `L`).
- [x] **Automated Unit & Regression Test Suite**:
  - **234 comprehensive automated unit and regression tests** in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) with 100% pass rate.

### Planned (Upcoming)
- [ ] **Phase 7**: Advanced Presenter Features (Live thumbnail previews, smooth transitions).
- [ ] **Phase 8**: Packaging and Distribution (MSIX store packaging option).

---

## 3. INVESTIGATION & KNOWN ISSUES

1. **Phase 6 Audio Policy**: Audio routing from video files to meeting participants is out of scope for Phase 6. Video playback is muted by default.
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

- **Level 1 (Compilation)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors).
- **Level 2 (Static Analysis)**: Nullable reference checks and analyzer validation -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (234 passed, 0 failed, 0 skipped).

---

## 7. NEXT RECOMMENDED TASK

**Task**: **Phase 7 — Advanced Presenter Features & Smoothing**
- **Objective**: Live thumbnail preview rendering on dashboard cards, configurable smooth transitions, and presentation profiles.
