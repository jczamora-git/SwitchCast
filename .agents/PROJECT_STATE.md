# SWITCHCAST — PROJECT STATE

This is the authoritative progress, state, and environmental tracking document for SwitchCast. All AI agents must consult and update this file before and after executing tasks.

---

## 1. EXECUTIVE SUMMARY

- **Project**: SwitchCast
- **Current Phase**: Phase 6.1: Complete Media Presentation Integration
- **Overall Status**: **Completed & Tested**
- **Last Updated**: 2026-10-09T15:20:00+08:00 (UTC+8)

---

## 2. FUNCTIONALITY STATUS

### Implemented & Verified
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
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (239 passed, 0 failed, 0 skipped).

---

## 7. NEXT RECOMMENDED TASK

**Task**: **Phase 7 — Advanced Presenter Features & Smoothing**
- **Objective**: Live thumbnail preview rendering on dashboard cards, configurable smooth transitions, and presentation profiles.
