# SWITCHCAST — PROJECT STATE

This is the authoritative progress, state, and environmental tracking document for SwitchCast. All AI agents must consult and update this file before and after executing tasks.

---

## 1. EXECUTIVE SUMMARY

- **Project**: SwitchCast
- **Current Phase**: Final UI Polish, Creator Attribution & v1.0 Release Preparation
- **Overall Status**: **Completed (v1.0.0 Release Ready)**
- **Last Updated**: 2026-10-09T06:30:00+08:00 (UTC+8)

---

## 2. FUNCTIONALITY STATUS

### Implemented & Verified
- [x] **Strict AI Development Harness (Phase 0)**: Standardized rules ([AGENTS.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/AGENTS.md)), 6 domain skills, architecture specifications, coding standards, and [.editorconfig](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.editorconfig).
- [x] **Settings Page Visual Polish & Compact Category Navigation**:
  - Replaced bulky radio-button circles in the Settings sidebar with a compact, modern `ListView` navigation list (38 DIP row height, 14 DIP icons, clean hover/selected states).
  - Two-pane layout with 190 DIP navigation sidebar and flexible content pane.
  - Aligned Appearance color mode options (System | Light | Dark) with clean spacing.
  - Aligned Window and Presenter preference toggles with responsive text wrapping.
  - Streamlined Keyboard Shortcuts panel with compact ~46 DIP rows, action descriptions, filter search, and monospace key badges.
- [x] **Creator Attribution & Authoritative v1.0 Metadata**:
  - Added full creator attribution: **John Christopher King Zamora**.
  - Added repository link: [https://github.com/jczamora-git/SwitchCast](https://github.com/jczamora-git/SwitchCast).
  - Single authoritative version metadata configured in [SwitchCast.csproj](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.csproj) (`Version 1.0.0`, `AssemblyVersion 1.0.0.0`, `InformationalVersion 1.0.0`) and dynamically read in [SettingsViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SettingsViewModel.cs).
  - Factual privacy architecture statement (`100% Offline & Local • Zero Telemetry • No Network Access`).
- [x] **Windows Release Packaging (win-x64)**:
  - Published self-contained Release package via `dotnet publish SwitchCast.csproj -c Release -r win-x64 --self-contained true`.
  - Built standalone zip archive `releases/SwitchCast-v1.0.0-win-x64.zip` with verified SHA-256 hash.
- [x] **Dynamic Native Application Icons Pipeline**:
  - WinUI 3 `SoftwareBitmapSource` thread affinity marshalled to UI thread via `DispatcherQueue`.
  - Multi-tier native icon extraction (`WM_GETICON`, `GetClassLongPtr`, `ExtractIconExW`, `SHGetFileInfoW`) with safe `DestroyIcon` lifecycle.
  - Dual-tier thread-safe caching (`_rawPixelCache` and `_iconSourceCache`).
- [x] **Centered Presentation Output Window Positioning**:
  - Implemented DPI-aware initial centering for `PresentationWindow` in [PresentationWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresentationWindow.xaml.cs) using pure math helper [WindowPositioningHelper.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/WindowPositioningHelper.cs).
- [x] **Native Windows Application Icon & Consistent Branding**:
  - Multi-resolution Windows ICO asset at [Assets/SwitchCast.ico](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Assets/SwitchCast.ico) (7 frames: 16x16 to 256x256) embedded into `SwitchCast.exe` and configured across `MainWindow` and `PresentationWindow`.
- [x] **Custom Integrated Presentation Output Title Bar (UI Consistency Hotfix)**:
  - Custom integrated, theme-aware title bar (`ExtendsContentIntoTitleBar = true`, `SetTitleBar(AppTitleBar)`).
- [x] **WinUI 3 Desktop Application Shell (Phase 1 & UI Refinement)**:
  - Custom integrated application top title bar, centralized semantic design tokens in `App.xaml`, compact left navigation sidebar.
- [x] **Presenter Actions Bring-to-Front & Window Focus Handoff**:
  - Window activation and focus restoration across `MainWindow`, `PresentationWindow`, and `PresenterDockMenuWindow`.
- [x] **Centered MainWindow Startup & DPI-Aware Saved Placement**:
  - Startup work-area centering with monitor fallback and boundary clamping.
- [x] **Application Window Hierarchy & Safe Exit Confirmation**:
  - Intercepts `AppWindow.Closing` on MainWindow with native confirmation dialog. Independent secondary window closure.
- [x] **Dependency Injection & MVVM Architecture**: Full DI container configured in `App.xaml.cs` with `CommunityToolkit.Mvvm`.
- [x] **Real Window & Monitor Discovery Engine (Phase 2)**: Windows.Graphics.Capture enumeration and Win32 display detection.
- [x] **Native Graphics Capture Pipeline (Phase 3 & Stabilization)**: Direct3D 11 swapchain rendering, frame pooling, rate-limiting, and GDI diagnostic fallbacks.
- [x] **Performance Profiling & Deterministic Frame Lifecycle (Phase 4.6)**: RefCountedSoftwareBitmap zero-leak memory management.
- [x] **Rapid Source Switching Hardening & Concurrency Serialization (Phase 4.7)**: Latest-Request-Wins request coalescing and generation filtering.
- [x] **Dedicated Presentation Output Window (Phase 4)**: 16:9 shareable presentation window with standby, live, pause, and blackout layers.
- [x] **Global Hotkeys & Minimal Presenter Companion Dock (Phase 5, 5.1 & 5.3 + Hotfix)**: Global hotkeys and floating toolbar in 3 switching modes (`A+L`, `A`, `L`).
- [x] **Automated Unit & Regression Test Suite**: 170 comprehensive unit & regression tests in [SwitchCast.Tests](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests) with 100% pass rate.

### Planned (Upcoming)
- [ ] **Phase 6**: Stability & Performance Optimization (Device loss recovery, extended stress profiling).
- [ ] **Phase 7**: Advanced Presenter Features (Live thumbnail previews, smooth transitions).
- [ ] **Phase 8**: Packaging and Distribution (MSIX store packaging option).

---

## 3. INVESTIGATION & KNOWN ISSUES

1. **SplitView Diagnostic (Phase 1 Investigation)**: Non-fatal upstream WindowsAppSDK diagnostic in `Microsoft.WinUI\Themes\generic.xaml:35014`.
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
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (170 passed, 0 failed, 0 skipped in 523ms).
- **Level 4 (Release Build & Package)**: `dotnet publish SwitchCast.csproj -c Release -r win-x64 --self-contained true` -> PASS; `releases/SwitchCast-v1.0.0-win-x64.zip` built and verified with SHA-256 hash.

---

## 7. NEXT RECOMMENDED TASK

**Task**: **Phase 6 — Stability & Performance Optimization**
- **Objective**: Direct3D 11 device loss resilience, DPI dynamic scaling across multi-monitor setups, and extended presentation load tests.




