# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Dynamic Application Icon Pipeline Repair (Native Icon-to-WinUI Rendering)
- **Date**: 2026-10-09T06:00:00+08:00 (UTC+8)
- **Status**: Completed

---

## 1. Objective
Diagnose and repair the native application icon-to-WinUI 3 rendering pipeline in SwitchCast so that running application windows (Google Chrome, Microsoft Visual Studio, File Explorer, Antigravity IDE, etc.) visibly render their authentic true-color native Windows application icons in the Sources list instead of the fallback orange monitor glyph.

---

## 2. Architecture & Implementation Details

1. **Root Cause Analysis & UI Thread Marshalling ([Services/Win32WindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowIconService.cs), [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs), [ViewModels/SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs))**:
   - `Win32WindowIconService` was initialized in `App.xaml.cs` constructor before `DispatcherQueue.GetForCurrentThread()` became valid on the UI thread, leaving `_dispatcherQueue` null.
   - When `SourcesViewModel.LoadIconsAsync` called `GetIconForSourceAsync` on a worker threadpool thread (`ConfigureAwait(false)`), `_dispatcherQueue` was null, leading to background execution of `SoftwareBitmapSource.SetBitmapAsync`. In WinUI 3, `SoftwareBitmapSource` enforces strict UI thread affinity; background execution throws `RPC_E_WRONG_THREAD (0x8001010E)`, caught by an outer try/catch and silently returning `null`.
   - In addition, setting `item.IconSource = icon` directly from the background thread triggered cross-thread exceptions in WinUI 3's compiled binding (`x:Bind`) property changed listeners in `SourcesPage.g.cs`.
   - **Fix Applied**:
     - Added `SetDispatcherQueue` and fallback resolution in `Win32WindowIconService.cs`.
     - Wired `_mainWindow.DispatcherQueue` into `Win32WindowIconService` during `App.OnLaunched`.
     - Ensured `SoftwareBitmapSource.SetBitmapAsync` is always dispatched onto `_dispatcherQueue.TryEnqueue`.
     - Captured `DispatcherQueue` in `SourcesViewModel` and marshalled `item.IconSource = icon` to the UI thread.

2. **Model Property Synchronization ([ViewModels/SelectableSourceItem.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SelectableSourceItem.cs))**:
   - Implemented `OnIconSourceChanged(ImageSource? value)` to automatically synchronize `HasIconSource = value is not null` and raise `PropertyChanged(nameof(HasNoIconSource))`.
   - XAML bindings in `SourcesPage.xaml` automatically switch visibility between the `<Image Source="{x:Bind IconSource, Mode=OneWay}" Visibility="{x:Bind HasIconSource, Mode=OneWay, Converter={StaticResource BoolToVis}}" />` and the `<FontIcon Glyph="&#xE7F4;" Visibility="{x:Bind HasNoIconSource, Mode=OneWay, Converter={StaticResource BoolToVis}}" />`.

3. **Dual-Tier Thread-Safe Caching ([Services/Win32WindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowIconService.cs))**:
   - `_rawPixelCache` (`ConcurrentDictionary<string, byte[]>`): Stores raw BGRA32 pixel byte arrays with 0 thread affinity across all background workers and secondary windows.
   - `_iconSourceCache` (`ConcurrentDictionary<string, ImageSource>`): Stores UI-bound `ImageSource` instances for instant rendering on the UI thread.

4. **Multi-Tier Native Icon Extraction**:
   - Tier 1: `SendMessageTimeout` with `WM_GETICON` (`ICON_SMALL2` -> `ICON_SMALL` -> `ICON_BIG`, 100ms timeout).
   - Tier 2: `GetClassLongPtr` (`GCLP_HICONSM` -> `GCLP_HICON`).
   - Tier 3: `ExtractIconExW` (32x32) -> `SHGetFileInfoW` (`SHGFI_ICON | SHGFI_LARGEICON`).
   - Safe native handle ownership: `DestroyIcon` is strictly called on owned shell/executable handles, never on borrowed window/class handles.

---

## 3. Files Modified

### Added Files
- [SwitchCast.Tests/Services/WindowIconExtractionTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/Services/WindowIconExtractionTests.cs)

### Modified Files
- [App.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/App.xaml.cs)
- [Services/Win32WindowIconService.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Services/Win32WindowIconService.cs)
- [ViewModels/SelectableSourceItem.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SelectableSourceItem.cs)
- [ViewModels/SourcesViewModel.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/ViewModels/SourcesViewModel.cs)
- [Views/PresenterDockWindow.xaml.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/Views/PresenterDockWindow.xaml.cs)
- [SwitchCast.Tests/ViewModels/SelectableSourceItemTests.cs](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/SwitchCast.Tests/ViewModels/SelectableSourceItemTests.cs)
- [.agents/PROJECT_STATE.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/PROJECT_STATE.md)
- [.agents/HANDOFF.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/HANDOFF.md)
- [.agents/CHANGELOG.md](file:///c:/Users/JC%20Zamora/source/repos/SwitchCast/SwitchCast/.agents/CHANGELOG.md)

---

## 4. Validation Performed
- **Level 1 (Build)**: `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64` -> PASS (0 warnings, 0 errors in 4.00s).
- **Level 2 (Static Analysis)**: Analyzers and nullable reference checks -> PASS (0 warnings).
- **Level 3 (Unit Tests)**: `dotnet test SwitchCast.Tests\SwitchCast.Tests.csproj -c Debug` -> PASS (169 passed, 0 failed, 0 skipped in 461ms).
- **Level 4 (Native Icon Extraction & Model Binding)**: Tested live extraction on running applications (`explorer.exe`, `chrome.exe`, `POWERPNT.exe`, `OpenCode.exe`, `Antigravity IDE.exe`), verified 32x32 BGRA32 pixel buffers (4096 bytes), and confirmed `SelectableSourceItem.OnIconSourceChanged` updates `HasIconSource` and `HasNoIconSource`.

---

## 5. Next Steps
- **Next Task**: **Phase 6 — Stability & Performance Optimization**
- Implement Direct3D 11 device loss resilience, dynamic multi-monitor DPI scaling adaptation, and extended load verification.
