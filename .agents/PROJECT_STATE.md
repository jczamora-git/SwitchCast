# SWITCHCAST — PROJECT STATE

This is the authoritative progress, state, and environmental tracking document for SwitchCast. All AI agents must consult and update this file before and after executing tasks.

---

## 1. EXECUTIVE SUMMARY

- **Project**: SwitchCast
- **Current Phase**: Phase 6 — Direct Media Sources & Settings UI Refinement
- **Overall Status**: **Completed (Phase 6 Feature Complete & Tested)**
- **Last Updated**: 2026-10-09T14:35:00+08:00 (UTC+8)

---

## 2. FUNCTIONALITY STATUS

### Implemented & Verified
- [x] **Strict AI Development Harness (Phase 0)**: Standardized rules ([AGENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/AGENTS.md)), 6 domain skills, architecture specifications, coding standards, and [.editorconfig](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.editorconfig).
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
  - **209 comprehensive automated unit and regression tests** in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) with 100% pass rate.

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
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (209 passed, 0 failed, 0 skipped in 6s).

---

## 7. NEXT RECOMMENDED TASK

**Task**: **Phase 7 — Advanced Presenter Features & Smoothing**
- **Objective**: Live thumbnail preview rendering on dashboard cards, configurable smooth transitions, and presentation profiles.




