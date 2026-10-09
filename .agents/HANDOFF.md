# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: SwitchCast v1.2.1 Release Preparation, Git Push & Release Tagging
- **Date**: 2026-10-10T05:50:00+08:00 (UTC+8)
- **Status**: Completed, Verified & Released

---

## 1. Objectives Implemented

1. **Media Source Activation Behavior Fix (Active + Live & Active Only)**:
   - **Context**: File-backed image and video presentation sources (`ImageMediaSource`, `VideoMediaSource`) do not have external OS application windows to bring to the foreground.
   - **Active + Live for Media**:
     - When an image or video source is taken live in `ActiveAndLive` mode, SwitchCast starts media rendering/playback and activates the existing `PresentationWindow` (restores if minimized and brings to the foreground via `IWindowActivationService.ActivateWindow`).
     - Never launches external media applications (e.g. VLC or Photos). Reuses the existing output window without recreation or recentering, preserving fullscreen/windowed geometry.
   - **Active Only for Media**:
     - When navigating to an image or video source in `ActiveOnly` mode, advances the selection cursor (`SetSelectedSource`), resolves `PresentationWindow`, restores if minimized, and brings it to the foreground.
     - Crucially preserves On-Air continuity: does NOT change `ActiveSource`, does NOT start media playback, does NOT seek or replace audience-facing frames. If closed, opens in Standby without taking media live.
   - **Live Only for Media**:
     - Preserves existing behavior: takes media live without requesting foreground focus or stealing focus from current active applications.
   - **Window and Monitor Source Semantics Preserved**:
     - Application window activation (`ActivateSource`) and monitor capture behavior remain completely untouched.
   - **Floating Dock Focus Handoff Coordination**:
     - Updated `PresenterDockMenuWindow.xaml.cs` (`OnSourceListItemClicked`) to dismiss and detach the dropdown menu (`CloseMenu()`) prior to executing `SwitchSourceCommand.ExecuteAsync(source)`. This prevents dropdown teardown from pulling focus back to the dock and away from the activated `PresentationWindow`.
   - **Shutdown & Lifecycle Safety**:
     - Presentation coordinator guards (`_isStoppingOrShuttingDown`) prevent activating output during stopping or application exit. Reuses existing window if already open without duplicate `ShowPresentationWindow` calls.

2. **Native Application Icon Integration**:
   - **Source Image**: User provided high-resolution `Assets/SwitchCast_1.png` (2085x2084).
   - **Multi-Resolution Windows Icon**: Generated multi-resolution `Assets/SwitchCast.ico` embedding 7 standard icon resolutions: 16x16, 24x24, 32x32, 48x48, 64x64, 128x128, and 256x256.
   - **Win32 & WinUI Compatibility Verified**: Verified via Win32 `LoadImage(..., IMAGE_ICON)` and `System.Drawing.Icon` that all icon resolutions load cleanly without errors.
   - **Project Configuration**: Added `Assets/SwitchCast_1.png` and `Assets/SwitchCast.ico` to `SwitchCast.csproj` Content items with `PreserveNewest` deployment.

---

## 2. Files Changed

- `Services/PresentationCoordinator.cs` — Added `ActivatePresentationOutput()` helper, `_isStoppingOrShuttingDown` lifecycle guard, and updated `StartPresentationAsync`, `SwitchPresentationSourceAsync`, and `ExecuteSourceSwitchAsync` for media activation.
- `Views/PresenterDockMenuWindow.xaml.cs` — Dismissed dropdown menu before triggering source switch to prevent focus stealing.
- `Assets/SwitchCast.ico` — Multi-resolution application icon generated from `Assets/SwitchCast_1.png`.
- `Assets/SwitchCast_1.png` — High-resolution source icon asset.
- `SwitchCast.csproj` — Configured Content deployment for `Assets/SwitchCast.ico` and `Assets/SwitchCast_1.png`.
- `SwitchCast.Tests/Services/PresentationCoordinatorMediaTests.cs` — 12 automated unit tests verifying Active + Live, Active Only, Live Only, Standby opening, shutdown guards, reuse of open window, and mixed queue navigation.
- `SwitchCast.Tests/Services/ApplicationBrandingTests.cs` — Verified icon file existence, multi-resolution header (16, 32, 48, 64, 128, 256), and csproj configuration.
- `.agents/PROJECT_STATE.md` — Updated project status and test coverage metrics.
- `.agents/HANDOFF.md` — This record.
- `.agents/CHANGELOG.md` — Documented changes under `[1.2.1] - 2026-10-10`.

---

## 3. Validation Results

- **Level 1 (Compilation)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> **PASS** (0 errors, 0 warnings).
- **Level 2 (Static Analysis)**: Roslyn compiler diagnostics -> **PASS** (0 errors, 0 warnings).
- **Level 3 (Unit & Regression Tests)**: `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Debug` -> **PASS** (281 passed, 0 failed, 0 skipped).
- **Runtime Environment Note**: Automated tests executed on Windows .NET 8 harness. Physical desktop window foreground activation is governed by Windows OS foreground lock rules.
