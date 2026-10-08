# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Desktop UX Hotfix (Presenter Actions Bring-to-Front, Centered Startup & Dynamic Application Icons)
- **Date**: 2026-10-09T03:00:00+08:00 (UTC+8)
- **Status**: Completed

---

## 1. Objective
Implement three targeted desktop usability hotfixes in SwitchCast:
1. **Presenter Actions Bring-to-Front**: Ensure selecting *Control Dashboard* or *Presentation Output* from the Floating Presenter Dock's Presenter Actions menu reliably restores minimized windows (`ShowWindowAsync(SW_RESTORE)`), brings the target window to the foreground (`SetForegroundWindow`), and prevents dock/popup focus handoff races without recreating existing instances.
2. **Centered MainWindow Startup**: Center `MainWindow` on the target monitor work area on cold launch / first launch with DPI awareness (`GetDpiForWindow`, `MonitorFromWindow`, `GetMonitorInfo`), while respecting saved user window dimensions/coordinates and recovering gracefully from disconnected monitors.
3. **Dynamic Windows Application Icons**: Replace generic orange monitor icons on the Sources page with real local application/window icons extracted via Win32 Shell APIs (`SendMessageTimeout` with `WM_GETICON`, `GetClassLongPtr` with `GCLP_HICONSM`/`GCLP_HICON`, `SHGetFileInfo` / process executable icon fallback), managed via a dedicated `IWindowIconService` with caching and safe native `HICON` lifetime management.

---

## 2. Architecture & Solutions Applied

1. **Presenter Actions Bring-to-Front & Focus Handoff**:
   - **Root Cause**: `PresenterDockMenuWindow` was owned by `PresenterDockWindow` (`GWLP_HWNDPARENT`), causing Windows to automatically return focus to the floating dock whenever the menu closed, overriding the target window activation. Furthermore, `MainWindow` and `PresentationWindow` were using bare `Window.Activate()` calls without `ShowWindowAsync(SW_RESTORE)`.
   - **Solution**:
     - Detached `GWLP_HWNDPARENT = IntPtr.Zero` in `PresenterDockMenuWindow.CloseMenu()` prior to window closure.
     - Enhanced `ActivateMainWindow()` in `App.xaml.cs` to invoke `IWindowActivationService.ActivateWindow(mw.WindowHandle)`.
     - Injected `IWindowActivationService` into `PresentationWindowService` to restore and activate existing `PresentationWindow` instances.
     - Never recreates existing `MainWindow` or `PresentationWindow` instances; preserves capture and presentation state.

2. **Centered Startup Placement & Saved Dimensions/Coordinates**:
   - Implemented DPI-aware calculation in `MainWindow.xaml.cs` using `GetDpiForWindow`, `MonitorFromWindow`, and `GetMonitorInfo`.
   - Centering math: `centerX = workArea.Left + (workArea.Width - windowWidth) / 2`, `centerY = workArea.Top + (workArea.Height - windowHeight) / 2`.
   - Added `WindowPositionX`, `WindowPositionY`, and `RememberWindowPosition` in `UserSettings` and `ApplicationSettingsService`.
   - On shutdown, saves physical positions and DIP dimensions; on startup, checks `MonitorFromPoint` to validate saved coordinates within connected monitor work areas, clamping to visible work area or defaulting to center if monitor was disconnected.

3. **Dynamic Application Icon Engine & Safe Native Lifetime**:
   - Implemented `IWindowIconService` and `Win32WindowIconService`.
   - Extraction sequence:
     1. Window icon: `SendMessageTimeout` with `WM_GETICON` (`ICON_SMALL2` -> `ICON_SMALL` -> `ICON_BIG`) with 200ms timeout.
     2. Class icon: `GetClassLongPtr` (`GCLP_HICONSM` -> `GCLP_HICON`).
     3. Shell icon: `SHGetFileInfo` on `ProcessPath`.
   - Safe handle lifetime: `DestroyIcon` is strictly called on owned shell handles, never on borrowed window/class handles.
   - GDI 32-bit DIB section rendering via `DrawIconEx` with alpha-channel verification, converting to `SoftwareBitmapSource`.
   - Thread-safe caching in `ConcurrentDictionary<string, ImageSource>`.
   - Sources page template updated with true-color 20x20 `<Image>` and theme-adaptive `<FontIcon>` fallback.

---

## 3. Files Modified / Created

### New Files
- [Services/IWindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IWindowIconService.cs)
- [Services/Win32WindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowIconService.cs)
- [SwitchCast.Tests/Services/WindowIconServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/WindowIconServiceTests.cs)
- [SwitchCast.Tests/Services/MainWindowPositioningMathTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/MainWindowPositioningMathTests.cs)
- [SwitchCast.Tests/Services/PresenterDockActionsActivationTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/PresenterDockActionsActivationTests.cs)
- [SwitchCast.Tests/ViewModels/SourcesViewModelIconTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/SourcesViewModelIconTests.cs)

### Modified Files
- [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs)
- [MainWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/MainWindow.xaml.cs)
- [Models/UserSettings.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Models/UserSettings.cs)
- [Services/IApplicationSettingsService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/IApplicationSettingsService.cs)
- [Services/ApplicationSettingsService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/ApplicationSettingsService.cs)
- [Services/PresentationWindowService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/PresentationWindowService.cs)
- [ViewModels/SelectableSourceItem.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SelectableSourceItem.cs)
- [ViewModels/SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs)
- [Views/PresenterDockMenuWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockMenuWindow.xaml.cs)
- [Views/SourcesPage.xaml](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/SourcesPage.xaml)
- [SwitchCast.Tests/Services/ApplicationSettingsServiceTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/ApplicationSettingsServiceTests.cs)
- [SwitchCast.Tests/Stubs/XamlStubs.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Stubs/XamlStubs.cs)
- [SwitchCast.Tests/SwitchCast.Tests.csproj](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/SwitchCast.Tests.csproj)

---

## 4. Validation Performed
- **Level 1 (Build)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 3.00s).
- **Level 2 (Static Analysis)**: Analyzers and nullable reference checks -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (152 passed, 0 failed, 0 skipped in 690ms).
- **Level 4 (Presenter Actions, Startup Centering & Dynamic Icons)**: Window focus handoff, restoration of minimized windows, DPI work-area centering, and native Win32 icon extraction verified.

---

## 5. Next Steps
- **Next Task**: **Phase 6 — Stability & Performance Optimization**
- Implement Direct3D 11 device loss resilience, dynamic multi-monitor DPI scaling adaptation, and extended load verification.

